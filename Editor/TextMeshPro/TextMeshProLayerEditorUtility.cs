using UnityEditor;
using UnityEngine;

namespace Tripledot.CanvasKit.TextMeshPro.Editor
{
    internal static class TextMeshProLayerEditorUtility
    {
        private static float GetRemainingSdfBudget(float availablePadding, float reservedPadding)
        {
            return Mathf.Max(0f, availablePadding - reservedPadding);
        }

        public static float GetEffectivePositiveSdfBudget(SerializedProperty enabled, SerializedProperty property, float availablePadding)
        {
            if (enabled is { hasMultipleDifferentValues: false, boolValue: false }) {
                return 0f;
            }

            return GetEffectivePositiveSdfBudget(GetFloatValueIfSame(property), availablePadding);
        }

        public static float GetEffectivePositiveSdfBudget(float value, float availablePadding)
        {
            return Mathf.Min(Mathf.Max(0f, value), Mathf.Max(0f, availablePadding));
        }

        public static void GetStrokeSliderBudgets(
            SerializedProperty width, SerializedProperty feather, SerializedProperty position, float availablePadding, float reservedPadding,
            out float widthMax, out float featherMax)
        {
            GetStrokeSliderBudgets(GetFloatValueIfSame(width), GetFloatValueIfSame(feather), GetStrokePosition(position), availablePadding, reservedPadding, out widthMax, out featherMax);
        }

        public static void GetStrokeSliderBudgets(
            float width, float feather, TextMeshProStrokePosition position, float availablePadding, float reservedPadding,
            out float widthMax, out float featherMax)
        {
            var remainingBudget = GetRemainingSdfBudget(availablePadding, reservedPadding);
            var strokeWidthFactor = TextMeshProUtility.GetStrokeVisualPaddingFactor(position);
            widthMax = strokeWidthFactor > 0.0f ? remainingBudget / strokeWidthFactor : remainingBudget;
            TextMeshProUtility.ClampStrokeEffect(width, feather, position, availablePadding, reservedPadding, out var effectiveWidth, out _);
            featherMax = GetRemainingSdfBudget(remainingBudget, effectiveWidth * strokeWidthFactor);
        }

        public static void GetShadowSliderBudgets(
            SerializedProperty spread, SerializedProperty blur, float availablePadding, float reservedPadding,
            out float spreadMin, out float spreadMax, out float blurMax)
        {
            GetShadowSliderBudgets(GetFloatValueIfSame(spread), GetFloatValueIfSame(blur), availablePadding, reservedPadding, out spreadMin, out spreadMax, out blurMax);
        }

        public static void GetShadowSliderBudgets(
            float spread, float blur, float availablePadding, float reservedPadding,
            out float spreadMin, out float spreadMax, out float blurMax)
        {
            spreadMax = GetRemainingSdfBudget(availablePadding, reservedPadding);
            spreadMin = -availablePadding;
            TextMeshProUtility.ClampShadowEffect(spread, blur, availablePadding, reservedPadding, out var effectiveSpread, out _);
            blurMax = GetRemainingSdfBudget(spreadMax, effectiveSpread);
        }

        public static bool IsShadowEffectClamped(float spread, float blur, float availablePadding, float reservedPadding)
        {
            TextMeshProUtility.ClampShadowEffect(spread, blur, availablePadding, reservedPadding, out var effectiveSpread, out var effectiveBlur);
            return !Mathf.Approximately(spread, effectiveSpread) || !Mathf.Approximately(blur, effectiveBlur);
        }

        public static void DrawShadowClampWarning(SerializedProperty spread, SerializedProperty blur, float availablePadding, float reservedPadding)
        {
            if (spread.hasMultipleDifferentValues || blur.hasMultipleDifferentValues) {
                return;
            }

            if (!IsShadowEffectClamped(spread.floatValue, blur.floatValue, availablePadding, reservedPadding)) {
                return;
            }

            EditorGUILayout.HelpBox(TextMeshProLayerInspectorStyles.ShadowClampWarning.text, MessageType.Warning);
        }

        private static float GetFloatValueIfSame(SerializedProperty property)
        {
            return property is { hasMultipleDifferentValues: false } ? property.floatValue : 0f;
        }

        private static TextMeshProStrokePosition GetStrokePosition(SerializedProperty property)
        {
            if (property is not { hasMultipleDifferentValues: false }) {
                return TextMeshProStrokePosition.Outside;
            }

            return (TextMeshProStrokePosition)Mathf.Clamp(property.enumValueIndex, 0, 2);
        }
    }
}
