using System;
using UnityEditor;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor.KeyframeInterpolation
{
    internal enum KeyframeInterpolationPreset
    {
        EaseIn = 1,
        EaseOut = 2,
        EaseInOut = 3,
        Back = 4,
        Bounce = 5,
        Circular = 6,
        Exponential = 7,
        Elastic = 8
    }

    internal static class KeyframeInterpolationCurveUtility
    {
        private const float CompareEpsilon = 0.0001f;
        private const float RelativeTangentCompareEpsilon = 0.001f;
        private const float TangentEpsilon = 0.0001f;
        private const float TimeEpsilon = 0.00001f;
        private const float MinUnityWeight = 0.0001f;
        private const float MaxUnityWeight = 1f - MinUnityWeight;
        private const WeightedMode ValidWeightedModeMask = WeightedMode.In | WeightedMode.Out;

        public const float DefaultWeight = 1f / 3f;

        public static AnimationCurve CreateDefaultCurve()
        {
            return GetPresetCurve(KeyframeInterpolationPreset.EaseInOut);
        }

        public static AnimationCurve GetPresetCurve(KeyframeInterpolationPreset preset)
        {
            return preset switch {
                KeyframeInterpolationPreset.EaseIn => CreateFreeCurve(0f, 2f, DefaultWeight, DefaultWeight, WeightedMode.None),
                KeyframeInterpolationPreset.EaseOut => CreateFreeCurve(2f, 0f, DefaultWeight, DefaultWeight, WeightedMode.None),
                KeyframeInterpolationPreset.EaseInOut => CreateFreeCurve(0f, 0f, DefaultWeight, DefaultWeight, WeightedMode.None),
                KeyframeInterpolationPreset.Back => CreateFreeCurve(-0.5625f, -0.5625f, 0.32f, 0.32f, WeightedMode.Both),
                KeyframeInterpolationPreset.Bounce => CreateFreeCurve(-0.2727273f, -0.6666667f, 0.22f, 0.42f, WeightedMode.Both),
                KeyframeInterpolationPreset.Circular => CreateFreeCurve(0.04347826f, 0.04347826f, 0.46f, 0.46f, WeightedMode.Both),
                KeyframeInterpolationPreset.Exponential => CreateFreeCurve(0f, 0f, 0.5f, 0.5f, WeightedMode.Both),
                KeyframeInterpolationPreset.Elastic => CreateFreeCurve(-5.227273f, -1.710526f, 0.22f, 0.38f, WeightedMode.Both),
                _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Preset does not define an editable curve.")
            };
        }

        public static string GetDisplayName(KeyframeInterpolationPreset preset)
        {
            return preset switch {
                KeyframeInterpolationPreset.EaseIn => "Ease In",
                KeyframeInterpolationPreset.EaseOut => "Ease Out",
                KeyframeInterpolationPreset.EaseInOut => "Ease In Out",
                KeyframeInterpolationPreset.Back => "Back",
                KeyframeInterpolationPreset.Bounce => "Bounce",
                KeyframeInterpolationPreset.Circular => "Circular",
                KeyframeInterpolationPreset.Exponential => "Exponential",
                KeyframeInterpolationPreset.Elastic => "Elastic",
                _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Preset does not define a display name.")
            };
        }

        public static AnimationCurve CreateModePreviewCurve(AnimationUtility.TangentMode mode)
        {
            return mode switch {
                AnimationUtility.TangentMode.Constant => CreateConstantPreviewCurve(),
                AnimationUtility.TangentMode.Linear or AnimationUtility.TangentMode.Auto or AnimationUtility.TangentMode.ClampedAuto => CreatePreviewCurve(mode),
                _ => CreateDefaultCurve()
            };
        }

        public static AnimationCurve CreateCurveFromSegment(Keyframe left, Keyframe right, float valueScale)
        {
            var weightedMode = GetSegmentWeightedMode(left, right);
            if (Mathf.Abs(valueScale) <= TangentEpsilon) {
                return CreateFreeCurve(0f, 0f, GetOutWeight(left), GetInWeight(right), weightedMode);
            }

            var start = new Keyframe(
                time: 0f,
                value: 0f,
                inTangent: 0f,
                outTangent: SanitizeTangent(left.outTangent / valueScale),
                inWeight: DefaultWeight,
                outWeight: SanitizeWeight(GetOutWeight(left))) {
                weightedMode = (weightedMode & WeightedMode.Out) == WeightedMode.Out ? WeightedMode.Out : WeightedMode.None
            };

            var end = new Keyframe(
                time: 1f,
                value: 1f,
                inTangent: SanitizeTangent(right.inTangent / valueScale),
                outTangent: 0f,
                inWeight: SanitizeWeight(GetInWeight(right)),
                outWeight: DefaultWeight) {
                weightedMode = (weightedMode & WeightedMode.In) == WeightedMode.In ? WeightedMode.In : WeightedMode.None
            };

            return NormalizeEditableCurve(new AnimationCurve(start, end));
        }

        public static AnimationCurve NormalizeEditableCurve(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0) {
                return CreateDefaultCurve();
            }

            var sourceKeys = curve.keys;
            var first = sourceKeys[0];
            var last = sourceKeys[^1];
            if (sourceKeys.Length == 1) {
                last = first;
            }

            var start = new Keyframe(
                time: 0f,
                value: 0f,
                inTangent: 0f,
                outTangent: SanitizeTangent(first.outTangent),
                inWeight: DefaultWeight,
                outWeight: SanitizeWeight(first.outWeight)) {
                weightedMode = (first.weightedMode & WeightedMode.Out) == WeightedMode.Out ? WeightedMode.Out : WeightedMode.None
            };

            var end = new Keyframe(
                time: 1f,
                value: 1f,
                inTangent: SanitizeTangent(last.inTangent),
                outTangent: 0f,
                inWeight: SanitizeWeight(last.inWeight),
                outWeight: DefaultWeight) {
                weightedMode = (last.weightedMode & WeightedMode.In) == WeightedMode.In ? WeightedMode.In : WeightedMode.None
            };

            return CreateNormalizedFreeCurve(start, end);
        }

        public static AnimationCurve Clone(AnimationCurve curve)
        {
            var clone = new AnimationCurve(curve.keys) {
                preWrapMode = curve.preWrapMode,
                postWrapMode = curve.postWrapMode
            };

            CopyTangentModes(curve, clone);
            return clone;
        }

        public static bool TryGetSanitizedCurveForSave(AnimationCurve source, out AnimationCurve sanitized)
        {
            sanitized = null;
            if (source == null || source.length == 0) {
                return false;
            }

            var sourceKeys = source.keys;
            var sanitizedKeys = new Keyframe[sourceKeys.Length];
            var previousTime = 0f;

            for (var i = 0; i < sourceKeys.Length; i++) {
                if (!TrySanitizeKeyForSave(source, i, sourceKeys[i], out var key)) {
                    return false;
                }

                if (i > 0 && key.time - previousTime <= TimeEpsilon) {
                    return false;
                }

                sanitizedKeys[i] = key;
                previousTime = key.time;
            }

            sanitized = new AnimationCurve(sanitizedKeys) {
                preWrapMode = source.preWrapMode,
                postWrapMode = source.postWrapMode
            };
            CopyTangentModes(source, sanitized);

            return HasValidSaveData(sanitized);
        }

        public static float SanitizeWeightForSave(float value)
        {
            return SanitizeWeight(value);
        }

        public static bool Approximately(AnimationCurve a, AnimationCurve b)
        {
            if (ReferenceEquals(a, b)) {
                return true;
            }

            if (a == null || b == null || a.length != b.length) {
                return false;
            }

            for (var i = 0; i < a.length; i++) {
                if (!Approximately(a[i], b[i])
                    || AnimationUtility.GetKeyLeftTangentMode(a, i) != AnimationUtility.GetKeyLeftTangentMode(b, i)
                    || AnimationUtility.GetKeyRightTangentMode(a, i) != AnimationUtility.GetKeyRightTangentMode(b, i)
                    || AnimationUtility.GetKeyBroken(a, i) != AnimationUtility.GetKeyBroken(b, i)) {
                    return false;
                }
            }

            return true;
        }

        public static bool ApproximatelyEditableShape(AnimationCurve a, AnimationCurve b)
        {
            if (ReferenceEquals(a, b)) {
                return true;
            }

            if (a == null || b == null || a.length != b.length) {
                return false;
            }

            for (var i = 0; i < a.length; i++) {
                if (!ApproximatelyEditableShape(a[i], b[i])) {
                    return false;
                }
            }

            return true;
        }

        public static Rect GetCurveRange(AnimationCurve curve)
        {
            var min = 0f;
            var max = 1f;

            if (curve != null) {
                var keys = curve.keys;
                foreach (var key in keys) {
                    min = Mathf.Min(min, key.value);
                    max = Mathf.Max(max, key.value);
                }

                const int sampleCount = 512;
                for (var i = 0; i < sampleCount; i++) {
                    min = Mathf.Min(min, curve.Evaluate(i / (float)(sampleCount - 1)));
                    max = Mathf.Max(max, curve.Evaluate(i / (float)(sampleCount - 1)));
                }

                if (curve.length >= 2) {
                    var normalized = NormalizeEditableCurve(curve);
                    var start = normalized[0];
                    var end = normalized[normalized.length - 1];
                    var outWeight = HasOutWeight(start) ? start.outWeight : DefaultWeight;
                    var inWeight = HasInWeight(end) ? end.inWeight : DefaultWeight;
                    min = Mathf.Min(min, start.outTangent * outWeight, 1f - end.inTangent * inWeight);
                    max = Mathf.Max(max, start.outTangent * outWeight, 1f - end.inTangent * inWeight);
                }
            }

            var height = Mathf.Max(1f, max - min);
            var padding = height * 0.18f;

            return new Rect(0f, min - padding, 1f, height + padding * 2f);
        }

        public static void CopyTangentModes(AnimationCurve source, AnimationCurve destination)
        {
            if (source == null || destination == null) {
                return;
            }

            var count = Mathf.Min(source.length, destination.length);
            for (var i = 0; i < count; i++) {
                AnimationUtility.SetKeyBroken(destination, i, AnimationUtility.GetKeyBroken(source, i));
                AnimationUtility.SetKeyLeftTangentMode(destination, i, AnimationUtility.GetKeyLeftTangentMode(source, i));
                AnimationUtility.SetKeyRightTangentMode(destination, i, AnimationUtility.GetKeyRightTangentMode(source, i));
            }
        }

        private static AnimationCurve CreateFreeCurve(
            float outTangent, float inTangent,
            float outWeight, float inWeight,
            WeightedMode weightedMode)
        {
            var start = new Keyframe(
                time: 0f,
                value: 0f,
                inTangent: 0f,
                outTangent: SanitizeTangent(outTangent),
                inWeight: DefaultWeight,
                outWeight: SanitizeWeight(outWeight)) {
                weightedMode = (weightedMode & WeightedMode.Out) == WeightedMode.Out ? WeightedMode.Out : WeightedMode.None
            };

            var end = new Keyframe(
                time: 1f,
                value: 1f,
                inTangent: SanitizeTangent(inTangent),
                outTangent: 0f,
                inWeight: SanitizeWeight(inWeight),
                outWeight: DefaultWeight) {
                weightedMode = (weightedMode & WeightedMode.In) == WeightedMode.In ? WeightedMode.In : WeightedMode.None
            };

            return CreateNormalizedFreeCurve(start, end);
        }

        private static AnimationCurve CreateConstantPreviewCurve()
        {
            var start = new Keyframe(0f, 0f) {
                outTangent = float.PositiveInfinity,
                outWeight = DefaultWeight,
                weightedMode = WeightedMode.None
            };

            var end = new Keyframe(1f, 1f) {
                inTangent = float.PositiveInfinity,
                inWeight = DefaultWeight,
                weightedMode = WeightedMode.None
            };

            var curve = new AnimationCurve(start, end);
            AnimationUtility.SetKeyBroken(curve, 0, true);
            AnimationUtility.SetKeyRightTangentMode(curve, 0, AnimationUtility.TangentMode.Constant);
            AnimationUtility.SetKeyBroken(curve, 1, true);
            AnimationUtility.SetKeyLeftTangentMode(curve, 1, AnimationUtility.TangentMode.Constant);
            return curve;
        }

        private static AnimationCurve CreatePreviewCurve(AnimationUtility.TangentMode mode)
        {
            var curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));

            for (var i = 0; i < curve.length; i++) {
                AnimationUtility.SetKeyBroken(curve, i, false);
                AnimationUtility.SetKeyLeftTangentMode(curve, i, mode);
                AnimationUtility.SetKeyRightTangentMode(curve, i, mode);
            }

            for (var i = 0; i < curve.length; i++) {
                AnimationUtility.UpdateTangentsFromModeSurrounding(curve, i);
            }

            return CreateCurveFromSegment(curve[0], curve[1], 1f);
        }

        private static AnimationCurve CreateNormalizedFreeCurve(Keyframe start, Keyframe end)
        {
            var curve = new AnimationCurve(start, end);
            AnimationUtility.SetKeyBroken(curve, 0, true);
            AnimationUtility.SetKeyRightTangentMode(curve, 0, AnimationUtility.TangentMode.Free);
            AnimationUtility.SetKeyBroken(curve, 1, true);
            AnimationUtility.SetKeyLeftTangentMode(curve, 1, AnimationUtility.TangentMode.Free);
            return curve;
        }

        private static bool Approximately(Keyframe a, Keyframe b)
        {
            return Mathf.Abs(a.time - b.time) <= CompareEpsilon
                   && Mathf.Abs(a.value - b.value) <= CompareEpsilon
                   && ApproximatelyTangent(a.inTangent, b.inTangent)
                   && ApproximatelyTangent(a.outTangent, b.outTangent)
                   && Mathf.Abs(a.inWeight - b.inWeight) <= CompareEpsilon
                   && Mathf.Abs(a.outWeight - b.outWeight) <= CompareEpsilon
                   && a.weightedMode == b.weightedMode;
        }

        private static bool ApproximatelyEditableShape(Keyframe a, Keyframe b)
        {
            return Mathf.Abs(a.time - b.time) <= CompareEpsilon
                   && Mathf.Abs(a.value - b.value) <= CompareEpsilon
                   && ApproximatelyTangent(a.inTangent, b.inTangent)
                   && ApproximatelyTangent(a.outTangent, b.outTangent)
                   && Mathf.Abs(GetInWeight(a) - GetInWeight(b)) <= CompareEpsilon
                   && Mathf.Abs(GetOutWeight(a) - GetOutWeight(b)) <= CompareEpsilon
                   && a.weightedMode == b.weightedMode;
        }

        private static bool ApproximatelyTangent(float a, float b)
        {
            if (float.IsInfinity(a) || float.IsInfinity(b)) {
                return float.IsInfinity(a) && float.IsInfinity(b) && Mathf.Sign(a) == Mathf.Sign(b);
            }

            var tolerance = Mathf.Max(CompareEpsilon, Mathf.Max(Mathf.Abs(a), Mathf.Abs(b)) * RelativeTangentCompareEpsilon);
            return Mathf.Abs(a - b) <= tolerance;
        }

        private static float SanitizeTangent(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }

        private static float SanitizeWeight(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? DefaultWeight
                : Mathf.Clamp(value, MinUnityWeight, MaxUnityWeight);
        }

        private static bool TrySanitizeKeyForSave(AnimationCurve source, int index, Keyframe key, out Keyframe sanitized)
        {
            sanitized = key;
            if (!IsFinite(key.time) || !IsFinite(key.value)) {
                return false;
            }

            var leftMode = AnimationUtility.GetKeyLeftTangentMode(source, index);
            var rightMode = AnimationUtility.GetKeyRightTangentMode(source, index);

            sanitized.inTangent = SanitizeTangentForSave(key.inTangent, leftMode);
            sanitized.outTangent = SanitizeTangentForSave(key.outTangent, rightMode);
            sanitized.inWeight = SanitizeWeight(key.inWeight);
            sanitized.outWeight = SanitizeWeight(key.outWeight);
            sanitized.weightedMode &= ValidWeightedModeMask;

            if (leftMode == AnimationUtility.TangentMode.Constant) {
                sanitized.inTangent = float.PositiveInfinity;
                sanitized.inWeight = DefaultWeight;
                sanitized.weightedMode &= ~WeightedMode.In;
            }

            if (rightMode == AnimationUtility.TangentMode.Constant) {
                sanitized.outTangent = float.PositiveInfinity;
                sanitized.outWeight = DefaultWeight;
                sanitized.weightedMode &= ~WeightedMode.Out;
            }

            return true;
        }

        private static bool HasValidSaveData(AnimationCurve curve)
        {
            var keys = curve.keys;
            var previousTime = 0f;

            for (var i = 0; i < keys.Length; i++) {
                var key = keys[i];
                if (!IsFinite(key.time) || !IsFinite(key.value)) {
                    return false;
                }

                if (i > 0 && key.time - previousTime <= TimeEpsilon) {
                    return false;
                }

                if (!HasValidWeightedMode(key)
                    || !HasValidWeight(key.inWeight)
                    || !HasValidWeight(key.outWeight)
                    || !HasValidTangentForSave(key.inTangent, AnimationUtility.GetKeyLeftTangentMode(curve, i))
                    || !HasValidTangentForSave(key.outTangent, AnimationUtility.GetKeyRightTangentMode(curve, i))) {
                    return false;
                }

                previousTime = key.time;
            }

            return true;
        }

        private static float SanitizeTangentForSave(float value, AnimationUtility.TangentMode mode)
        {
            return mode == AnimationUtility.TangentMode.Constant ? float.PositiveInfinity : SanitizeTangent(value);
        }

        private static bool HasValidTangentForSave(float value, AnimationUtility.TangentMode mode)
        {
            if (mode == AnimationUtility.TangentMode.Constant) {
                return float.IsInfinity(value) && value > 0f;
            }

            return IsFinite(value);
        }

        private static bool HasValidWeightedMode(Keyframe key)
        {
            return ((int)key.weightedMode & ~(int)ValidWeightedModeMask) == 0;
        }

        private static bool HasValidWeight(float value)
        {
            return IsFinite(value) && value >= MinUnityWeight && value <= MaxUnityWeight;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float GetOutWeight(Keyframe key)
        {
            return HasOutWeight(key) ? key.outWeight : DefaultWeight;
        }

        private static float GetInWeight(Keyframe key)
        {
            return HasInWeight(key) ? key.inWeight : DefaultWeight;
        }

        private static WeightedMode GetSegmentWeightedMode(Keyframe left, Keyframe right)
        {
            var weightedMode = WeightedMode.None;

            if (HasOutWeight(left)) {
                weightedMode |= WeightedMode.Out;
            }

            if (HasInWeight(right)) {
                weightedMode |= WeightedMode.In;
            }

            return weightedMode;
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