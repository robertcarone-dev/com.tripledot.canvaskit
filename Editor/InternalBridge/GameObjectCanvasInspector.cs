using Tripledot.CanvasKit.Editor;
using UnityEditor;
using UnityEngine;

namespace Tripledot.CanvasKit.InternalEditorBridge
{
    [CustomEditor(typeof(GameObject))]
    [CanEditMultipleObjects]
    internal class GameObjectCanvasInspector : GameObjectInspector
    {
        private readonly CanvasPreviewCache canvasPreviewCache = new CanvasPreviewCache();
        private Object[] cachedTargets;
        private Object[] singleTargetArray;
        private Object cachedSingleTarget;
        private int selectedSizeIndex = CanvasPreviewSize.DefaultIndex;
        private bool selectedSizeIndexInitialized;

        public override void ReloadPreviewInstances()
        {
            base.ReloadPreviewInstances();
            canvasPreviewCache.ReleasePreviewTexture();
        }

        public override bool HasPreviewGUI()
        {
            return CanvasInspectorPreview.HasPreviewGUI(target, RefreshCachedTargets(), false) || base.HasPreviewGUI();
        }

        public override GUIContent GetPreviewTitle()
        {
            return CanvasInspectorPreview.TryGetPreviewTarget(target, RefreshCachedTargets(), out _)
                ? CanvasInspectorPreview.GetPreviewTitle(target, GetCachedTargets(), GUIContent.none)
                : base.GetPreviewTitle();
        }

        public override string GetInfoString()
        {
            return CanvasInspectorPreview.TryGetPreviewTarget(target, RefreshCachedTargets(), out _)
                ? CanvasInspectorPreview.GetInfoString(target, GetCachedTargets(), GetSelectedSizeIndex(), string.Empty)
                : base.GetInfoString();
        }

        public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
        {
            var preview = CanvasInspectorPreview.RenderStaticPreview(target, assetPath, subAssets, width, height);
            return preview != null ? preview : base.RenderStaticPreview(assetPath, subAssets, width, height);
        }

        public override void OnPreviewSettings()
        {
            var currentTargets = RefreshCachedTargets();
            if (CanvasInspectorPreview.CanPreview(currentTargets)) {
                EnsureSelectedSizeIndexInitialized();
                CanvasInspectorPreview.OnPreviewSettings(target, currentTargets, ref selectedSizeIndex, canvasPreviewCache.ReleasePreviewTexture);
                return;
            }

            base.OnPreviewSettings();
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            if (CanvasInspectorPreview.OnPreviewGUI(target, GetCachedTargets(), r, background, canvasPreviewCache, GetSelectedSizeIndex())) {
                return;
            }

            base.OnPreviewGUI(r, background);
        }

        protected new void OnDisable()
        {
            base.OnDisable();
            cachedTargets = null;
            singleTargetArray = null;
            cachedSingleTarget = null;
            selectedSizeIndexInitialized = false;
            canvasPreviewCache.ReleasePreviewTexture();
        }

        private int GetSelectedSizeIndex()
        {
            EnsureSelectedSizeIndexInitialized();
            return selectedSizeIndex;
        }

        private void EnsureSelectedSizeIndexInitialized()
        {
            if (selectedSizeIndexInitialized) {
                return;
            }

            selectedSizeIndex = CanvasPreviewSettings.SelectedReferenceSizeIndex;
            selectedSizeIndexInitialized = true;
        }

        private Object[] RefreshCachedTargets()
        {
            var currentTargets = targets;
            if (currentTargets is not { Length: > 0 }) {
                cachedTargets = GetSingleTargetArray();
                return cachedTargets;
            }

            if (!TargetsMatch(cachedTargets, currentTargets)) {
                cachedTargets = (Object[])currentTargets.Clone();
            }

            return cachedTargets;
        }

        private Object[] GetCachedTargets()
        {
            return cachedTargets is { Length: > 0 }
                ? cachedTargets
                : GetSingleTargetArray();
        }

        private Object[] GetSingleTargetArray()
        {
            if (target == null) {
                singleTargetArray = null;
                cachedSingleTarget = null;
                return System.Array.Empty<Object>();
            }

            if (singleTargetArray == null || cachedSingleTarget != target) {
                cachedSingleTarget = target;
                singleTargetArray = new Object[] { target };
            }

            return singleTargetArray;
        }

        private static bool TargetsMatch(Object[] cached, Object[] current)
        {
            if (cached == null || current == null || cached.Length != current.Length) {
                return false;
            }

            for (int i = 0; i < current.Length; i++) {
                if (cached[i] != current[i]) {
                    return false;
                }
            }

            return true;
        }
    }
}
