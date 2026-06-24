using System;
using UnityEditor;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor.KeyframeInterpolation
{
    internal static class KeyframeInterpolationCurveClipboard
    {
        private const int CurrentVersion = 1;
        private const string ClipboardPrefix = "Tripledot.CanvasKit.KeyframeInterpolationCurve:v1:";

        [Serializable]
        private sealed class CurvePayload
        {
            public int Version;
            public float OutTangent;
            public float InTangent;
            public float OutWeight;
            public float InWeight;
            public int WeightedMode;
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
                Version = CurrentVersion,
                OutTangent = start.outTangent,
                InTangent = end.inTangent,
                OutWeight = start.outWeight,
                InWeight = end.inWeight,
                WeightedMode = (int)weightedMode
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
                if (payload == null || payload.Version != CurrentVersion) {
                    return false;
                }

                var weightedMode = (WeightedMode)payload.WeightedMode;
                var start = new Keyframe(
                    time: 0f,
                    value: 0f,
                    inTangent: 0f,
                    outTangent: payload.OutTangent,
                    inWeight: KeyframeInterpolationCurveUtility.DefaultWeight,
                    outWeight: payload.OutWeight) {
                    weightedMode = (weightedMode & WeightedMode.Out) == WeightedMode.Out ? WeightedMode.Out : WeightedMode.None
                };

                var end = new Keyframe(
                    time: 1f,
                    value: 1f,
                    inTangent: payload.InTangent,
                    outTangent: 0f,
                    inWeight: payload.InWeight,
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