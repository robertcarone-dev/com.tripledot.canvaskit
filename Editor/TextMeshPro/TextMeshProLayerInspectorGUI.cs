using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditorInternal;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    internal static class TextMeshProLayerInspectorGUI
    {
        private static class Content
        {
            public static readonly GUIContent Appearance = L10n.TextContent("Appearance", "Paint settings that control how this layer is rendered.");
            public static readonly GUIContent Blend = L10n.TextContent("Blend", "Controls for layer opacity and how this layer blends with previous layers.");
            public static readonly GUIContent BlendMode = L10n.TextContent("Blend Mode", "Choose how this layer is composited with layers below it.");
            public static readonly GUIContent Blur = L10n.TextContent("Blur", "Soften the shadow edge within the available SDF padding.");
            public static readonly GUIContent Dilate = L10n.TextContent("Dilate", "Expand or contract the face shape within the available SDF padding.");
            public static readonly GUIContent Effect = L10n.TextContent("Effect", "Controls for the shadow spread, blur, and offset.");
            public static readonly GUIContent Face = L10n.TextContent("Fill", "Enable and edit the main text fill for this layer.");
            public static readonly GUIContent Glow = L10n.TextContent("Glow", "Add an additive glow layer.");
            public static readonly GUIContent Feather = L10n.TextContent("Feather", "Soften the stroke edge within the available SDF padding.");
            public static readonly GUIContent Label = L10n.TextContent("Label", "Optional display name for this layer.");
            public static readonly GUIContent Layer = L10n.TextContent("Layer", "Add a text layer.");
            public static readonly GUIContent InstanceLayer = L10n.TextContent("I", "Instance layer: this row overrides the shared preset on this object.");
            public static readonly GUIContent SharedLayer = L10n.TextContent("S", "Shared layer: this row uses the assigned preset asset.");
            public static readonly GUIContent Layers = L10n.TextContent("Layers", "TextMeshPro rendering layers applied by this stack or preset.");
            public static readonly GUIContent LayerStackEmptyInfo = L10n.TextContent("TextMeshPro renders normally until at least one TextMeshPro layer is added.");
            public static readonly GUIContent Offset = L10n.TextContent("Offset", "Shift this effect relative to the text face.");
            public static readonly GUIContent Opacity = L10n.TextContent("Opacity", "Fade the entire layer before it is blended with layers below it.");
            public static readonly GUIContent Outline = L10n.TextContent("Stroke", "Enable and edit the stroke effect for this layer.");
            public static readonly GUIContent Position = L10n.TextContent("Position", "Choose where the stroke is placed relative to the glyph edge.");
            public static readonly GUIContent Shadow = L10n.TextContent("Shadow", "Add or edit a shadow layer.");
            public static readonly GUIContent Shape = L10n.TextContent("Shape", "Controls for SDF shape expansion and edge softness.");
            public static readonly GUIContent Spread = L10n.TextContent("Spread", "Expand or contract the shadow shape within the available SDF padding.");
            public static readonly GUIContent Stroke = L10n.TextContent("Stroke", "Add or edit a stroke layer.");
            public static readonly GUIContent Underlay = L10n.TextContent("Shadow", "Enable and edit the shadow effect for this layer.");
            public static readonly GUIContent Width = L10n.TextContent("Width", "Set the stroke thickness within the available SDF padding.");
        }

        private static readonly Color LayerHeaderBackgroundColorDark = new Color(0.2f, 0.205f, 0.21f, 1f);
        private static readonly Color LayerHeaderBackgroundColorLight = new Color(0.76f, 0.77f, 0.79f, 1f);
        private static readonly Color LayerHeaderTopSeparatorColorDark = new Color(0.28f, 0.285f, 0.29f, 1f);
        private static readonly Color LayerHeaderTopSeparatorColorLight = new Color(0.86f, 0.87f, 0.89f, 1f);
        private static readonly Color LayerHeaderBottomSeparatorColorDark = new Color(0.08f, 0.08f, 0.08f, 1f);
        private static readonly Color LayerHeaderBottomSeparatorColorLight = new Color(0.52f, 0.53f, 0.55f, 1f);
        private static readonly Color InstanceMarkerBackgroundColorDark = new Color(0.22f, 0.28f, 0.34f, 1f);
        private static readonly Color InstanceMarkerBackgroundColorLight = new Color(0.72f, 0.82f, 0.93f, 1f);
        private static readonly Color InstanceMarkerBorderColorDark = new Color(0.34f, 0.42f, 0.5f, 1f);
        private static readonly Color InstanceMarkerBorderColorLight = new Color(0.48f, 0.6f, 0.74f, 1f);
        private static readonly Color UnderlayFallbackColor = new Color(0f, 0f, 0f, 0.5f);
        private static Texture2D fillLayerIcon;
        private static Texture2D strokeLayerIcon;
        private static Texture2D shadowLayerIcon;
        private static GUIStyle instanceMarkerStyle;
        private const float LayerHeaderHeight = 26f;
        private const float FoldoutSize = 13f;
        private const float EnabledToggleSize = 13f;
        private const float LayerSwatchSize = 16f;
        private const float LayerIconSize = 16f;
        private const float InstanceMarkerSize = 16f;
        private const float FeatureIconBadgeSize = 16f;
        private const float FeatureIconBadgeGap = 3f;
        private const float HeaderControlGap = 6f;
        private const float TrailingControlWidth = 126f;
        private const float FillSectionHeaderHeight = 25f;

        internal readonly struct LayerSwatchDescriptor
        {
            internal readonly bool HasFill;
            internal readonly CanvasPaint Fill;
            internal readonly Color FillFallback;
            internal readonly bool HasInsetOutline;
            internal readonly CanvasPaint InsetOutline;
            internal readonly Color InsetOutlineFallback;

            internal LayerSwatchDescriptor(
                bool hasFill,
                CanvasPaint fill,
                Color fillFallback,
                bool hasInsetOutline,
                CanvasPaint insetOutline,
                Color insetOutlineFallback)
            {
                HasFill = hasFill;
                Fill = fill;
                FillFallback = fillFallback;
                HasInsetOutline = hasInsetOutline;
                InsetOutline = insetOutline;
                InsetOutlineFallback = insetOutlineFallback;
            }
        }

        internal readonly struct LayerFeatureIconDescriptor
        {
            internal readonly string Name;
            internal readonly Texture2D Icon;

            internal LayerFeatureIconDescriptor(string name, Texture2D icon)
            {
                Name = name;
                Icon = icon;
            }
        }

        internal static GUIContent InstanceLayerMarkerContent => Content.InstanceLayer;
        internal static GUIContent SharedLayerMarkerContent => Content.SharedLayer;

        private static GUIStyle InstanceMarkerStyle => instanceMarkerStyle ??= new GUIStyle(EditorStyles.miniLabel) {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.66f, 0.82f, 1f, 1f) : new Color(0.12f, 0.28f, 0.55f, 1f) }
        };

        static TextMeshProLayerInspectorGUI()
        {
            Content.Face.image = LoadLayerIcon(ref fillLayerIcon, "TextIcon.png");
            Content.Outline.image = LoadLayerIcon(ref strokeLayerIcon, "StrokeIcon.png");
            Content.Underlay.image = LoadLayerIcon(ref shadowLayerIcon, "ShadowIcon.png");
        }

        public static ReorderableList CreateLayerList(
            SerializedProperty layers,
            Action changed,
            bool allowReorder,
            bool allowAddRemove = true,
            Func<int, SerializedProperty> getRowLayer = null,
            Action<int, SerializedProperty> rowChanged = null,
            Action<Rect, int> drawTrailing = null)
        {
            return new ReorderableList(layers.serializedObject, layers, allowReorder, true, allowAddRemove, allowAddRemove) {
                elementHeight = 26f,
                headerHeight = 23f,
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, Content.Layers),
                drawNoneElementCallback = rect => EditorGUI.HelpBox(rect, Content.LayerStackEmptyInfo.text, MessageType.Info),
                drawElementCallback = (rect, index, active, focused) => DrawLayerListRow(
                    rect,
                    getRowLayer?.Invoke(index) ?? layers.GetArrayElementAtIndex(index),
                    index,
                    changed,
                    rowChanged,
                    drawTrailing),
                onAddDropdownCallback = (rect, list) => ShowAddLayerMenu(rect, layers, changed),
                onRemoveCallback = list => {
                    ReorderableList.defaultBehaviours.DoRemoveButton(list);
                    changed?.Invoke();
                },
                onReorderCallback = list => changed?.Invoke()
            };
        }

        public static void DoLayerList(ReorderableList list)
        {
            list.DoLayoutList();
        }

        public static void DrawLayerInspectorBlocks(
            SerializedProperty layers,
            Action changed,
            Func<int, SerializedProperty> getLayer = null,
            Action<int, SerializedProperty> layerChanged = null,
            string contextKey = null,
            float availablePadding = TextMeshProUtility.DefaultEditorSliderPadding,
            UnityEngine.Object sceneTarget = null,
            Func<int, bool> isInstanceLayer = null)
        {
            if (layers.arraySize == 0) {
                return;
            }

            for (int i = 0; i < layers.arraySize; i++) {
                var layer = getLayer?.Invoke(i) ?? layers.GetArrayElementAtIndex(i);
                if (layer == null) {
                    continue;
                }

                var expanded = DrawLayerInspectorHeader(
                    layer,
                    i,
                    changed,
                    layerChanged,
                    contextKey,
                    isInstanceLayer != null,
                    isInstanceLayer?.Invoke(i) ?? false);

                if (expanded) {
                    using (new EditorGUI.DisabledScope(IsLayerDisabled(layer))) {
                        DrawLayerDetails(layer, availablePadding, sceneTarget);
                    }
                }

                if (expanded) {
                    EditorGUILayout.Space(1f);
                }
            }
        }

        public static void DrawLayerDetails(SerializedProperty layer, float availablePadding, UnityEngine.Object sceneTarget = null)
        {
            GUILayout.Space(4f);
            EditorGUILayout.PropertyField(layer.FindPropertyRelative("label"), Content.Label);
            EditorGUILayout.PropertyField(layer.FindPropertyRelative("blendMode"), Content.BlendMode);
            EditorGUILayout.Slider(layer.FindPropertyRelative("opacity"), 0f, 1f, Content.Opacity);
            GUILayout.Space(5f);
            CoreEditorUtils.DrawSplitter();
            GUILayout.Space(5f);
            DrawUnifiedLayer(layer, availablePadding, sceneTarget);
        }

        public static string GetLayerTitle(SerializedProperty layer, int index)
        {
            return GetLayerDisplayContent(layer).text;
        }

        private static void DrawLayerListRow(
            Rect rect,
            SerializedProperty layer,
            int index,
            Action changed,
            Action<int, SerializedProperty> rowChanged,
            Action<Rect, int> drawTrailing)
        {
            if (layer == null) {
                return;
            }

            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;

            CalculateLayerListRowRects(
                rect,
                drawTrailing != null,
                out _,
                out var enabledRect,
                out var swatchRect,
                out var labelRect,
                out var trailingRect);

            var enabled = layer.FindPropertyRelative("enabled");
            var disabled = IsLayerDisabled(layer);

            EditorGUI.BeginChangeCheck();
            var layerEnabled = EditorGUI.Toggle(enabledRect, enabled.boolValue);
            if (EditorGUI.EndChangeCheck()) {
                enabled.boolValue = layerEnabled;
                rowChanged?.Invoke(index, layer);
                if (rowChanged == null) {
                    changed?.Invoke();
                }
            }

            DrawLayerSwatch(swatchRect, layer);

            var featureIcons = GetLayerFeatureIcons(layer);
            var titleRect = GetLayerTitleRect(labelRect, featureIcons.Length);
            using (new EditorGUI.DisabledScope(disabled)) {
                CanvasEditorGUI.DrawSwatchLabel(titleRect, GetLayerDisplayContent(layer));
                DrawFeatureIconBadges(labelRect, featureIcons);
            }

            drawTrailing?.Invoke(trailingRect, index);
        }

        private static bool DrawLayerInspectorHeader(
            SerializedProperty layer,
            int index,
            Action changed,
            Action<int, SerializedProperty> rowChanged,
            string contextKey,
            bool showPresetModeMarker,
            bool instance)
        {
            var rect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(1f, LayerHeaderHeight));
            var backgroundRect = rect;
            backgroundRect.xMin = 0f;
            backgroundRect.width += 4f;
            DrawLayerHeaderBackground(backgroundRect);

            var key = GetLayerExpansionKey(layer, index, contextKey);
            var expanded = SessionState.GetBool(key, true);

            CalculateLayerHeaderRects(
                rect,
                showPresetModeMarker,
                instance,
                out var foldoutRect,
                out _,
                out var enabledRect,
                out var swatchRect,
                out var instanceMarkerRect,
                out var labelRect);

            var enabled = layer.FindPropertyRelative("enabled");
            var disabled = IsLayerDisabled(layer);

            expanded = GUI.Toggle(foldoutRect, expanded, GUIContent.none, EditorStyles.foldout);

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = enabled.hasMultipleDifferentValues;
            var layerEnabled = GUI.Toggle(
                enabledRect,
                enabled.hasMultipleDifferentValues || enabled.boolValue,
                GUIContent.none,
                enabled.hasMultipleDifferentValues ? CoreEditorStyles.smallMixedTickbox : CoreEditorStyles.smallTickbox);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck()) {
                enabled.boolValue = layerEnabled;
                rowChanged?.Invoke(index, layer);
                if (rowChanged == null) {
                    changed?.Invoke();
                }
            }

            DrawLayerSwatch(swatchRect, layer);

            var titleRect = GetLayerTitleRect(labelRect, 0);
            using (new EditorGUI.DisabledScope(disabled)) {
                EditorGUI.LabelField(titleRect, GetLayerDisplayContent(layer), EditorStyles.boldLabel);
            }

            if (showPresetModeMarker) {
                DrawPresetModeMarker(instanceMarkerRect, instance ? InstanceLayerMarkerContent : SharedLayerMarkerContent);
            }

            var currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown &&
                currentEvent.button == 0 &&
                rect.Contains(currentEvent.mousePosition) &&
                !foldoutRect.Contains(currentEvent.mousePosition) &&
                !enabledRect.Contains(currentEvent.mousePosition)) {
                expanded = !expanded;
                currentEvent.Use();
            }

            SessionState.SetBool(key, expanded);
            return expanded;
        }

        internal static void CalculateLayerListRowRects(
            Rect rect,
            bool hasTrailingControl,
            out Rect iconRect,
            out Rect enabledRect,
            out Rect swatchRect,
            out Rect labelRect,
            out Rect trailingRect)
        {
            iconRect = Rect.zero;
            enabledRect = new Rect(rect.x, rect.y, EnabledToggleSize, rect.height);
            var swatchSize = Mathf.Min(LayerSwatchSize, rect.height);
            swatchRect = new Rect(enabledRect.xMax + HeaderControlGap, rect.y + (rect.height - swatchSize) * 0.5f, swatchSize, swatchSize);
            var trailingWidth = hasTrailingControl ? TrailingControlWidth : 0f;
            var labelStart = swatchRect.xMax + HeaderControlGap;
            labelRect = new Rect(labelStart, rect.y, rect.xMax - labelStart - trailingWidth - 8f, rect.height);
            trailingRect = new Rect(rect.xMax - trailingWidth, rect.y, trailingWidth, rect.height);
        }

        internal static void CalculateLayerHeaderRects(
            Rect rect,
            bool showPresetModeMarker,
            bool instance,
            out Rect foldoutRect,
            out Rect iconRect,
            out Rect enabledRect,
            out Rect swatchRect,
            out Rect instanceMarkerRect,
            out Rect labelRect)
        {
            foldoutRect = rect;
            foldoutRect.x += 2f;
            foldoutRect.y += Mathf.Floor((rect.height - FoldoutSize) * 0.5f);
            foldoutRect.width = FoldoutSize;
            foldoutRect.height = FoldoutSize;

            iconRect = Rect.zero;

            enabledRect = rect;
            enabledRect.x = foldoutRect.xMax + 4f;
            enabledRect.y += Mathf.Floor((rect.height - EnabledToggleSize) * 0.5f);
            enabledRect.width = EnabledToggleSize;
            enabledRect.height = EnabledToggleSize;

            swatchRect = rect;
            swatchRect.x = enabledRect.xMax + HeaderControlGap;
            swatchRect.y += Mathf.Floor((rect.height - LayerSwatchSize) * 0.5f);
            swatchRect.width = LayerSwatchSize;
            swatchRect.height = LayerSwatchSize;

            instanceMarkerRect = showPresetModeMarker
                ? new Rect(rect.xMax - 8f - InstanceMarkerSize, rect.y + Mathf.Floor((rect.height - InstanceMarkerSize) * 0.5f), InstanceMarkerSize, InstanceMarkerSize)
                : Rect.zero;

            var labelStart = swatchRect.xMax + 8f;
            var labelEnd = showPresetModeMarker ? instanceMarkerRect.xMin - HeaderControlGap : rect.xMax - 8f;
            labelRect = new Rect(labelStart, rect.y, Mathf.Max(0f, labelEnd - labelStart), rect.height);
        }

        private static void DrawLayerHeaderBackground(Rect rect)
        {
            var isProSkin = EditorGUIUtility.isProSkin;
            EditorGUI.DrawRect(rect, isProSkin ? LayerHeaderBackgroundColorDark : LayerHeaderBackgroundColorLight);

            var topSeparatorColor = isProSkin ? LayerHeaderTopSeparatorColorDark : LayerHeaderTopSeparatorColorLight;
            var bottomSeparatorColor = isProSkin ? LayerHeaderBottomSeparatorColorDark : LayerHeaderBottomSeparatorColorLight;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), topSeparatorColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), bottomSeparatorColor);
        }

        private static string GetLayerExpansionKey(SerializedProperty layer, int index, string contextKey)
        {
            var root = layer.serializedObject.targetObject;
            var rootId = root != null ? root.GetInstanceID().ToString() : "none";
            var prefix = string.IsNullOrEmpty(contextKey) ? "Default" : contextKey;
            return "TextMeshProLayerInspector." + prefix + "." + rootId + "." + layer.propertyPath + "." + index;
        }

        private static void ShowAddLayerMenu(Rect rect, SerializedProperty layers, Action changed)
        {
            var menu = new GenericMenu();
            menu.AddItem(Content.Layer, false, () => AddLayer(layers, TextMeshProLayerData.Layer, Content.Layer.text, changed));
            menu.AddItem(Content.Stroke, false, () => AddLayer(layers, TextMeshProLayerData.StrokePreset, Content.Stroke.text, changed));
            menu.AddItem(Content.Shadow, false, () => AddLayer(layers, TextMeshProLayerData.ShadowPreset, Content.Shadow.text, changed));
            menu.AddItem(Content.Glow, false, () => AddLayer(layers, TextMeshProLayerData.GlowPreset, Content.Glow.text, changed));
            menu.DropDown(rect);
        }

        private static void AddLayer(SerializedProperty layers, Func<TextMeshProLayerData> createLayer, string label, Action changed)
        {
            Undo.RecordObjects(layers.serializedObject.targetObjects, "Add TextMeshPro Layer");
            for (int i = 0; i < layers.serializedObject.targetObjects.Length; i++) {
                var layer = CreateLabeledLayer(createLayer, label);
                switch (layers.serializedObject.targetObjects[i]) {
                    case TextMeshProLayerStack stack when layers.propertyPath == "localLayers":
                        stack.LocalLayers.Add(layer);
                        EditorUtility.SetDirty(stack);
                        break;
                    case TextMeshProLayerPreset preset when layers.propertyPath == "layers":
                        preset.MutableLayers.Add(layer);
                        EditorUtility.SetDirty(preset);
                        preset.NotifyChanged();
                        break;
                }
            }

            layers.serializedObject.Update();
            changed?.Invoke();
        }

        internal static TextMeshProLayerData CreateLabeledLayerForTests(Func<TextMeshProLayerData> createLayer, string label)
        {
            return CreateLabeledLayer(createLayer, label);
        }

        private static TextMeshProLayerData CreateLabeledLayer(Func<TextMeshProLayerData> createLayer, string label)
        {
            var layer = createLayer();
            layer.Label = label;
            return layer;
        }

        private static GUIContent GetLayerContent(SerializedProperty layer)
        {
            return new GUIContent(GetLayerLabel(layer), Content.Layer.tooltip);
        }

        internal static GUIContent GetLayerDisplayContent(SerializedProperty layer)
        {
            return GetLayerContent(layer);
        }

        internal static string[] GetLayerFeatureIconNamesForTests(SerializedProperty layer)
        {
            var icons = GetLayerFeatureIcons(layer);
            var names = new string[icons.Length];
            for (int i = 0; i < icons.Length; i++) {
                names[i] = icons[i].Name;
            }

            return names;
        }

        internal static Rect GetLayerTitleRectForTests(Rect rect, int iconCount)
        {
            return GetLayerTitleRect(rect, iconCount);
        }

        internal static float GetLayerFeatureIconBadgesWidthForTests(int iconCount)
        {
            return GetLayerFeatureIconBadgesWidth(iconCount);
        }

        private static string GetLayerLabel(SerializedProperty layer)
        {
            var label = layer.FindPropertyRelative("label");
            if (label != null && !string.IsNullOrWhiteSpace(label.stringValue)) {
                return label.stringValue.Trim();
            }

            return Content.Layer.text;
        }

        private static LayerFeatureIconDescriptor[] GetLayerFeatureIcons(SerializedProperty layer)
        {
            var icons = new List<LayerFeatureIconDescriptor>(3);
            if (layer.FindPropertyRelative("face").FindPropertyRelative("Enabled").boolValue) {
                icons.Add(new LayerFeatureIconDescriptor(Content.Face.text, fillLayerIcon));
            }

            if (layer.FindPropertyRelative("stroke").FindPropertyRelative("Enabled").boolValue) {
                icons.Add(new LayerFeatureIconDescriptor(Content.Outline.text, strokeLayerIcon));
            }

            if (layer.FindPropertyRelative("shadow").FindPropertyRelative("Enabled").boolValue) {
                icons.Add(new LayerFeatureIconDescriptor(Content.Underlay.text, shadowLayerIcon));
            }

            return icons.ToArray();
        }

        private static Rect GetLayerTitleRect(Rect rect, int iconCount)
        {
            var iconWidth = GetLayerFeatureIconBadgesWidth(iconCount);
            if (iconWidth <= 0f) {
                return rect;
            }

            rect.width = Mathf.Max(0f, rect.width - iconWidth - HeaderControlGap);
            return rect;
        }

        private static float GetLayerFeatureIconBadgesWidth(int iconCount)
        {
            if (iconCount <= 0) {
                return 0f;
            }

            return iconCount * FeatureIconBadgeSize + (iconCount - 1) * FeatureIconBadgeGap;
        }

        private static void DrawFeatureIconBadges(Rect rect, LayerFeatureIconDescriptor[] icons)
        {
            if (icons == null || icons.Length == 0) {
                return;
            }

            var totalWidth = GetLayerFeatureIconBadgesWidth(icons.Length);
            var iconRect = new Rect(
                rect.xMax - totalWidth,
                rect.y + Mathf.Floor((rect.height - FeatureIconBadgeSize) * 0.5f),
                FeatureIconBadgeSize,
                FeatureIconBadgeSize);

            for (int i = 0; i < icons.Length; i++) {
                if (icons[i].Icon != null) {
                    GUI.DrawTexture(iconRect, icons[i].Icon, ScaleMode.ScaleToFit);
                }

                iconRect.x += FeatureIconBadgeSize + FeatureIconBadgeGap;
            }
        }

        private static bool IsLayerDisabled(SerializedProperty layer)
        {
            var enabled = layer.FindPropertyRelative("enabled");
            return enabled is { hasMultipleDifferentValues: false, boolValue: false };
        }

        private static void DrawUnifiedLayer(SerializedProperty layer, float availablePadding, UnityEngine.Object sceneTarget)
        {
            var face = new SerializedFace(layer.FindPropertyRelative("face"));
            var stroke = new SerializedStroke(layer.FindPropertyRelative("stroke"));
            var shadow = new SerializedShadow(layer.FindPropertyRelative("shadow"));

            GUILayout.Space(6f);

            var faceExpanded = BeginToggleSection(layer, Content.Face, face.Enabled);
            if (faceExpanded) {
                using (new EditorGUI.DisabledScope(face.Enabled is { hasMultipleDifferentValues: false, boolValue: false })) {
                    CanvasPaintDrawer.DrawFillMode(face.Paint);
                    CanvasPaintDrawer.DrawAppearance(face.Paint);
                    DrawPaintMapping(face.Paint, sceneTarget, true);
                    CanvasEditorGUI.DrawRoundedInspectorSubsection(Content.Shape);
                    CanvasEditorGUI.SdfLengthSlider(face.Dilate, face.DilateUnit, Content.Dilate, availablePadding, -availablePadding, availablePadding);
                }
            }
            EndToggleSection(faceExpanded);

            var strokeExpanded = BeginToggleSection(layer, Content.Outline, stroke.Enabled);
            if (strokeExpanded) {
                using (new EditorGUI.DisabledScope(stroke.Enabled is { hasMultipleDifferentValues: false, boolValue: false })) {
                    CanvasPaintDrawer.DrawFillMode(stroke.Paint);
                    CanvasPaintDrawer.DrawAppearance(stroke.Paint);
                    DrawPaintMapping(stroke.Paint, sceneTarget, true);
                    CanvasEditorGUI.DrawRoundedInspectorSubsection(Content.Shape);
                    CanvasEditorGUI.PropertyField(stroke.Position, Content.Position);
                    var reservedFacePadding = GetEffectivePositiveSdfBudget(face.Enabled, face.Dilate, availablePadding);
                    GetStrokeSliderBudgets(stroke.Width, stroke.Feather, stroke.Position, availablePadding, reservedFacePadding, out var widthMax, out var featherMax);
                    CanvasEditorGUI.ConstrainedSdfLengthSlider(stroke.Width, stroke.WidthUnit, Content.Width, availablePadding, 0f, widthMax);
                    CanvasEditorGUI.ConstrainedSdfLengthSlider(stroke.Feather, stroke.FeatherUnit, Content.Feather, availablePadding, 0f, featherMax);
                    CanvasEditorGUI.Vector2Field(stroke.Offset, Content.Offset);
                }
            }
            EndToggleSection(strokeExpanded);

            var shadowExpanded = BeginToggleSection(layer, Content.Underlay, shadow.Enabled);
            if (shadowExpanded) {
                using (new EditorGUI.DisabledScope(shadow.Enabled is { hasMultipleDifferentValues: false, boolValue: false })) {
                    CanvasPaintDrawer.DrawFillMode(shadow.Paint);
                    CanvasPaintDrawer.DrawAppearance(shadow.Paint);
                    DrawPaintMapping(shadow.Paint, sceneTarget, true);
                    CanvasEditorGUI.DrawRoundedInspectorSubsection(Content.Effect);
                    var reservedFacePadding = GetEffectivePositiveSdfBudget(face.Enabled, face.Dilate, availablePadding);
                    GetShadowSliderBudgets(shadow.Spread, shadow.Blur, availablePadding, reservedFacePadding, out var spreadMin, out var spreadMax, out var blurMax);
                    CanvasEditorGUI.ConstrainedSdfLengthSlider(shadow.Spread, shadow.SpreadUnit, Content.Spread, availablePadding, spreadMin, spreadMax);
                    CanvasEditorGUI.ConstrainedSdfLengthSlider(shadow.Blur, shadow.BlurUnit, Content.Blur, availablePadding, 0f, blurMax);
                    CanvasEditorGUI.Vector2Field(shadow.Offset, Content.Offset);
                }
            }
            EndToggleSection(shadowExpanded);
        }

        private static float GetRemainingSdfBudget(float availablePadding, float reservedPadding)
        {
            if (float.IsPositiveInfinity(availablePadding)) {
                return availablePadding;
            }

            return Mathf.Max(0f, availablePadding - reservedPadding);
        }

        internal static float GetEffectivePositiveSdfBudget(SerializedProperty enabled, SerializedProperty property, float availablePadding)
        {
            if (enabled is { hasMultipleDifferentValues: false, boolValue: false }) {
                return 0f;
            }

            return GetEffectivePositiveSdfBudget(GetSdfValue(property), availablePadding);
        }

        internal static float GetEffectivePositiveSdfBudget(float value, float availablePadding)
        {
            if (float.IsPositiveInfinity(availablePadding)) {
                return Mathf.Max(0f, value);
            }

            return Mathf.Min(Mathf.Max(0f, value), Mathf.Max(0f, availablePadding));
        }

        internal static void GetStrokeSliderBudgets(SerializedProperty width, SerializedProperty feather, float availablePadding, float reservedPadding, out float widthMax, out float featherMax)
        {
            GetStrokeSliderBudgets(GetSdfValue(width), GetSdfValue(feather), TextMeshProStrokePosition.Outside, availablePadding, reservedPadding, out widthMax, out featherMax);
        }

        internal static void GetStrokeSliderBudgets(SerializedProperty width, SerializedProperty feather, SerializedProperty position, float availablePadding, float reservedPadding, out float widthMax, out float featherMax)
        {
            GetStrokeSliderBudgets(GetSdfValue(width), GetSdfValue(feather), GetStrokePosition(position), availablePadding, reservedPadding, out widthMax, out featherMax);
        }

        internal static void GetStrokeSliderBudgets(float width, float feather, float availablePadding, float reservedPadding, out float widthMax, out float featherMax)
        {
            GetStrokeSliderBudgets(width, feather, TextMeshProStrokePosition.Outside, availablePadding, reservedPadding, out widthMax, out featherMax);
        }

        internal static void GetStrokeSliderBudgets(float width, float feather, TextMeshProStrokePosition position, float availablePadding, float reservedPadding, out float widthMax, out float featherMax)
        {
            var remainingBudget = GetRemainingSdfBudget(availablePadding, Mathf.Max(0f, reservedPadding));
            var strokeWidthFactor = TextMeshProUtility.GetStrokeEffectPaddingFactor(position);
            widthMax = strokeWidthFactor > 0.000001f ? remainingBudget / strokeWidthFactor : remainingBudget;
            TextMeshProUtility.ClampStrokeEffect(width, feather, position, availablePadding, reservedPadding, out var effectiveWidth, out _);
            featherMax = GetRemainingSdfBudget(remainingBudget, effectiveWidth * strokeWidthFactor);
        }

        internal static void GetShadowSliderBudgets(SerializedProperty spread, SerializedProperty blur, float availablePadding, float reservedPadding, out float spreadMin, out float spreadMax, out float blurMax)
        {
            GetShadowSliderBudgets(GetSdfValue(spread), GetSdfValue(blur), availablePadding, reservedPadding, out spreadMin, out spreadMax, out blurMax);
        }

        internal static void GetShadowSliderBudgets(float spread, float blur, float availablePadding, float reservedPadding, out float spreadMin, out float spreadMax, out float blurMax)
        {
            spreadMax = GetRemainingSdfBudget(availablePadding, Mathf.Max(0f, reservedPadding));
            spreadMin = float.IsPositiveInfinity(availablePadding) ? float.NegativeInfinity : -Mathf.Max(0f, availablePadding);
            TextMeshProUtility.ClampShadowEffect(spread, blur, availablePadding, reservedPadding, out var effectiveSpread, out _);
            blurMax = GetRemainingSdfBudget(spreadMax, effectiveSpread);
        }

        private static float GetSdfValue(SerializedProperty property)
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

        private static void DrawPaintMapping(SerializedCanvasPaint paint, UnityEngine.Object sceneTarget, bool boxed = false)
        {
            if (CanvasPaintDrawer.HasMapping(paint)) {
                CanvasPaintDrawer.DrawMappingHeader(paint, sceneTarget, boxed);
                CanvasPaintDrawer.DrawMapping(paint);
            }
        }

        private static bool BeginToggleSection(SerializedProperty layer, GUIContent title, SerializedProperty enabledProperty)
        {
            var key = layer.serializedObject.targetObject.GetInstanceID() + "." + layer.propertyPath + "." + title.text;
            var expanded = SessionState.GetBool(key, true);

            EditorGUILayout.BeginVertical(CanvasEditorGUI.RoundedInspectorPanelStyle);
            expanded = DrawHeaderToggleFoldout(title, expanded, enabledProperty);
            SessionState.SetBool(key, expanded);
            if (expanded) {
                EditorGUILayout.BeginVertical(CanvasEditorGUI.RoundedInspectorPanelContentStyle);
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

        private static bool DrawHeaderToggleFoldout(GUIContent title, bool expanded, SerializedProperty enabledProperty)
        {
            var headerRect = GUILayoutUtility.GetRect(1f, FillSectionHeaderHeight);
            GUI.Label(headerRect, GUIContent.none, CanvasEditorGUI.GetRoundedInspectorPanelHeaderStyle(expanded));

            var foldoutRect = headerRect;
            foldoutRect.x += 9f;
            foldoutRect.y += Mathf.Floor((headerRect.height - FoldoutSize) * 0.5f);
            foldoutRect.width = FoldoutSize;
            foldoutRect.height = FoldoutSize;

            var toggleRect = headerRect;
            toggleRect.x = foldoutRect.xMax + 5f;
            toggleRect.y += Mathf.Floor((headerRect.height - EnabledToggleSize) * 0.5f);
            toggleRect.width = EnabledToggleSize;
            toggleRect.height = EnabledToggleSize;

            var nextX = toggleRect.xMax + 8f;
            if (title.image != null) {
                var iconRect = headerRect;
                iconRect.x = nextX;
                iconRect.y += Mathf.Floor((headerRect.height - LayerIconSize) * 0.5f);
                iconRect.width = LayerIconSize;
                iconRect.height = LayerIconSize;
                GUI.DrawTexture(iconRect, title.image, ScaleMode.ScaleToFit);
                nextX = iconRect.xMax + 6f;
            }

            var labelRect = headerRect;
            labelRect.xMin = nextX;
            labelRect.xMax -= 8f;

            using (new EditorGUI.DisabledScope(enabledProperty is { hasMultipleDifferentValues: false, boolValue: false })) {
                EditorGUI.LabelField(labelRect, new GUIContent(title.text, title.tooltip), EditorStyles.boldLabel);
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

            var currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown &&
                currentEvent.button == 0 &&
                headerRect.Contains(currentEvent.mousePosition) &&
                !foldoutRect.Contains(currentEvent.mousePosition) &&
                !toggleRect.Contains(currentEvent.mousePosition)) {
                expanded = !expanded;
                currentEvent.Use();
            }

            if (expanded) {
                CanvasEditorGUI.DrawRoundedInspectorHeaderSeparator(headerRect);
            }

            return expanded;
        }

        private static void DrawLayerSwatch(Rect rect, SerializedProperty layer)
        {
            var descriptor = GetLayerSwatchDescriptor(layer);
            if (descriptor.HasFill) {
                CanvasEditorGUI.DrawPaintSwatch(rect, descriptor.Fill, descriptor.FillFallback);
            } else {
                CanvasEditorGUI.DrawTransparentSwatch(rect);
            }

            if (descriptor.HasInsetOutline) {
                CanvasEditorGUI.DrawPaintOutlineSwatch(rect, descriptor.InsetOutline, descriptor.InsetOutlineFallback);
            }
        }

        private static void DrawPresetModeMarker(Rect rect, GUIContent content)
        {
            var isProSkin = EditorGUIUtility.isProSkin;
            EditorGUI.DrawRect(rect, isProSkin ? InstanceMarkerBackgroundColorDark : InstanceMarkerBackgroundColorLight);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), isProSkin ? InstanceMarkerBorderColorDark : InstanceMarkerBorderColorLight);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), isProSkin ? InstanceMarkerBorderColorDark : InstanceMarkerBorderColorLight);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), isProSkin ? InstanceMarkerBorderColorDark : InstanceMarkerBorderColorLight);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), isProSkin ? InstanceMarkerBorderColorDark : InstanceMarkerBorderColorLight);
            GUI.Label(rect, content, InstanceMarkerStyle);
        }

        private static Texture2D LoadLayerIcon(ref Texture2D icon, string filename)
        {
            if (icon == null) {
                icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.tripledot.canvaskit/Editor Default Resources/Icons/" + filename);
            }

            return icon;
        }

        internal static LayerSwatchDescriptor GetLayerSwatchDescriptorForTests(SerializedProperty layer)
        {
            return GetLayerSwatchDescriptor(layer);
        }

        private static LayerSwatchDescriptor GetLayerSwatchDescriptor(SerializedProperty layer)
        {
            var face = layer.FindPropertyRelative("face");
            var stroke = layer.FindPropertyRelative("stroke");
            var shadow = layer.FindPropertyRelative("shadow");
            var strokeEnabled = stroke.FindPropertyRelative("Enabled").boolValue;
            var strokePaint = strokeEnabled ? ReadPaintForSwatch(stroke.FindPropertyRelative("Paint")) : default;
            if (face.FindPropertyRelative("Enabled").boolValue) {
                return new LayerSwatchDescriptor(
                    true,
                    ReadPaintForSwatch(face.FindPropertyRelative("Paint")),
                    Color.white,
                    strokeEnabled,
                    strokePaint,
                    Color.black);
            }

            if (strokeEnabled) {
                return new LayerSwatchDescriptor(
                    false,
                    default,
                    Color.clear,
                    true,
                    strokePaint,
                    Color.black);
            }

            if (shadow.FindPropertyRelative("Enabled").boolValue) {
                return new LayerSwatchDescriptor(
                    true,
                    ReadPaintForSwatch(shadow.FindPropertyRelative("Paint")),
                    UnderlayFallbackColor,
                    false,
                    default,
                    Color.clear);
            }

            return new LayerSwatchDescriptor(
                true,
                CanvasPaint.Solid(Color.clear),
                Color.clear,
                false,
                default,
                Color.clear);
        }

        internal static CanvasPaint ReadPaintForSwatch(SerializedProperty paint)
        {
            return new CanvasPaint {
                Type = (CanvasPaintType)paint.FindPropertyRelative("Type").enumValueIndex,
                GradientMode = (CanvasGradientMode)paint.FindPropertyRelative("GradientMode").enumValueIndex,
                Color = paint.FindPropertyRelative("Color").colorValue,
                SecondaryColor = paint.FindPropertyRelative("SecondaryColor").colorValue,
                Opacity = paint.FindPropertyRelative("Opacity").floatValue,
                Gradient = paint.FindPropertyRelative("Gradient").gradientValue,
                Texture = paint.FindPropertyRelative("Texture").objectReferenceValue as Texture2D
            };
        }

        private sealed class SerializedFace
        {
            public readonly SerializedProperty Enabled;
            public readonly SerializedCanvasPaint Paint;
            public readonly SerializedProperty Dilate;
            public readonly SerializedProperty DilateUnit;

            public SerializedFace(SerializedProperty root)
            {
                Enabled = root.FindPropertyRelative("Enabled");
                Paint = new SerializedCanvasPaint(root.FindPropertyRelative("Paint"));
                Dilate = root.FindPropertyRelative("Dilate");
                DilateUnit = root.FindPropertyRelative("DilateUnit");
            }
        }

        private sealed class SerializedStroke
        {
            public readonly SerializedProperty Enabled;
            public readonly SerializedCanvasPaint Paint;
            public readonly SerializedProperty Position;
            public readonly SerializedProperty Width;
            public readonly SerializedProperty WidthUnit;
            public readonly SerializedProperty Feather;
            public readonly SerializedProperty FeatherUnit;
            public readonly SerializedProperty Offset;

            public SerializedStroke(SerializedProperty root)
            {
                Enabled = root.FindPropertyRelative("Enabled");
                Paint = new SerializedCanvasPaint(root.FindPropertyRelative("Paint"));
                Position = root.FindPropertyRelative("Position");
                Width = root.FindPropertyRelative("Width");
                WidthUnit = root.FindPropertyRelative("WidthUnit");
                Feather = root.FindPropertyRelative("Feather");
                FeatherUnit = root.FindPropertyRelative("FeatherUnit");
                Offset = root.FindPropertyRelative("Offset");
            }
        }

        private sealed class SerializedShadow
        {
            public readonly SerializedProperty Enabled;
            public readonly SerializedCanvasPaint Paint;
            public readonly SerializedProperty Offset;
            public readonly SerializedProperty Blur;
            public readonly SerializedProperty BlurUnit;
            public readonly SerializedProperty Spread;
            public readonly SerializedProperty SpreadUnit;

            public SerializedShadow(SerializedProperty root)
            {
                Enabled = root.FindPropertyRelative("Enabled");
                Paint = new SerializedCanvasPaint(root.FindPropertyRelative("Paint"));
                Offset = root.FindPropertyRelative("Offset");
                Blur = root.FindPropertyRelative("Blur");
                BlurUnit = root.FindPropertyRelative("BlurUnit");
                Spread = root.FindPropertyRelative("Spread");
                SpreadUnit = root.FindPropertyRelative("SpreadUnit");
            }
        }
    }
}
