using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using Tripledot.CanvasKit.Editor;
using UnityObject = UnityEngine.Object;

namespace Tripledot.CanvasKit.TextMeshPro.Editor
{
    internal sealed class TextMeshProLayerDetailsView
    {
        public void DrawLayerDetails(TextMeshProSerializedLayer layer, int layerIndex, TextMeshProLayerInspectorContext context)
        {
            GUILayout.Space(4f);
            EditorGUILayout.PropertyField(layer.Label, TextMeshProLayerInspectorStyles.Label);

            EditorGUILayout.PropertyField(layer.BlendMode, TextMeshProLayerInspectorStyles.BlendMode);
            EditorGUILayout.Slider(layer.Opacity, 0f, 1f, TextMeshProLayerInspectorStyles.Opacity);

            GUILayout.Space(5f);
            CoreEditorUtils.DrawSplitter();
            GUILayout.Space(5f);
            DrawUnifiedLayer(layer, layerIndex, context);
        }

        private void DrawUnifiedLayer(TextMeshProSerializedLayer layer, int layerIndex, TextMeshProLayerInspectorContext context)
        {
            var face = layer.Face;
            var stroke = layer.Stroke;
            var shadow = layer.Shadow;
            GUILayout.Space(6f);

            var faceExpanded = BeginToggleSection(layer, TextMeshProLayerInspectorStyles.Face, face.Enabled);
            if (faceExpanded) {
                using (new EditorGUI.DisabledScope(face.Enabled is { hasMultipleDifferentValues: false, boolValue: false })) {
                    CanvasPaintDrawer.DrawFillMode(face.Paint);
                    CanvasPaintDrawer.DrawAppearance(face.Paint);
                    DrawPaintMapping(face.Paint, context.SceneTarget, layerIndex, true);

                    CanvasEditorGUI.DrawRoundedInspectorSubsection(TextMeshProLayerInspectorStyles.Shape);
                    CanvasEditorGUI.SdfLengthSlider(face.Dilate, TextMeshProLayerInspectorStyles.Dilate, context.AvailablePadding, -context.AvailablePadding, context.AvailablePadding);

                    CanvasEditorGUI.DrawRoundedInspectorSubsection(TextMeshProLayerInspectorStyles.Lighting);
                    DrawFaceLighting(face.Lighting);
                }
            }
            EndToggleSection(faceExpanded);

            var strokeExpanded = BeginToggleSection(layer, TextMeshProLayerInspectorStyles.Outline, stroke.Enabled);
            if (strokeExpanded) {
                using (new EditorGUI.DisabledScope(stroke.Enabled is { hasMultipleDifferentValues: false, boolValue: false })) {
                    CanvasPaintDrawer.DrawFillMode(stroke.Paint);
                    CanvasPaintDrawer.DrawAppearance(stroke.Paint);
                    DrawPaintMapping(stroke.Paint, context.SceneTarget, layerIndex, true);

                    CanvasEditorGUI.DrawRoundedInspectorSubsection(TextMeshProLayerInspectorStyles.Shape);
                    CanvasEditorGUI.PropertyField(stroke.Position, TextMeshProLayerInspectorStyles.Position);
                    var reservedFacePadding = TextMeshProLayerEditorUtility.GetEffectivePositiveSdfBudget(face.Enabled, face.Dilate, context.AvailablePadding);
                    TextMeshProLayerEditorUtility.GetStrokeSliderBudgets(stroke.Width, stroke.Feather, stroke.Position, context.AvailablePadding, reservedFacePadding, out var widthMax, out var featherMax);
                    CanvasEditorGUI.ConstrainedSdfLengthSlider(stroke.Width, TextMeshProLayerInspectorStyles.Width, context.AvailablePadding, 0f, widthMax);
                    CanvasEditorGUI.ConstrainedSdfLengthSlider(stroke.Feather, TextMeshProLayerInspectorStyles.Feather, context.AvailablePadding, 0f, featherMax);
                    CanvasEditorGUI.Vector2Field(stroke.Offset, TextMeshProLayerInspectorStyles.Offset);
                }
            }
            EndToggleSection(strokeExpanded);

            var shadowExpanded = BeginToggleSection(layer, TextMeshProLayerInspectorStyles.Shadow, shadow.Enabled);
            if (shadowExpanded) {
                using (new EditorGUI.DisabledScope(shadow.Enabled is { hasMultipleDifferentValues: false, boolValue: false })) {
                    CanvasPaintDrawer.DrawFillMode(shadow.Paint);
                    CanvasPaintDrawer.DrawAppearance(shadow.Paint);
                    DrawPaintMapping(shadow.Paint, context.SceneTarget, layerIndex, true);

                    CanvasEditorGUI.DrawRoundedInspectorSubsection(TextMeshProLayerInspectorStyles.Effect);
                    var reservedFacePadding = TextMeshProLayerEditorUtility.GetEffectivePositiveSdfBudget(face.Enabled, face.Dilate, context.AvailablePadding);
                    TextMeshProLayerEditorUtility.GetShadowSliderBudgets(shadow.Spread, shadow.Blur, context.AvailablePadding, reservedFacePadding, out var spreadMin, out var spreadMax, out var blurMax);
                    CanvasEditorGUI.ConstrainedSdfLengthSlider(shadow.Spread, TextMeshProLayerInspectorStyles.Spread, context.AvailablePadding, spreadMin, spreadMax);
                    CanvasEditorGUI.ConstrainedSdfLengthSlider(shadow.Blur, TextMeshProLayerInspectorStyles.Blur, context.AvailablePadding, 0f, blurMax);
                    CanvasEditorGUI.Vector2Field(shadow.Offset, TextMeshProLayerInspectorStyles.Offset);
                    if (shadow.Enabled is { hasMultipleDifferentValues: false, boolValue: true }) {
                        TextMeshProLayerEditorUtility.DrawShadowClampWarning(shadow.Spread, shadow.Blur, context.AvailablePadding, reservedFacePadding);
                    }
                }
            }
            EndToggleSection(shadowExpanded);
        }

        private static void DrawPaintMapping(SerializedCanvasPaint paint, UnityObject sceneTarget, int layerIndex, bool boxed = false)
        {
            if (CanvasPaintDrawer.HasMapping(paint)) {
                CanvasPaintDrawer.DrawMappingHeader(paint, sceneTarget, boxed, layerIndex);
                CanvasPaintDrawer.DrawMapping(paint);
            }
        }

        private static void DrawFaceLighting(TextMeshProSerializedFaceLighting lighting)
        {
            EditorGUILayout.PropertyField(lighting.Enabled, TextMeshProLayerInspectorStyles.EnableLighting);

            using (new EditorGUI.DisabledScope(lighting.Enabled is { hasMultipleDifferentValues: false, boolValue: false })) {
                EditorGUILayout.Slider(lighting.BevelWidth, 0f, 1f, TextMeshProLayerInspectorStyles.BevelWidth);
                EditorGUILayout.Slider(lighting.BevelSoftness, 0f, 1f, TextMeshProLayerInspectorStyles.BevelSoftness);
                EditorGUILayout.Slider(lighting.LightAngle, 0f, 360f, TextMeshProLayerInspectorStyles.LightAngle);
                CanvasPaintDrawer.DrawColor(lighting.HighlightColor, TextMeshProLayerInspectorStyles.HighlightColor, lighting.HighlightColorUsesHdrPicker);
                CanvasPaintDrawer.DrawColor(lighting.ShadowColor, TextMeshProLayerInspectorStyles.ShadowColor, lighting.ShadowColorUsesHdrPicker);
            }
        }

        private bool BeginToggleSection(TextMeshProSerializedLayer layer, GUIContent title, SerializedProperty enabledProperty)
        {
            var key = layer.Root.serializedObject.targetObject.GetInstanceID() + "." + layer.Root.propertyPath + "." + title.text;
            var expanded = SessionState.GetBool(key, true);

            EditorGUILayout.BeginVertical(CanvasEditorGUI.Styles.RoundedInspectorPanelStyle);
            expanded = DrawHeaderToggleFoldout(title, expanded, enabledProperty);
            SessionState.SetBool(key, expanded);
            if (expanded) {
                EditorGUILayout.BeginVertical(CanvasEditorGUI.Styles.RoundedInspectorPanelContentStyle);
            }

            return expanded;
        }

        private static void EndToggleSection(bool expanded)
        {
            if (expanded) {
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
            CanvasEditorGUI.DrawRoundedInspectorPanelBorder(GUILayoutUtility.GetLastRect());
        }

        private bool DrawHeaderToggleFoldout(GUIContent title, bool expanded, SerializedProperty enabledProperty)
        {
            var headerRect = GUILayoutUtility.GetRect(1f, TextMeshProLayerInspectorStyles.FillSectionHeaderHeight);
            GUI.Label(headerRect, GUIContent.none, CanvasEditorGUI.Styles.GetRoundedInspectorPanelHeaderStyle(expanded));

            var foldoutRect = headerRect;
            foldoutRect.x += 9f;
            foldoutRect.y += Mathf.Floor((headerRect.height - TextMeshProLayerInspectorStyles.FoldoutSize) * 0.5f);
            foldoutRect.width = TextMeshProLayerInspectorStyles.FoldoutSize;
            foldoutRect.height = TextMeshProLayerInspectorStyles.FoldoutSize;

            var toggleRect = headerRect;
            toggleRect.x = foldoutRect.xMax + 5f;
            toggleRect.y += Mathf.Floor((headerRect.height - TextMeshProLayerInspectorStyles.EnabledToggleSize) * 0.5f);
            toggleRect.width = TextMeshProLayerInspectorStyles.EnabledToggleSize;
            toggleRect.height = TextMeshProLayerInspectorStyles.EnabledToggleSize;

            var nextX = toggleRect.xMax + 8f;
            if (title.image != null) {
                var iconRect = headerRect;
                iconRect.x = nextX;
                iconRect.y += Mathf.Floor((headerRect.height - TextMeshProLayerInspectorStyles.LayerIconSize) * 0.5f);
                iconRect.width = TextMeshProLayerInspectorStyles.LayerIconSize;
                iconRect.height = TextMeshProLayerInspectorStyles.LayerIconSize;
                GUI.DrawTexture(iconRect, title.image, ScaleMode.ScaleToFit);
                nextX = iconRect.xMax + 6f;
            }

            var labelRect = headerRect;
            labelRect.xMin = nextX;
            labelRect.xMax -= 8f;

            using (new EditorGUI.DisabledScope(enabledProperty is { hasMultipleDifferentValues: false, boolValue: false })) {
                EditorGUI.LabelField(labelRect, TextMeshProLayerInspectorStyles.GetTextOnlyContent(title), EditorStyles.boldLabel);
            }

            expanded = GUI.Toggle(foldoutRect, expanded, GUIContent.none, EditorStyles.foldout);

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = enabledProperty.hasMultipleDifferentValues;
            var enabled = GUI.Toggle(
                toggleRect,
                enabledProperty.hasMultipleDifferentValues || enabledProperty.boolValue,
                GUIContent.none,
                enabledProperty.hasMultipleDifferentValues ? CoreEditorStyles.smallMixedTickbox : CoreEditorStyles.smallTickbox);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck()) {
                enabledProperty.boolValue = enabled;
            }

            var evt = Event.current;
            if (evt.type == EventType.MouseDown &&
                evt.button == 0 &&
                headerRect.Contains(evt.mousePosition) &&
                !foldoutRect.Contains(evt.mousePosition) &&
                !toggleRect.Contains(evt.mousePosition)) {
                expanded = !expanded;
                evt.Use();
            }

            if (expanded) {
                CanvasEditorGUI.DrawRoundedInspectorHeaderSeparator(headerRect);
            }

            return expanded;
        }
    }
}
