using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tripledot.CanvasKit.Editor.CanvasPreview
{
    [CustomEditor(typeof(GameObject))]
    [CanEditMultipleObjects]
    internal class CanvasGameObjectInspector : GameObjectInspector
    {
        private readonly CanvasPreviewCache canvasPreviewCache = new CanvasPreviewCache();
        private int selectedSizeIndex = CanvasPreviewSize.DefaultIndex;
        private bool selectedSizeIndexInitialized;

        protected new void OnDisable()
        {
            base.OnDisable();
            selectedSizeIndexInitialized = false;
            canvasPreviewCache.ReleasePreviewTexture();
        }

        public override void ReloadPreviewInstances()
        {
            base.ReloadPreviewInstances();
            canvasPreviewCache.ReleasePreviewTexture();
        }

        public override bool HasPreviewGUI()
        {
            return CanvasPreviewWindow.HasPreviewGUI(target, targets, false) || base.HasPreviewGUI();
        }

        public override GUIContent GetPreviewTitle()
        {
            return CanvasPreviewWindow.TryGetPreviewTarget(target, targets, out _)
                ? CanvasPreviewWindow.GetPreviewTitle(target, targets, GUIContent.none)
                : base.GetPreviewTitle();
        }

        public override string GetInfoString()
        {
            return CanvasPreviewWindow.TryGetPreviewTarget(target, targets, out _)
                ? CanvasPreviewWindow.GetInfoString(target, targets, GetSelectedSizeIndex(), string.Empty)
                : base.GetInfoString();
        }

        public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
        {
            var preview = CanvasPreviewWindow.RenderStaticPreview(target, width, height);
            return preview != null ? preview : base.RenderStaticPreview(assetPath, subAssets, width, height);
        }

        public override void OnPreviewSettings()
        {
            if (CanvasPreviewWindow.CanPreview(targets)) {
                EnsureSelectedSizeIndexInitialized();
                CanvasPreviewWindow.OnPreviewSettings(target, targets, ref selectedSizeIndex, canvasPreviewCache.ReleasePreviewTexture);
            } else {
                base.OnPreviewSettings();
            }
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            if (CanvasPreviewWindow.OnPreviewGUI(target, targets, r, background, canvasPreviewCache, GetSelectedSizeIndex())) {
                return;
            }

            base.OnPreviewGUI(r, background);
        }

        private int GetSelectedSizeIndex()
        {
            EnsureSelectedSizeIndexInitialized();
            return selectedSizeIndex;
        }

        private void EnsureSelectedSizeIndexInitialized()
        {
            if (!selectedSizeIndexInitialized) {
                selectedSizeIndex = CanvasPreviewSettings.SelectedReferenceSizeIndex;
                selectedSizeIndexInitialized = true;
            }
        }
    }
}