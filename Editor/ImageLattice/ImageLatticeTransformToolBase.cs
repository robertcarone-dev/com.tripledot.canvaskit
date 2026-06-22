using System;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    internal abstract class ImageLatticeTransformToolBase : EditorTool
    {
        private const string DefaultIconName = "RectTool";

        private readonly ImageLatticeDragState drag = new ImageLatticeDragState();
        private GUIContent icon;

        public override GUIContent toolbarIcon => icon ?? new GUIContent(ToolbarFallbackText, ToolbarTooltip);

        protected ImageLatticeDragState Drag => drag;
        protected virtual string ToolbarIconName => DefaultIconName;
        protected virtual string ToolbarFallbackText => "Lattice";
        protected virtual string ToolbarTooltip => "Edit Image Lattice points.";

        protected virtual void OnEnable()
        {
            icon = EditorGUIUtility.TrIconContent(ToolbarIconName, ToolbarTooltip);
            ImageLatticeToolState.InteractionCancelled += EndDrag;
        }

        protected virtual void OnDisable()
        {
            ImageLatticeToolState.InteractionCancelled -= EndDrag;
            EndDrag();
        }

        public override bool IsAvailable()
        {
            return target is ImageLattice;
        }

        public override void OnWillBeDeactivated()
        {
            EndDrag();
            base.OnWillBeDeactivated();
        }

        public sealed override void OnToolGUI(EditorWindow window)
        {
            if (target is not ImageLattice image) {
                return;
            }

            ImageLatticeToolState.SetActiveImage(image);
            ImageLatticeToolState.NormalizeSelection(image.ControlPointColumns * image.ControlPointRows);

            if (!ImageLatticeSceneView.TryGetLocalRect(image, out var localRect)) {
                EndDrag();
                return;
            }

            ImageLatticeSceneView.Draw(image, localRect);

            var points = ImageLatticeSceneView.CapturePoints(image);
            if (Drag.Active && Drag.SourcePoints?.Length != points.Length) {
                EndDrag();
            }

            if (ImageLatticeSceneView.HandlePriorityInput()) {
                return;
            }

            if (ImageLatticeToolState.HasSelection) {
                DrawLatticeTool(image, localRect, points);
            }

            ImageLatticeSceneView.HandleSelectionInput(image, localRect);

            if (Event.current.rawType == EventType.MouseUp ||
                Event.current.rawType == EventType.Ignore ||
                (Drag.Active && GUIUtility.hotControl == 0 && Event.current.type == EventType.Layout)) {
                EndDrag();
            }
        }

        protected abstract void DrawLatticeTool(ImageLattice image, Rect localRect, Vector2[] points);

        protected Vector2 GetHandlePivot(ImageLattice image, Vector2[] points)
        {
            if (Tools.pivotMode == PivotMode.Center &&
                ImageLatticeSceneView.TryGetAffectedBounds(image, points, out var bounds)) {
                return bounds.center;
            }

            var activeCell = ImageLatticeToolState.ActiveCellIndex;
            if (ImageLatticeToolState.EditTarget == ImageLatticeEditTarget.Cells &&
                activeCell >= 0 && activeCell < (image.ControlPointColumns - 1) * (image.ControlPointRows - 1)) {
                return image.EvaluateLattice(ImageLatticeSelectionUtility.GetCellCenterUv(activeCell, image.ControlPointColumns, image.ControlPointRows));
            }

            var activePoint = ImageLatticeToolState.ActivePointIndex;
            if (activePoint >= 0 && activePoint < points.Length && ImageLatticeToolState.Selection.Contains(activePoint)) {
                return points[activePoint];
            }

            return ImageLatticeSceneView.TryGetSelectionCenter(image, points, out var center) ? center : new Vector2(0.5f, 0.5f);
        }

        protected Quaternion GetHandleRotation(ImageLattice image)
        {
            if (Tools.pivotRotation == PivotRotation.Global) {
                return Quaternion.identity;
            }

            return ((RectTransform)image.transform).rotation;
        }

        protected float GetSignedHandleAngle(ImageLattice image, Quaternion startRotation, Quaternion currentRotation)
        {
            var normal = ((RectTransform)image.transform).forward;
            var startRight = Vector3.ProjectOnPlane(startRotation * Vector3.right, normal);
            var currentRight = Vector3.ProjectOnPlane(currentRotation * Vector3.right, normal);
            if (startRight.sqrMagnitude < 0.000001f || currentRight.sqrMagnitude < 0.000001f) {
                return 0f;
            }

            return Vector3.SignedAngle(startRight, currentRight, normal);
        }

        protected Vector2 WorldDeltaToLatticeUv(ImageLattice image, Rect localRect, Vector3 startWorld, Vector3 currentWorld)
        {
            return ImageLatticeSceneView.WorldToLatticeUv(image, localRect, currentWorld) -
                   ImageLatticeSceneView.WorldToLatticeUv(image, localRect, startWorld);
        }

        protected void ApplyTransformedPoints(ImageLattice image, Vector2[] points)
        {
            if (ImageLatticeSerializedPointUtility.ApplyPoints(image, points)) {
                image.UpdateRuntimeMaterialPayloadOrDirtyImage();
                PrefabUtility.RecordPrefabInstancePropertyModifications(image);
                EditorUtility.SetDirty(image);
                SceneView.RepaintAll();
            }
        }

        protected void EndDrag()
        {
            drag.End();
            ImageLatticeToolState.EndUndo();
        }
    }

    internal sealed class ImageLatticeDragState
    {
        public bool Active { get; private set; }
        public Vector2[] SourcePoints { get; private set; }
        public Vector2[] DestinationPoints { get; private set; }
        public Vector2 Pivot { get; private set; }
        public Vector3 StartPositionWorld { get; private set; }
        public Vector3 CurrentPositionWorld { get; set; }
        public Quaternion StartRotation { get; private set; }
        public Quaternion CurrentRotation { get; set; }
        public Vector3 CurrentScale { get; set; }
        public Rect StartBounds { get; set; }
        public bool HasStartBounds { get; set; }

        public void Begin(ImageLattice image, string undoName, Vector2[] points, Vector2 pivot, Vector3 positionWorld, Quaternion rotation, Vector3 scale)
        {
            if (Active) {
                return;
            }

            Active = true;
            SourcePoints = new Vector2[points.Length];
            DestinationPoints = new Vector2[points.Length];
            Array.Copy(points, SourcePoints, points.Length);
            Array.Copy(points, DestinationPoints, points.Length);
            Pivot = pivot;
            StartPositionWorld = positionWorld;
            CurrentPositionWorld = positionWorld;
            StartRotation = rotation;
            CurrentRotation = rotation;
            CurrentScale = scale;
            StartBounds = default;
            HasStartBounds = false;
            ImageLatticeToolState.RecordTransform(image, undoName);
        }

        public void End()
        {
            Active = false;
            SourcePoints = null;
            DestinationPoints = null;
            HasStartBounds = false;
        }
    }
}