using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    [EditorTool("Scale Lattice Points", typeof(ImageLattice), typeof(ImageLatticeToolContext))]
    internal sealed class ImageLatticeScaleTool : ImageLatticeTransformToolBase
    {
        protected override string ToolbarIconName => "ScaleTool";
        protected override string ToolbarFallbackText => "Scale";
        protected override string ToolbarTooltip => "Scale selected Image Lattice points.";

        protected override void DrawLatticeTool(ImageLattice image, Rect localRect, Vector2[] points)
        {
            var pivot = GetHandlePivot(image, points);
            var pivotWorld = ImageLatticeSceneView.LatticeUvToWorld(image, localRect, pivot);
            var handleScale = Drag.Active ? Drag.CurrentScale : Vector3.one;

            EditorGUI.BeginChangeCheck();
            var nextScale = Handles.ScaleHandle(handleScale, pivotWorld, GetHandleRotation(image), HandleUtility.GetHandleSize(pivotWorld));
            if (!EditorGUI.EndChangeCheck()) {
                return;
            }

            var scale = new Vector2(nextScale.x, nextScale.y);
            if (Event.current.shift) {
                scale = ImageLatticeSelectionUtility.ConstrainUniformScale(scale, true, true);
                nextScale = new Vector3(scale.x, scale.y, scale.x);
            }

            Drag.Begin(image, "Scale Lattice Selection", points, pivot, pivotWorld, GetHandleRotation(image), Vector3.one);
            
            ImageLatticeSelectionUtility.ScalePoints(
                source: Drag.SourcePoints,
                destination: Drag.DestinationPoints,
                controlPointColumns: image.ControlPointColumns,
                controlPointRows: image.ControlPointRows,
                selected: ImageLatticeToolState.Selection,
                pivot: Drag.Pivot,
                scale: scale,
                mirrorMode: ImageLatticeToolState.MirrorMode,
                editTarget: ImageLatticeToolState.EditTarget,
                softSelectionMode: ImageLatticeToolState.SoftSelectionMode,
                softSelectionRadius: ImageLatticeToolState.SoftSelectionRadius,
                selectedCells: ImageLatticeToolState.CellSelection);
            
            Drag.CurrentScale = nextScale;
            ApplyTransformedPoints(image, Drag.DestinationPoints);
        }
    }
}
