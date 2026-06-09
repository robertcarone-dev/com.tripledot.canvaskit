using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    [EditorTool("Transform Lattice Points", typeof(ImageLattice), typeof(ImageLatticeToolContext))]
    internal sealed class ImageLatticeTransformTool : ImageLatticeTransformToolBase
    {
        protected override string ToolbarIconName => "TransformTool";
        protected override string ToolbarFallbackText => "Transform";
        protected override string ToolbarTooltip => "Move, rotate, and scale selected Image Lattice points.";

        protected override void DrawLatticeTool(ImageLattice image, Rect localRect, Vector2[] points)
        {
            var pivot = GetHandlePivot(image, points);
            var pivotWorld = ImageLatticeSceneView.LatticeUvToWorld(image, localRect, pivot);
            var position = Drag.Active ? Drag.CurrentPositionWorld : pivotWorld;
            var rotation = Drag.Active ? Drag.CurrentRotation : GetHandleRotation(image);
            var scale = Drag.Active ? Drag.CurrentScale : Vector3.one;
            var startRotation = GetHandleRotation(image);

            EditorGUI.BeginChangeCheck();
            Handles.TransformHandle(ref position, ref rotation, ref scale);
            if (!EditorGUI.EndChangeCheck()) {
                return;
            }

            var scale2D = new Vector2(scale.x, scale.y);
            if (Event.current.shift) {
                scale2D = ImageLatticeSelectionUtility.ConstrainUniformScale(scale2D, true, true);
                scale = new Vector3(scale2D.x, scale2D.y, scale2D.x);
            }

            Drag.Begin(image, "Transform Lattice Selection", points, pivot, pivotWorld, startRotation, Vector3.one);
            var delta = WorldDeltaToLatticeUv(image, localRect, Drag.StartPositionWorld, position);
            var angle = GetSignedHandleAngle(image, Drag.StartRotation, rotation);
            
            ImageLatticeSelectionUtility.TransformPoints(
                source: Drag.SourcePoints, 
                destination: Drag.DestinationPoints,
                controlPointColumns: image.ControlPointColumns,
                controlPointRows: image.ControlPointRows,
                selected: ImageLatticeToolState.Selection,
                pivot: Drag.Pivot,
                translation: delta,
                angleDegrees: angle, 
                scale: scale2D, 
                mirrorMode: ImageLatticeToolState.MirrorMode,
                editTarget: ImageLatticeToolState.EditTarget,
                softSelectionMode: ImageLatticeToolState.SoftSelectionMode,
                softSelectionRadius: ImageLatticeToolState.SoftSelectionRadius,
                selectedCells: ImageLatticeToolState.CellSelection);
            
            Drag.CurrentPositionWorld = position;
            Drag.CurrentRotation = rotation;
            Drag.CurrentScale = scale;
            ApplyTransformedPoints(image, Drag.DestinationPoints);
        }
    }
}
