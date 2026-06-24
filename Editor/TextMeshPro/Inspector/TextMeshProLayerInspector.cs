using UnityEditor;

namespace Tripledot.CanvasKit.TextMeshPro.Editor
{
    internal sealed class TextMeshProLayerInspector
    {
        private enum InspectorMode
        {
            Preset,
            Stack
        }

        private readonly InspectorMode mode;
        private readonly TextMeshProLayerInspectorState state;
        private readonly TextMeshProLayerListView listView;

        public TextMeshProLayerInspector(
            TextMeshProLayerPreset preset,
            SerializedObject presetObject,
            SerializedProperty presetLayers)
        {
            mode = InspectorMode.Preset;
            state = TextMeshProLayerInspectorState.ForPreset(preset, presetObject, presetLayers);
            listView = new TextMeshProLayerListView(state);
        }

        public TextMeshProLayerInspector(
            TextMeshProLayerStack stack,
            SerializedObject stackObject,
            SerializedProperty presetProperty,
            SerializedProperty localLayers,
            SerializedProperty presetLayerOverrides)
        {
            mode = InspectorMode.Stack;
            state = TextMeshProLayerInspectorState.ForStack(stack, stackObject, presetProperty, localLayers, presetLayerOverrides);
            listView = new TextMeshProLayerListView(state);
        }

        public void Draw()
        {
            if (mode == InspectorMode.Preset) {
                listView.Draw(state.Layers, TextMeshProLayerSource.Preset);
                return;
            }

            var assignedPreset = state.PresetProperty.objectReferenceValue as TextMeshProLayerPreset;
            if (assignedPreset == null) {
                state.ClearLinkedPresetCache();
                listView.Draw(state.Layers, TextMeshProLayerSource.Local);
                return;
            }

            DrawLinkedPreset(assignedPreset);
        }

        public void ClearLinkedPresetCache()
        {
            state.ClearLinkedPresetCache();
        }

        private void DrawLinkedPreset(TextMeshProLayerPreset assignedPreset)
        {
            state.EnsureLinkedPresetCache(assignedPreset);
            state.LinkedPresetObject.Update();
            state.EnsureOverrideArraySize(state.LinkedPresetLayers.arraySize);
            listView.Draw(state.LinkedPresetLayers, TextMeshProLayerSource.LinkedPreset);
            state.ApplyLinkedPresetProperties(assignedPreset);
        }

        internal static bool GetPresetInstanceSegmentResult(bool currentInstance, bool sharedSelectedAfterDraw, bool instanceSelectedAfterDraw)
        {
            return TextMeshProLayerListView.GetPresetInstanceSegmentResult(currentInstance, sharedSelectedAfterDraw, instanceSelectedAfterDraw);
        }

        internal static void GetStrokeSliderBudgets(
            SerializedProperty width, SerializedProperty feather, SerializedProperty position, float availablePadding, float reservedPadding,
            out float widthMax, out float featherMax)
        {
            TextMeshProLayerEditorUtility.GetStrokeSliderBudgets(width, feather, position, availablePadding, reservedPadding, out widthMax, out featherMax);
        }

        internal static void GetStrokeSliderBudgets(
            float width, float feather, TextMeshProStrokePosition position, float availablePadding, float reservedPadding,
            out float widthMax, out float featherMax)
        {
            TextMeshProLayerEditorUtility.GetStrokeSliderBudgets(width, feather, position, availablePadding, reservedPadding, out widthMax, out featherMax);
        }

        internal static void GetShadowSliderBudgets(
            SerializedProperty spread, SerializedProperty blur, float availablePadding, float reservedPadding,
            out float spreadMin, out float spreadMax, out float blurMax)
        {
            TextMeshProLayerEditorUtility.GetShadowSliderBudgets(spread, blur, availablePadding, reservedPadding, out spreadMin, out spreadMax, out blurMax);
        }

        internal static void GetShadowSliderBudgets(
            float spread, float blur, float availablePadding, float reservedPadding,
            out float spreadMin, out float spreadMax, out float blurMax)
        {
            TextMeshProLayerEditorUtility.GetShadowSliderBudgets(spread, blur, availablePadding, reservedPadding, out spreadMin, out spreadMax, out blurMax);
        }

        internal static float GetEffectivePositiveSdfBudget(SerializedProperty enabled, SerializedProperty property, float availablePadding)
        {
            return TextMeshProLayerEditorUtility.GetEffectivePositiveSdfBudget(enabled, property, availablePadding);
        }

        internal static float GetEffectivePositiveSdfBudget(float value, float availablePadding)
        {
            return TextMeshProLayerEditorUtility.GetEffectivePositiveSdfBudget(value, availablePadding);
        }

        internal static bool IsShadowEffectClamped(float spread, float blur, float availablePadding, float reservedPadding)
        {
            return TextMeshProLayerEditorUtility.IsShadowEffectClamped(spread, blur, availablePadding, reservedPadding);
        }

        internal static CanvasPaint ReadPaintForSwatch(SerializedProperty paint)
        {
            return TextMeshProLayerSwatches.ReadPaintForSwatch(paint);
        }

        internal static UnityEngine.GUIContent GetLayerDisplayContent(SerializedProperty layer)
        {
            return TextMeshProLayerInspectorStyles.GetLayerDisplayContent(new TextMeshProSerializedLayer(layer).DisplayLabel);
        }
    }
}
