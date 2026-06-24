using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Tripledot.CanvasKit.Editor.CanvasPreview
{
    internal static class CanvasPreviewEligibility
    {
        private static readonly List<Canvas> CanvasBuffer = new List<Canvas>(8);
        private static readonly List<RectTransform> RectTransformBuffer = new List<RectTransform>(32);
        private static readonly List<Graphic> GraphicBuffer = new List<Graphic>(32);

        public static bool TryGetPreviewTarget(GameObject gameObject, out CanvasPreviewTarget target)
        {
            target = CanvasPreviewTarget.Empty;
            return IsPrefabAssetRoot(gameObject) &&
                   TryGetPreviewTargetInHierarchy(gameObject, out target);
        }

        public static bool TryGetPreviewTargetInHierarchy(GameObject root, out CanvasPreviewTarget target)
        {
            target = CanvasPreviewTarget.Empty;
            if (root == null) {
                return false;
            }

            root.GetComponentsInChildren(true, CanvasBuffer);
            try {
                foreach (var canvas in CanvasBuffer) {
                    if (canvas == null || !IsHierarchyActive(canvas.transform) || !canvas.TryGetComponent(out RectTransform canvasRect)) {
                        continue;
                    }

                    target = new CanvasPreviewTarget(CanvasPreviewTargetKind.Canvas, root, canvas, canvasRect);
                    return true;
                }
            } finally {
                CanvasBuffer.Clear();
            }

            var rootTransform = root.GetComponent<RectTransform>();
            if (rootTransform != null && HasVisibleGraphic(rootTransform)) {
                target = new CanvasPreviewTarget(CanvasPreviewTargetKind.RectTransform, root, null, rootTransform);
                return true;
            }

            root.GetComponentsInChildren(true, RectTransformBuffer);
            try {
                foreach (var transform in RectTransformBuffer) {
                    if (transform != null && HasVisibleGraphic(transform)) {
                        target = new CanvasPreviewTarget(CanvasPreviewTargetKind.RectTransform, root, null, transform);
                        return true;
                    }
                }
            } finally {
                RectTransformBuffer.Clear();
            }

            return false;
        }

        private static bool IsPrefabAssetRoot(GameObject gameObject)
        {
            if (gameObject == null || !PrefabUtility.IsPartOfPrefabAsset(gameObject)) {
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(gameObject);
            return !string.IsNullOrEmpty(assetPath) && AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) == gameObject;
        }

        private static bool HasVisibleGraphic(RectTransform root)
        {
            root.GetComponentsInChildren(true, GraphicBuffer);
            try {
                foreach (var graphic in GraphicBuffer) {
                    if (graphic == null || !graphic.enabled || graphic.color.a <= 0f || !IsHierarchyActive(graphic.transform)) {
                        continue;
                    }

                    if (graphic.rectTransform != null && HasRenderableRect(graphic.rectTransform)) {
                        return true;
                    }
                }
            } finally {
                GraphicBuffer.Clear();
            }

            return false;
        }

        private static bool HasRenderableRect(RectTransform rectTransform)
        {
            var rect = rectTransform.rect;
            return rect is { width: > 0f, height: > 0f }
                   || !Mathf.Approximately(rectTransform.anchorMin.x, rectTransform.anchorMax.x)
                   || !Mathf.Approximately(rectTransform.anchorMin.y, rectTransform.anchorMax.y);
        }

        private static bool IsHierarchyActive(Transform transform)
        {
            while (transform != null) {
                if (!transform.gameObject.activeSelf) {
                    return false;
                }

                transform = transform.parent;
            }

            return true;
        }
    }
}