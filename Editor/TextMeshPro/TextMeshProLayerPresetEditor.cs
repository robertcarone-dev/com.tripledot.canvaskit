using System;
using UnityEditor;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    [CustomEditor(typeof(TextMeshProLayerPreset))]
    internal sealed class TextMeshProLayerPresetEditor : UnityEditor.Editor
    {
        [Flags]
        private enum PendingDirtyScopes
        {
            None = 0,
            Preview = 1 << 0,
        }

        private static class Styles
        {
            public static readonly GUIContent FontAsset = L10n.TextContent("Font Asset", "TMP font asset this layer preset was authored for.");
            public static readonly GUIContent PreviewText = L10n.TextContent("Preview Text", "Optional text shown in the preset preview.");
            public static readonly GUIContent MissingPreviewFont = L10n.TextContent("Assign a TMP font asset to enable the preset preview.");
        }

        private SerializedProperty fontAsset;
        private SerializedProperty previewText;
        private SerializedProperty layers;
        private Texture2D previewTexture;
        private int previewWidth;
        private int previewHeight;
        private int previewVersion;
        private PendingDirtyScopes pendingPreviewDirtyScopes;
        private TextMeshProLayerInspector layerInspector;

        private TextMeshProLayerPreset Preset => (TextMeshProLayerPreset)target;
        
        private void OnEnable()
        {
            fontAsset = serializedObject.FindProperty("fontAsset");
            previewText = serializedObject.FindProperty("previewText");
            layers = serializedObject.FindProperty("layers");
            layerInspector = new TextMeshProLayerInspector(Preset, serializedObject, layers);
        }

        private void OnDisable()
        {
            ReleasePreviewTexture();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(fontAsset, Styles.FontAsset);
            if (EditorGUI.EndChangeCheck()) {
                layerInspector.QueuePresetDirty(TextMeshProLayerStack.MaterialDirtyFlags);
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(previewText, Styles.PreviewText);
            if (EditorGUI.EndChangeCheck()) {
                pendingPreviewDirtyScopes |= PendingDirtyScopes.Preview;
            }

            if (fontAsset.objectReferenceValue == null) {
                EditorGUILayout.HelpBox(Styles.MissingPreviewFont.text, MessageType.Info);
            }
            EditorGUILayout.Space();
            layerInspector.Draw();

            var preset = Preset;
            preset.BeginSuppressingOnValidateNotifications();

            bool appliedProperties;
            try {
                appliedProperties = serializedObject.ApplyModifiedProperties();
            } finally {
                preset.EndSuppressingOnValidateNotifications();
            }

            if (appliedProperties) {
                EditorUtility.SetDirty(target);
            }

            FlushPreviewDirty();
            layerInspector.FlushPresetDirties(Preset);
        }

        public override bool HasPreviewGUI()
        {
            return TextMeshProLayerPresetPreviewRenderer.CanPreview(Preset);
        }

        public override void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            EnsurePreviewTexture(Preset, Mathf.CeilToInt(rect.width), Mathf.CeilToInt(rect.height));
            TextMeshProLayerPresetPreviewRenderer.DrawPreview(rect, previewTexture, background);
        }

        public override Texture2D RenderStaticPreview(string assetPath, UnityEngine.Object[] subAssets, int width, int height)
        {
            return TextMeshProLayerPresetPreviewRenderer.RenderPreviewTexture(Preset, width, height);
        }

        public override string GetInfoString()
        {
            if (Preset == null || Preset.FontAsset == null) {
                return "Assign a TMP font asset to enable preview.";
            }

            if (Preset.LayerCount == 0) {
                return "Add at least one layer to enable preview.";
            }

            return Preset.GetPreviewText() + " - " + Preset.FontAsset.name;
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

            if (previewTexture != null && (GUIUtility.hotControl != 0 || EditorGUIUtility.editingTextField)) {
                Repaint();
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

        private void FlushPreviewDirty()
        {
            var previewScopes = pendingPreviewDirtyScopes;
            pendingPreviewDirtyScopes = PendingDirtyScopes.None;

            if ((previewScopes & PendingDirtyScopes.Preview) != 0) {
                ReleasePreviewTexture();
                Repaint();
            }
        }
    }
}
