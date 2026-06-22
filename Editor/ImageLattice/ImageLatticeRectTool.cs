using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    [EditorTool("Rect Lattice Points", typeof(ImageLattice), typeof(ImageLatticeToolContext))]
    internal sealed class ImageLatticeRectTool : ImageLatticeTransformToolBase
    {
        private const float RectHandleSize = 0.075f;

        protected override string ToolbarIconName => "RectTool";
        protected override string ToolbarFallbackText => "Rect";
        protected override string ToolbarTooltip => "Resize or rotate selected Image Lattice points.";

        protected override void DrawLatticeTool(ImageLattice image, Rect localRect, Vector2[] points)
        {
            if (!ImageLatticeSceneView.TryGetAffectedBounds(image, points, out var bounds)) {
                return;
            }

            DrawMoveHandle(image, localRect, points, bounds);
            DrawResizeHandle(image, localRect, points, bounds, new Vector2(bounds.xMin, bounds.yMin), true, false, true, false);
            DrawResizeHandle(image, localRect, points, bounds, new Vector2(bounds.center.x, bounds.yMin), false, false, true, false);
            DrawResizeHandle(image, localRect, points, bounds, new Vector2(bounds.xMax, bounds.yMin), false, true, true, false);
            DrawResizeHandle(image, localRect, points, bounds, new Vector2(bounds.xMin, bounds.center.y), true, false, false, false);
            DrawResizeHandle(image, localRect, points, bounds, new Vector2(bounds.xMax, bounds.center.y), false, true, false, false);
            DrawResizeHandle(image, localRect, points, bounds, new Vector2(bounds.xMin, bounds.yMax), true, false, false, true);
            DrawResizeHandle(image, localRect, points, bounds, new Vector2(bounds.center.x, bounds.yMax), false, false, false, true);
            DrawResizeHandle(image, localRect, points, bounds, new Vector2(bounds.xMax, bounds.yMax), false, true, false, true);
            DrawRotationHandle(image, localRect, points, bounds);
        }

        private void DrawMoveHandle(ImageLattice image, Rect localRect, Vector2[] points, Rect bounds)
        {
            var pivot = bounds.center;
            var pivotWorld = ImageLatticeSceneView.LatticeUvToWorld(image, localRect, pivot);
            var handlePosition = Drag.Active ? Drag.CurrentPositionWorld : pivotWorld;

            EditorGUI.BeginChangeCheck();
            var nextPosition = Handles.PositionHandle(handlePosition, GetHandleRotation(image));
            if (!EditorGUI.EndChangeCheck()) {
                return;
            }

            Drag.Begin(image, "Move Lattice Selection", points, pivot, pivotWorld, GetHandleRotation(image), Vector3.one);
            var delta = WorldDeltaToLatticeUv(image, localRect, Drag.StartPositionWorld, nextPosition);

            ImageLatticeSelectionUtility.MovePoints(
                source: Drag.SourcePoints,
                destination: Drag.DestinationPoints,
                controlPointColumns: image.ControlPointColumns,
                controlPointRows: image.ControlPointRows,
                selected: ImageLatticeToolState.Selection,
                delta: delta,
                mirrorMode: ImageLatticeToolState.MirrorMode,
                editTarget: ImageLatticeToolState.EditTarget,
                softSelectionMode: ImageLatticeToolState.SoftSelectionMode,
                softSelectionRadius: ImageLatticeToolState.SoftSelectionRadius,
                selectedCells: ImageLatticeToolState.CellSelection);

            Drag.CurrentPositionWorld = nextPosition;
            ApplyTransformedPoints(image, Drag.DestinationPoints);
        }

        private void DrawResizeHandle(
            ImageLattice image,
            Rect localRect,
            Vector2[] points,
            Rect bounds,
            Vector2 handleUv,
            bool minX,
            bool maxX,
            bool minY,
            bool maxY)
        {
            var world = ImageLatticeSceneView.LatticeUvToWorld(image, localRect, handleUv);
            var size = HandleUtility.GetHandleSize(world) * RectHandleSize;
            var rectTransform = (RectTransform)image.transform;
            var rotation = rectTransform.rotation;
            var normal = rectTransform.forward;
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;

            EditorGUI.BeginChangeCheck();
            var nextWorld = Handles.Slider2D(world, normal, right, up, size, Handles.RectangleHandleCap, Vector2.zero, false);
            if (!EditorGUI.EndChangeCheck()) {
                return;
            }

            Drag.Begin(image, "Resize Lattice Selection", points, bounds.center, world, rotation, Vector3.one);
            if (!Drag.HasStartBounds) {
                Drag.StartBounds = bounds;
                Drag.HasStartBounds = true;
            }

            var nextUv = ImageLatticeSceneView.WorldToLatticeUv(image, localRect, nextWorld);
            var targetBounds = Drag.StartBounds;

            if (minX) {
                targetBounds.xMin = Mathf.Min(nextUv.x, Drag.StartBounds.xMax - 0.0001f);
            }
            if (maxX) {
                targetBounds.xMax = Mathf.Max(nextUv.x, Drag.StartBounds.xMin + 0.0001f);
            }
            if (minY) {
                targetBounds.yMin = Mathf.Min(nextUv.y, Drag.StartBounds.yMax - 0.0001f);
            }
            if (maxY) {
                targetBounds.yMax = Mathf.Max(nextUv.y, Drag.StartBounds.yMin + 0.0001f);
            }

            ImageLatticeSelectionUtility.ResizePoints(
                source: Drag.SourcePoints,
                destination: Drag.DestinationPoints,
                controlPointColumns: image.ControlPointColumns,
                controlPointRows: image.ControlPointRows,
                selected: ImageLatticeToolState.Selection,
                sourceRect: Drag.StartBounds,
                targetRect: targetBounds,
                mirrorMode: ImageLatticeToolState.MirrorMode,
                editTarget: ImageLatticeToolState.EditTarget,
                softSelectionMode: ImageLatticeToolState.SoftSelectionMode,
                softSelectionRadius: ImageLatticeToolState.SoftSelectionRadius,
                selectedCells: ImageLatticeToolState.CellSelection);

            ApplyTransformedPoints(image, Drag.DestinationPoints);
        }

        private void DrawRotationHandle(ImageLattice image, Rect localRect, Vector2[] points, Rect bounds)
        {
            var pivot = bounds.center;
            var pivotWorld = ImageLatticeSceneView.LatticeUvToWorld(image, localRect, pivot);
            var handleRotation = GetHandleRotation(image);
            var currentRotation = Drag.Active ? Drag.CurrentRotation : handleRotation;
            var radius = HandleUtility.GetHandleSize(pivotWorld) * Mathf.Max(0.8f, Mathf.Max(bounds.width, bounds.height));
            radius = Mathf.Max(radius, HandleUtility.GetHandleSize(pivotWorld) * 0.35f);
            var normal = ((RectTransform)image.transform).forward;

            EditorGUI.BeginChangeCheck();
            var nextRotation = Handles.Disc(currentRotation, pivotWorld, normal, radius, false, 0f);
            if (!EditorGUI.EndChangeCheck()) {
                return;
            }

            Drag.Begin(image, "Rotate Lattice Selection", points, pivot, pivotWorld, handleRotation, Vector3.one);
            var angle = GetSignedHandleAngle(image, Drag.StartRotation, nextRotation);

            ImageLatticeSelectionUtility.RotatePoints(
                source: Drag.SourcePoints,
                destination: Drag.DestinationPoints,
                controlPointColumns: image.ControlPointColumns,
                controlPointRows: image.ControlPointRows,
                selected: ImageLatticeToolState.Selection,
                pivot: Drag.Pivot,
                angleDegrees: angle,
                mirrorMode: ImageLatticeToolState.MirrorMode,
                editTarget: ImageLatticeToolState.EditTarget,
                softSelectionMode: ImageLatticeToolState.SoftSelectionMode,
                softSelectionRadius: ImageLatticeToolState.SoftSelectionRadius,
                selectedCells: ImageLatticeToolState.CellSelection);

            Drag.CurrentRotation = nextRotation;
            ApplyTransformedPoints(image, Drag.DestinationPoints);
        }
    }
}