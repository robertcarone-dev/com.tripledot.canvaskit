using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    internal static class KeyframeInterpolationTangentUtility
    {
        private const float TimeEpsilon = 0.00001f;
        private const float DefaultWeight = KeyframeInterpolationCurveUtility.DefaultWeight;

        private delegate bool SegmentApplicator(Keyframe[] keys, int leftIndex, int rightIndex);

        internal readonly struct KeyframeInterpolationSegmentSelection
        {
            public readonly int LeftIndex;
            public readonly int RightIndex;

            public KeyframeInterpolationSegmentSelection(int leftIndex, int rightIndex)
            {
                LeftIndex = leftIndex;
                RightIndex = rightIndex;
            }
        }

        internal readonly struct KeyframeInterpolationKeyEditSelection
        {
            public readonly int Index;
            public readonly CurveSelection.SelectionType Type;

            public KeyframeInterpolationKeyEditSelection(int index, CurveSelection.SelectionType type)
            {
                Index = index;
                Type = type;
            }
        }

        public static int ApplyCurve(AnimationCurve curve, IReadOnlyList<int> selectedKeyIndexes, AnimationCurve interpolationCurve)
        {
            return ApplyCurve(curve, CreateKeySelections(selectedKeyIndexes), interpolationCurve);
        }

        public static int ApplyCurve(AnimationCurve curve, IReadOnlyList<KeyframeInterpolationKeySelection> selectedKeys, AnimationCurve interpolationCurve)
        {
            return ApplyCurve(curve, ResolveSelectedSegments(curve, selectedKeys), interpolationCurve);
        }

        internal static int ApplyCurve(AnimationCurve curve, IReadOnlyList<KeyframeInterpolationSegmentSelection> selectedSegments, AnimationCurve interpolationCurve)
        {
            var normalized = KeyframeInterpolationCurveUtility.NormalizeEditableCurve(interpolationCurve);
            return ApplyManualFreeSegments(curve, selectedSegments,
                (keys, leftIndex, rightIndex) => ApplyEditableCurveSegment(keys, leftIndex, rightIndex, normalized));
        }

        public static int ApplyPreset(AnimationCurve curve, IReadOnlyList<int> selectedKeyIndexes, KeyframeInterpolationPreset preset)
        {
            return ApplyPreset(curve, CreateKeySelections(selectedKeyIndexes), preset);
        }

        public static int ApplyPreset(AnimationCurve curve, IReadOnlyList<KeyframeInterpolationKeySelection> selectedKeys, KeyframeInterpolationPreset preset)
        {
            return ApplyCurve(curve, selectedKeys, KeyframeInterpolationCurveUtility.GetPresetCurve(preset));
        }

        internal static int ApplyPreset(AnimationCurve curve, IReadOnlyList<KeyframeInterpolationSegmentSelection> selectedSegments, KeyframeInterpolationPreset preset)
        {
            return ApplyCurve(curve, selectedSegments, KeyframeInterpolationCurveUtility.GetPresetCurve(preset));
        }

        public static int ApplyMode(AnimationCurve curve, IReadOnlyList<int> selectedKeyIndexes, AnimationUtility.TangentMode mode)
        {
            return ApplyMode(curve, CreateKeySelections(selectedKeyIndexes), mode);
        }

        public static int ApplyMode(AnimationCurve curve, IReadOnlyList<KeyframeInterpolationKeySelection> selectedKeys, AnimationUtility.TangentMode mode)
        {
            return ApplyMode(curve, ResolveSelectedSegments(curve, selectedKeys), mode);
        }

        internal static int ApplyMode(AnimationCurve curve, IReadOnlyList<KeyframeInterpolationSegmentSelection> selectedSegments, AnimationUtility.TangentMode mode)
        {
            return mode switch {
                AnimationUtility.TangentMode.Free => ApplyManualFreeSegments(curve, selectedSegments, ApplyFreeModeSegment),
                AnimationUtility.TangentMode.Auto => ApplyUnityModeSegments(curve, selectedSegments, AnimationUtility.TangentMode.Auto, ClearSegmentWeights),
                AnimationUtility.TangentMode.Linear => ApplyUnityModeSegments(curve, selectedSegments, AnimationUtility.TangentMode.Linear, ClearSegmentWeights),
                AnimationUtility.TangentMode.Constant => ApplyUnityModeSegments(curve, selectedSegments, AnimationUtility.TangentMode.Constant, ApplyConstantSegment),
                AnimationUtility.TangentMode.ClampedAuto => ApplyUnityModeSegments(curve, selectedSegments, AnimationUtility.TangentMode.ClampedAuto, ClearSegmentWeights),
                _ => 0
            };
        }

        internal static List<KeyframeInterpolationSegmentSelection> ResolveSelectedSegments(AnimationCurve curve, IReadOnlyList<KeyframeInterpolationKeySelection> selectedKeys)
        {
            var segments = new List<KeyframeInterpolationSegmentSelection>();
            if (curve == null || selectedKeys == null) {
                return segments;
            }

            for (var i = 0; i < selectedKeys.Count; i++) {
                var selectedKey = selectedKeys[i];
                var resolvedIndex = ResolveSelectedKeyIndex(curve, selectedKey);
                AddSelectedKeySegments(curve, segments, resolvedIndex, selectedKey.Type);
            }

            return segments;
        }

        internal static List<KeyframeInterpolationKeyEditSelection> ResolveSelectedKeyEdits(AnimationCurve curve, IReadOnlyList<KeyframeInterpolationKeySelection> selectedKeys)
        {
            var edits = new List<KeyframeInterpolationKeyEditSelection>();
            if (curve == null || selectedKeys == null) {
                return edits;
            }

            for (var i = 0; i < selectedKeys.Count; i++) {
                var selectedKey = selectedKeys[i];
                var resolvedIndex = ResolveSelectedKeyIndex(curve, selectedKey);
                if (!HasEditableSegmentForSelection(curve, resolvedIndex, selectedKey.Type)) {
                    continue;
                }

                AddUniqueKeyEdit(edits, resolvedIndex, selectedKey.Type);
            }

            edits.Sort((left, right) => left.Index != right.Index
                ? left.Index.CompareTo(right.Index)
                : left.Type.CompareTo(right.Type));
            return edits;
        }

        internal static bool HasEditableKeySelection(AnimationCurve curve, IReadOnlyList<KeyframeInterpolationKeySelection> selectedKeys)
        {
            return ResolveSelectedSegments(curve, selectedKeys).Count > 0;
        }

        internal static bool HasDuplicateKeyTimes(AnimationCurve curve)
        {
            if (curve == null || curve.length < 2) {
                return false;
            }

            for (var i = 0; i < curve.length - 1; i++) {
                if (Mathf.Abs(curve[i + 1].time - curve[i].time) <= TimeEpsilon) {
                    return true;
                }
            }

            return false;
        }

        internal static bool TryGetSegmentInterpolation(AnimationCurve curve, int leftIndex, int rightIndex, out KeyframeInterpolationSegmentInterpolation interpolation)
        {
            interpolation = default;
            if (curve == null || leftIndex < 0 || rightIndex >= curve.length || leftIndex >= rightIndex) {
                return false;
            }

            var leftKeyframe = curve[leftIndex];
            var rightKeyframe = curve[rightIndex];
            
            var duration = rightKeyframe.time - leftKeyframe.time;
            if (Mathf.Abs(duration) <= TimeEpsilon) {
                return false;
            }

            var rightTangentMode = AnimationUtility.GetKeyRightTangentMode(curve, leftIndex);
            var leftTangentMode = AnimationUtility.GetKeyLeftTangentMode(curve, rightIndex);
            
            if (IsConstantSegment(leftKeyframe, rightKeyframe, rightTangentMode, leftTangentMode)) {
                var constantPreviewCurve = KeyframeInterpolationCurveUtility.CreateModePreviewCurve(AnimationUtility.TangentMode.Constant);
                interpolation = new KeyframeInterpolationSegmentInterpolation(AnimationUtility.TangentMode.Constant, constantPreviewCurve, false);
                return true;
            }

            var valueScale = (rightKeyframe.value - leftKeyframe.value) / duration;
            if (Mathf.Abs(valueScale) <= TimeEpsilon) {
                var mode = rightTangentMode == leftTangentMode ? rightTangentMode : AnimationUtility.TangentMode.Free;
                interpolation = new KeyframeInterpolationSegmentInterpolation(mode, null, false, true);
                return true;
            }

            var normalizedCurve = KeyframeInterpolationCurveUtility.CreateCurveFromSegment(leftKeyframe, rightKeyframe, valueScale);
            if (rightTangentMode == leftTangentMode) {
                interpolation = new KeyframeInterpolationSegmentInterpolation(rightTangentMode, normalizedCurve, true);
                return true;
            }

            interpolation = new KeyframeInterpolationSegmentInterpolation(AnimationUtility.TangentMode.Free, normalizedCurve, true);
            
            return true;
        }

        private static int ApplyManualFreeSegments(AnimationCurve curve, IReadOnlyList<KeyframeInterpolationSegmentSelection> selectedSegments, SegmentApplicator applySegment)
        {
            if (curve == null || selectedSegments == null || selectedSegments.Count == 0) {
                return 0;
            }

            var keys = curve.keys;
            var segments = new List<KeyframeInterpolationSegmentSelection>();
            var changedIndexes = new HashSet<int>();
            var rightTangentIndexes = new HashSet<int>();
            var leftTangentIndexes = new HashSet<int>();
            
            if (!CollectSegments(
                keys,
                selectedSegments,
                segments,
                changedIndexes,
                rightTangentIndexes,
                leftTangentIndexes)) {
                return 0;
            }

            var scratchKeys = (Keyframe[])keys.Clone();
            if (!ApplySegments(scratchKeys, segments, applySegment)) {
                return 0;
            }

            var originalCurve = KeyframeInterpolationCurveUtility.Clone(curve);
            SetTangentModes(curve, rightTangentIndexes, leftTangentIndexes, AnimationUtility.TangentMode.Free, true);
            keys = curve.keys;
            if (!ApplySegments(keys, segments, applySegment)) {
                RestoreCurve(curve, originalCurve);
                return 0;
            }

            MoveChangedKeys(curve, keys, changedIndexes);
            SetTangentModes(curve, rightTangentIndexes, leftTangentIndexes, AnimationUtility.TangentMode.Free, true);
            return segments.Count;
        }

        private static int ApplyUnityModeSegments(AnimationCurve curve,
            IReadOnlyList<KeyframeInterpolationSegmentSelection> selectedSegments, AnimationUtility.TangentMode tangentMode, SegmentApplicator applySegment)
        {
            if (curve == null || selectedSegments == null || selectedSegments.Count == 0) {
                return 0;
            }

            var keys = curve.keys;
            var segments = new List<KeyframeInterpolationSegmentSelection>();
            var changedIndexes = new HashSet<int>();
            var rightTangentIndexes = new HashSet<int>();
            var leftTangentIndexes = new HashSet<int>();
            
            if (!CollectSegments(
                keys,
                selectedSegments,
                segments,
                changedIndexes,
                rightTangentIndexes,
                leftTangentIndexes)) {
                return 0;
            }

            var scratchKeys = (Keyframe[])keys.Clone();
            if (!ApplySegments(scratchKeys, segments, applySegment)) {
                return 0;
            }

            if (!ApplySegments(keys, segments, applySegment)) {
                return 0;
            }

            MoveChangedKeys(curve, keys, changedIndexes);
            SetTangentModes(curve, rightTangentIndexes, leftTangentIndexes, tangentMode, false);
            UpdateChangedTangents(curve, changedIndexes);
            
            return segments.Count;
        }

        private static bool CollectSegments(Keyframe[] keyframes,
            IReadOnlyList<KeyframeInterpolationSegmentSelection> selectedSegments,
            List<KeyframeInterpolationSegmentSelection> segments,
            HashSet<int> changedIndexes,
            HashSet<int> rightTangentIndexes,
            HashSet<int> leftTangentIndexes)
        {
            for (var i = 0; i < selectedSegments.Count; i++) {
                var segment = selectedSegments[i];
                var leftIndex = segment.LeftIndex;
                var rightIndex = segment.RightIndex;
                if (!CanApplyToSegment(keyframes, leftIndex, rightIndex)) {
                    return false;
                }

                segments.Add(segment);
                changedIndexes.Add(leftIndex);
                changedIndexes.Add(rightIndex);
                rightTangentIndexes.Add(leftIndex);
                leftTangentIndexes.Add(rightIndex);
            }

            return segments.Count > 0;
        }

        private static bool ApplySegments(
            Keyframe[] keyframes,
            IReadOnlyList<KeyframeInterpolationSegmentSelection> segments,
            SegmentApplicator applySegment)
        {
            for (var i = 0; i < segments.Count; i++) {
                var segment = segments[i];
                if (!applySegment(keyframes, segment.LeftIndex, segment.RightIndex)) {
                    return false;
                }
            }

            return true;
        }

        private static void RestoreCurve(AnimationCurve curve, AnimationCurve originalCurve)
        {
            if (curve == null || originalCurve == null) {
                return;
            }

            curve.keys = originalCurve.keys;
            curve.preWrapMode = originalCurve.preWrapMode;
            curve.postWrapMode = originalCurve.postWrapMode;
            KeyframeInterpolationCurveUtility.CopyTangentModes(originalCurve, curve);
        }

        private static List<KeyframeInterpolationKeySelection> CreateKeySelections(IReadOnlyList<int> selectedKeyIndexes)
        {
            var selections = new List<KeyframeInterpolationKeySelection>();
            if (selectedKeyIndexes == null) {
                return selections;
            }

            for (var i = 0; i < selectedKeyIndexes.Count; i++) {
                selections.Add(new KeyframeInterpolationKeySelection(selectedKeyIndexes[i], float.NaN));
            }

            return selections;
        }

        private static void AddSelectedKeySegments(AnimationCurve curve, List<KeyframeInterpolationSegmentSelection> segments, int keyIndex, CurveSelection.SelectionType type)
        {
            if (curve == null || keyIndex < 0 || keyIndex >= curve.length) {
                return;
            }

            if (type == CurveSelection.SelectionType.InTangent) {
                if (keyIndex > 0) {
                    AddUniqueSegment(curve, segments, keyIndex - 1, keyIndex);
                } else {
                    AddUniqueSegment(curve, segments, keyIndex, keyIndex + 1);
                }
            } else {
                if (keyIndex < curve.length - 1) {
                    AddUniqueSegment(curve, segments, keyIndex, keyIndex + 1);
                } else {
                    AddUniqueSegment(curve, segments, keyIndex - 1, keyIndex);
                }
            }
        }

        private static void AddUniqueSegment(AnimationCurve curve, List<KeyframeInterpolationSegmentSelection> segments, int leftIndex, int rightIndex)
        {
            if (rightIndex != leftIndex + 1 || leftIndex < 0 || rightIndex >= curve.length) {
                return;
            }

            for (var i = 0; i < segments.Count; i++) {
                if (segments[i].LeftIndex == leftIndex && segments[i].RightIndex == rightIndex) {
                    return;
                }
            }

            segments.Add(new KeyframeInterpolationSegmentSelection(leftIndex, rightIndex));
        }

        private static void MoveChangedKeys(AnimationCurve curve, Keyframe[] keys, HashSet<int> changedIndexes)
        {
            var sortedChangedIndexes = new List<int>(changedIndexes);
            sortedChangedIndexes.Sort();
            
            for (var i = 0; i < sortedChangedIndexes.Count; i++) {
                var keyIndex = sortedChangedIndexes[i];
                curve.MoveKey(keyIndex, keys[keyIndex]);
            }
        }

        private static int ResolveSelectedKeyIndex(AnimationCurve curve, KeyframeInterpolationKeySelection selectedKey)
        {
            if (selectedKey.Index >= 0 && selectedKey.Index < curve.length) {
                var key = curve[selectedKey.Index];
                if (float.IsNaN(selectedKey.Time) || Mathf.Abs(key.time - selectedKey.Time) <= TimeEpsilon) {
                    return selectedKey.Index;
                }
            }

            for (var i = 0; i < curve.length; i++) {
                if (Mathf.Abs(curve[i].time - selectedKey.Time) <= TimeEpsilon) {
                    return i;
                }
            }

            return -1;
        }

        private static bool CanApplyToSegment(Keyframe[] keyframes, int leftIndex, int rightIndex)
        {
            if (leftIndex < 0 || rightIndex >= keyframes.Length || leftIndex >= rightIndex) {
                return false;
            }

            var duration = keyframes[rightIndex].time - keyframes[leftIndex].time;
            return Mathf.Abs(duration) > TimeEpsilon;
        }

        private static void AddUniqueKeyEdit(
            List<KeyframeInterpolationKeyEditSelection> edits,
            int keyIndex,
            CurveSelection.SelectionType type)
        {
            for (var i = 0; i < edits.Count; i++) {
                if (edits[i].Index == keyIndex && edits[i].Type == type) {
                    return;
                }
            }

            edits.Add(new KeyframeInterpolationKeyEditSelection(keyIndex, type));
        }

        private static bool HasEditableSegmentForSelection(AnimationCurve curve, int keyIndex, CurveSelection.SelectionType type)
        {
            if (curve == null || keyIndex < 0 || keyIndex >= curve.length || curve.length < 2) {
                return false;
            }

            return type switch {
                CurveSelection.SelectionType.InTangent => keyIndex > 0 || keyIndex < curve.length - 1,
                CurveSelection.SelectionType.OutTangent => keyIndex < curve.length - 1 || keyIndex > 0,
                _ => keyIndex < curve.length - 1 || keyIndex > 0
            };
        }

        private static bool ApplyFreeModeSegment(Keyframe[] keyframes, int leftIndex, int rightIndex)
        {
            if (!CanApplyToSegment(keyframes, leftIndex, rightIndex)) {
                return false;
            }

            var left = keyframes[leftIndex];
            var right = keyframes[rightIndex];
            var fallbackTangent = GetLinearTangent(left, right);
            
            if (float.IsInfinity(left.outTangent) || float.IsNaN(left.outTangent)) {
                left.outTangent = fallbackTangent;
            }

            if (float.IsInfinity(right.inTangent) || float.IsNaN(right.inTangent)) {
                right.inTangent = fallbackTangent;
            }

            left.outWeight = KeyframeInterpolationCurveUtility.SanitizeWeightForSave(GetOutWeight(left));
            right.inWeight = KeyframeInterpolationCurveUtility.SanitizeWeightForSave(GetInWeight(right));
            
            keyframes[leftIndex] = left;
            keyframes[rightIndex] = right;
            
            return true;
        }

        private static bool ApplyConstantSegment(Keyframe[] keyframes, int leftIndex, int rightIndex)
        {
            if (!CanApplyToSegment(keyframes, leftIndex, rightIndex)) {
                return false;
            }

            var left = keyframes[leftIndex];
            var right = keyframes[rightIndex];
            
            left.outTangent = float.PositiveInfinity;
            left.outWeight = DefaultWeight;
            left.weightedMode &= ~WeightedMode.Out;

            right.inTangent = float.PositiveInfinity;
            right.inWeight = DefaultWeight;
            right.weightedMode &= ~WeightedMode.In;

            keyframes[leftIndex] = left;
            keyframes[rightIndex] = right;
            
            return true;
        }

        private static bool ClearSegmentWeights(Keyframe[] keyframes, int leftIndex, int rightIndex)
        {
            if (!CanApplyToSegment(keyframes, leftIndex, rightIndex)) {
                return false;
            }

            var left = keyframes[leftIndex];
            var right = keyframes[rightIndex];
            
            left.outWeight = DefaultWeight;
            left.weightedMode &= ~WeightedMode.Out;

            right.inWeight = DefaultWeight;
            right.weightedMode &= ~WeightedMode.In;

            keyframes[leftIndex] = left;
            keyframes[rightIndex] = right;
            
            return true;
        }

        private static bool ApplyEditableCurveSegment(Keyframe[] keyframes, int leftIndex, int rightIndex, AnimationCurve interpolationCurve)
        {
            if (!CanApplyToSegment(keyframes, leftIndex, rightIndex) || interpolationCurve == null || interpolationCurve.length < 2) {
                return false;
            }

            var left = keyframes[leftIndex];
            var right = keyframes[rightIndex];
            var start = interpolationCurve[0];
            var end = interpolationCurve[interpolationCurve.length - 1];

            if (!TryCalculateScaledTangent(left, right, start.outTangent, out var outTangent)
                || !TryCalculateScaledTangent(left, right, end.inTangent, out var inTangent)) {
                return false;
            }

            left.outTangent = outTangent;
            left.outWeight = HasOutWeight(start)
                ? KeyframeInterpolationCurveUtility.SanitizeWeightForSave(start.outWeight)
                : DefaultWeight;
            
            if (HasOutWeight(start)) {
                left.weightedMode |= WeightedMode.Out;
            } else {
                left.weightedMode &= ~WeightedMode.Out;
            }

            right.inTangent = inTangent;
            right.inWeight = HasInWeight(end)
                ? KeyframeInterpolationCurveUtility.SanitizeWeightForSave(end.inWeight)
                : DefaultWeight;
            
            if (HasInWeight(end)) {
                right.weightedMode |= WeightedMode.In;
            } else {
                right.weightedMode &= ~WeightedMode.In;
            }

            keyframes[leftIndex] = left;
            keyframes[rightIndex] = right;
            
            return true;
        }

        private static bool IsConstantSegment(
            Keyframe leftKeyframe,
            Keyframe rightKeyframe,
            AnimationUtility.TangentMode rightTangentMode,
            AnimationUtility.TangentMode leftTangentMode)
        {
            return float.IsInfinity(leftKeyframe.outTangent)
                   && float.IsInfinity(rightKeyframe.inTangent) 
                   || rightTangentMode == AnimationUtility.TangentMode.Constant 
                   && leftTangentMode == AnimationUtility.TangentMode.Constant;
        }

        private static void SetTangentModes(
            AnimationCurve curve,
            HashSet<int> rightTangentIndexes,
            HashSet<int> leftTangentIndexes,
            AnimationUtility.TangentMode tangentMode,
            bool breakChangedKeys)
        {
            var changedIndexes = new HashSet<int>(rightTangentIndexes);
            changedIndexes.UnionWith(leftTangentIndexes);
            
            foreach (var keyIndex in changedIndexes) {
                if (keyIndex < 0 || keyIndex >= curve.length) {
                    continue;
                }

                var changesRight = rightTangentIndexes.Contains(keyIndex);
                var changesLeft = leftTangentIndexes.Contains(keyIndex);
                SetKeyBrokenForEditedSides(curve, keyIndex, changesLeft, changesRight, breakChangedKeys);

                if (changesRight) {
                    AnimationUtility.SetKeyRightTangentMode(curve, keyIndex, tangentMode);
                }

                if (changesLeft) {
                    AnimationUtility.SetKeyLeftTangentMode(curve, keyIndex, tangentMode);
                }
            }
        }

        private static void SetKeyBrokenForEditedSides(
            AnimationCurve curve,
            int keyIndex,
            bool changesLeft,
            bool changesRight,
            bool breakChangedKeys)
        {
            if (keyIndex >= 0 && keyIndex < curve.length) {
                AnimationUtility.SetKeyBroken(curve, keyIndex, breakChangedKeys || changesLeft != changesRight);
            }
        }

        private static void UpdateChangedTangents(AnimationCurve curve, HashSet<int> changedIndexes)
        {
            foreach (var keyIndex in changedIndexes) {
                if (keyIndex >= 0 && keyIndex < curve.length) {
                    AnimationUtility.UpdateTangentsFromModeSurrounding(curve, keyIndex);
                }
            }
        }

        private static float GetLinearTangent(Keyframe left, Keyframe right)
        {
            var duration = right.time - left.time;
            return Mathf.Abs(duration) <= TimeEpsilon ? 0f : (right.value - left.value) / duration;
        }

        private static bool TryCalculateScaledTangent(Keyframe left, Keyframe right, float normalizedTangent, out float tangent)
        {
            tangent = 0f;
            var duration = (double)right.time - left.time;
            if (Math.Abs(duration) <= TimeEpsilon) {
                return false;
            }

            var valueScale = ((double)right.value - left.value) / duration;
            if (Math.Abs(valueScale) <= TimeEpsilon) {
                return true;
            }

            var scaledTangent = valueScale * normalizedTangent;
            if (double.IsNaN(scaledTangent) || double.IsInfinity(scaledTangent) || scaledTangent > float.MaxValue || scaledTangent < -float.MaxValue) {
                return false;
            }

            tangent = (float)scaledTangent;
            return !float.IsNaN(tangent) && !float.IsInfinity(tangent);
        }

        private static float GetOutWeight(Keyframe key)
        {
            return HasOutWeight(key) ? key.outWeight : DefaultWeight;
        }

        private static float GetInWeight(Keyframe key)
        {
            return HasInWeight(key) ? key.inWeight : DefaultWeight;
        }

        private static bool HasOutWeight(Keyframe key)
        {
            return (key.weightedMode & WeightedMode.Out) == WeightedMode.Out;
        }

        private static bool HasInWeight(Keyframe key)
        {
            return (key.weightedMode & WeightedMode.In) == WeightedMode.In;
        }
    }
}
