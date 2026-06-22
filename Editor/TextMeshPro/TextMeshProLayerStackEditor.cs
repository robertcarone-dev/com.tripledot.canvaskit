using UnityEditor;
using UnityEngine;
using TMPro;

namespace Tripledot.CanvasKit.Editor
{
    [CustomEditor(typeof(TextMeshProLayerStack))]
    internal sealed class TextMeshProLayerStackEditor : UnityEditor.Editor
    {
        private static class Styles
        {
            public static readonly GUIContent LayerPreset = L10n.TextContent("Layer Preset", "Use a shared TextMeshPro layer preset instead of local layers.");
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
        private TextMeshProLayerInspector layerInspector;

        private TextMeshProLayerStack Stack => (TextMeshProLayerStack)target;

        private void OnEnable()
        {
            preset = serializedObject.FindProperty("preset");
            localLayers = serializedObject.FindProperty("localLayers");
            presetLayerOverrides = serializedObject.FindProperty("presetLayerOverrides");
            layerInspector = new TextMeshProLayerInspector(Stack, serializedObject, preset, localLayers, presetLayerOverrides);
        }

        private void OnDisable()
        {
            layerInspector?.ClearLinkedPresetCache();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPresetField();
            if (preset.objectReferenceValue is TextMeshProLayerPreset assignedPreset) {
                DrawPresetFontMismatch(assignedPreset);
            }
            EditorGUILayout.Space();

            if (preset.objectReferenceValue == null) {
                layerInspector.ClearLinkedPresetCache();
            }
            layerInspector.Draw();

            var appliedStackProperties = serializedObject.ApplyModifiedProperties();
            if (appliedStackProperties) {
                EditorUtility.SetDirty(target);
            }
        }

        public void OnSceneGUI()
        {
            var result = CanvasGradientSceneView.Draw(target);
            if (result.Changed) {
                Repaint();
            }
        }

        private void DrawPresetFontMismatch(TextMeshProLayerPreset layerPreset)
        {
            if (!Stack.TryGetComponent(out TextMeshProUGUI text) || !TextMeshProLayerPresetUtility.HasFontMismatch(layerPreset, text)) {
                return;
            }

            EditorGUILayout.HelpBox(
                "The assigned layer preset is associated with a different TMP font asset than this TextMeshPro component.",
                MessageType.Warning);

            if (GUILayout.Button(Styles.ApplyPresetFont, EditorStyles.miniButton)) {
                serializedObject.ApplyModifiedProperties();
                TextMeshProLayerPresetUtility.ApplyPresetFont(layerPreset, text, Stack);
                serializedObject.Update();
                Repaint();
            }
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

            EditorGUI.PropertyField(fieldRect, preset, Styles.LayerPreset);

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
            var path = EditorUtility.SaveFilePanelInProject("Save TextMeshPro Layer Preset", target.name + " Layer Preset", "asset", "Choose where to save the TextMeshPro layer preset.");
            if (string.IsNullOrEmpty(path)) {
                return;
            }

            var layerPreset = TextMeshProLayerPresetUtility.CreateFromLocalLayers(Stack, path);
            if (layerPreset == null) {
                return;
            }

            preset.objectReferenceValue = layerPreset;
            layerInspector.ClearLinkedPresetCache();
        }

        private void ClonePreset()
        {
            var path = EditorUtility.SaveFilePanelInProject("Clone TextMeshPro Layer Preset", target.name + " Layer Preset", "asset", "Choose where to save the cloned TextMeshPro layer preset.");
            if (string.IsNullOrEmpty(path)) {
                return;
            }

            var layerPreset = TextMeshProLayerPresetUtility.DuplicateEffectivePreset(Stack, path);
            if (layerPreset == null) {
                return;
            }

            preset.objectReferenceValue = layerPreset;
            layerInspector.ClearLinkedPresetCache();
            serializedObject.Update();
        }

        private void ClearPreset()
        {
            preset.objectReferenceValue = null;
            layerInspector.ClearLinkedPresetCache();
        }
    }
}