using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Tripledot.CanvasKit.Editor.CanvasPreview
{
    internal sealed class CanvasPreviewCache
    {
        internal const int MaxPreviewTextureSize = 512;

        private Texture2D previewTexture;
        private int previewWidth;
        private int previewHeight;
        private int previewSizeIndex;
        private Hash128 previewHash;
        private string previewAssetPath;
        private int previewSettingsRevision;
        private CanvasPreviewEnvironmentKey previewEnvironmentKey;

        public Texture2D EnsurePreviewTexture(GameObject prefabAsset, CanvasPreviewSize previewSize, int selectedSizeIndex, int width, int height)
        {
            if (width <= 0 || height <= 0 || prefabAsset == null) {
                ReleasePreviewTexture();
                return null;
            }

            var assetPath = AssetDatabase.GetAssetPath(prefabAsset);
            var assetHash = !string.IsNullOrEmpty(assetPath) ? AssetDatabase.GetAssetDependencyHash(assetPath) : default;
            var settingsRevision = CanvasPreviewSettings.Revision;
            var environmentKey = CanvasPreviewEnvironment.CreateCacheKey();
            var renderSize = GetRenderSize(previewSize);

            if (previewTexture != null
                && previewWidth == renderSize.x
                && previewHeight == renderSize.y
                && previewSizeIndex == selectedSizeIndex
                && previewHash == assetHash
                && previewAssetPath == assetPath
                && previewSettingsRevision == settingsRevision
                && previewEnvironmentKey == environmentKey) {
                return previewTexture;
            }

            ReleasePreviewTexture();
            var result = CanvasPreviewRenderer.RenderPreviewTexture(prefabAsset, previewSize, renderSize.x, renderSize.y);
            if (result.Texture == null) {
                return null;
            }

            previewTexture = result.Texture;
            previewTexture.hideFlags = HideFlags.HideAndDontSave;
            previewWidth = renderSize.x;
            previewHeight = renderSize.y;
            previewSizeIndex = selectedSizeIndex;
            previewHash = assetHash;
            previewAssetPath = assetPath;
            previewSettingsRevision = settingsRevision;
            previewEnvironmentKey = environmentKey;

            return previewTexture;
        }

        public void ReleasePreviewTexture()
        {
            if (previewTexture != null) {
                UnityObject.DestroyImmediate(previewTexture);
            }

            previewTexture = null;
            previewWidth = 0;
            previewHeight = 0;
            previewSizeIndex = CanvasPreviewSize.DefaultIndex;
            previewHash = default;
            previewAssetPath = null;
            previewSettingsRevision = 0;
            previewEnvironmentKey = default;
        }

        private static Vector2Int GetRenderSize(CanvasPreviewSize previewSize)
        {
            var width = Mathf.Max(1, previewSize.Width);
            var height = Mathf.Max(1, previewSize.Height);
            var max = Mathf.Max(1, MaxPreviewTextureSize);
            var scale = Mathf.Min(1f, max / (float)Mathf.Max(width, height));

            return new Vector2Int(
                Mathf.Max(1, Mathf.RoundToInt(width * scale)),
                Mathf.Max(1, Mathf.RoundToInt(height * scale)));
        }
    }
}