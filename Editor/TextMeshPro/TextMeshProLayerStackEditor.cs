using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using TMPro;

namespace Tripledot.CanvasKit.Editor
{
    [CustomEditor(typeof(TextMeshProLayerStack))]
    internal sealed class TextMeshProLayerStackEditor : UnityEditor.Editor
    {
        private static class Content
        {
            public static readonly GUIContent LayerPreset = L10n.TextContent("Layer Preset", "Use a shared TextMeshPro layer preset instead of local layers.");
            public static readonly GUIContent SharedMode = L10n.TextContent("Shared", "Use the shared preset layer. Editing this row changes the preset asset.");
            public static readonly GUIContent InstanceMode = L10n.TextContent("Instance", "Use a layer copy on this TextMeshPro object for animation or object-specific edits.");
            public static readonly GUIContent SaveLayerPreset = L10n.TextContent("Save", "Save local layers as a TextMeshPro layer preset.");
            public static readonly GUIContent CloneLayerPreset = L10n.TextContent("Clone", "Copy the current effective layers into a new preset asset and assign it to this stack.");
            public static readonly GUIContent ClearLayerPreset = L10n.TextContent("Clear", "Stop using the assigned preset and show this stack's local layers.");
            public static readonly GUIContent ApplyPresetFont = L10n.TextContent("Apply Font", "Assign the preset font asset to this TextMeshPro component.");
        }

        private const float PresetActionButtonWidth = 52f;
        private const float PresetFieldGap = 4f;

        private SerializedProperty preset;
        private SerializedProperty localLayers;
        private SerializedProperty presetLayerOverrides;
        private ReorderableList localLayerList;

        private void OnEnable()
        {
            preset = serializedObject.FindProperty("preset");
            localLayers = serializedObject.FindProperty("localLayers");
            presetLayerOverrides = serializedObject.FindProperty("presetLayerOverrides");
            localLayerList = TextMeshProLayerInspectorGUI.CreateLayerList(localLayers, MarkStackDirty, true);
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
                TextMeshProLayerInspectorGUI.DoLayerList(localLayerList);
                TextMeshProLayerInspectorGUI.DrawLayerInspectorBlocks(
                    localLayers,
                    MarkStackDirty,
                    contextKey: GetLayerListContextKey("Local"),
                    availablePadding: GetAvailablePadding(),
                    sceneTarget: target);
            }

            if (serializedObject.ApplyModifiedProperties()) {
                MarkStackDirty();
            }
        }

        public void OnSceneGUI()
        {
            if (CanvasGradientSceneView.Draw(target)) {
                MarkStackDirty();
                Repaint();
            }
        }

        private void DrawPresetLinkedLayers(TextMeshProLayerPreset layerPreset)
        {
            var presetObject = new SerializedObject(layerPreset);
            presetObject.Update();
            var presetLayers = presetObject.FindProperty("layers");
            EnsureOverrideArraySize(presetLayers.arraySize);

            var linkedLayerList = TextMeshProLayerInspectorGUI.CreateLayerList(
                presetLayers,
                MarkPresetAndStackDirty,
                false,
                false,
                index => GetLinkedRowLayer(presetLayers, index),
                HandleLinkedLayerRowChanged,
                DrawLayerModeControl);
            TextMeshProLayerInspectorGUI.DoLayerList(linkedLayerList);
            TextMeshProLayerInspectorGUI.DrawLayerInspectorBlocks(
                presetLayers,
                MarkPresetAndStackDirty,
                index => GetLinkedRowLayer(presetLayers, index),
                HandleLinkedLayerRowChanged,
                GetLayerListContextKey("Linked." + layerPreset.GetInstanceID()),
                GetAvailablePadding(),
                target,
                IsPresetLayerInstance);

            if (presetObject.ApplyModifiedProperties()) {
                EditorUtility.SetDirty(layerPreset);
                layerPreset.NotifyChanged();
                MarkStackDirty();
            }
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
                MarkStackDirty();
            } else {
                MarkPresetAndStackDirty();
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

            var sharedSelectedAfterDraw = DrawModeSegment(presetRect, Content.SharedMode, !instance, true);
            var instanceSelectedAfterDraw = DrawModeSegment(instanceRect, Content.InstanceMode, instance, false);
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
            MarkStackDirty();
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
            if (GUILayout.Button(Content.ApplyPresetFont, EditorStyles.miniButton)) {
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
                ? PresetActionButtonWidth * 2f + PresetFieldGap
                : PresetActionButtonWidth;
            var buttonRect = new Rect(rect.xMax - PresetActionButtonWidth, rect.y, PresetActionButtonWidth, rect.height);
            var fieldRect = new Rect(rect.x, rect.y, Mathf.Max(0f, rect.width - actionWidth - PresetFieldGap), rect.height);

            EditorGUI.PropertyField(fieldRect, preset, Content.LayerPreset);

            if (assignedPreset) {
                var cloneRect = new Rect(buttonRect.x - PresetFieldGap - PresetActionButtonWidth, rect.y, PresetActionButtonWidth, rect.height);
                if (GUI.Button(cloneRect, Content.CloneLayerPreset, EditorStyles.miniButtonLeft)) {
                    ClonePreset();
                }

                if (GUI.Button(buttonRect, Content.ClearLayerPreset, EditorStyles.miniButtonRight)) {
                    ClearPreset();
                }
            } else {
                using (new EditorGUI.DisabledScope(localLayers.arraySize == 0)) {
                    if (GUI.Button(buttonRect, Content.SaveLayerPreset, EditorStyles.miniButton)) {
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
            MarkStackDirty();
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
            serializedObject.Update();
            MarkStackDirty();
        }

        private void ClearPreset()
        {
            preset.objectReferenceValue = null;
            MarkStackDirty();
        }

        private bool IsPresetLayerInstance(int index)
        {
            return ((TextMeshProLayerStack)target).IsPresetLayerInstance(index);
        }

        private void MarkStackDirty()
        {
            var layerStack = (TextMeshProLayerStack)target;
            layerStack.SetLayerStackDirty();
            EditorUtility.SetDirty(layerStack);
        }

        private void MarkPresetAndStackDirty()
        {
            if (preset.objectReferenceValue is TextMeshProLayerPreset layerPreset) {
                EditorUtility.SetDirty(layerPreset);
                layerPreset.NotifyChanged();
            }

            MarkStackDirty();
        }

        private float GetAvailablePadding()
        {
            if (!((TextMeshProLayerStack)target).TryGetComponent(out TextMeshProUGUI text)) {
                return TextMeshProUtility.DefaultEditorSliderPadding;
            }

            var sourceMaterial = text.fontSharedMaterial != null ? text.fontSharedMaterial : text.materialForRendering;
            return TextMeshProUtility.CalculateAvailablePadding(text, sourceMaterial);
        }

        private string GetLayerListContextKey(string scope)
        {
            return "TextMeshProLayerStack." + target.GetInstanceID() + "." + scope;
        }

    }
}
