using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    [CustomEditor(typeof(TextMeshProLayerPreset))]
    internal sealed class TextMeshProLayerPresetEditor : UnityEditor.Editor
    {
        private static class Content
        {
            public static readonly GUIContent FontAsset = L10n.TextContent("Font Asset", "TMP font asset this layer preset was authored for.");
            public static readonly GUIContent PreviewText = L10n.TextContent("Preview Text", "Optional text shown in the preset preview.");
            public static readonly GUIContent MissingPreviewFont = L10n.TextContent("Assign a TMP font asset to enable the preset preview.");
        }

        private SerializedProperty fontAsset;
        private SerializedProperty previewText;
        private SerializedProperty layers;
        private ReorderableList layerList;
        private Texture2D previewTexture;
        private int previewWidth;
        private int previewHeight;
        private int previewVersion;

        private void OnEnable()
        {
            fontAsset = serializedObject.FindProperty("fontAsset");
            previewText = serializedObject.FindProperty("previewText");
            layers = serializedObject.FindProperty("layers");
            layerList = TextMeshProLayerInspectorGUI.CreateLayerList(layers, MarkPresetDirty, true);
        }

        private void OnDisable()
        {
            ReleasePreviewTexture();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(fontAsset, Content.FontAsset);
            EditorGUILayout.PropertyField(previewText, Content.PreviewText);
            if (fontAsset.objectReferenceValue == null) {
                EditorGUILayout.HelpBox(Content.MissingPreviewFont.text, MessageType.Info);
            }
            EditorGUILayout.Space();
            TextMeshProLayerInspectorGUI.DoLayerList(layerList);
            TextMeshProLayerInspectorGUI.DrawLayerInspectorBlocks(
                layers,
                MarkPresetDirty,
                contextKey: "TextMeshProLayerPreset." + target.GetInstanceID());
            if (serializedObject.ApplyModifiedProperties()) {
                MarkPresetDirty();
            }
        }

        public override bool HasPreviewGUI()
        {
            return TextMeshProLayerPresetPreviewRenderer.CanPreview((TextMeshProLayerPreset)target);
        }

        public override void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            var preset = (TextMeshProLayerPreset)target;
            EnsurePreviewTexture(preset, Mathf.CeilToInt(rect.width), Mathf.CeilToInt(rect.height));
            TextMeshProLayerPresetPreviewRenderer.DrawPreview(rect, previewTexture, background);
        }

        public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
        {
            return TextMeshProLayerPresetPreviewRenderer.RenderPreviewTexture((TextMeshProLayerPreset)target, width, height);
        }

        public override string GetInfoString()
        {
            var preset = (TextMeshProLayerPreset)target;
            if (preset == null || preset.FontAsset == null) {
                return "Assign a TMP font asset to enable preview.";
            }

            if (preset.LayerCount == 0) {
                return "Add at least one layer to enable preview.";
            }

            return preset.GetPreviewText() + " - " + preset.FontAsset.name;
        }

        private void EnsurePreviewTexture(TextMeshProLayerPreset preset, int width, int height)
        {
            if (Event.current.type != EventType.Repaint) {
                return;
            }

            if (!TextMeshProLayerPresetPreviewRenderer.CanPreview(preset) || width <= 0 || height <= 0) {
                ReleasePreviewTexture();
                return;
            }

            if (previewTexture != null && previewWidth == width && previewHeight == height && previewVersion == preset.Version) {
                return;
            }

            ReleasePreviewTexture();
            previewTexture = TextMeshProLayerPresetPreviewRenderer.RenderPreviewTexture(preset, width, height);
            previewWidth = width;
            previewHeight = height;
            previewVersion = preset.Version;
        }

        private void ReleasePreviewTexture()
        {
            if (previewTexture != null) {
                DestroyImmediate(previewTexture);
            }

            previewTexture = null;
            previewWidth = 0;
            previewHeight = 0;
            previewVersion = 0;
        }

        private void MarkPresetDirty()
        {
            var preset = (TextMeshProLayerPreset)target;
            EditorUtility.SetDirty(preset);
            preset.NotifyChanged();
        }
    }
}
