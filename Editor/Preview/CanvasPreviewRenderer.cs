using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tripledot.CanvasKit.Editor
{
    internal static class CanvasPreviewRenderer
    {
        private const int PreviewLayer = 31;
        private const float ElementPadding = 5f;
        private static readonly Color PreviewBackgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        private static readonly Vector2 FallbackElementReferenceSize = new Vector2(100f, 100f);
        private static readonly List<Canvas> CanvasBuffer = new List<Canvas>(8);
        private static readonly List<CanvasScaler> ScalerBuffer = new List<CanvasScaler>(4);
        private static readonly List<Graphic> GraphicBuffer = new List<Graphic>(32);
        private static readonly List<Transform> TransformBuffer = new List<Transform>(64);
        private static readonly Vector3[] CornerBuffer = new Vector3[4];

        internal static PreviewResult RenderPreviewTexture(
            GameObject prefabAsset, CanvasPreviewSize previewSize, int width, int height, bool preserveRequestedOutputSize = false)
        {
            if (prefabAsset == null || width <= 0 || height <= 0) {
                return PreviewResult.Empty;
            }

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) {
                return PreviewResult.Empty;
            }

            var prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
            if (string.IsNullOrEmpty(prefabPath)) {
                return PreviewResult.Empty;
            }

            GameObject prefabRoot = null;
            GameObject canvasObject = null;
            GameObject cameraObject = null;
            Scene previewScene = default;
            try {
                bool usesEnvironmentScene;
                using (new SampleScope("CanvasPreview.Instantiate")) {
                    previewScene = CanvasPreviewEnvironment.OpenPreviewScene(out usesEnvironmentScene);
                    PrefabUtility.LoadPrefabContentsIntoPreviewScene(prefabPath, previewScene, out prefabRoot);
                    if (prefabRoot == null) {
                        return PreviewResult.Empty;
                    }

                    prefabRoot.name = prefabAsset.name + " Preview";
                    prefabRoot.hideFlags = HideFlags.HideAndDontSave;
                }

                if (!CanvasPreviewEligibility.TryGetPreviewTargetInHierarchy(prefabRoot, out var target)) {
                    return PreviewResult.Empty;
                }

                var roleResult = CanvasPreviewRoleResolver.ResolveDetailed(prefabAsset, target);
                var usesPreset = CanvasPreviewRoleResolver.UsesPreset(roleResult.Role);
                var previewRootRect = GetPreviewRootRect(target);
                var referenceSize = GetReferenceSize(roleResult.Role, previewSize, previewRootRect);

                var environmentCanvas = usesEnvironmentScene
                    ? CanvasPreviewEnvironment.SelectEnvironmentCanvas(previewScene.GetRootGameObjects(), prefabRoot.transform)
                    : null;
                if (environmentCanvas != null) {
                    canvasObject = UnityEngine.Object.Instantiate(environmentCanvas.gameObject);
                    canvasObject.name = environmentCanvas.gameObject.name + " Preview";
                    canvasObject.hideFlags = HideFlags.HideAndDontSave;
                } else {
                    canvasObject = CreateReferenceCanvas(referenceSize);
                }

                var canvasRect = canvasObject.GetComponent<RectTransform>();
                ConfigureBaseCanvasRect(canvasRect, referenceSize);
                if (!usesPreset) {
                    ConfigureElementPreviewScalers(canvasObject);
                }

                PreparePreviewRootRect(previewRootRect, canvasRect, referenceSize, roleResult.Role, 
                    preserveRootRectSettings: target.Kind == CanvasPreviewTargetKind.Canvas);

                cameraObject = new GameObject("Canvas Preview Camera") {
                    hideFlags = HideFlags.HideAndDontSave
                };
                
                var camera = cameraObject.AddComponent<Camera>();
                camera.hideFlags = HideFlags.HideAndDontSave;

                using (new SampleScope("CanvasPreview.ConfigureCanvases")) {
                    SetLayerRecursively(canvasObject, PreviewLayer);
                    ConfigureCamera(camera, null, width, height, new Bounds(Vector3.zero, new Vector3(referenceSize.x, referenceSize.y, 0f)));
                    ConfigureCanvases(canvasObject, camera);
                }

                using (new SampleScope("CanvasPreview.Layout")) {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);
                    Canvas.ForceUpdateCanvases();
                }

                Bounds contentBounds;
                using (new SampleScope("CanvasPreview.Bounds")) {
                    contentBounds = CalculateGraphicBounds(canvasRect, previewRootRect);
                }

                if (contentBounds.size.x <= 0f || contentBounds.size.y <= 0f) {
                    return PreviewResult.Empty;
                }

                if (!usesPreset) {
                    var elementRenderSize = GetElementRenderSize(contentBounds);
                    referenceSize = elementRenderSize;
                    canvasRect.sizeDelta = referenceSize;
                    CenterPreviewRootRect(previewRootRect, contentBounds);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);
                    Canvas.ForceUpdateCanvases();
                    contentBounds = CalculateGraphicBounds(canvasRect, previewRootRect);
                }

                var frameBounds = ExpandBounds(contentBounds, ElementPadding);
                var outputSize = usesPreset || preserveRequestedOutputSize ? new Vector2Int(width, height) : GetElementRenderSize(contentBounds);
                return RenderCanvas(camera: camera, canvasRect: canvasRect, frameBounds: frameBounds, width: outputSize.x, height: outputSize.y);
            } catch {
                return PreviewResult.Empty;
            } finally {
                if (cameraObject != null) {
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                }

                if (canvasObject != null) {
                    UnityEngine.Object.DestroyImmediate(canvasObject);
                }

                if (previewScene.IsValid()) {
                    EditorSceneManager.ClosePreviewScene(previewScene);
                } else if (prefabRoot != null) {
                    UnityEngine.Object.DestroyImmediate(prefabRoot);
                }
            }
        }

        private static GameObject CreateReferenceCanvas(Vector2 referenceSize)
        {
            var canvasObject = new GameObject(
                name: "Canvas Preview Reference", 
                components: new[]{typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)}) {
                hideFlags = HideFlags.HideAndDontSave
            };
            SetLayerRecursively(canvasObject, PreviewLayer);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.planeDistance = 10f;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            CanvasPreviewSettings.ConfigureScaler(scaler);

            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
            canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.anchoredPosition = Vector2.zero;
            canvasRect.sizeDelta = referenceSize;
            canvasRect.localPosition = Vector3.zero;
            canvasRect.localRotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;

            return canvasObject;
        }

        private static void ConfigureBaseCanvasRect(RectTransform canvasRect, Vector2 referenceSize)
        {
            canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
            canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.anchoredPosition = Vector2.zero;
            canvasRect.sizeDelta = referenceSize;
            canvasRect.localPosition = Vector3.zero;
            canvasRect.localRotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one;
        }

        private static void ConfigureElementPreviewScalers(GameObject root)
        {
            root.GetComponentsInChildren(true, ScalerBuffer);
            
            try {
                for (int i = 0; i < ScalerBuffer.Count; i++) {
                    var scaler = ScalerBuffer[i];
                    if (scaler == null) {
                        continue;
                    }

                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                    scaler.scaleFactor = 1f;
                }
            } finally {
                ScalerBuffer.Clear();
            }
        }

        private static RectTransform GetPreviewRootRect(CanvasPreviewTarget target)
        {
            return target.PrefabRoot != null && target.PrefabRoot.TryGetComponent(out RectTransform rootRect)
                ? rootRect
                : target.RectTransform;
        }

        private static void PreparePreviewRootRect(
            RectTransform rootRect,
            RectTransform canvasRect,
            Vector2 referenceSize,
            CanvasPreviewRole role,
            bool preserveRootRectSettings)
        {
            rootRect.SetParent(canvasRect, false);

            var originalSize = rootRect.rect.size;
            rootRect.localPosition = Vector3.zero;
            rootRect.localRotation = Quaternion.identity;
            rootRect.localScale = Vector3.one;

            if (preserveRootRectSettings && HasPositiveSize(originalSize)) {
                return;
            }

            if (CanvasPreviewRoleResolver.UsesPreset(role) || !HasPositiveSize(originalSize)) {
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
                rootRect.pivot = new Vector2(0.5f, 0.5f);
                rootRect.sizeDelta = Vector2.zero;
            } else {
                rootRect.anchorMin = new Vector2(0.5f, 0.5f);
                rootRect.anchorMax = new Vector2(0.5f, 0.5f);
                rootRect.pivot = new Vector2(0.5f, 0.5f);
                rootRect.anchoredPosition = Vector2.zero;
                rootRect.sizeDelta = EnsurePositiveSize(originalSize, referenceSize);
            }
        }

        private static Vector2 GetReferenceSize(CanvasPreviewRole role, CanvasPreviewSize previewSize, RectTransform previewRootRect)
        {
            if (CanvasPreviewRoleResolver.UsesPreset(role)) {
                return previewSize.Vector;
            }

            return EnsurePositiveSize(previewRootRect != null ? previewRootRect.rect.size : Vector2.zero, FallbackElementReferenceSize);
        }

        private static Vector2 EnsurePositiveSize(Vector2 size, Vector2 fallback)
        {
            if (size.x <= 0f || size.y <= 0f) {
                return fallback;
            }

            return size;
        }

        private static bool HasPositiveSize(Vector2 size)
        {
            return size is { x: > 0f, y: > 0f };
        }

        private static Vector2Int GetElementRenderSize(Bounds contentBounds)
        {
            return new Vector2Int(
                Mathf.Max(1, Mathf.CeilToInt(contentBounds.size.x + ElementPadding * 2f)),
                Mathf.Max(1, Mathf.CeilToInt(contentBounds.size.y + ElementPadding * 2f)));
        }

        private static void CenterPreviewRootRect(RectTransform rootRect, Bounds contentBounds)
        {
            var offset = -(Vector2)contentBounds.center;
            rootRect.anchoredPosition += offset;
        }

        private static PreviewResult RenderCanvas(
            Camera camera, RectTransform canvasRect, Bounds frameBounds, int width, int height)
        {
            var renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previousActive = RenderTexture.active;
            Texture2D texture = null;

            try {
                ConfigureCamera(camera, renderTexture, width, height, frameBounds);

                canvasRect.localPosition = Vector3.zero;
                canvasRect.localRotation = Quaternion.identity;
                canvasRect.localScale = Vector3.one;

                Canvas.ForceUpdateCanvases();

                using (new SampleScope("CanvasPreview.CameraRender")) {
                    camera.Render();
                }

                RenderTexture.active = renderTexture;
                using (new SampleScope("CanvasPreview.Readback")) {
                    texture = new Texture2D(width, height, TextureFormat.ARGB32, false) {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                    texture.Apply(false, false);
                }

                return new PreviewResult(texture);
            } catch {
                if (texture != null) {
                    UnityEngine.Object.DestroyImmediate(texture);
                }

                return PreviewResult.Empty;
            } finally {
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private static void ConfigureCanvases(GameObject root, Camera camera)
        {
            root.GetComponentsInChildren(true, CanvasBuffer);
            try {
                for (int i = 0; i < CanvasBuffer.Count; i++) {
                    var canvas = CanvasBuffer[i];
                    if (canvas == null) {
                        continue;
                    }

                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                }
            } finally {
                CanvasBuffer.Clear();
            }
        }

        private static void ConfigureCamera(Camera camera, RenderTexture targetTexture, int width, int height, Bounds frameBounds)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = PreviewBackgroundColor;
            camera.orthographic = true;
            camera.aspect = width / (float)height;
            camera.cullingMask = 1 << PreviewLayer;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 10000f;
            camera.transform.position = new Vector3(frameBounds.center.x, frameBounds.center.y, -10f);
            camera.orthographicSize = GetOrthographicSize(frameBounds, camera.aspect);
            camera.targetTexture = targetTexture;
        }

        private static float GetOrthographicSize(Bounds frameBounds, float aspect)
        {
            var safeAspect = Mathf.Max(0.0001f, aspect);
            var extents = frameBounds.extents;
            return Mathf.Max(0.5f, extents.y, extents.x / safeAspect);
        }

        private static Bounds CalculateGraphicBounds(RectTransform relativeTo, RectTransform root)
        {
            var hasBounds = false;
            var bounds = new Bounds();

            root.GetComponentsInChildren(true, GraphicBuffer);
            try {
                for (int i = 0; i < GraphicBuffer.Count; i++) {
                    var graphic = GraphicBuffer[i];
                    if (graphic == null || !graphic.isActiveAndEnabled || graphic.color.a <= 0f) {
                        continue;
                    }

                    var rectTransform = graphic.rectTransform;
                    if (rectTransform == null || rectTransform.rect.width <= 0f || rectTransform.rect.height <= 0f) {
                        continue;
                    }

                    rectTransform.GetWorldCorners(CornerBuffer);
                    for (int cornerIndex = 0; cornerIndex < CornerBuffer.Length; cornerIndex++) {
                        var localCorner = relativeTo.InverseTransformPoint(CornerBuffer[cornerIndex]);
                        if (!hasBounds) {
                            bounds = new Bounds(localCorner, Vector3.zero);
                            hasBounds = true;
                        } else {
                            bounds.Encapsulate(localCorner);
                        }
                    }
                }
            } finally {
                GraphicBuffer.Clear();
            }

            return bounds;
        }

        private static Bounds ExpandBounds(Bounds bounds, float padding)
        {
            var size = bounds.size;
            size.x = Mathf.Max(1f, size.x + padding * 2f);
            size.y = Mathf.Max(1f, size.y + padding * 2f);
            return new Bounds(bounds.center, size);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.GetComponentsInChildren(true, TransformBuffer);
            try {
                for (int i = 0; i < TransformBuffer.Count; i++) {
                    TransformBuffer[i].gameObject.layer = layer;
                }
            } finally {
                TransformBuffer.Clear();
            }
        }

        private readonly struct SampleScope : IDisposable
        {
            internal SampleScope(string name)
            {
                Profiler.BeginSample(name);
            }

            public void Dispose()
            {
                Profiler.EndSample();
            }
        }

        internal sealed class PreviewResult : IDisposable
        {
            internal static readonly PreviewResult Empty = new PreviewResult(null);

            internal readonly Texture2D Texture;

            internal PreviewResult(Texture2D texture)
            {
                Texture = texture;
            }

            public void Dispose()
            {
                if (Texture != null) {
                    UnityEngine.Object.DestroyImmediate(Texture);
                }
            }
        }
    }
}
