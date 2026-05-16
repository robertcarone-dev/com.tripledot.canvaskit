using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using TMPro;

namespace Tripledot.CanvasKit.Editor
{
    [CustomEditor(typeof(TextMeshProLayerStack))]
    internal sealed class TextMeshProLayerStackEditor : UnityEditor.Editor
    {
        private readonly struct PendingPresetDirty
        {
            public readonly TextMeshProLayerStack.DirtyFlags Flags;
            public readonly int LayerIndex;

            public PendingPresetDirty(TextMeshProLayerStack.DirtyFlags flags, int layerIndex)
            {
                Flags = flags;
                LayerIndex = layerIndex;
            }
        }

        private static class Styles
        {
            public static readonly GUIContent LayerPreset = L10n.TextContent("Layer Preset", "Use a shared TextMeshPro layer preset instead of local layers.");
            public static readonly GUIContent SharedMode = L10n.TextContent("Shared", "Use the shared preset layer. Editing this row changes the preset asset.");
            public static readonly GUIContent InstanceMode = L10n.TextContent("Instance", "Use a layer copy on this TextMeshPro object for animation or object-specific edits.");
            public static readonly GUIContent SaveLayerPreset = L10n.TextContent("Save", "Save local layers as a TextMeshPro layer preset.");
            public static readonly GUIContent CloneLayerPreset = L10n.TextContent("Clone", "Copy the current effective layers into a new preset asset and assign it to this stack.");
            public static readonly GUIContent ClearLayerPreset = L10n.TextContent("Clear", "Stop using the assigned preset and show this stack's local layers.");
            public static readonly GUIContent ApplyPresetFont = L10n.TextContent("Apply Font", "Assign the preset font asset to this TextMeshPro component.");

            public const float PresetActionButtonWidth = 52f;
            public const float PresetFieldGap = 4f;
        }

        private SerializedProperty preset;
        private SerializedProperty localLayers;
        private SerializedProperty presetLayerOverrides;
        private TextMeshProLayerStack.DirtyFlags pendingLayerDirtyFlags;
        private TextMeshProLayerStack.DirtyFlags pendingPresetDirtyFlags;
        private readonly List<int> pendingLayerMaterialDirties = new List<int>();
        private readonly List<PendingPresetDirty> pendingPresetLayerDirties = new List<PendingPresetDirty>();
        private readonly TextMeshProLayerInspectorGUI.LayerInspectorDirtyState layerDirtyState = new TextMeshProLayerInspectorGUI.LayerInspectorDirtyState();
        private ReorderableList localLayerList;
        private TextMeshProLayerPreset linkedPreset;
        private SerializedObject linkedPresetObject;
        private SerializedProperty linkedPresetLayers;
        private ReorderableList linkedLayerList;
        private int linkedPresetLayerCount = -1;

        private void OnEnable()
        {
            preset = serializedObject.FindProperty("preset");
            localLayers = serializedObject.FindProperty("localLayers");
            presetLayerOverrides = serializedObject.FindProperty("presetLayerOverrides");
            localLayerList = TextMeshProLayerInspectorGUI.CreateLayerList(localLayers, MarkLayerCompositionDirty, true);
        }

        private void OnDisable()
        {
            ClearLinkedPresetCache();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPresetField();
            if (preset.objectReferenceValue is TextMeshProLayerPreset assignedPreset) {
                DrawPresetFontMismatch(assignedPreset);
            }
            EditorGUILayout.Space();

            if (preset.objectReferenceValue is TextMeshProLayerPreset layerPreset) {
                DrawPresetLinkedLayers(layerPreset);
            } else {
                ClearLinkedPresetCache();
                TextMeshProLayerInspectorGUI.DoLayerList(localLayerList);
                layerDirtyState.Clear();
                TextMeshProLayerInspectorGUI.DrawLayerInspectorBlocks(
                    localLayers,
                    MarkLayerCompositionDirty,
                    contextKey: GetLayerListContextKey("Local"),
                    availablePadding: GetAvailablePadding(),
                    sceneTarget: target,
                    dirtyState: layerDirtyState);
                QueueLocalLayerDirty(layerDirtyState);
            }

            var appliedStackProperties = serializedObject.ApplyModifiedProperties();
            if (appliedStackProperties) {
                EditorUtility.SetDirty(target);
            }

            FlushPendingLayerDirty();
        }

        public void OnSceneGUI()
        {
            var result = CanvasGradientSceneView.Draw(target);
            if (result.Changed) {
                if (result.LayerIndex >= 0) {
                    MarkLayerMaterialDirty(result.LayerIndex);
                } else {
                    MarkLayerDirty(TextMeshProLayerStack.MaterialDirtyFlags);
                }

                Repaint();
            }
        }

        private void DrawPresetLinkedLayers(TextMeshProLayerPreset layerPreset)
        {
            EnsureLinkedPresetCache(layerPreset);
            linkedPresetObject.Update();
            EnsureOverrideArraySize(linkedPresetLayers.arraySize);
            EnsureLinkedLayerList();

            TextMeshProLayerInspectorGUI.DoLayerList(linkedLayerList);
            layerDirtyState.Clear();
            TextMeshProLayerInspectorGUI.DrawLayerInspectorBlocks(
                linkedPresetLayers,
                MarkPresetAndStackCompositionDirty,
                index => GetLinkedRowLayer(linkedPresetLayers, index),
                HandleLinkedLayerRowChanged,
                GetLayerListContextKey("Linked." + layerPreset.GetInstanceID()),
                GetAvailablePadding(),
                target,
                IsPresetLayerInstance,
                layerDirtyState);
            QueueLinkedLayerDirty(layerDirtyState);

            layerPreset.BeginSuppressingOnValidateNotifications();
            bool appliedPresetProperties;
            try {
                appliedPresetProperties = linkedPresetObject.ApplyModifiedProperties();
            } finally {
                layerPreset.EndSuppressingOnValidateNotifications();
            }

            if (appliedPresetProperties) {
                EditorUtility.SetDirty(layerPreset);
            }

            FlushPendingPresetDirty();
        }

        private void EnsureLinkedPresetCache(TextMeshProLayerPreset layerPreset)
        {
            if (linkedPreset == layerPreset && linkedPresetObject != null && linkedPresetLayers != null) {
                return;
            }

            linkedPreset = layerPreset;
            linkedPresetObject = new SerializedObject(layerPreset);
            linkedPresetLayers = linkedPresetObject.FindProperty("layers");
            linkedLayerList = null;
            linkedPresetLayerCount = -1;
        }

        private void EnsureLinkedLayerList()
        {
            if (linkedLayerList != null && linkedPresetLayerCount == linkedPresetLayers.arraySize) {
                return;
            }

            linkedPresetLayerCount = linkedPresetLayers.arraySize;
            linkedLayerList = TextMeshProLayerInspectorGUI.CreateLayerList(
                linkedPresetLayers,
                MarkPresetAndStackCompositionDirty,
                false,
                false,
                index => GetLinkedRowLayer(linkedPresetLayers, index),
                HandleLinkedLayerRowChanged,
                DrawLayerModeControl);
        }

        private void ClearLinkedPresetCache()
        {
            linkedPreset = null;
            linkedPresetObject = null;
            linkedPresetLayers = null;
            linkedLayerList = null;
            linkedPresetLayerCount = -1;
        }

        private SerializedProperty GetLinkedRowLayer(SerializedProperty presetLayers, int index)
        {
            if (index < 0 || index >= presetLayers.arraySize) {
                return null;
            }

            if (index >= presetLayerOverrides.arraySize) {
                return presetLayers.GetArrayElementAtIndex(index);
            }

            var layerOverride = presetLayerOverrides.GetArrayElementAtIndex(index);
            var overrideEnabled = layerOverride.FindPropertyRelative("overrideLayer");
            return overrideEnabled.boolValue ? layerOverride.FindPropertyRelative("layer") : presetLayers.GetArrayElementAtIndex(index);
        }

        private void HandleLinkedLayerRowChanged(int index, SerializedProperty layer)
        {
            if (index < 0 || index >= presetLayerOverrides.arraySize) {
                return;
            }

            var layerOverride = presetLayerOverrides.GetArrayElementAtIndex(index);
            var overrideEnabled = layerOverride.FindPropertyRelative("overrideLayer");
            if (overrideEnabled.boolValue) {
                MarkLayerCompositionDirty();
            } else {
                MarkPresetAndStackCompositionDirty();
            }
        }

        private void DrawLayerModeControl(Rect rect, int index)
        {
            if (index < 0 || index >= presetLayerOverrides.arraySize) {
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
            if (currentInstance && sharedSelectedAfterDraw) {
                return false;
            }

            if (!currentInstance && instanceSelectedAfterDraw) {
                return true;
            }

            return currentInstance;
        }

        private static bool DrawModeSegment(Rect rect, GUIContent content, bool selected, bool left)
        {
            var style = left ? EditorStyles.miniButtonLeft : EditorStyles.miniButtonRight;
            return GUI.Toggle(rect, selected, content, style);
        }

        private void SetInstanceMode(int index, bool instance)
        {
            var overrideEnabled = presetLayerOverrides.GetArrayElementAtIndex(index).FindPropertyRelative("overrideLayer");
            if (overrideEnabled.boolValue == instance) {
                return;
            }

            serializedObject.ApplyModifiedProperties();

            var stack = (TextMeshProLayerStack)target;
            stack.SetPresetLayerInstance(index, instance);
            serializedObject.Update();
            MarkLayerCompositionDirty();
        }

        private void DrawPresetFontMismatch(TextMeshProLayerPreset layerPreset)
        {
            var stack = (TextMeshProLayerStack)target;
            if (!stack.TryGetComponent(out TextMeshProUGUI text) || !TextMeshProLayerPresetUtility.HasFontMismatch(layerPreset, text)) {
                return;
            }

            EditorGUILayout.HelpBox(
                "The assigned layer preset is associated with a different TMP font asset than this TextMeshPro component.",
                MessageType.Warning);
            if (GUILayout.Button(Styles.ApplyPresetFont, EditorStyles.miniButton)) {
                serializedObject.ApplyModifiedProperties();
                TextMeshProLayerPresetUtility.ApplyPresetFont(layerPreset, text, stack);
                serializedObject.Update();
                Repaint();
            }
        }

        private void EnsureOverrideArraySize(int size)
        {
            if (presetLayerOverrides.arraySize == size) {
                return;
            }

            presetLayerOverrides.arraySize = size;
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPresetField()
        {
            var rect = EditorGUILayout.GetControlRect();
            var assignedPreset = preset.objectReferenceValue != null;
            var actionWidth = assignedPreset
                ? Styles.PresetActionButtonWidth * 2f + Styles.PresetFieldGap
                : Styles.PresetActionButtonWidth;
            var buttonRect = new Rect(rect.xMax - Styles.PresetActionButtonWidth, rect.y, Styles.PresetActionButtonWidth, rect.height);
            var fieldRect = new Rect(rect.x, rect.y, Mathf.Max(0f, rect.width - actionWidth - Styles.PresetFieldGap), rect.height);

            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(fieldRect, preset, Styles.LayerPreset);
            if (EditorGUI.EndChangeCheck()) {
                MarkLayerCompositionDirty();
            }

            if (assignedPreset) {
                var cloneRect = new Rect(buttonRect.x - Styles.PresetFieldGap - Styles.PresetActionButtonWidth, rect.y, Styles.PresetActionButtonWidth, rect.height);
                if (GUI.Button(cloneRect, Styles.CloneLayerPreset, EditorStyles.miniButtonLeft)) {
                    ClonePreset();
                }

                if (GUI.Button(buttonRect, Styles.ClearLayerPreset, EditorStyles.miniButtonRight)) {
                    ClearPreset();
                }
            } else {
                using (new EditorGUI.DisabledScope(localLayers.arraySize == 0)) {
                    if (GUI.Button(buttonRect, Styles.SaveLayerPreset, EditorStyles.miniButton)) {
                        SaveLocalLayersAsPreset();
                    }
                }
            }
        }

        private void SaveLocalLayersAsPreset()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Save TextMeshPro Layer Preset",
                target.name + " Layer Preset",
                "asset",
                "Choose where to save the TextMeshPro layer preset.");
            if (string.IsNullOrEmpty(path)) {
                return;
            }

            var layerPreset = TextMeshProLayerPresetUtility.CreateFromLocalLayers((TextMeshProLayerStack)target, path);
            if (layerPreset == null) {
                return;
            }

            preset.objectReferenceValue = layerPreset;
            ClearLinkedPresetCache();
            MarkLayerCompositionDirty();
        }

        private void ClonePreset()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Clone TextMeshPro Layer Preset",
                target.name + " Layer Preset",
                "asset",
                "Choose where to save the cloned TextMeshPro layer preset.");
            if (string.IsNullOrEmpty(path)) {
                return;
            }

            var stack = (TextMeshProLayerStack)target;
            var layerPreset = TextMeshProLayerPresetUtility.DuplicateEffectivePreset(stack, path);
            if (layerPreset == null) {
                return;
            }

            preset.objectReferenceValue = layerPreset;
            ClearLinkedPresetCache();
            serializedObject.Update();
            MarkLayerCompositionDirty();
        }

        private void ClearPreset()
        {
            preset.objectReferenceValue = null;
            ClearLinkedPresetCache();
            MarkLayerCompositionDirty();
        }

        private bool IsPresetLayerInstance(int index)
        {
            return ((TextMeshProLayerStack)target).IsPresetLayerInstance(index);
        }

        private void MarkPresetAndStackCompositionDirty()
        {
            MarkPresetDirty(TextMeshProLayerStack.CompositionDirtyFlags);
            MarkLayerCompositionDirty();
        }

        private void QueueLocalLayerDirty(TextMeshProLayerInspectorGUI.LayerInspectorDirtyState dirtyState)
        {
            if (dirtyState == null) {
                return;
            }

            var layerDirties = dirtyState.LayerDirties;
            for (int i = 0; i < layerDirties.Count; i++) {
                QueueLayerDirty(layerDirties[i].Flags, layerDirties[i].LayerIndex);
            }
        }

        private void QueueLinkedLayerDirty(TextMeshProLayerInspectorGUI.LayerInspectorDirtyState dirtyState)
        {
            if (dirtyState == null) {
                return;
            }

            var layerDirties = dirtyState.LayerDirties;
            for (int i = 0; i < layerDirties.Count; i++) {
                var dirty = layerDirties[i];
                if (IsPresetLayerInstance(dirty.LayerIndex)) {
                    QueueLayerDirty(dirty.Flags, dirty.LayerIndex);
                } else {
                    QueuePresetDirty(dirty.Flags, dirty.LayerIndex);
                }
            }
        }

        private void MarkLayerCompositionDirty()
        {
            MarkLayerDirty(TextMeshProLayerStack.CompositionDirtyFlags);
        }

        private void MarkLayerDirty(TextMeshProLayerStack.DirtyFlags flags)
        {
            if (flags == TextMeshProLayerStack.DirtyFlags.None) {
                return;
            }

            var layerStack = (TextMeshProLayerStack)target;
            layerStack.SetLayerStackDirty(flags);
            EditorUtility.SetDirty(layerStack);
        }

        private void MarkLayerMaterialDirty(int layerIndex)
        {
            var layerStack = (TextMeshProLayerStack)target;
            layerStack.SetLayerMaterialChanged(layerIndex);
            EditorUtility.SetDirty(layerStack);
        }

        private void QueueLayerDirty(TextMeshProLayerStack.DirtyFlags flags)
        {
            pendingLayerDirtyFlags |= flags;
        }

        private void QueueLayerDirty(TextMeshProLayerStack.DirtyFlags flags, int layerIndex)
        {
            if (flags == TextMeshProLayerStack.MaterialDirtyFlags) {
                QueueLayerMaterialDirty(layerIndex);
                return;
            }

            QueueLayerDirty(flags);
        }

        private void QueueLayerMaterialDirty(int layerIndex)
        {
            if (layerIndex < 0) {
                QueueLayerDirty(TextMeshProLayerStack.MaterialDirtyFlags);
                return;
            }

            if (!pendingLayerMaterialDirties.Contains(layerIndex)) {
                pendingLayerMaterialDirties.Add(layerIndex);
            }
        }

        private void QueuePresetDirty(TextMeshProLayerStack.DirtyFlags flags)
        {
            pendingPresetDirtyFlags |= flags;
        }

        private void QueuePresetDirty(TextMeshProLayerStack.DirtyFlags flags, int layerIndex)
        {
            if (flags == TextMeshProLayerStack.DirtyFlags.None || layerIndex < 0) {
                QueuePresetDirty(flags);
                return;
            }

            for (int i = 0; i < pendingPresetLayerDirties.Count; i++) {
                var dirty = pendingPresetLayerDirties[i];
                if (dirty.Flags == flags && dirty.LayerIndex == layerIndex) {
                    return;
                }
            }

            pendingPresetLayerDirties.Add(new PendingPresetDirty(flags, layerIndex));
        }

        private void FlushPendingLayerDirty()
        {
            var flags = pendingLayerDirtyFlags;
            pendingLayerDirtyFlags = TextMeshProLayerStack.DirtyFlags.None;
            if ((flags & TextMeshProLayerStack.DirtyFlags.Layers) != 0) {
                pendingLayerMaterialDirties.Clear();
            }

            MarkLayerDirty(flags);

            for (int i = 0; i < pendingLayerMaterialDirties.Count; i++) {
                MarkLayerMaterialDirty(pendingLayerMaterialDirties[i]);
            }

            pendingLayerMaterialDirties.Clear();
        }

        private void FlushPendingPresetDirty()
        {
            var flags = pendingPresetDirtyFlags;
            pendingPresetDirtyFlags = TextMeshProLayerStack.DirtyFlags.None;
            if ((flags & TextMeshProLayerStack.DirtyFlags.Layers) != 0) {
                pendingPresetLayerDirties.Clear();
            }

            MarkPresetDirty(flags);

            for (int i = 0; i < pendingPresetLayerDirties.Count; i++) {
                var dirty = pendingPresetLayerDirties[i];
                MarkPresetDirty(dirty.Flags, dirty.LayerIndex);
            }

            pendingPresetLayerDirties.Clear();
        }

        private void MarkPresetDirty(TextMeshProLayerStack.DirtyFlags flags)
        {
            MarkPresetDirty(flags, -1);
        }

        private void MarkPresetDirty(TextMeshProLayerStack.DirtyFlags flags, int layerIndex)
        {
            if (flags == TextMeshProLayerStack.DirtyFlags.None || preset.objectReferenceValue is not TextMeshProLayerPreset layerPreset) {
                return;
            }

            EditorUtility.SetDirty(layerPreset);
            layerPreset.NotifyChanged(flags, layerIndex);
        }

        private float GetAvailablePadding()
        {
            if (!((TextMeshProLayerStack)target).TryGetComponent(out TextMeshProUGUI text)) {
                return CanvasEditorGUI.Styles.DefaultSdfSliderPadding;
            }

            return TextMeshProUtility.CalculateAvailablePadding(text);
        }

        private string GetLayerListContextKey(string scope)
        {
            return "TextMeshProLayerStack." + target.GetInstanceID() + "." + scope;
        }

    }
}
