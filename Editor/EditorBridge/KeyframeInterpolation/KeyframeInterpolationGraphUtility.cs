using System;
using System.Globalization;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    internal static class KeyframeInterpolationGraphUtility
    {
        public const float HandleHitSize = 10f;
        public const float GraphMinHeight = 96f;
        public const float GraphPlotInset = 9f;
        public const float UnityHandleWeightEpsilon = 0.0001f;
        public const int GraphCurveMinSegments = 160;
        public const int GraphCurveMaxSegments = 1024;

        private const float DragOverscrollSoftness = 1.15f;
        private const float DragOverscrollMinScale = 0.18f;

        public static readonly Color GraphColor = new Color(0.095f, 0.095f, 0.095f, 1f);
        public static readonly Color GraphBorderColor = new Color(0.06f, 0.06f, 0.06f, 1f);
        public static readonly Color AxisColor = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color TimeCursorColor = new Color(1f, 1f, 1f, 0.86f);
        public static readonly Color OutHandleColor = new Color(0.24f, 0.74f, 0.64f, 1f);
        public static readonly Color InHandleColor = new Color(0.24f, 0.62f, 0.95f, 1f);

        public static Rect GetGraphPlotRect(Rect graphRect)
        {
            return new Rect(
                graphRect.x + GraphPlotInset,
                graphRect.y + GraphPlotInset,
                Mathf.Max(1f, graphRect.width - GraphPlotInset * 2f),
                Mathf.Max(1f, graphRect.height - GraphPlotInset * 2f));
        }

        public static Color GetColor(Color color, bool enabled)
        {
            return enabled ? color : new Color(color.r, color.g, color.b, color.a * 0.35f);
        }

        public static Vector2 CurveToGUI(Rect rect, Vector2 value, float minY, float maxY)
        {
            return new Vector2(
                rect.x + value.x * rect.width,
                Mathf.Lerp(rect.yMax, rect.y, Mathf.InverseLerp(minY, maxY, value.y)));
        }

        public static Vector2 ApplyHandleDragDelta(Vector2 handle, Vector2 mouseDelta, Rect rect, Rect inputRange)
        {
            var rangeHeight = Mathf.Max(0.0001f, inputRange.height);
            var delta = new Vector2(
                rect.width > 0f ? mouseDelta.x / rect.width : 0f,
                rect.height > 0f ? -mouseDelta.y / rect.height * rangeHeight : 0f);
            delta.y *= GetOverscrollDragScale(handle.y, inputRange.yMin, inputRange.yMax);
            return handle + delta;
        }

        public static void GetHandlePoints(AnimationCurve curve, out Vector2 outHandle, out Vector2 inHandle)
        {
            var normalized = KeyframeInterpolationCurveUtility.NormalizeEditableCurve(curve);
            var start = normalized[0];
            var end = normalized[normalized.length - 1];
            var outWeight = HasOutWeight(start) ? start.outWeight : KeyframeInterpolationCurveUtility.DefaultWeight;
            var inWeight = HasInWeight(end) ? end.inWeight : KeyframeInterpolationCurveUtility.DefaultWeight;
            outHandle = new Vector2(DisplayHandleX(outWeight), start.outTangent * outWeight);
            inHandle = new Vector2(DisplayHandleX(1f - inWeight), 1f - end.inTangent * inWeight);
        }

        public static AnimationCurve SetOutHandle(AnimationCurve curve, Vector2 handle)
        {
            var normalized = KeyframeInterpolationCurveUtility.NormalizeEditableCurve(curve);
            var start = normalized[0];
            var outWeight = SanitizeHandleWeight(handle.x);
            start.outWeight = outWeight;
            start.outTangent = handle.y / outWeight;
            start.weightedMode |= WeightedMode.Out;
            normalized.MoveKey(0, start);
            return KeyframeInterpolationCurveUtility.NormalizeEditableCurve(normalized);
        }

        public static AnimationCurve SetInHandle(AnimationCurve curve, Vector2 handle)
        {
            var normalized = KeyframeInterpolationCurveUtility.NormalizeEditableCurve(curve);
            var endIndex = normalized.length - 1;
            var end = normalized[endIndex];
            var inWeight = SanitizeHandleWeight(1f - handle.x);
            end.inWeight = inWeight;
            end.inTangent = (1f - handle.y) / inWeight;
            end.weightedMode |= WeightedMode.In;
            normalized.MoveKey(endIndex, end);
            return KeyframeInterpolationCurveUtility.NormalizeEditableCurve(normalized);
        }

        public static string FormatHandleValue(float value)
        {
            if (Mathf.Abs(value) < 0.005f) {
                value = 0f;
            }

            if (Mathf.Abs(value - Mathf.Round(value)) < 0.005f) {
                return Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture);
            }

            var text = value.ToString("0.##", CultureInfo.InvariantCulture);
            if (text.StartsWith("0.", StringComparison.Ordinal)) {
                return text[1..];
            }

            if (text.StartsWith("-0.", StringComparison.Ordinal)) {
                return "-" + text[2..];
            }

            return text;
        }

        private static float GetOverscrollDragScale(float value, float minY, float maxY)
        {
            var rangeHeight = Mathf.Max(0.0001f, maxY - minY);
            var overscroll = value < minY
                ? (minY - value) / rangeHeight
                : value > maxY ? (value - maxY) / rangeHeight : 0f;
            
            if (overscroll <= 0f) {
                return 1f;
            }

            var t = overscroll / (overscroll + DragOverscrollSoftness);
            var cubicSlowdown = 1f - t * t * t;
            return DragOverscrollMinScale + (1f - DragOverscrollMinScale) * cubicSlowdown;
        }

        private static float SanitizeHandleWeight(float value)
        {
            return Mathf.Clamp(value, UnityHandleWeightEpsilon, 1f - UnityHandleWeightEpsilon);
        }

        private static float DisplayHandleX(float value)
        {
            if (value <= UnityHandleWeightEpsilon * 1.01f) {
                return 0f;
            }

            return value >= 1f - UnityHandleWeightEpsilon * 1.01f ? 1f : value;
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
