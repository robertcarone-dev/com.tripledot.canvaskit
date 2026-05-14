using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Tripledot.CanvasKit.Editor
{
    internal static class CanvasPreviewEligibility
    {
        private static readonly List<Canvas> CanvasBuffer = new List<Canvas>(8);
        private static readonly List<RectTransform> RectTransformBuffer = new List<RectTransform>(32);
        private static readonly List<Graphic> GraphicBuffer = new List<Graphic>(32);

        internal static bool TryGetPreviewTarget(GameObject gameObject, out CanvasPreviewTarget target)
        {
            target = CanvasPreviewTarget.Empty;
            if (!IsPrefabAssetRoot(gameObject)) {
                return false;
            }

            return TryGetPreviewTargetInHierarchy(gameObject, out target);
        }

        internal static bool TryGetPreviewTargetInHierarchy(GameObject root, out CanvasPreviewTarget target)
        {
            target = CanvasPreviewTarget.Empty;
            if (root == null) {
                return false;
            }

            root.GetComponentsInChildren(true, CanvasBuffer);
            try {
                for (int i = 0; i < CanvasBuffer.Count; i++) {
                    var canvas = CanvasBuffer[i];
                    if (canvas == null || !IsHierarchyActive(canvas.transform) || !canvas.TryGetComponent(out RectTransform canvasRect)) {
                        continue;
                    }

                    target = new CanvasPreviewTarget(CanvasPreviewTargetKind.Canvas, root, canvas, canvasRect);
                    return true;
                }
            } finally {
                CanvasBuffer.Clear();
            }

            var rootRect = root.GetComponent<RectTransform>();
            if (rootRect != null && HasVisibleGraphic(rootRect)) {
                target = new CanvasPreviewTarget(CanvasPreviewTargetKind.RectTransform, root, null, rootRect);
                return true;
            }

            root.GetComponentsInChildren(true, RectTransformBuffer);
            try {
                for (int i = 0; i < RectTransformBuffer.Count; i++) {
                    var rect = RectTransformBuffer[i];
                    if (rect != null && HasVisibleGraphic(rect)) {
                        target = new CanvasPreviewTarget(CanvasPreviewTargetKind.RectTransform, root, null, rect);
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
                for (int i = 0; i < GraphicBuffer.Count; i++) {
                    var graphic = GraphicBuffer[i];
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
            return (rect.width > 0f && rect.height > 0f)
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
