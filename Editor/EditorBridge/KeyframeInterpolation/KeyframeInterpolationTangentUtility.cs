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
        private delegate bool KeyApplicator(Keyframe[] keys, int keyIndex, CurveSelection.SelectionType type);

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
            var normalized = KeyframeInterpolationCurveUtility.NormalizeEditableCurve(interpolationCurve);
            return ApplyManualFreeKeys(curve, selectedKeys,
                (keys, keyIndex, type) => ApplyEditableCurveKey(keys, keyIndex, type, normalized));
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
            return mode switch {
                AnimationUtility.TangentMode.Free => ApplyManualFreeKeys(curve, selectedKeys, ApplyFreeModeKey),
                AnimationUtility.TangentMode.Auto => ApplyUnityModeKeys(curve, selectedKeys, AnimationUtility.TangentMode.Auto, ClearKeyWeights),
                AnimationUtility.TangentMode.Linear => ApplyUnityModeKeys(curve, selectedKeys, AnimationUtility.TangentMode.Linear, ClearKeyWeights),
                AnimationUtility.TangentMode.Constant => ApplyUnityModeKeys(curve, selectedKeys, AnimationUtility.TangentMode.Constant, ApplyConstantKey),
                AnimationUtility.TangentMode.ClampedAuto => ApplyUnityModeKeys(curve, selectedKeys, AnimationUtility.TangentMode.ClampedAuto, ClearKeyWeights),
                _ => 0
            };
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
            var selectedKeyEdits = ResolveSelectedKeyEdits(curve, selectedKeys);
            
            for (var i = 0; i < selectedKeyEdits.Count; i++) {
                var selectedKey = selectedKeyEdits[i];
                AddSelectedKeySegments(curve, segments, selectedKey.Index, selectedKey.Type);
            }

            return segments;
        }

        internal static List<KeyframeInterpolationKeyEditSelection> ResolveSelectedKeyEdits(AnimationCurve curve, IReadOnlyList<KeyframeInterpolationKeySelection> selectedKeys)
        {
            var edits = new List<KeyframeInterpolationKeyEditSelection>();
            if (curve == null || selectedKeys == null) {
                return edits;
            }

            var indexes = new List<int>();
            var typesByIndex = new Dictionary<int, CurveSelection.SelectionType>();
            
            for (var i = 0; i < selectedKeys.Count; i++) {
                var selectedKey = selectedKeys[i];
                var resolvedIndex = ResolveSelectedKeyIndex(curve, selectedKey);
                if (resolvedIndex < 0 || !HasEditableKeySide(curve, resolvedIndex, selectedKey.Type)) {
                    continue;
                }

                if (indexes.Contains(resolvedIndex)) {
                    typesByIndex[resolvedIndex] = MergeSelectionTypes(typesByIndex[resolvedIndex], selectedKey.Type);
                } else {
                    indexes.Add(resolvedIndex);
                    typesByIndex[resolvedIndex] = selectedKey.Type;
                }
            }

            indexes.Sort();
            
            for (var i = 0; i < indexes.Count; i++) {
                edits.Add(new KeyframeInterpolationKeyEditSelection(indexes[i], typesByIndex[indexes[i]]));
            }

            return edits;
        }

        internal static bool HasEditableKeySelection(AnimationCurve curve, IReadOnlyList<KeyframeInterpolationKeySelection> selectedKeys)
        {
            return ResolveSelectedKeyEdits(curve, selectedKeys).Count > 0;
        }

        internal static bool TryGetKeyInterpolation(AnimationCurve curve, KeyframeInterpolationKeyEditSelection selectedKey, out KeyframeInterpolationSegmentInterpolation interpolation)
        {
            interpolation = default;
            if (curve == null || selectedKey.Index < 0 || selectedKey.Index >= curve.length) {
                return false;
            }

            var editsRight = EditsRightTangent(selectedKey.Type);
            var editsLeft = EditsLeftTangent(selectedKey.Type);
            if (!editsRight && !editsLeft) {
                return false;
            }

            var keyframe = curve[selectedKey.Index];
            var rightTangentMode = AnimationUtility.GetKeyRightTangentMode(curve, selectedKey.Index);
            var leftTangentMode = AnimationUtility.GetKeyLeftTangentMode(curve, selectedKey.Index);
            if (IsConstantKey(keyframe, rightTangentMode, leftTangentMode, editsRight, editsLeft)) {
                interpolation = new KeyframeInterpolationSegmentInterpolation(AnimationUtility.TangentMode.Constant, KeyframeInterpolationCurveUtility.CreateModePreviewCurve(AnimationUtility.TangentMode.Constant), false);
                return true;
            }

            var normalizedCurve = CreateCurveFromKey(curve, selectedKey.Index, editsRight, editsLeft);
            var commonKeyMode = GetCommonKeyMode(rightTangentMode, leftTangentMode, editsRight, editsLeft);
            interpolation = new KeyframeInterpolationSegmentInterpolation(commonKeyMode, normalizedCurve, true);
            return true;
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

            var segments = new List<KeyframeInterpolationSegmentSelection>();
            var changedIndexes = new HashSet<int>();
            var rightTangentIndexes = new HashSet<int>();
            var leftTangentIndexes = new HashSet<int>();
            var keys = curve.keys;
            
            var appliedSegments = CollectSegments(
                keys,
                selectedSegments,
                segments,
                changedIndexes,
                rightTangentIndexes,
                leftTangentIndexes);

            if (appliedSegments == 0) {
                return 0;
            }

            SetTangentModes(curve, rightTangentIndexes, leftTangentIndexes, AnimationUtility.TangentMode.Free, true);
            keys = curve.keys;
            for (var i = 0; i < segments.Count; i++) {
                var segment = segments[i];
                applySegment(keys, segment.LeftIndex, segment.RightIndex);
            }

            MoveChangedKeys(curve, keys, changedIndexes);
            SetTangentModes(curve, rightTangentIndexes, leftTangentIndexes, AnimationUtility.TangentMode.Free, true);
            return appliedSegments;
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
            
            var appliedSegments = CollectSegments(
                keys,
                selectedSegments,
                segments,
                changedIndexes,
                rightTangentIndexes,
                leftTangentIndexes);

            if (appliedSegments == 0) {
                return 0;
            }

            for (var i = 0; i < segments.Count; i++) {
                applySegment(keys, segments[i].LeftIndex, segments[i].RightIndex);
            }

            MoveChangedKeys(curve, keys, changedIndexes);
            SetTangentModes(curve, rightTangentIndexes, leftTangentIndexes, tangentMode, false);
            UpdateChangedTangents(curve, changedIndexes);
            
            return appliedSegments;
        }

        private static int ApplyManualFreeKeys(AnimationCurve curve, 
            IReadOnlyList<KeyframeInterpolationKeySelection> selectedKeys, KeyApplicator applyKey)
        {
            if (curve == null || selectedKeys == null || selectedKeys.Count == 0) {
                return 0;
            }

            var edits = ResolveSelectedKeyEdits(curve, selectedKeys);
            if (edits.Count == 0) {
                return 0;
            }

            var changedIndexes = new HashSet<int>();
            var rightTangentIndexes = new HashSet<int>();
            var leftTangentIndexes = new HashSet<int>();
            var keys = curve.keys;
            
            var appliedKeys = CollectKeySides(keys, edits, changedIndexes, rightTangentIndexes, leftTangentIndexes);
            if (appliedKeys == 0) {
                return 0;
            }

            SetTangentModes(curve, rightTangentIndexes, leftTangentIndexes, AnimationUtility.TangentMode.Free, true);
            
            keys = curve.keys;
            for (var i = 0; i < edits.Count; i++) {
                var edit = edits[i];
                applyKey(keys, edit.Index, edit.Type);
            }

            MoveChangedKeys(curve, keys, changedIndexes);
            SetTangentModes(curve, rightTangentIndexes, leftTangentIndexes, AnimationUtility.TangentMode.Free, true);
            
            return appliedKeys;
        }

        private static int ApplyUnityModeKeys(AnimationCurve curve,
            IReadOnlyList<KeyframeInterpolationKeySelection> selectedKeys, AnimationUtility.TangentMode tangentMode, KeyApplicator applyKey)
        {
            if (curve == null || selectedKeys == null || selectedKeys.Count == 0) {
                return 0;
            }

            var edits = ResolveSelectedKeyEdits(curve, selectedKeys);
            if (edits.Count == 0) {
                return 0;
            }

            var changedIndexes = new HashSet<int>();
            var rightTangentIndexes = new HashSet<int>();
            var leftTangentIndexes = new HashSet<int>();
            var keys = curve.keys;
            
            var appliedKeys = CollectKeySides(keys, edits, changedIndexes, rightTangentIndexes, leftTangentIndexes);
            if (appliedKeys == 0) {
                return 0;
            }

            for (var i = 0; i < edits.Count; i++) {
                var edit = edits[i];
                applyKey(keys, edit.Index, edit.Type);
            }

            MoveChangedKeys(curve, keys, changedIndexes);
            SetTangentModes(curve, rightTangentIndexes, leftTangentIndexes, tangentMode, false);
            UpdateChangedTangents(curve, changedIndexes);
            
            return appliedKeys;
        }

        private static int CollectSegments(Keyframe[] keyframes,
            IReadOnlyList<KeyframeInterpolationSegmentSelection> selectedSegments,
            List<KeyframeInterpolationSegmentSelection> segments,
            HashSet<int> changedIndexes,
            HashSet<int> rightTangentIndexes,
            HashSet<int> leftTangentIndexes)
        {
            var appliedSegments = 0;
            for (var i = 0; i < selectedSegments.Count; i++) {
                var segment = selectedSegments[i];
                var leftIndex = segment.LeftIndex;
                var rightIndex = segment.RightIndex;
                if (!CanApplyToSegment(keyframes, leftIndex, rightIndex)) {
                    continue;
                }

                segments.Add(segment);
                changedIndexes.Add(leftIndex);
                changedIndexes.Add(rightIndex);
                rightTangentIndexes.Add(leftIndex);
                leftTangentIndexes.Add(rightIndex);
                appliedSegments++;
            }

            return appliedSegments;
        }

        private static int CollectKeySides(Keyframe[] keyframes,
            IReadOnlyList<KeyframeInterpolationKeyEditSelection> selectedKeys,
            HashSet<int> changedIndexes,
            HashSet<int> rightTangentIndexes,
            HashSet<int> leftTangentIndexes)
        {
            var appliedKeys = 0;
            for (var i = 0; i < selectedKeys.Count; i++) {
                var selectedKey = selectedKeys[i];
                if (keyframes == null || selectedKey.Index < 0 || selectedKey.Index >= keyframes.Length) {
                    continue;
                }

                var changesRight = EditsRightTangent(selectedKey.Type);
                var changesLeft = EditsLeftTangent(selectedKey.Type);
                if (!changesRight && !changesLeft) {
                    continue;
                }

                changedIndexes.Add(selectedKey.Index);
                if (changesRight) {
                    rightTangentIndexes.Add(selectedKey.Index);
                }

                if (changesLeft) {
                    leftTangentIndexes.Add(selectedKey.Index);
                }

                appliedKeys++;
            }

            return appliedKeys;
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

        private static void AddSelectedKeySegments(AnimationCurve curve,
            List<KeyframeInterpolationSegmentSelection> segments,
            int keyIndex,
            CurveSelection.SelectionType type)
        {
            if (type == CurveSelection.SelectionType.InTangent) {
                AddUniqueSegment(curve, segments, keyIndex - 1, keyIndex);
                return;
            }

            if (type == CurveSelection.SelectionType.OutTangent) {
                AddUniqueSegment(curve, segments, keyIndex, keyIndex + 1);
                return;
            }

            if (keyIndex < curve.length - 1) {
                AddUniqueSegment(curve, segments, keyIndex, keyIndex + 1);
            }
        }

        private static void AddUniqueSegment(AnimationCurve curve,
            List<KeyframeInterpolationSegmentSelection> segments,
            int leftIndex,
            int rightIndex)
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

        private static CurveSelection.SelectionType MergeSelectionTypes(CurveSelection.SelectionType current, CurveSelection.SelectionType next)
        {
            if (current == next || next == CurveSelection.SelectionType.Key) {
                return current;
            }

            return current == CurveSelection.SelectionType.Key ? next : CurveSelection.SelectionType.Key;
        }

        private static bool CanApplyToSegment(Keyframe[] keyframes, int leftIndex, int rightIndex)
        {
            if (leftIndex < 0 || rightIndex >= keyframes.Length || leftIndex >= rightIndex) {
                return false;
            }

            var duration = keyframes[rightIndex].time - keyframes[leftIndex].time;
            return Mathf.Abs(duration) > TimeEpsilon;
        }

        private static bool HasEditableKeySide(AnimationCurve curve, int keyIndex, CurveSelection.SelectionType type)
        {
            return curve != null
                && keyIndex >= 0
                && keyIndex < curve.length
                && (EditsRightTangent(type) || EditsLeftTangent(type));
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

            left.outWeight = GetOutWeight(left);
            right.inWeight = GetInWeight(right);
            
            keyframes[leftIndex] = left;
            keyframes[rightIndex] = right;
            
            return true;
        }

        private static bool ApplyFreeModeKey(Keyframe[] keyframes, int keyIndex, CurveSelection.SelectionType type)
        {
            if (keyIndex < 0 || keyIndex >= keyframes.Length) {
                return false;
            }

            var key = keyframes[keyIndex];
            if (EditsRightTangent(type)) {
                if (float.IsInfinity(key.outTangent) || float.IsNaN(key.outTangent)) {
                    key.outTangent = GetOutFallbackTangent(keyframes, keyIndex);
                }

                key.outWeight = GetOutWeight(key);
            }

            if (EditsLeftTangent(type)) {
                if (float.IsInfinity(key.inTangent) || float.IsNaN(key.inTangent)) {
                    key.inTangent = GetInFallbackTangent(keyframes, keyIndex);
                }

                key.inWeight = GetInWeight(key);
            }

            keyframes[keyIndex] = key;
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

        private static bool ApplyConstantKey(Keyframe[] keyframes, int keyIndex, CurveSelection.SelectionType type)
        {
            if (keyframes == null || keyIndex < 0 || keyIndex >= keyframes.Length) {
                return false;
            }

            var key = keyframes[keyIndex];
            
            if (EditsRightTangent(type)) {
                key.outTangent = float.PositiveInfinity;
                key.outWeight = DefaultWeight;
                key.weightedMode &= ~WeightedMode.Out;
            }

            if (EditsLeftTangent(type)) {
                key.inTangent = float.PositiveInfinity;
                key.inWeight = DefaultWeight;
                key.weightedMode &= ~WeightedMode.In;
            }

            keyframes[keyIndex] = key;
            
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

        private static bool ClearKeyWeights(Keyframe[] keyframes, int keyIndex, CurveSelection.SelectionType type)
        {
            if (keyIndex < 0 || keyIndex >= keyframes.Length) {
                return false;
            }

            var key = keyframes[keyIndex];
            if (EditsRightTangent(type)) {
                key.outWeight = DefaultWeight;
                key.weightedMode &= ~WeightedMode.Out;
            }

            if (EditsLeftTangent(type)) {
                key.inWeight = DefaultWeight;
                key.weightedMode &= ~WeightedMode.In;
            }

            keyframes[keyIndex] = key;
            
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
            var valueScale = (right.value - left.value) / (right.time - left.time);

            left.outTangent = Mathf.Abs(valueScale) <= TimeEpsilon ? 0f : valueScale * start.outTangent;
            left.outWeight = HasOutWeight(start) ? start.outWeight : DefaultWeight;
            
            if (HasOutWeight(start)) {
                left.weightedMode |= WeightedMode.Out;
            } else {
                left.weightedMode &= ~WeightedMode.Out;
            }

            right.inTangent = Mathf.Abs(valueScale) <= TimeEpsilon ? 0f : valueScale * end.inTangent;
            right.inWeight = HasInWeight(end) ? end.inWeight : DefaultWeight;
            
            if (HasInWeight(end)) {
                right.weightedMode |= WeightedMode.In;
            } else {
                right.weightedMode &= ~WeightedMode.In;
            }

            keyframes[leftIndex] = left;
            keyframes[rightIndex] = right;
            
            return true;
        }

        private static bool ApplyEditableCurveKey(Keyframe[] keyframes, int keyIndex, CurveSelection.SelectionType type, AnimationCurve interpolationCurve)
        {
            if (keyIndex < 0 || keyIndex >= keyframes.Length || interpolationCurve == null || interpolationCurve.length < 2) {
                return false;
            }

            var key = keyframes[keyIndex];
            var start = interpolationCurve[0];
            var end = interpolationCurve[interpolationCurve.length - 1];

            if (EditsRightTangent(type)) {
                ApplyOutTangent(ref key, start, GetOutValueScale(keyframes, keyIndex));
            }

            if (EditsLeftTangent(type)) {
                ApplyInTangent(ref key, end, GetInValueScale(keyframes, keyIndex));
            }

            keyframes[keyIndex] = key;
            
            return true;
        }

        private static AnimationCurve CreateCurveFromKey(AnimationCurve curve, int keyIndex, bool useRightTangent, bool useLeftTangent)
        {
            var keys = curve.keys;
            var key = keys[keyIndex];
            
            var start = new Keyframe(
                time: 0f,
                value: 0f,
                inTangent: 0f,
                outTangent: useRightTangent ? NormalizeTangent(key.outTangent, GetOutValueScale(keys, keyIndex)) : 0f,
                inWeight: DefaultWeight,
                outWeight: useRightTangent ? GetOutWeight(key) : DefaultWeight) {
                weightedMode = useRightTangent && HasOutWeight(key: key) ? WeightedMode.Out : WeightedMode.None
            };
            
            var end = new Keyframe(
                time: 1f,
                value: 1f,
                inTangent: useLeftTangent ? NormalizeTangent(key.inTangent, GetInValueScale(keys, keyIndex)) : 0f,
                outTangent: 0f,
                inWeight: useLeftTangent ? GetInWeight(key) : DefaultWeight,
                outWeight: DefaultWeight) {
                weightedMode = useLeftTangent && HasInWeight(key: key) ? WeightedMode.In : WeightedMode.None
            };

            return KeyframeInterpolationCurveUtility.NormalizeEditableCurve(new AnimationCurve(start, end));
        }

        private static AnimationUtility.TangentMode GetCommonKeyMode(
            AnimationUtility.TangentMode rightTangentMode,
            AnimationUtility.TangentMode leftTangentMode,
            bool useRightTangent,
            bool useLeftTangent)
        {
            if (useRightTangent && useLeftTangent) {
                return rightTangentMode == leftTangentMode ? rightTangentMode : AnimationUtility.TangentMode.Free;
            }

            if (useRightTangent) {
                return rightTangentMode;
            }

            return useLeftTangent ? leftTangentMode : AnimationUtility.TangentMode.Free;
        }

        private static bool IsConstantKey(
            Keyframe keyframe,
            AnimationUtility.TangentMode rightTangentMode,
            AnimationUtility.TangentMode leftTangentMode,
            bool useRightTangent,
            bool useLeftTangent)
        {
            var rightIsConstant = !useRightTangent || float.IsInfinity(keyframe.outTangent) || rightTangentMode == AnimationUtility.TangentMode.Constant;
            var leftIsConstant = !useLeftTangent || float.IsInfinity(keyframe.inTangent) || leftTangentMode == AnimationUtility.TangentMode.Constant;
            return rightIsConstant && leftIsConstant;
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

        private static float GetOutFallbackTangent(Keyframe[] keys, int keyIndex)
        {
            return keyIndex >= 0 && keyIndex < keys.Length - 1
                ? GetLinearTangent(keys[keyIndex], keys[keyIndex + 1])
                : 0f;
        }

        private static float GetInFallbackTangent(Keyframe[] keys, int keyIndex)
        {
            return keyIndex > 0 && keyIndex < keys.Length
                ? GetLinearTangent(keys[keyIndex - 1], keys[keyIndex])
                : 0f;
        }

        private static float GetOutValueScale(Keyframe[] keys, int keyIndex)
        {
            if (keyIndex < 0 || keyIndex >= keys.Length - 1) {
                return 1f;
            }

            var duration = keys[keyIndex + 1].time - keys[keyIndex].time;
            return Mathf.Abs(duration) <= TimeEpsilon ? 0f
                : (keys[keyIndex + 1].value - keys[keyIndex].value) / duration;
        }

        private static float GetInValueScale(Keyframe[] keys, int keyIndex)
        {
            if (keyIndex <= 0 || keyIndex >= keys.Length) {
                return 1f;
            }

            var duration = keys[keyIndex].time - keys[keyIndex - 1].time;
            return Mathf.Abs(duration) <= TimeEpsilon ? 0f
                : (keys[keyIndex].value - keys[keyIndex - 1].value) / duration;
        }

        private static float NormalizeTangent(float tangent, float valueScale)
        {
            return Mathf.Abs(valueScale) <= TimeEpsilon ? 0f : tangent / valueScale;
        }

        private static void ApplyOutTangent(ref Keyframe key, Keyframe source, float valueScale)
        {
            key.outTangent = Mathf.Abs(valueScale) <= TimeEpsilon ? 0f : valueScale * source.outTangent;
            key.outWeight = HasOutWeight(source) ? source.outWeight : DefaultWeight;
            if (HasOutWeight(source)) {
                key.weightedMode |= WeightedMode.Out;
            } else {
                key.weightedMode &= ~WeightedMode.Out;
            }
        }

        private static void ApplyInTangent(ref Keyframe key, Keyframe source, float valueScale)
        {
            key.inTangent = Mathf.Abs(valueScale) <= TimeEpsilon ? 0f : valueScale * source.inTangent;
            key.inWeight = HasInWeight(source) ? source.inWeight : DefaultWeight;
            if (HasInWeight(source)) {
                key.weightedMode |= WeightedMode.In;
            } else {
                key.weightedMode &= ~WeightedMode.In;
            }
        }

        private static bool EditsRightTangent(CurveSelection.SelectionType type)
        {
            return type != CurveSelection.SelectionType.InTangent;
        }

        private static bool EditsLeftTangent(CurveSelection.SelectionType type)
        {
            return type != CurveSelection.SelectionType.OutTangent;
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
