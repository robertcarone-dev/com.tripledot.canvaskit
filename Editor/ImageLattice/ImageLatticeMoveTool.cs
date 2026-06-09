using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    [EditorTool("Move Lattice Points", typeof(ImageLattice), typeof(ImageLatticeToolContext))]
    internal sealed class ImageLatticeMoveTool : ImageLatticeTransformToolBase
    {
        protected override string ToolbarIconName => "MoveTool";
        protected override string ToolbarFallbackText => "Move";
        protected override string ToolbarTooltip => "Move selected Image Lattice points.";

        protected override void DrawLatticeTool(ImageLattice image, Rect localRect, Vector2[] points)
        {
            var pivot = GetHandlePivot(image, points);
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
    }
}
