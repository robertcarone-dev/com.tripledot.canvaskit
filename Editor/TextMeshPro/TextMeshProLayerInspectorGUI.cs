using System;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditorInternal;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    internal static class TextMeshProLayerInspectorGUI
    {
        private static class Styles
        {
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

            public static readonly Color LayerHeaderBackgroundColorDark = new Color(0.2f, 0.205f, 0.21f, 1f);
            public static readonly Color LayerHeaderBackgroundColorLight = new Color(0.76f, 0.77f, 0.79f, 1f);
            public static readonly Color LayerHeaderTopSeparatorColorDark = new Color(0.28f, 0.285f, 0.29f, 1f);
            public static readonly Color LayerHeaderTopSeparatorColorLight = new Color(0.86f, 0.87f, 0.89f, 1f);
            public static readonly Color LayerHeaderBottomSeparatorColorDark = new Color(0.08f, 0.08f, 0.08f, 1f);
            public static readonly Color LayerHeaderBottomSeparatorColorLight = new Color(0.52f, 0.53f, 0.55f, 1f);
            public static readonly Color InstanceMarkerBackgroundColorDark = new Color(0.22f, 0.28f, 0.34f, 1f);
            public static readonly Color InstanceMarkerBackgroundColorLight = new Color(0.72f, 0.82f, 0.93f, 1f);
            public static readonly Color InstanceMarkerBorderColorDark = new Color(0.34f, 0.42f, 0.5f, 1f);
            public static readonly Color InstanceMarkerBorderColorLight = new Color(0.48f, 0.6f, 0.74f, 1f);
            public static readonly Color UnderlayFallbackColor = new Color(0f, 0f, 0f, 0.5f);
            public static readonly Color InstanceMarkerTextColorDark = new Color(0.66f, 0.82f, 1f, 1f);
            public static readonly Color InstanceMarkerTextColorLight = new Color(0.12f, 0.28f, 0.55f, 1f);

            public static Texture2D FillLayerIcon;
            public static Texture2D StrokeLayerIcon;
            public static Texture2D ShadowLayerIcon;
            public static readonly GUIContent ScratchContent = new GUIContent();

            public const float LayerHeaderHeight = 26f;
            public const float FoldoutSize = 13f;
            public const float EnabledToggleSize = 13f;
            public const float LayerSwatchSize = 16f;
            public const float LayerIconSize = 16f;
            public const float InstanceMarkerSize = 16f;
            public const float FeatureIconBadgeSize = 16f;
            public const float FeatureIconBadgeGap = 3f;
            public const float HeaderControlGap = 6f;
            public const float TrailingControlWidth = 126f;
            public const float FillSectionHeaderHeight = 25f;

            private static GUIStyle instanceMarkerStyle;

            static Styles()
            {
                FillLayerIcon = LoadLayerIcon("TextIcon.png");
                StrokeLayerIcon = LoadLayerIcon("StrokeIcon.png");
                ShadowLayerIcon = LoadLayerIcon("ShadowIcon.png");
                Face.image = FillLayerIcon;
                Outline.image = StrokeLayerIcon;
                Underlay.image = ShadowLayerIcon;
            }

            public static GUIStyle InstanceMarkerStyle => instanceMarkerStyle ??= new GUIStyle(EditorStyles.miniLabel) {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = EditorGUIUtility.isProSkin ? InstanceMarkerTextColorDark : InstanceMarkerTextColorLight }
            };

            public static GUIContent GetLayerDisplayContent(string text)
            {
                ScratchContent.text = text;
                ScratchContent.tooltip = Layer.tooltip;
                ScratchContent.image = null;
                return ScratchContent;
            }

            public static GUIContent GetTextOnlyContent(GUIContent content)
            {
                ScratchContent.text = content.text;
                ScratchContent.tooltip = content.tooltip;
                ScratchContent.image = null;
                return ScratchContent;
            }
        }

        internal readonly struct LayerSwatchDescriptor
        {
            public readonly bool HasFill;
            public readonly CanvasPaint Fill;
            public readonly Color FillFallback;
            public readonly bool HasInsetOutline;
            public readonly CanvasPaint InsetOutline;
            public readonly Color InsetOutlineFallback;

            public LayerSwatchDescriptor(
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

        [Flags]
        private enum LayerFeatureFlags
        {
            None = 0,
            Face = 1 << 0,
            Stroke = 1 << 1,
            Shadow = 1 << 2
        }

        private sealed class SerializedLayer
        {
            public readonly SerializedProperty Root;
            private SerializedProperty enabled;
            private SerializedProperty label;
            private SerializedProperty blendMode;
            private SerializedProperty opacity;
            private SerializedFace face;
            private SerializedStroke stroke;
            private SerializedShadow shadow;

            public SerializedLayer(SerializedProperty root)
            {
                Root = root;
            }

            public SerializedProperty Enabled => enabled ??= Root.FindPropertyRelative("enabled");
            public SerializedProperty Label => label ??= Root.FindPropertyRelative("label");
            public SerializedProperty BlendMode => blendMode ??= Root.FindPropertyRelative("blendMode");
            public SerializedProperty Opacity => opacity ??= Root.FindPropertyRelative("opacity");
            public SerializedFace Face => face ??= new SerializedFace(Root.FindPropertyRelative("face"));
            public SerializedStroke Stroke => stroke ??= new SerializedStroke(Root.FindPropertyRelative("stroke"));
            public SerializedShadow Shadow => shadow ??= new SerializedShadow(Root.FindPropertyRelative("shadow"));
            public bool IsDisabled => Enabled is { hasMultipleDifferentValues: false, boolValue: false };

            public string DisplayLabel
            {
                get
                {
                    if (Label != null && !string.IsNullOrWhiteSpace(Label.stringValue)) {
                        return Label.stringValue.Trim();
                    }

                    return Styles.Layer.text;
                }
            }

            public LayerFeatureFlags FeatureFlags
            {
                get
                {
                    var flags = LayerFeatureFlags.None;
                    if (Face.Enabled.boolValue) {
                        flags |= LayerFeatureFlags.Face;
                    }

                    if (Stroke.Enabled.boolValue) {
                        flags |= LayerFeatureFlags.Stroke;
                    }

                    if (Shadow.Enabled.boolValue) {
                        flags |= LayerFeatureFlags.Shadow;
                    }

                    return flags;
                }
            }
        }

        internal static GUIContent InstanceLayerMarkerContent => Styles.InstanceLayer;
        internal static GUIContent SharedLayerMarkerContent => Styles.SharedLayer;

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
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, Styles.Layers),
                drawNoneElementCallback = rect => EditorGUI.HelpBox(rect, Styles.LayerStackEmptyInfo.text, MessageType.Info),
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
                var serializedLayer = new SerializedLayer(layer);

                var expanded = DrawLayerInspectorHeader(
                    serializedLayer,
                    i,
                    changed,
                    layerChanged,
                    contextKey,
                    isInstanceLayer != null,
                    isInstanceLayer?.Invoke(i) ?? false);

                if (expanded) {
                    using (new EditorGUI.DisabledScope(serializedLayer.IsDisabled)) {
                        DrawLayerDetails(serializedLayer, availablePadding, sceneTarget);
                    }
                }

                if (expanded) {
                    EditorGUILayout.Space(1f);
                }
            }
        }

        public static void DrawLayerDetails(SerializedProperty layer, float availablePadding, UnityEngine.Object sceneTarget = null)
        {
            DrawLayerDetails(new SerializedLayer(layer), availablePadding, sceneTarget);
        }

        private static void DrawLayerDetails(SerializedLayer layer, float availablePadding, UnityEngine.Object sceneTarget = null)
        {
            GUILayout.Space(4f);
            EditorGUILayout.PropertyField(layer.Label, Styles.Label);
            EditorGUILayout.PropertyField(layer.BlendMode, Styles.BlendMode);
            EditorGUILayout.Slider(layer.Opacity, 0f, 1f, Styles.Opacity);
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
            var serializedLayer = new SerializedLayer(layer);

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

            var enabled = serializedLayer.Enabled;

            EditorGUI.BeginChangeCheck();
            var layerEnabled = EditorGUI.Toggle(enabledRect, enabled.boolValue);
            if (EditorGUI.EndChangeCheck()) {
                enabled.boolValue = layerEnabled;
                rowChanged?.Invoke(index, layer);
                if (rowChanged == null) {
                    changed?.Invoke();
                }
            }

            DrawLayerSwatch(swatchRect, serializedLayer);

            var featureFlags = serializedLayer.FeatureFlags;
            var titleRect = GetLayerTitleRect(labelRect, GetLayerFeatureIconCount(featureFlags));
            using (new EditorGUI.DisabledScope(serializedLayer.IsDisabled)) {
                CanvasEditorGUI.DrawSwatchLabel(titleRect, GetLayerDisplayContent(serializedLayer));
                DrawFeatureIconBadges(labelRect, featureFlags);
            }

            drawTrailing?.Invoke(trailingRect, index);
        }

        private static bool DrawLayerInspectorHeader(
            SerializedLayer layer,
            int index,
            Action changed,
            Action<int, SerializedProperty> rowChanged,
            string contextKey,
            bool showPresetModeMarker,
            bool instance)
        {
            var rect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(1f, Styles.LayerHeaderHeight));
            var backgroundRect = rect;
            backgroundRect.xMin = 0f;
            backgroundRect.width += 4f;
            DrawLayerHeaderBackground(backgroundRect);

            var key = GetLayerExpansionKey(layer.Root, index, contextKey);
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

            var enabled = layer.Enabled;

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
                rowChanged?.Invoke(index, layer.Root);
                if (rowChanged == null) {
                    changed?.Invoke();
                }
            }

            DrawLayerSwatch(swatchRect, layer);

            var titleRect = GetLayerTitleRect(labelRect, 0);
            using (new EditorGUI.DisabledScope(layer.IsDisabled)) {
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
            enabledRect = new Rect(rect.x, rect.y, Styles.EnabledToggleSize, rect.height);
            var swatchSize = Mathf.Min(Styles.LayerSwatchSize, rect.height);
            swatchRect = new Rect(enabledRect.xMax + Styles.HeaderControlGap, rect.y + (rect.height - swatchSize) * 0.5f, swatchSize, swatchSize);
            var trailingWidth = hasTrailingControl ? Styles.TrailingControlWidth : 0f;
            var labelStart = swatchRect.xMax + Styles.HeaderControlGap;
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
            foldoutRect.y += Mathf.Floor((rect.height - Styles.FoldoutSize) * 0.5f);
            foldoutRect.width = Styles.FoldoutSize;
            foldoutRect.height = Styles.FoldoutSize;

            iconRect = Rect.zero;

            enabledRect = rect;
            enabledRect.x = foldoutRect.xMax + 4f;
            enabledRect.y += Mathf.Floor((rect.height - Styles.EnabledToggleSize) * 0.5f);
            enabledRect.width = Styles.EnabledToggleSize;
            enabledRect.height = Styles.EnabledToggleSize;

            swatchRect = rect;
            swatchRect.x = enabledRect.xMax + Styles.HeaderControlGap;
            swatchRect.y += Mathf.Floor((rect.height - Styles.LayerSwatchSize) * 0.5f);
            swatchRect.width = Styles.LayerSwatchSize;
            swatchRect.height = Styles.LayerSwatchSize;

            instanceMarkerRect = showPresetModeMarker
                ? new Rect(rect.xMax - 8f - Styles.InstanceMarkerSize, rect.y + Mathf.Floor((rect.height - Styles.InstanceMarkerSize) * 0.5f), Styles.InstanceMarkerSize, Styles.InstanceMarkerSize)
                : Rect.zero;

            var labelStart = swatchRect.xMax + 8f;
            var labelEnd = showPresetModeMarker ? instanceMarkerRect.xMin - Styles.HeaderControlGap : rect.xMax - 8f;
            labelRect = new Rect(labelStart, rect.y, Mathf.Max(0f, labelEnd - labelStart), rect.height);
        }

        private static void DrawLayerHeaderBackground(Rect rect)
        {
            var isProSkin = EditorGUIUtility.isProSkin;
            EditorGUI.DrawRect(rect, isProSkin ? Styles.LayerHeaderBackgroundColorDark : Styles.LayerHeaderBackgroundColorLight);

            var topSeparatorColor = isProSkin ? Styles.LayerHeaderTopSeparatorColorDark : Styles.LayerHeaderTopSeparatorColorLight;
            var bottomSeparatorColor = isProSkin ? Styles.LayerHeaderBottomSeparatorColorDark : Styles.LayerHeaderBottomSeparatorColorLight;
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
            menu.AddItem(Styles.Layer, false, () => AddLayer(layers, TextMeshProLayerData.Default, Styles.Layer.text, changed));
            menu.AddItem(Styles.Stroke, false, () => AddLayer(layers, TextMeshProLayerData.StrokePreset, Styles.Stroke.text, changed));
            menu.AddItem(Styles.Shadow, false, () => AddLayer(layers, TextMeshProLayerData.ShadowPreset, Styles.Shadow.text, changed));
            menu.AddItem(Styles.Glow, false, () => AddLayer(layers, TextMeshProLayerData.GlowPreset, Styles.Glow.text, changed));
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

        internal static GUIContent GetLayerDisplayContent(SerializedProperty layer)
        {
            return GetLayerDisplayContent(new SerializedLayer(layer));
        }

        private static GUIContent GetLayerDisplayContent(SerializedLayer layer)
        {
            return Styles.GetLayerDisplayContent(layer.DisplayLabel);
        }

        internal static string[] GetLayerFeatureIconNamesForTests(SerializedProperty layer)
        {
            var flags = new SerializedLayer(layer).FeatureFlags;
            var names = new string[GetLayerFeatureIconCount(flags)];
            var index = 0;
            if ((flags & LayerFeatureFlags.Face) != 0) {
                names[index++] = Styles.Face.text;
            }

            if ((flags & LayerFeatureFlags.Stroke) != 0) {
                names[index++] = Styles.Outline.text;
            }

            if ((flags & LayerFeatureFlags.Shadow) != 0) {
                names[index] = Styles.Underlay.text;
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

        private static Rect GetLayerTitleRect(Rect rect, int iconCount)
        {
            var iconWidth = GetLayerFeatureIconBadgesWidth(iconCount);
            if (iconWidth <= 0f) {
                return rect;
            }

            rect.width = Mathf.Max(0f, rect.width - iconWidth - Styles.HeaderControlGap);
            return rect;
        }

        private static float GetLayerFeatureIconBadgesWidth(int iconCount)
        {
            if (iconCount <= 0) {
                return 0f;
            }

            return iconCount * Styles.FeatureIconBadgeSize + (iconCount - 1) * Styles.FeatureIconBadgeGap;
        }

        private static int GetLayerFeatureIconCount(LayerFeatureFlags flags)
        {
            var count = 0;
            if ((flags & LayerFeatureFlags.Face) != 0) {
                count++;
            }

            if ((flags & LayerFeatureFlags.Stroke) != 0) {
                count++;
            }

            if ((flags & LayerFeatureFlags.Shadow) != 0) {
                count++;
            }

            return count;
        }

        private static void DrawFeatureIconBadges(Rect rect, LayerFeatureFlags flags)
        {
            var iconCount = GetLayerFeatureIconCount(flags);
            if (iconCount == 0) {
                return;
            }

            var totalWidth = GetLayerFeatureIconBadgesWidth(iconCount);
            var iconRect = new Rect(
                rect.xMax - totalWidth,
                rect.y + Mathf.Floor((rect.height - Styles.FeatureIconBadgeSize) * 0.5f),
                Styles.FeatureIconBadgeSize,
                Styles.FeatureIconBadgeSize);

            DrawFeatureIconBadge(ref iconRect, flags, LayerFeatureFlags.Face, Styles.FillLayerIcon);
            DrawFeatureIconBadge(ref iconRect, flags, LayerFeatureFlags.Stroke, Styles.StrokeLayerIcon);
            DrawFeatureIconBadge(ref iconRect, flags, LayerFeatureFlags.Shadow, Styles.ShadowLayerIcon);
        }

        private static void DrawFeatureIconBadge(ref Rect iconRect, LayerFeatureFlags flags, LayerFeatureFlags flag, Texture2D icon)
        {
            if ((flags & flag) == 0) {
                return;
            }

            if (icon != null) {
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            }

            iconRect.x += Styles.FeatureIconBadgeSize + Styles.FeatureIconBadgeGap;
        }

        private static void DrawUnifiedLayer(SerializedLayer layer, float availablePadding, UnityEngine.Object sceneTarget)
        {
            var face = layer.Face;
            var stroke = layer.Stroke;
            var shadow = layer.Shadow;
            GUILayout.Space(6f);

            var faceExpanded = BeginToggleSection(layer, Styles.Face, face.Enabled);
            if (faceExpanded) {
                using (new EditorGUI.DisabledScope(face.Enabled is { hasMultipleDifferentValues: false, boolValue: false })) {
                    CanvasPaintDrawer.DrawFillMode(face.Paint);
                    CanvasPaintDrawer.DrawAppearance(face.Paint);
                    DrawPaintMapping(face.Paint, sceneTarget, true);
                    CanvasEditorGUI.DrawRoundedInspectorSubsection(Styles.Shape);
                    CanvasEditorGUI.SdfLengthSlider(face.Dilate, face.DilateUnit, Styles.Dilate, availablePadding, -availablePadding, availablePadding);
                }
            }
            EndToggleSection(faceExpanded);

            var strokeExpanded = BeginToggleSection(layer, Styles.Outline, stroke.Enabled);
            if (strokeExpanded) {
                using (new EditorGUI.DisabledScope(stroke.Enabled is { hasMultipleDifferentValues: false, boolValue: false })) {
                    CanvasPaintDrawer.DrawFillMode(stroke.Paint);
                    CanvasPaintDrawer.DrawAppearance(stroke.Paint);
                    DrawPaintMapping(stroke.Paint, sceneTarget, true);
                    CanvasEditorGUI.DrawRoundedInspectorSubsection(Styles.Shape);
                    CanvasEditorGUI.PropertyField(stroke.Position, Styles.Position);
                    var reservedFacePadding = GetEffectivePositiveSdfBudget(face.Enabled, face.Dilate, availablePadding);
                    GetStrokeSliderBudgets(stroke.Width, stroke.Feather, stroke.Position, availablePadding, reservedFacePadding, out var widthMax, out var featherMax);
                    CanvasEditorGUI.ConstrainedSdfLengthSlider(stroke.Width, stroke.WidthUnit, Styles.Width, availablePadding, 0f, widthMax);
                    CanvasEditorGUI.ConstrainedSdfLengthSlider(stroke.Feather, stroke.FeatherUnit, Styles.Feather, availablePadding, 0f, featherMax);
                    CanvasEditorGUI.Vector2Field(stroke.Offset, Styles.Offset);
                }
            }
            EndToggleSection(strokeExpanded);

            var shadowExpanded = BeginToggleSection(layer, Styles.Underlay, shadow.Enabled);
            if (shadowExpanded) {
                using (new EditorGUI.DisabledScope(shadow.Enabled is { hasMultipleDifferentValues: false, boolValue: false })) {
                    CanvasPaintDrawer.DrawFillMode(shadow.Paint);
                    CanvasPaintDrawer.DrawAppearance(shadow.Paint);
                    DrawPaintMapping(shadow.Paint, sceneTarget, true);
                    CanvasEditorGUI.DrawRoundedInspectorSubsection(Styles.Effect);
                    var reservedFacePadding = GetEffectivePositiveSdfBudget(face.Enabled, face.Dilate, availablePadding);
                    GetShadowSliderBudgets(shadow.Spread, shadow.Blur, availablePadding, reservedFacePadding, out var spreadMin, out var spreadMax, out var blurMax);
                    CanvasEditorGUI.ConstrainedSdfLengthSlider(shadow.Spread, shadow.SpreadUnit, Styles.Spread, availablePadding, spreadMin, spreadMax);
                    CanvasEditorGUI.ConstrainedSdfLengthSlider(shadow.Blur, shadow.BlurUnit, Styles.Blur, availablePadding, 0f, blurMax);
                    CanvasEditorGUI.Vector2Field(shadow.Offset, Styles.Offset);
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
            var strokeWidthFactor = TextMeshProUtility.GetStrokeVisualPaddingFactor(position);
            widthMax = strokeWidthFactor > 0.0f ? remainingBudget / strokeWidthFactor : remainingBudget;
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

        private static bool BeginToggleSection(SerializedLayer layer, GUIContent title, SerializedProperty enabledProperty)
        {
            var key = layer.Root.serializedObject.targetObject.GetInstanceID() + "." + layer.Root.propertyPath + "." + title.text;
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
            var headerRect = GUILayoutUtility.GetRect(1f, Styles.FillSectionHeaderHeight);
            GUI.Label(headerRect, GUIContent.none, CanvasEditorGUI.GetRoundedInspectorPanelHeaderStyle(expanded));

            var foldoutRect = headerRect;
            foldoutRect.x += 9f;
            foldoutRect.y += Mathf.Floor((headerRect.height - Styles.FoldoutSize) * 0.5f);
            foldoutRect.width = Styles.FoldoutSize;
            foldoutRect.height = Styles.FoldoutSize;

            var toggleRect = headerRect;
            toggleRect.x = foldoutRect.xMax + 5f;
            toggleRect.y += Mathf.Floor((headerRect.height - Styles.EnabledToggleSize) * 0.5f);
            toggleRect.width = Styles.EnabledToggleSize;
            toggleRect.height = Styles.EnabledToggleSize;

            var nextX = toggleRect.xMax + 8f;
            if (title.image != null) {
                var iconRect = headerRect;
                iconRect.x = nextX;
                iconRect.y += Mathf.Floor((headerRect.height - Styles.LayerIconSize) * 0.5f);
                iconRect.width = Styles.LayerIconSize;
                iconRect.height = Styles.LayerIconSize;
                GUI.DrawTexture(iconRect, title.image, ScaleMode.ScaleToFit);
                nextX = iconRect.xMax + 6f;
            }

            var labelRect = headerRect;
            labelRect.xMin = nextX;
            labelRect.xMax -= 8f;

            using (new EditorGUI.DisabledScope(enabledProperty is { hasMultipleDifferentValues: false, boolValue: false })) {
                EditorGUI.LabelField(labelRect, Styles.GetTextOnlyContent(title), EditorStyles.boldLabel);
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
            DrawLayerSwatch(rect, new SerializedLayer(layer));
        }

        private static void DrawLayerSwatch(Rect rect, SerializedLayer layer)
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
            var borderColor = isProSkin ? Styles.InstanceMarkerBorderColorDark : Styles.InstanceMarkerBorderColorLight;
            EditorGUI.DrawRect(rect, isProSkin ? Styles.InstanceMarkerBackgroundColorDark : Styles.InstanceMarkerBackgroundColorLight);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), borderColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), borderColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), borderColor);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), borderColor);
            GUI.Label(rect, content, Styles.InstanceMarkerStyle);
        }

        private static Texture2D LoadLayerIcon(string filename)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.tripledot.canvaskit/Editor Default Resources/Icons/" + filename);
        }

        internal static LayerSwatchDescriptor GetLayerSwatchDescriptorForTests(SerializedProperty layer)
        {
            return GetLayerSwatchDescriptor(layer);
        }

        private static LayerSwatchDescriptor GetLayerSwatchDescriptor(SerializedProperty layer)
        {
            return GetLayerSwatchDescriptor(new SerializedLayer(layer));
        }

        private static LayerSwatchDescriptor GetLayerSwatchDescriptor(SerializedLayer layer)
        {
            var strokeEnabled = layer.Stroke.Enabled.boolValue;
            var strokePaint = strokeEnabled ? ReadPaintForSwatch(layer.Stroke.PaintRoot) : default;
            if (layer.Face.Enabled.boolValue) {
                return new LayerSwatchDescriptor(
                    true,
                    ReadPaintForSwatch(layer.Face.PaintRoot),
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

            if (layer.Shadow.Enabled.boolValue) {
                return new LayerSwatchDescriptor(
                    true,
                    ReadPaintForSwatch(layer.Shadow.PaintRoot),
                    Styles.UnderlayFallbackColor,
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
            return ReadPaintForSwatch(new SerializedPaintSnapshot(paint));
        }

        private static CanvasPaint ReadPaintForSwatch(SerializedPaintSnapshot paint)
        {
            return new CanvasPaint {
                Type = (CanvasPaintType)paint.Type.enumValueIndex,
                GradientMode = (CanvasGradientMode)paint.GradientMode.enumValueIndex,
                Color = paint.Color.colorValue,
                SecondaryColor = paint.SecondaryColor.colorValue,
                Opacity = paint.Opacity.floatValue,
                Gradient = paint.Gradient.gradientValue,
                Texture = paint.Texture.objectReferenceValue as Texture2D
            };
        }

        private readonly struct SerializedPaintSnapshot
        {
            public readonly SerializedProperty Type;
            public readonly SerializedProperty GradientMode;
            public readonly SerializedProperty Color;
            public readonly SerializedProperty SecondaryColor;
            public readonly SerializedProperty Opacity;
            public readonly SerializedProperty Gradient;
            public readonly SerializedProperty Texture;

            public SerializedPaintSnapshot(SerializedProperty root)
            {
                Type = root.FindPropertyRelative("Type");
                GradientMode = root.FindPropertyRelative("GradientMode");
                Color = root.FindPropertyRelative("Color");
                SecondaryColor = root.FindPropertyRelative("SecondaryColor");
                Opacity = root.FindPropertyRelative("Opacity");
                Gradient = root.FindPropertyRelative("Gradient");
                Texture = root.FindPropertyRelative("Texture");
            }
        }

        private sealed class SerializedFace
        {
            public readonly SerializedProperty Root;
            private SerializedCanvasPaint paint;
            private SerializedProperty paintRoot;
            private SerializedProperty dilate;
            private SerializedProperty dilateUnit;

            public SerializedFace(SerializedProperty root)
            {
                Root = root;
                Enabled = root.FindPropertyRelative("Enabled");
            }

            public readonly SerializedProperty Enabled;
            public SerializedProperty PaintRoot => paintRoot ??= Root.FindPropertyRelative("Paint");
            public SerializedCanvasPaint Paint => paint ??= new SerializedCanvasPaint(PaintRoot);
            public SerializedProperty Dilate => dilate ??= Root.FindPropertyRelative("Dilate");
            public SerializedProperty DilateUnit => dilateUnit ??= Root.FindPropertyRelative("DilateUnit");
        }

        private sealed class SerializedStroke
        {
            public readonly SerializedProperty Root;
            public readonly SerializedProperty Enabled;
            private SerializedCanvasPaint paint;
            private SerializedProperty paintRoot;
            private SerializedProperty position;
            private SerializedProperty width;
            private SerializedProperty widthUnit;
            private SerializedProperty feather;
            private SerializedProperty featherUnit;
            private SerializedProperty offset;

            public SerializedStroke(SerializedProperty root)
            {
                Root = root;
                Enabled = root.FindPropertyRelative("Enabled");
            }

            public SerializedProperty PaintRoot => paintRoot ??= Root.FindPropertyRelative("Paint");
            public SerializedCanvasPaint Paint => paint ??= new SerializedCanvasPaint(PaintRoot);
            public SerializedProperty Position => position ??= Root.FindPropertyRelative("Position");
            public SerializedProperty Width => width ??= Root.FindPropertyRelative("Width");
            public SerializedProperty WidthUnit => widthUnit ??= Root.FindPropertyRelative("WidthUnit");
            public SerializedProperty Feather => feather ??= Root.FindPropertyRelative("Feather");
            public SerializedProperty FeatherUnit => featherUnit ??= Root.FindPropertyRelative("FeatherUnit");
            public SerializedProperty Offset => offset ??= Root.FindPropertyRelative("Offset");
        }

        private sealed class SerializedShadow
        {
            public readonly SerializedProperty Root;
            public readonly SerializedProperty Enabled;
            private SerializedCanvasPaint paint;
            private SerializedProperty paintRoot;
            private SerializedProperty offset;
            private SerializedProperty blur;
            private SerializedProperty blurUnit;
            private SerializedProperty spread;
            private SerializedProperty spreadUnit;

            public SerializedShadow(SerializedProperty root)
            {
                Root = root;
                Enabled = root.FindPropertyRelative("Enabled");
            }

            public SerializedProperty PaintRoot => paintRoot ??= Root.FindPropertyRelative("Paint");
            public SerializedCanvasPaint Paint => paint ??= new SerializedCanvasPaint(PaintRoot);
            public SerializedProperty Offset => offset ??= Root.FindPropertyRelative("Offset");
            public SerializedProperty Blur => blur ??= Root.FindPropertyRelative("Blur");
            public SerializedProperty BlurUnit => blurUnit ??= Root.FindPropertyRelative("BlurUnit");
            public SerializedProperty Spread => spread ??= Root.FindPropertyRelative("Spread");
            public SerializedProperty SpreadUnit => spreadUnit ??= Root.FindPropertyRelative("SpreadUnit");
        }
    }
}
