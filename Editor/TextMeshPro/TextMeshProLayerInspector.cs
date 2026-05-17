using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditorInternal;
using UnityEngine;
using TMPro;
using UnityObject = UnityEngine.Object;

namespace Tripledot.CanvasKit.Editor
{
    internal sealed class TextMeshProLayerInspector
    {
        private enum InspectorMode
        {
            Preset,
            Stack
        }

        private enum LayerSource
        {
            Preset,
            Local,
            LinkedPreset
        }

        private readonly InspectorMode mode;
        private readonly SerializedObject serializedObject;
        
        private readonly SerializedProperty layers;
        private readonly SerializedProperty presetProperty;
        private readonly SerializedProperty presetLayerOverrides;
        
        private readonly TextMeshProLayerStack stack;
        private readonly TextMeshProLayerPreset preset;

        private bool pendingPresetDirty;
        
        private TextMeshProLayerPreset linkedPreset;
        private SerializedObject linkedPresetObject;
        private SerializedProperty linkedPresetLayers;

        private readonly List<int> dirtyLayerIndices = new List<int>();

        public TextMeshProLayerInspector(
            TextMeshProLayerPreset preset,
            SerializedObject presetObject,
            SerializedProperty presetLayers)
        {
            mode = InspectorMode.Preset;
            this.preset = preset;
            serializedObject = presetObject;
            layers = presetLayers;
        }

        public TextMeshProLayerInspector(
            TextMeshProLayerStack stack,
            SerializedObject stackObject,
            SerializedProperty presetProperty,
            SerializedProperty localLayers,
            SerializedProperty presetLayerOverrides)
        {
            mode = InspectorMode.Stack;
            this.stack = stack;
            serializedObject = stackObject;
            this.presetProperty = presetProperty;
            layers = localLayers;
            this.presetLayerOverrides = presetLayerOverrides;
        }

        public void Draw()
        {
            if (mode == InspectorMode.Preset) {
                DrawLayerListAndBlocks(layers, LayerSource.Preset);
                return;
            }

            var assignedPreset = presetProperty?.objectReferenceValue as TextMeshProLayerPreset;
            if (assignedPreset == null) {
                ClearLinkedPresetCache();
                DrawLayerListAndBlocks(layers, LayerSource.Local);
            } else {
                DrawLinkedPreset(assignedPreset);
            }
        }

        private void DrawLinkedPreset(TextMeshProLayerPreset assignedPreset)
        {
            EnsureLinkedPresetCache(assignedPreset);
            if (linkedPresetObject == null || linkedPresetLayers == null) {
                return;
            }

            linkedPresetObject.Update();
            EnsureOverrideArraySize(serializedObject, presetLayerOverrides, linkedPresetLayers.arraySize);
            DrawLayerListAndBlocks(linkedPresetLayers, LayerSource.LinkedPreset);
            ApplyLinkedPresetProperties(assignedPreset);
        }

        private void DrawLayerListAndBlocks(
            SerializedProperty layerProperty, LayerSource source)
        {
            var layerList = CreateLayerList(layerProperty, source);
            layerList.DoLayoutList();
            DrawLayerInspectorBlocks(layerProperty, source);
        }

        private ReorderableList CreateLayerList(
            SerializedProperty layerProperty, LayerSource source)
        {
            var editable = source != LayerSource.LinkedPreset;

            return new ReorderableList(layerProperty.serializedObject, layerProperty, editable, true, editable, editable) {
                elementHeight = 26f,
                headerHeight = 23f,
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, Styles.Layers),
                drawNoneElementCallback = rect => EditorGUI.HelpBox(rect, Styles.LayerStackEmptyInfo.text, MessageType.Info),
                drawElementCallback = (rect, index, _, _) => DrawLayerListRow(rect, index, layerProperty, source),
                onAddDropdownCallback = (rect, list) => ShowAddLayerMenu(rect, layerProperty, source),
                onRemoveCallback = list => {
                    ReorderableList.defaultBehaviours.DoRemoveButton(list);
                    OnLayerListChanged(source);
                },
                onReorderCallback = list => OnLayerListChanged(source)
            };
        }

        private void OnLayerListChanged(LayerSource source)
        {
            switch (source)
            {
                case LayerSource.LinkedPreset:
                    QueuePresetDirty();
                    return;
                case LayerSource.Preset:
                    QueuePresetDirty();
                    return;
            }
        }

        private void OnLayerChanged(LayerSource source, int index)
        {
            switch (source)
            {
                case LayerSource.LinkedPreset:
                {
                    if (!IsLinkedPresetInstanceLayer(index)) {
                        QueuePresetDirty();
                    }
                    return;
                }
                case LayerSource.Preset:
                    QueuePresetDirty();
                    return;
            }
        }

        private void OnLayerDirty(LayerSource source, int layerIndex)
        {
            switch (source)
            {
                case LayerSource.LinkedPreset when !IsLinkedPresetInstanceLayer(layerIndex):
                    QueuePresetDirty();
                    return;
                case LayerSource.Preset:
                    QueuePresetDirty();
                    return;
            }
        }

        public void ClearLinkedPresetCache()
        {
            linkedPreset = null;
            linkedPresetObject = null;
            linkedPresetLayers = null;
        }

        private void EnsureLinkedPresetCache(TextMeshProLayerPreset preset)
        {
            if (linkedPreset == preset && linkedPresetObject != null && linkedPresetLayers != null) {
                return;
            }

            linkedPreset = preset;
            linkedPresetObject = preset != null ? new SerializedObject(preset) : null;
            linkedPresetLayers = linkedPresetObject?.FindProperty("layers");
        }

        private void EnsureOverrideArraySize(
            SerializedObject stackObject, SerializedProperty presetLayerOverrides, int size)
        {
            if (presetLayerOverrides == null || presetLayerOverrides.arraySize == size) {
                return;
            }

            presetLayerOverrides.arraySize = size;
            stackObject.ApplyModifiedProperties();
        }

        private void ApplyLinkedPresetProperties(TextMeshProLayerPreset preset)
        {
            if (preset == null || linkedPresetObject == null) {
                return;
            }

            preset.BeginSuppressingOnValidateNotifications();
            bool appliedPresetProperties = linkedPresetObject.ApplyModifiedProperties();
            preset.EndSuppressingOnValidateNotifications();
            if (appliedPresetProperties) {
                EditorUtility.SetDirty(preset);
            }

            FlushPresetDirties(preset);
        }

        private SerializedProperty GetLinkedRowLayer(int index, SerializedProperty sourceLayer)
        {
            if (presetLayerOverrides == null || index < 0 || index >= presetLayerOverrides.arraySize) {
                return sourceLayer;
            }

            var layerOverride = presetLayerOverrides.GetArrayElementAtIndex(index);
            var overrideEnabled = layerOverride.FindPropertyRelative("overrideLayer");
            return overrideEnabled.boolValue ? layerOverride.FindPropertyRelative("layer") : sourceLayer;
        }

        private bool IsLinkedPresetInstanceLayer(int index)
        {
            return stack != null && stack.IsPresetLayerInstance(index);
        }

        private void DrawLayerModeControl(Rect rect, int index)
        {
            if (stack == null || presetLayerOverrides == null || index < 0 || index >= presetLayerOverrides.arraySize) {
                return;
            }

            var layerOverride = presetLayerOverrides.GetArrayElementAtIndex(index);
            var overrideEnabled = layerOverride.FindPropertyRelative("overrideLayer");
            EditorGUI.BeginChangeCheck();
            var instance = DrawPresetInstanceSegmentedControl(rect, overrideEnabled.boolValue);
            if (EditorGUI.EndChangeCheck()) {
                SetInstanceMode(index, instance);
            }
        }

        private static bool DrawPresetInstanceSegmentedControl(Rect rect, bool instance)
        {
            rect = EditorGUI.IndentedRect(rect);
            var presetRect = new Rect(rect.x, rect.y, Mathf.Floor(rect.width * 0.5f), rect.height);
            var instanceRect = new Rect(presetRect.xMax - 1f, rect.y, rect.xMax - presetRect.xMax + 1f, rect.height);

            var sharedSelectedAfterDraw = DrawModeSegment(presetRect, Styles.SharedMode, !instance, true);
            var instanceSelectedAfterDraw = DrawModeSegment(instanceRect, Styles.InstanceMode, instance, false);
            return GetPresetInstanceSegmentResult(instance, sharedSelectedAfterDraw, instanceSelectedAfterDraw);
        }

        internal static bool GetPresetInstanceSegmentResult(bool currentInstance, bool sharedSelectedAfterDraw, bool instanceSelectedAfterDraw)
        {
            return currentInstance switch
            {
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

        private void SetInstanceMode(int index, bool instance)
        {
            if (stack == null || presetLayerOverrides == null || index < 0 || index >= presetLayerOverrides.arraySize) {
                return;
            }

            var overrideEnabled = presetLayerOverrides.GetArrayElementAtIndex(index).FindPropertyRelative("overrideLayer");
            if (overrideEnabled.boolValue == instance) {
                return;
            }

            presetLayerOverrides.serializedObject.ApplyModifiedProperties();
            stack.SetPresetLayerInstance(index, instance);
            presetLayerOverrides.serializedObject.Update();
        }

        public void QueuePresetDirty()
        {
            pendingPresetDirty = true;
        }

        public void FlushPresetDirties(TextMeshProLayerPreset preset)
        {
            if (!pendingPresetDirty) {
                return;
            }

            pendingPresetDirty = false;
            MarkPresetDirty(preset);
        }

        private static void MarkPresetDirty(TextMeshProLayerPreset preset)
        {
            if (preset == null) {
                return;
            }

            EditorUtility.SetDirty(preset);
            preset.NotifyChanged();
        }

        private void DrawLayerInspectorBlocks(SerializedProperty layers, LayerSource source)
        {
            ClearLayerDirties();
            var context = CreateDrawContext(source);

            try {
                if (layers.arraySize == 0) {
                    return;
                }

                for (int i = 0; i < layers.arraySize; i++) {
                    var layer = GetLayer(layers, source, i);
                    if (layer == null) {
                        continue;
                    }

                    var serializedLayer = new SerializedLayer(layer);
                    var expanded = DrawLayerInspectorHeader(serializedLayer, i, context);

                    if (expanded) {
                        using (new EditorGUI.DisabledScope(serializedLayer.IsDisabled)) {
                            DrawLayerDetails(serializedLayer, i, context);
                        }
                    }

                    if (expanded) {
                        EditorGUILayout.Space(1f);
                    }
                }
            } finally {
                FlushLayerDirties(source);
            }
        }

        private SerializedProperty GetLayer(SerializedProperty layers, LayerSource source, int index)
        {
            var sourceLayer = layers.GetArrayElementAtIndex(index);
            if (source == LayerSource.LinkedPreset) {
                return GetLinkedRowLayer(index, sourceLayer);
            }

            return sourceLayer;
        }

        private DrawContext CreateDrawContext(LayerSource source)
        {
            return new DrawContext(
                source,
                GetContextKey(source),
                GetAvailablePadding(),
                stack,
                source == LayerSource.LinkedPreset);
        }

        private string GetContextKey(LayerSource source)
        {
            if (source == LayerSource.LinkedPreset && stack != null && linkedPreset != null) {
                return "TextMeshProLayerStack." + stack.GetInstanceID() + ".Linked." + linkedPreset.GetInstanceID();
            }

            if (source == LayerSource.Local && stack != null) {
                return "TextMeshProLayerStack." + stack.GetInstanceID() + ".Local";
            }

            if (source == LayerSource.Preset && preset != null) {
                return "TextMeshProLayerPreset." + preset.GetInstanceID();
            }

            return source.ToString();
        }

        private float GetAvailablePadding()
        {
            if (stack != null && stack.TryGetComponent(out TextMeshProUGUI text)) {
                return TextMeshProUtility.CalculateAvailablePadding(text);
            }

            return CanvasEditorGUI.Styles.DefaultSdfSliderPadding;
        }

        private void ClearLayerDirties()
        {
            dirtyLayerIndices.Clear();
        }

        private void MarkLayerDirty(int layerIndex)
        {
            if (layerIndex < 0) {
                return;
            }

            for (int i = 0; i < dirtyLayerIndices.Count; i++) {
                if (dirtyLayerIndices[i] == layerIndex) {
                    return;
                }
            }

            dirtyLayerIndices.Add(layerIndex);
        }

        private void FlushLayerDirties(LayerSource source)
        {
            for (int i = 0; i < dirtyLayerIndices.Count; i++) {
                OnLayerDirty(source, dirtyLayerIndices[i]);
            }
        }

        private void DrawLayerDetails(SerializedLayer layer, int layerIndex, DrawContext context)
        {
            GUILayout.Space(4f);
            EditorGUILayout.PropertyField(layer.Label, Styles.Label);

            using (new LayerChangeCheckScope(this, layerIndex)) {
                EditorGUILayout.PropertyField(layer.BlendMode, Styles.BlendMode);
                EditorGUILayout.Slider(layer.Opacity, 0f, 1f, Styles.Opacity);
            }

            GUILayout.Space(5f);
            CoreEditorUtils.DrawSplitter();
            GUILayout.Space(5f);
            DrawUnifiedLayer(layer, layerIndex, context);
        }

        private void DrawLayerListRow(
            Rect rect,
            int index,
            SerializedProperty layers,
            LayerSource source)
        {
            var layer = GetLayer(layers, source, index);
            if (layer == null) {
                return;
            }
            
            var serializedLayer = new SerializedLayer(layer);
            var hasTrailingControl = source == LayerSource.LinkedPreset;

            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;

            CalculateLayerListRowRects(
                rect,
                hasTrailingControl,
                out var enabledRect,
                out var swatchRect,
                out var labelRect,
                out var trailingRect);

            var enabled = serializedLayer.Enabled;

            EditorGUI.BeginChangeCheck();
            var layerEnabled = EditorGUI.Toggle(enabledRect, enabled.boolValue);
            if (EditorGUI.EndChangeCheck()) {
                enabled.boolValue = layerEnabled;
                OnLayerChanged(source, index);
            }

            DrawLayerSwatch(swatchRect, serializedLayer);

            var featureFlags = serializedLayer.FeatureFlags;
            var titleRect = GetLayerTitleRect(labelRect, GetLayerFeatureIconCount(featureFlags));
            using (new EditorGUI.DisabledScope(serializedLayer.IsDisabled)) {
                CanvasEditorGUI.DrawSwatchLabel(titleRect, GetLayerDisplayContent(serializedLayer));
                DrawFeatureIconBadges(labelRect, featureFlags);
            }

            if (hasTrailingControl) {
                DrawLayerModeControl(trailingRect, index);
            }
        }

        private bool DrawLayerInspectorHeader(SerializedLayer layer, int index, DrawContext context)
        {
            var rect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(1f, Styles.LayerHeaderHeight));
            var backgroundRect = rect;
            backgroundRect.xMin = 0f;
            backgroundRect.width += 4f;
            DrawLayerHeaderBackground(backgroundRect);

            var key = GetLayerExpansionKey(layer.Root, index, context.ContextKey);
            var expanded = SessionState.GetBool(key, true);
            var instanceLayer = context.Source == LayerSource.LinkedPreset && IsLinkedPresetInstanceLayer(index);

            CalculateLayerHeaderRects(
                rect,
                context.ShowPresetModeMarker,
                out var foldoutRect,
                out var enabledRect,
                out var swatchRect,
                out var instanceMarkerRect,
                out var labelRect);

            var enabled = layer.Enabled;

            expanded = GUI.Toggle(foldoutRect, expanded, GUIContent.none, EditorStyles.foldout);

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = enabled.hasMultipleDifferentValues;
            var layerEnabled = GUI.Toggle(
                enabledRect, enabled.hasMultipleDifferentValues || enabled.boolValue, GUIContent.none,
                enabled.hasMultipleDifferentValues ? CoreEditorStyles.smallMixedTickbox : CoreEditorStyles.smallTickbox);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck()) {
                enabled.boolValue = layerEnabled;
                OnLayerChanged(context.Source, index);
            }

            DrawLayerSwatch(swatchRect, layer);

            var titleRect = GetLayerTitleRect(labelRect, 0);
            using (new EditorGUI.DisabledScope(layer.IsDisabled)) {
                EditorGUI.LabelField(titleRect, GetLayerDisplayContent(layer), EditorStyles.boldLabel);
            }

            if (context.ShowPresetModeMarker) {
                DrawPresetModeMarker(instanceMarkerRect, instanceLayer ? Styles.InstanceLayer : Styles.SharedLayer);
            }

            var evt = Event.current;
            if (evt.type == EventType.MouseDown &&
                evt.button == 0 &&
                rect.Contains(evt.mousePosition) &&
                !foldoutRect.Contains(evt.mousePosition) &&
                !enabledRect.Contains(evt.mousePosition)) {
                expanded = !expanded;
                evt.Use();
            }

            SessionState.SetBool(key, expanded);
            return expanded;
        }

        private static void CalculateLayerListRowRects(
            Rect rect, bool hasTrailingControl, out Rect enabledRect, out Rect swatchRect, out Rect labelRect, out Rect trailingRect)
        {
            enabledRect = new Rect(rect.x, rect.y, Styles.EnabledToggleSize, rect.height);
            var swatchSize = Mathf.Min(Styles.LayerSwatchSize, rect.height);
            swatchRect = new Rect(enabledRect.xMax + Styles.HeaderControlGap, rect.y + (rect.height - swatchSize) * 0.5f, swatchSize, swatchSize);
            var trailingWidth = hasTrailingControl ? Styles.TrailingControlWidth : 0f;
            var labelStart = swatchRect.xMax + Styles.HeaderControlGap;
            labelRect = new Rect(labelStart, rect.y, rect.xMax - labelStart - trailingWidth - 8f, rect.height);
            trailingRect = new Rect(rect.xMax - trailingWidth, rect.y, trailingWidth, rect.height);
        }

        private static void CalculateLayerHeaderRects(
            Rect rect, bool showPresetModeMarker, out Rect foldoutRect, out Rect enabledRect, out Rect swatchRect, out Rect instanceMarkerRect, out Rect labelRect)
        {
            foldoutRect = rect;
            foldoutRect.x += 2f;
            foldoutRect.y += Mathf.Floor((rect.height - Styles.FoldoutSize) * 0.5f);
            foldoutRect.width = Styles.FoldoutSize;
            foldoutRect.height = Styles.FoldoutSize;

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

        private void ShowAddLayerMenu(
            Rect rect, SerializedProperty layers, LayerSource source)
        {
            var menu = new GenericMenu();
            menu.AddItem(Styles.Layer, false, () => AddLayer(layers, TextMeshProLayerData.Default, Styles.Layer.text, source));
            menu.AddItem(Styles.Stroke, false, () => AddLayer(layers, TextMeshProLayerData.StrokePreset, Styles.Stroke.text, source));
            menu.AddItem(Styles.Shadow, false, () => AddLayer(layers, TextMeshProLayerData.ShadowPreset, Styles.Shadow.text, source));
            menu.AddItem(Styles.Glow, false, () => AddLayer(layers, TextMeshProLayerData.GlowPreset, Styles.Glow.text, source));
            menu.DropDown(rect);
        }

        private void AddLayer(
            SerializedProperty layers, Func<TextMeshProLayerData> createLayer, string label, LayerSource source)
        {
            Undo.RecordObjects(layers.serializedObject.targetObjects, "Add TextMeshPro Layer");
            for (int i = 0; i < layers.serializedObject.targetObjects.Length; i++) {
                var layer = CreateLabeledLayer(createLayer, label);
                switch (layers.serializedObject.targetObjects[i]) {
                    case TextMeshProLayerStack stack when layers.propertyPath == "localLayers":
                        stack.LocalLayers.Add(layer);
                        stack.SetLayerCompositionChanged();
                        EditorUtility.SetDirty(stack);
                        break;
                    case TextMeshProLayerPreset preset when layers.propertyPath == "layers":
                        preset.MutableLayers.Add(layer);
                        EditorUtility.SetDirty(preset);
                        break;
                }
            }

            layers.serializedObject.Update();
            OnLayerListChanged(source);
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
            if ((flags & LayerFeatureFlags.Face) != 0)   { count++; }
            if ((flags & LayerFeatureFlags.Stroke) != 0) { count++; }
            if ((flags & LayerFeatureFlags.Shadow) != 0) { count++; }
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

        private static void DrawFeatureIconBadge(
            ref Rect iconRect, LayerFeatureFlags flags, LayerFeatureFlags flag, Texture2D icon)
        {
            if ((flags & flag) == 0) {
                return;
            }

            if (icon != null) {
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            }

            iconRect.x += Styles.FeatureIconBadgeSize + Styles.FeatureIconBadgeGap;
        }

        private void DrawUnifiedLayer(SerializedLayer layer, int layerIndex, DrawContext context)
        {
            var face = layer.Face;
            var stroke = layer.Stroke;
            var shadow = layer.Shadow;
            GUILayout.Space(6f);

            var faceExpanded = BeginToggleSection(layer, Styles.Face, face.Enabled, layerIndex);
            if (faceExpanded) {
                using (new EditorGUI.DisabledScope(face.Enabled is { hasMultipleDifferentValues: false, boolValue: false })) {
                    using (new LayerChangeCheckScope(this, layerIndex)) {
                        CanvasPaintDrawer.DrawFillMode(face.Paint);
                        CanvasPaintDrawer.DrawAppearance(face.Paint);
                        DrawPaintMapping(face.Paint, context.SceneTarget, layerIndex, true);
                    }

                    CanvasEditorGUI.DrawRoundedInspectorSubsection(Styles.Shape);
                    using (new LayerChangeCheckScope(this, layerIndex)) {
                        CanvasEditorGUI.SdfLengthSlider(face.Dilate, face.DilateUnit, Styles.Dilate, context.AvailablePadding, -context.AvailablePadding, context.AvailablePadding);
                    }
                }
            }
            EndToggleSection(faceExpanded);

            var strokeExpanded = BeginToggleSection(layer, Styles.Outline, stroke.Enabled, layerIndex);
            if (strokeExpanded) {
                using (new EditorGUI.DisabledScope(stroke.Enabled is { hasMultipleDifferentValues: false, boolValue: false })) {
                    using (new LayerChangeCheckScope(this, layerIndex)) {
                        CanvasPaintDrawer.DrawFillMode(stroke.Paint);
                        CanvasPaintDrawer.DrawAppearance(stroke.Paint);
                        DrawPaintMapping(stroke.Paint, context.SceneTarget, layerIndex, true);
                    }

                    CanvasEditorGUI.DrawRoundedInspectorSubsection(Styles.Shape);
                    using (new LayerChangeCheckScope(this, layerIndex)) {
                        CanvasEditorGUI.PropertyField(stroke.Position, Styles.Position);
                        var reservedFacePadding = GetEffectivePositiveSdfBudget(face.Enabled, face.Dilate, context.AvailablePadding);
                        GetStrokeSliderBudgets(stroke.Width, stroke.Feather, stroke.Position, context.AvailablePadding, reservedFacePadding, out var widthMax, out var featherMax);
                        CanvasEditorGUI.ConstrainedSdfLengthSlider(stroke.Width, stroke.WidthUnit, Styles.Width, context.AvailablePadding, 0f, widthMax);
                        CanvasEditorGUI.ConstrainedSdfLengthSlider(stroke.Feather, stroke.FeatherUnit, Styles.Feather, context.AvailablePadding, 0f, featherMax);
                        CanvasEditorGUI.Vector2Field(stroke.Offset, Styles.Offset);
                    }
                }
            }
            EndToggleSection(strokeExpanded);

            var shadowExpanded = BeginToggleSection(layer, Styles.Underlay, shadow.Enabled, layerIndex);
            if (shadowExpanded) {
                using (new EditorGUI.DisabledScope(shadow.Enabled is { hasMultipleDifferentValues: false, boolValue: false })) {
                    using (new LayerChangeCheckScope(this, layerIndex)) {
                        CanvasPaintDrawer.DrawFillMode(shadow.Paint);
                        CanvasPaintDrawer.DrawAppearance(shadow.Paint);
                        DrawPaintMapping(shadow.Paint, context.SceneTarget, layerIndex, true);
                    }

                    CanvasEditorGUI.DrawRoundedInspectorSubsection(Styles.Effect);
                    using (new LayerChangeCheckScope(this, layerIndex)) {
                        var reservedFacePadding = GetEffectivePositiveSdfBudget(face.Enabled, face.Dilate, context.AvailablePadding);
                        GetShadowSliderBudgets(shadow.Spread, shadow.Blur, context.AvailablePadding, reservedFacePadding, out var spreadMin, out var spreadMax, out var blurMax);
                        CanvasEditorGUI.ConstrainedSdfLengthSlider(shadow.Spread, shadow.SpreadUnit, Styles.Spread, context.AvailablePadding, spreadMin, spreadMax);
                        CanvasEditorGUI.ConstrainedSdfLengthSlider(shadow.Blur, shadow.BlurUnit, Styles.Blur, context.AvailablePadding, 0f, blurMax);
                        CanvasEditorGUI.Vector2Field(shadow.Offset, Styles.Offset);
                    }
                }
            }
            EndToggleSection(shadowExpanded);
        }

        private static float GetRemainingSdfBudget(float availablePadding, float reservedPadding)
        {
            return Mathf.Max(0f, availablePadding - reservedPadding);
        }

        internal static float GetEffectivePositiveSdfBudget(SerializedProperty enabled, SerializedProperty property, float availablePadding)
        {
            if (enabled is { hasMultipleDifferentValues: false, boolValue: false }) {
                return 0f;
            }

            return GetEffectivePositiveSdfBudget(GetFloatValueIfSame(property), availablePadding);
        }

        internal static float GetEffectivePositiveSdfBudget(float value, float availablePadding)
        {
            return Mathf.Min(Mathf.Max(0f, value), Mathf.Max(0f, availablePadding));
        }

        internal static void GetStrokeSliderBudgets(SerializedProperty width, SerializedProperty feather, SerializedProperty position, float availablePadding, float reservedPadding, out float widthMax, out float featherMax)
        {
            GetStrokeSliderBudgets(GetFloatValueIfSame(width), GetFloatValueIfSame(feather), GetStrokePosition(position), availablePadding, reservedPadding, out widthMax, out featherMax);
        }

        internal static void GetStrokeSliderBudgets(float width, float feather, TextMeshProStrokePosition position, float availablePadding, float reservedPadding, out float widthMax, out float featherMax)
        {
            var remainingBudget = GetRemainingSdfBudget(availablePadding, reservedPadding);
            var strokeWidthFactor = TextMeshProUtility.GetStrokeVisualPaddingFactor(position);
            widthMax = strokeWidthFactor > 0.0f ? remainingBudget / strokeWidthFactor : remainingBudget;
            TextMeshProUtility.ClampStrokeEffect(width, feather, position, availablePadding, reservedPadding, out var effectiveWidth, out _);
            featherMax = GetRemainingSdfBudget(remainingBudget, effectiveWidth * strokeWidthFactor);
        }

        internal static void GetShadowSliderBudgets(SerializedProperty spread, SerializedProperty blur, float availablePadding, float reservedPadding, out float spreadMin, out float spreadMax, out float blurMax)
        {
            GetShadowSliderBudgets(GetFloatValueIfSame(spread), GetFloatValueIfSame(blur), availablePadding, reservedPadding, out spreadMin, out spreadMax, out blurMax);
        }

        internal static void GetShadowSliderBudgets(float spread, float blur, float availablePadding, float reservedPadding, out float spreadMin, out float spreadMax, out float blurMax)
        {
            spreadMax = GetRemainingSdfBudget(availablePadding, reservedPadding);
            spreadMin = -availablePadding;
            TextMeshProUtility.ClampShadowEffect(spread, blur, availablePadding, reservedPadding, out var effectiveSpread, out _);
            blurMax = GetRemainingSdfBudget(spreadMax, effectiveSpread);
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

        private static void DrawPaintMapping(SerializedCanvasPaint paint, UnityObject sceneTarget, int layerIndex, bool boxed = false)
        {
            if (CanvasPaintDrawer.HasMapping(paint)) {
                CanvasPaintDrawer.DrawMappingHeader(paint, sceneTarget, boxed, layerIndex);
                CanvasPaintDrawer.DrawMapping(paint);
            }
        }

        private bool BeginToggleSection(
            SerializedLayer layer, GUIContent title, SerializedProperty enabledProperty, int layerIndex)
        {
            var key = layer.Root.serializedObject.targetObject.GetInstanceID() + "." + layer.Root.propertyPath + "." + title.text;
            var expanded = SessionState.GetBool(key, true);

            EditorGUILayout.BeginVertical(CanvasEditorGUI.Styles.RoundedInspectorPanelStyle);
            expanded = DrawHeaderToggleFoldout(title, expanded, enabledProperty, layerIndex);
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

        private bool DrawHeaderToggleFoldout(
            GUIContent title, bool expanded, SerializedProperty enabledProperty, int layerIndex)
        {
            var headerRect = GUILayoutUtility.GetRect(1f, Styles.FillSectionHeaderHeight);
            GUI.Label(headerRect, GUIContent.none, CanvasEditorGUI.Styles.GetRoundedInspectorPanelHeaderStyle(expanded));

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
                MarkLayerDirty(layerIndex);
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

        private static void DrawLayerSwatch(Rect rect, SerializedLayer layer)
        {
            var descriptor = GetLayerSwatchDescriptor(layer);
            if (descriptor.HasFill) {
                CanvasEditorGUI.DrawPaintSwatch(rect, descriptor.Fill);
            } else {
                CanvasEditorGUI.DrawTransparentSwatch(rect);
            }

            if (descriptor.HasInsetOutline) {
                CanvasEditorGUI.DrawPaintOutlineSwatch(rect, descriptor.InsetOutline);
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
            return ReadPaintForSwatch(new SerializedPaint(paint));
        }

        private static CanvasPaint ReadPaintForSwatch(SerializedPaint paint)
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

        [Flags]
        private enum LayerFeatureFlags
        {
            None = 0,
            Face = 1 << 0,
            Stroke = 1 << 1,
            Shadow = 1 << 2
        }

        private readonly struct DrawContext
        {
            public readonly LayerSource Source;
            public readonly string ContextKey;
            public readonly float AvailablePadding;
            public readonly UnityObject SceneTarget;
            public readonly bool ShowPresetModeMarker;

            public DrawContext(
                LayerSource source, string contextKey, float availablePadding, UnityObject sceneTarget, bool showPresetModeMarker)
            {
                Source = source;
                ContextKey = contextKey;
                AvailablePadding = availablePadding;
                SceneTarget = sceneTarget;
                ShowPresetModeMarker = showPresetModeMarker;
            }
        }

        private sealed class SerializedLayer
        {
            public readonly SerializedProperty Root;
            public readonly SerializedProperty Enabled;
            public readonly SerializedProperty Label;
            public readonly SerializedProperty BlendMode;
            public readonly SerializedProperty Opacity;
            public readonly SerializedFace Face;
            public readonly SerializedStroke Stroke;
            public readonly SerializedShadow Shadow;

            public SerializedLayer(SerializedProperty root)
            {
                Root = root;
                Enabled = root.FindPropertyRelative("enabled");
                Label = root.FindPropertyRelative("label");
                BlendMode = root.FindPropertyRelative("blendMode");
                Opacity = root.FindPropertyRelative("opacity");
                Face = new SerializedFace(root.FindPropertyRelative("face"));
                Stroke = new SerializedStroke(root.FindPropertyRelative("stroke"));
                Shadow = new SerializedShadow(root.FindPropertyRelative("shadow"));
            }

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
                    if (Face.Enabled.boolValue) { flags |= LayerFeatureFlags.Face; }
                    if (Stroke.Enabled.boolValue) { flags |= LayerFeatureFlags.Stroke; }
                    if (Shadow.Enabled.boolValue) { flags |= LayerFeatureFlags.Shadow; }
                    return flags;
                }
            }
        }

        private readonly struct LayerSwatchDescriptor
        {
            public readonly bool HasFill;
            public readonly CanvasPaint Fill;
            public readonly Color FillFallback;
            public readonly bool HasInsetOutline;
            public readonly CanvasPaint InsetOutline;
            public readonly Color InsetOutlineFallback;

            public LayerSwatchDescriptor(
                bool hasFill, CanvasPaint fill, Color fillFallback,
                bool hasInsetOutline, CanvasPaint insetOutline, Color insetOutlineFallback)
            {
                HasFill = hasFill;
                Fill = fill;
                FillFallback = fillFallback;
                HasInsetOutline = hasInsetOutline;
                InsetOutline = insetOutline;
                InsetOutlineFallback = insetOutlineFallback;
            }
        }

        private readonly struct LayerChangeCheckScope : IDisposable
        {
            private readonly TextMeshProLayerInspector inspector;
            private readonly int layerIndex;

            public LayerChangeCheckScope(
                TextMeshProLayerInspector inspector, int layerIndex)
            {
                this.inspector = inspector;
                this.layerIndex = layerIndex;
                EditorGUI.BeginChangeCheck();
            }

            public void Dispose()
            {
                if (EditorGUI.EndChangeCheck()) {
                    inspector?.MarkLayerDirty(layerIndex);
                }
            }
        }

        private readonly struct SerializedPaint
        {
            public readonly SerializedProperty Type;
            public readonly SerializedProperty GradientMode;
            public readonly SerializedProperty Color;
            public readonly SerializedProperty SecondaryColor;
            public readonly SerializedProperty Opacity;
            public readonly SerializedProperty Gradient;
            public readonly SerializedProperty Texture;

            public SerializedPaint(SerializedProperty root)
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
            public static readonly GUIContent InstanceMode = L10n.TextContent("Instance", "Use a layer copy on this TextMeshPro object for animation or object-specific edits.");
            public static readonly GUIContent SharedLayer = L10n.TextContent("S", "Shared layer: this row uses the assigned preset asset.");
            public static readonly GUIContent SharedMode = L10n.TextContent("Shared", "Use the shared preset layer. Editing this row changes the preset asset.");
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

            public static readonly Texture2D FillLayerIcon;
            public static readonly Texture2D StrokeLayerIcon;
            public static readonly Texture2D ShadowLayerIcon;
            public static readonly GUIContent ScratchContent = new GUIContent();
            public static readonly GUIStyle InstanceMarkerStyle;

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

            static Styles()
            {
                InstanceMarkerStyle = new GUIStyle(EditorStyles.miniLabel) {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = EditorGUIUtility.isProSkin ? InstanceMarkerTextColorDark : InstanceMarkerTextColorLight }
                };
                
                FillLayerIcon = LoadLayerIcon("TextIcon.png");
                StrokeLayerIcon = LoadLayerIcon("StrokeIcon.png");
                ShadowLayerIcon = LoadLayerIcon("ShadowIcon.png");
                
                Face.image = FillLayerIcon;
                Outline.image = StrokeLayerIcon;
                Underlay.image = ShadowLayerIcon;
            }

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
    }
}
