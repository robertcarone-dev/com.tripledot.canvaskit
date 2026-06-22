using System;
using UnityEditor;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    internal static class KeyframeInterpolationCurveClipboard
    {
        private const int Version = 1;
        private const string ClipboardPrefix = "Tripledot.CanvasKit.KeyframeInterpolationCurve:v1:";

        [Serializable]
        private sealed class CurvePayload
        {
            public int version;
            public float outTangent;
            public float inTangent;
            public float outWeight;
            public float inWeight;
            public int weightedMode;
        }

        public static bool Copy(AnimationCurve curve)
        {
            if (curve == null) {
                return false;
            }

            var normalized = KeyframeInterpolationCurveUtility.NormalizeEditableCurve(curve);
            var start = normalized[0];
            var end = normalized[1];
            var weightedMode = (start.weightedMode & WeightedMode.Out) | (end.weightedMode & WeightedMode.In);

            var payload = new CurvePayload {
                version = Version,
                outTangent = start.outTangent,
                inTangent = end.inTangent,
                outWeight = start.outWeight,
                inWeight = end.inWeight,
                weightedMode = (int)weightedMode
            };

            EditorGUIUtility.systemCopyBuffer = ClipboardPrefix + JsonUtility.ToJson(payload);
            return true;
        }

        public static bool HasCurve()
        {
            return TryGetCurve(out _);
        }

        public static bool TryGetCurve(out AnimationCurve curve)
        {
            curve = null;

            var clipboard = EditorGUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(clipboard) || !clipboard.StartsWith(ClipboardPrefix, StringComparison.Ordinal)) {
                return false;
            }

            try {
                var payload = JsonUtility.FromJson<CurvePayload>(clipboard[ClipboardPrefix.Length..]);
                if (payload == null || payload.version != Version) {
                    return false;
                }

                var weightedMode = (WeightedMode)payload.weightedMode;
                var start = new Keyframe(
                    time: 0f,
                    value: 0f,
                    inTangent: 0f,
                    outTangent: payload.outTangent,
                    inWeight: KeyframeInterpolationCurveUtility.DefaultWeight,
                    outWeight: payload.outWeight) {
                    weightedMode = (weightedMode & WeightedMode.Out) == WeightedMode.Out ? WeightedMode.Out : WeightedMode.None
                };

                var end = new Keyframe(
                    time: 1f,
                    value: 1f,
                    inTangent: payload.inTangent,
                    outTangent: 0f,
                    inWeight: payload.inWeight,
                    outWeight: KeyframeInterpolationCurveUtility.DefaultWeight) {
                    weightedMode = (weightedMode & WeightedMode.In) == WeightedMode.In ? WeightedMode.In : WeightedMode.None
                };

                curve = KeyframeInterpolationCurveUtility.NormalizeEditableCurve(new AnimationCurve(start, end));
                return true;
            } catch (ArgumentException) {
                return false;
            }
        }
    }
}
