using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    [EditorTool("Rotate Lattice Points", typeof(ImageLattice), typeof(ImageLatticeToolContext))]
    internal sealed class ImageLatticeRotateTool : ImageLatticeTransformToolBase
    {
        protected override string ToolbarIconName => "RotateTool";
        protected override string ToolbarFallbackText => "Rotate";
        protected override string ToolbarTooltip => "Rotate selected Image Lattice points.";

        protected override void DrawLatticeTool(ImageLattice image, Rect localRect, Vector2[] points)
        {
            var pivot = GetHandlePivot(image, points);
            var pivotWorld = ImageLatticeSceneView.LatticeUvToWorld(image, localRect, pivot);
            var handleRotation = GetHandleRotation(image);
            var currentRotation = Drag.Active ? Drag.CurrentRotation : handleRotation;

            EditorGUI.BeginChangeCheck();
            var nextRotation = Handles.RotationHandle(currentRotation, pivotWorld);
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
