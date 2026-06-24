using System;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditorInternal;
using UnityEngine;
using Tripledot.CanvasKit.Editor;

namespace Tripledot.CanvasKit.TextMeshPro.Editor
{
    internal sealed class TextMeshProLayerListView
    {
        private readonly TextMeshProLayerInspectorState state;
        private readonly TextMeshProLayerDetailsView detailsView = new TextMeshProLayerDetailsView();

        public TextMeshProLayerListView(TextMeshProLayerInspectorState state)
        {
            this.state = state;
        }

        public void Draw(SerializedProperty layerProperty, TextMeshProLayerSource source)
        {
            var layerList = CreateLayerList(layerProperty, source);
            layerList.DoLayoutList();
            DrawLayerInspectorBlocks(layerProperty, source);
        }

        private ReorderableList CreateLayerList(SerializedProperty layerProperty, TextMeshProLayerSource source)
        {
            var editable = source != TextMeshProLayerSource.LinkedPreset;

            return new ReorderableList(layerProperty.serializedObject, layerProperty, editable, true, editable, editable) {
                elementHeight = 26f,
                headerHeight = 23f,
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, TextMeshProLayerInspectorStyles.Layers),
                drawNoneElementCallback = rect => EditorGUI.HelpBox(rect, TextMeshProLayerInspectorStyles.LayerStackEmptyInfo.text, MessageType.Info),
                drawElementCallback = (rect, index, _, _) => DrawLayerListRow(rect, index, layerProperty, source),
                onAddDropdownCallback = (rect, _) => ShowAddLayerMenu(rect, layerProperty),
                onRemoveCallback = list => ReorderableList.defaultBehaviours.DoRemoveButton(list)
            };
        }

        private void DrawLayerInspectorBlocks(SerializedProperty layers, TextMeshProLayerSource source)
        {
            if (layers.arraySize == 0) {
                return;
            }

            var context = state.CreateDrawContext(source);
            for (var i = 0; i < layers.arraySize; i++) {
                var layer = state.GetLayer(layers, source, i);
                var serializedLayer = new TextMeshProSerializedLayer(layer);
                var expanded = DrawLayerInspectorHeader(serializedLayer, i, context);

                if (expanded) {
                    using (new EditorGUI.DisabledScope(serializedLayer.IsDisabled)) {
                        detailsView.DrawLayerDetails(serializedLayer, i, context);
                    }

                    EditorGUILayout.Space(1f);
                }
            }
        }

        private void DrawLayerListRow(
            Rect rect,
            int index,
            SerializedProperty layers,
            TextMeshProLayerSource source)
        {
            var layer = state.GetLayer(layers, source, index);
            var serializedLayer = new TextMeshProSerializedLayer(layer);
            var hasTrailingControl = source == TextMeshProLayerSource.LinkedPreset;

            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;

            var rowRects = new TextMeshProLayerListRowRects(rect, hasTrailingControl);
            var enabled = serializedLayer.Enabled;

            EditorGUI.BeginChangeCheck();
            var layerEnabled = EditorGUI.Toggle(rowRects.Enabled, enabled.boolValue);
            if (EditorGUI.EndChangeCheck()) {
                enabled.boolValue = layerEnabled;
            }

            TextMeshProLayerSwatches.DrawLayerSwatch(rowRects.Swatch, serializedLayer);

            var featureFlags = serializedLayer.FeatureFlags;
            var titleRect = TextMeshProLayerSwatches.GetLayerTitleRect(rowRects.Label, TextMeshProLayerSwatches.GetLayerFeatureIconCount(featureFlags));
            using (new EditorGUI.DisabledScope(serializedLayer.IsDisabled)) {
                CanvasEditorGUI.DrawSwatchLabel(titleRect, TextMeshProLayerSwatches.GetLayerDisplayContent(serializedLayer));
                TextMeshProLayerSwatches.DrawFeatureIconBadges(rowRects.Label, featureFlags);
            }

            if (hasTrailingControl) {
                DrawLayerModeControl(rowRects.Trailing, index);
            }
        }

        private bool DrawLayerInspectorHeader(TextMeshProSerializedLayer layer, int index, TextMeshProLayerInspectorContext context)
        {
            var rect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(1f, TextMeshProLayerInspectorStyles.LayerHeaderHeight));
            var backgroundRect = rect;
            backgroundRect.xMin = 0f;
            backgroundRect.width += 4f;
            DrawLayerHeaderBackground(backgroundRect);

            var key = GetLayerExpansionKey(layer.Root, index, context.ContextKey);
            var expanded = SessionState.GetBool(key, true);
            var instanceLayer = context.Source == TextMeshProLayerSource.LinkedPreset && state.IsLinkedPresetInstanceLayer(index);
            var headerRects = new TextMeshProLayerHeaderRects(rect, context.ShowPresetModeMarker);
            var enabled = layer.Enabled;

            expanded = GUI.Toggle(headerRects.Foldout, expanded, GUIContent.none, EditorStyles.foldout);

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = enabled.hasMultipleDifferentValues;
            var layerEnabled = GUI.Toggle(
                headerRects.Enabled,
                enabled.hasMultipleDifferentValues || enabled.boolValue,
                GUIContent.none,
                enabled.hasMultipleDifferentValues ? CoreEditorStyles.smallMixedTickbox : CoreEditorStyles.smallTickbox);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck()) {
                enabled.boolValue = layerEnabled;
            }

            TextMeshProLayerSwatches.DrawLayerSwatch(headerRects.Swatch, layer);

            var titleRect = TextMeshProLayerSwatches.GetLayerTitleRect(headerRects.Label, 0);
            using (new EditorGUI.DisabledScope(layer.IsDisabled)) {
                EditorGUI.LabelField(titleRect, TextMeshProLayerSwatches.GetLayerDisplayContent(layer), EditorStyles.boldLabel);
            }

            if (context.ShowPresetModeMarker) {
                TextMeshProLayerSwatches.DrawPresetModeMarker(
                    headerRects.InstanceMarker,
                    instanceLayer ? TextMeshProLayerInspectorStyles.InstanceLayer : TextMeshProLayerInspectorStyles.SharedLayer);
            }

            var evt = Event.current;
            if (evt.type == EventType.MouseDown &&
                evt.button == 0 &&
                rect.Contains(evt.mousePosition) &&
                !headerRects.Foldout.Contains(evt.mousePosition) &&
                !headerRects.Enabled.Contains(evt.mousePosition)) {
                expanded = !expanded;
                evt.Use();
            }

            SessionState.SetBool(key, expanded);
            return expanded;
        }

        private static void DrawLayerHeaderBackground(Rect rect)
        {
            var isProSkin = EditorGUIUtility.isProSkin;
            EditorGUI.DrawRect(rect, isProSkin ? TextMeshProLayerInspectorStyles.LayerHeaderBackgroundColorDark : TextMeshProLayerInspectorStyles.LayerHeaderBackgroundColorLight);

            var topSeparatorColor = isProSkin ? TextMeshProLayerInspectorStyles.LayerHeaderTopSeparatorColorDark : TextMeshProLayerInspectorStyles.LayerHeaderTopSeparatorColorLight;
            var bottomSeparatorColor = isProSkin ? TextMeshProLayerInspectorStyles.LayerHeaderBottomSeparatorColorDark : TextMeshProLayerInspectorStyles.LayerHeaderBottomSeparatorColorLight;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), topSeparatorColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), bottomSeparatorColor);
        }

        private static string GetLayerExpansionKey(SerializedProperty layer, int index, string contextKey)
        {
            var rootId = layer.serializedObject.targetObject.GetInstanceID().ToString();
            return "TextMeshProLayerInspector." + contextKey + "." + rootId + "." + layer.propertyPath + "." + index;
        }

        private void ShowAddLayerMenu(Rect rect, SerializedProperty layers)
        {
            var menu = new GenericMenu();
            menu.AddItem(TextMeshProLayerInspectorStyles.Layer, false, () => AddLayer(layers, TextMeshProLayerData.Default, TextMeshProLayerInspectorStyles.Layer.text));
            menu.AddItem(TextMeshProLayerInspectorStyles.Stroke, false, () => AddLayer(layers, TextMeshProLayerData.StrokePreset, TextMeshProLayerInspectorStyles.Stroke.text));
            menu.AddItem(TextMeshProLayerInspectorStyles.Shadow, false, () => AddLayer(layers, TextMeshProLayerData.ShadowPreset, TextMeshProLayerInspectorStyles.Shadow.text));
            menu.AddItem(TextMeshProLayerInspectorStyles.Glow, false, () => AddLayer(layers, TextMeshProLayerData.GlowPreset, TextMeshProLayerInspectorStyles.Glow.text));
            menu.DropDown(rect);
        }

        private static void AddLayer(SerializedProperty layers, Func<TextMeshProLayerData> createLayer, string label)
        {
            Undo.RecordObjects(layers.serializedObject.targetObjects, "Add TextMeshPro Layer");

            foreach (var target in layers.serializedObject.targetObjects) {
                var layer = CreateLabeledLayer(createLayer, label);
                switch (target) {
                    case TextMeshProLayerStack stack:
                        stack.AddLocalLayer(layer);
                        EditorUtility.SetDirty(stack);
                        break;
                    case TextMeshProLayerPreset preset:
                        preset.AddLayer(layer);
                        EditorUtility.SetDirty(preset);
                        break;
                }
            }

            layers.serializedObject.Update();
        }

        private static TextMeshProLayerData CreateLabeledLayer(Func<TextMeshProLayerData> createLayer, string label)
        {
            var layer = createLayer();
            layer.Label = label;
            return layer;
        }

        private void DrawLayerModeControl(Rect rect, int index)
        {
            var layerOverride = state.PresetLayerOverrides.GetArrayElementAtIndex(index);
            var overrideEnabled = layerOverride.FindPropertyRelative("overrideLayer");

            EditorGUI.BeginChangeCheck();
            var instance = DrawPresetInstanceSegmentedControl(rect, overrideEnabled.boolValue);
            if (EditorGUI.EndChangeCheck()) {
                state.SetInstanceMode(index, instance);
            }
        }

        private static bool DrawPresetInstanceSegmentedControl(Rect rect, bool instance)
        {
            rect = EditorGUI.IndentedRect(rect);

            var sharedRect = new Rect(rect.x, rect.y, Mathf.Floor(rect.width * 0.5f), rect.height);
            var instanceRect = new Rect(sharedRect.xMax - 1f, rect.y, rect.xMax - sharedRect.xMax + 1f, rect.height);

            var sharedSelectedAfterDraw = DrawModeSegment(sharedRect, TextMeshProLayerInspectorStyles.SharedMode, !instance, true);
            var instanceSelectedAfterDraw = DrawModeSegment(instanceRect, TextMeshProLayerInspectorStyles.InstanceMode, instance, false);

            return GetPresetInstanceSegmentResult(instance, sharedSelectedAfterDraw, instanceSelectedAfterDraw);
        }

        internal static bool GetPresetInstanceSegmentResult(bool currentInstance, bool sharedSelectedAfterDraw, bool instanceSelectedAfterDraw)
        {
            return currentInstance switch {
                true when sharedSelectedAfterDraw => false,
                false when instanceSelectedAfterDraw => true,
                _ => currentInstance
            };
        }

        private static bool DrawModeSegment(Rect rect, GUIContent content, bool selected, bool left)
        {
            var style = left ? EditorStyles.miniButtonLeft : EditorStyles.miniButtonRight;
            return GUI.Toggle(rect, selected, content, style);
        }
    }
}
