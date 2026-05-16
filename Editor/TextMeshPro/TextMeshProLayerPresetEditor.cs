using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
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

        private readonly struct PendingLayerDirty
        {
            public readonly TextMeshProLayerStack.DirtyFlags Flags;
            public readonly int LayerIndex;

            public PendingLayerDirty(TextMeshProLayerStack.DirtyFlags flags, int layerIndex)
            {
                Flags = flags;
                LayerIndex = layerIndex;
            }
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
        private ReorderableList layerList;
        private Texture2D previewTexture;
        private int previewWidth;
        private int previewHeight;
        private int previewVersion;
        private PendingDirtyScopes pendingPreviewDirtyScopes;
        private TextMeshProLayerStack.DirtyFlags pendingDirtyFlags;
        private readonly List<PendingLayerDirty> pendingLayerDirties = new List<PendingLayerDirty>();
        private readonly TextMeshProLayerInspectorGUI.LayerInspectorDirtyState layerDirtyState = new TextMeshProLayerInspectorGUI.LayerInspectorDirtyState();

        private void OnEnable()
        {
            fontAsset = serializedObject.FindProperty("fontAsset");
            previewText = serializedObject.FindProperty("previewText");
            layers = serializedObject.FindProperty("layers");
            layerList = TextMeshProLayerInspectorGUI.CreateLayerList(layers, MarkPresetCompositionDirty, true);
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
                QueuePresetDirty(TextMeshProLayerStack.MaterialDirtyFlags);
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
            TextMeshProLayerInspectorGUI.DoLayerList(layerList);
            
            layerDirtyState.Clear();
            TextMeshProLayerInspectorGUI.DrawLayerInspectorBlocks(
                layers, MarkPresetCompositionDirty, contextKey: "TextMeshProLayerPreset." + target.GetInstanceID(), dirtyState: layerDirtyState);
            QueuePresetDirty(layerDirtyState);
            
            var preset = (TextMeshProLayerPreset)target;
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

            FlushPendingDirty();
        }

        public override bool HasPreviewGUI()
        {
            return TextMeshProLayerPresetPreviewRenderer.CanPreview((TextMeshProLayerPreset)target);
        }

        public override void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            EnsurePreviewTexture((TextMeshProLayerPreset)target, Mathf.CeilToInt(rect.width), Mathf.CeilToInt(rect.height));
            TextMeshProLayerPresetPreviewRenderer.DrawPreview(rect, previewTexture, background);
        }

        public override Texture2D RenderStaticPreview(string assetPath, UnityEngine.Object[] subAssets, int width, int height)
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

        private void MarkPresetCompositionDirty()
        {
            MarkPresetDirty(TextMeshProLayerStack.CompositionDirtyFlags);
        }

        private void QueuePresetDirty(TextMeshProLayerInspectorGUI.LayerInspectorDirtyState dirtyState)
        {
            if (dirtyState == null) {
                return;
            }

            var layerDirties = dirtyState.LayerDirties;
            for (int i = 0; i < layerDirties.Count; i++) {
                QueuePresetDirty(layerDirties[i].Flags, layerDirties[i].LayerIndex);
            }
        }

        private void QueuePresetDirty(TextMeshProLayerStack.DirtyFlags flags)
        {
            pendingDirtyFlags |= flags;
        }

        private void QueuePresetDirty(TextMeshProLayerStack.DirtyFlags flags, int layerIndex)
        {
            if (flags == TextMeshProLayerStack.DirtyFlags.None || layerIndex < 0) {
                QueuePresetDirty(flags);
                return;
            }

            for (int i = 0; i < pendingLayerDirties.Count; i++) {
                var dirty = pendingLayerDirties[i];
                if (dirty.Flags == flags && dirty.LayerIndex == layerIndex) {
                    return;
                }
            }

            pendingLayerDirties.Add(new PendingLayerDirty(flags, layerIndex));
        }

        private void FlushPendingDirty()
        {
            var flags = pendingDirtyFlags;
            var previewScopes = pendingPreviewDirtyScopes;
            pendingDirtyFlags = TextMeshProLayerStack.DirtyFlags.None;
            pendingPreviewDirtyScopes = PendingDirtyScopes.None;

            if ((flags & TextMeshProLayerStack.DirtyFlags.Layers) != 0) {
                MarkPresetDirty(flags);
                pendingLayerDirties.Clear();
                return;
            }

            if (flags != TextMeshProLayerStack.DirtyFlags.None) {
                MarkPresetDirty(flags);
            }

            if ((previewScopes & PendingDirtyScopes.Preview) != 0) {
                ReleasePreviewTexture();
                Repaint();
            }

            for (int i = 0; i < pendingLayerDirties.Count; i++) {
                var dirty = pendingLayerDirties[i];
                MarkPresetDirty(dirty.Flags, dirty.LayerIndex);
            }

            pendingLayerDirties.Clear();
        }

        private void MarkPresetDirty(TextMeshProLayerStack.DirtyFlags flags, int layerIndex = -1)
        {
            if (flags == TextMeshProLayerStack.DirtyFlags.None) {
                return;
            }

            var preset = (TextMeshProLayerPreset)target;
            EditorUtility.SetDirty(preset);
            preset.NotifyChanged(flags, layerIndex);
        }
    }
}
