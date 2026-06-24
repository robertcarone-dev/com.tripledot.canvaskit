using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.UI;

namespace Tripledot.CanvasKit.Editor
{
    [InitializeOnLoad]
    internal static class ImageLatticeToolState
    {
        public const float MinSoftSelectionRadius = 0.25f;
        public const float RadiusShortcutStep = 0.25f;

        private static readonly HashSet<int> SelectedPoints = new HashSet<int>();
        private static readonly HashSet<int> SelectedCells = new HashSet<int>();
        private static readonly List<int> ExpandedSelectedCellPoints = new List<int>(ImageLattice.MaxControlPointsPerAxis * ImageLattice.MaxControlPointsPerAxis);
        private static readonly List<int> AffectedPoints = new List<int>(ImageLattice.MaxControlPointsPerAxis * ImageLattice.MaxControlPointsPerAxis);
        private static readonly List<int> AffectedCells = new List<int>(ImageLattice.MaxControlPointsPerAxis * ImageLattice.MaxControlPointsPerAxis);

        private static ImageLattice activeImage;
        private static ImageLatticeSelectionUndoState selectionUndoState;
        private static ImageLatticeMirrorMode mirrorMode;
        private static ImageLatticeEditTarget editTarget;
        private static ImageLatticeSoftSelectionMode softSelectionMode;
        private static float softSelectionRadius = 1.5f;
        private static int activePointIndex = -1;
        private static int activeCellIndex = -1;
        private static int activeControlPointColumns = -1;
        private static int activeControlPointRows = -1;
        private static int activeSegmentsPerCell = -1;
        private static Image.Type activeImageType;
        private static bool radiusClutchActive;
        private static bool undoActive;
        private static int undoGroup = -1;

        public static event Action MirrorModeChanged;
        public static event Action EditTargetChanged;
        public static event Action SoftSelectionChanged;
        public static event Action RadiusClutchChanged;
        public static event Action InteractionCancelled;

        static ImageLatticeToolState()
        {
            Undo.undoRedoPerformed += RefreshAfterUndoRedo;
            AssemblyReloadEvents.beforeAssemblyReload += DestroySelectionUndoState;
        }

        public static ImageLattice ActiveImage => activeImage;
        public static ICollection<int> Selection => editTarget == ImageLatticeEditTarget.Cells ? ExpandedSelectedCellPoints : SelectedPoints;
        public static ICollection<int> CellSelection => SelectedCells;
        public static int ActivePointIndex => activePointIndex;
        public static int ActiveCellIndex => activeCellIndex;
        public static bool IsRadiusClutchActive => radiusClutchActive;
        public static bool HasSelection => editTarget == ImageLatticeEditTarget.Cells ? SelectedCells.Count > 0 : SelectedPoints.Count > 0;
        public static bool IsToolActive =>
            ToolManager.activeContextType == typeof(ImageLatticeToolContext) &&
            activeImage != null &&
            (UnityEditor.Selection.activeObject == activeImage ||
             UnityEditor.Selection.activeGameObject == activeImage.gameObject);

        public static ImageLatticeMirrorMode MirrorMode {
            get => mirrorMode;
            set {
                if (mirrorMode == value) {
                    return;
                }

                mirrorMode = value;
                MirrorModeChanged?.Invoke();
                SceneView.RepaintAll();
            }
        }

        public static ImageLatticeEditTarget EditTarget {
            get => editTarget;
            set {
                if (editTarget == value) {
                    return;
                }

                editTarget = value;
                CancelInteraction(false);
                EditTargetChanged?.Invoke();
                StoreSelectionUndoState();
                SceneView.RepaintAll();
            }
        }

        public static ImageLatticeSoftSelectionMode SoftSelectionMode {
            get => softSelectionMode;
            set {
                if (softSelectionMode == value) {
                    return;
                }

                softSelectionMode = value;
                SoftSelectionChanged?.Invoke();
                SceneView.RepaintAll();
            }
        }

        public static float SoftSelectionRadius {
            get => softSelectionRadius;
            set {
                value = Mathf.Clamp(SnapRadius(value), MinSoftSelectionRadius, GetMaxSoftSelectionRadius());
                if (Mathf.Approximately(softSelectionRadius, value)) {
                    return;
                }

                softSelectionRadius = value;
                SoftSelectionChanged?.Invoke();
                SceneView.RepaintAll();
            }
        }

        public static bool IsSelected(int index)
        {
            return editTarget == ImageLatticeEditTarget.Cells ? ExpandedSelectedCellPoints.Contains(index) : SelectedPoints.Contains(index);
        }

        public static bool IsCellSelected(int index)
        {
            return SelectedCells.Contains(index);
        }

        public static void SetRadiusClutchActive(bool active)
        {
            if (radiusClutchActive == active) {
                return;
            }

            radiusClutchActive = active;
            RadiusClutchChanged?.Invoke();
            SceneView.RepaintAll();
        }

        public static void AdjustSoftSelectionRadius(float delta)
        {
            SoftSelectionRadius += delta;
        }

        public static float GetMaxSoftSelectionRadius()
        {
            if (activeControlPointColumns > 1 && activeControlPointRows > 1) {
                return Mathf.Max(MinSoftSelectionRadius, Mathf.Max(activeControlPointColumns - 1, activeControlPointRows - 1));
            }

            return ImageLattice.MaxControlPointsPerAxis - 1;
        }

        public static bool IsEditing(ImageLattice image)
        {
            return image != null && IsToolActive && activeImage == image;
        }

        public static void SetActiveImage(ImageLattice image)
        {
            if (activeImage == image) {
                if (HasActiveImageSignatureChanged(image)) {
                    UpdateActiveImageSignature(image);
                    NormalizeSelection(image.ControlPointColumns * image.ControlPointRows);
                    CancelInteraction(false);
                    StoreSelectionUndoState();
                }

                return;
            }

            activeImage = image;
            UpdateActiveImageSignature(image);
            CancelInteraction(true);
            StoreSelectionUndoState();
        }

        public static void ClearActiveImage(ImageLattice image)
        {
            if (activeImage != image) {
                return;
            }

            activeImage = null;
            activeControlPointColumns = -1;
            activeControlPointRows = -1;
            activeSegmentsPerCell = -1;
            CancelInteraction(true);
            StoreSelectionUndoState();
        }

        public static void StopEditing(ImageLattice image)
        {
            ClearActiveImage(image);
            if (ToolManager.activeContextType == typeof(ImageLatticeToolContext)) {
                ToolManager.SetActiveContext<GameObjectToolContext>();
                Tools.current = Tool.Move;
            }

            SceneView.RepaintAll();
        }

        public static void ClearSelection()
        {
            if (HasSelection) {
                RecordSelectionUndo("Clear Lattice Selection");
                CancelInteraction(true);
                StoreSelectionUndoState();
                return;
            }

            CancelInteraction(true);
        }

        public static void ClearSelection(ImageLattice image)
        {
            if (activeImage == image) {
                ClearSelection();
            }
        }

        public static void NormalizeSelection(int pointCount)
        {
            var removedPoints = SelectedPoints.RemoveWhere(index => index < 0 || index >= pointCount);
            var previousActivePoint = activePointIndex;
            if (activePointIndex < 0 || activePointIndex >= pointCount || !SelectedPoints.Contains(activePointIndex)) {
                activePointIndex = GetFirstSelectedPoint();
            }

            var previousActiveCell = activeCellIndex;
            var cellCount = GetActiveCellCount();
            var removedCells = SelectedCells.RemoveWhere(index => index < 0 || index >= cellCount);
            if (activeCellIndex < 0 || activeCellIndex >= cellCount || !SelectedCells.Contains(activeCellIndex)) {
                activeCellIndex = GetFirstSelectedCell(SelectedCells);
            }

            RebuildExpandedCellSelection();

            if (removedPoints > 0 || removedCells > 0 || previousActivePoint != activePointIndex || previousActiveCell != activeCellIndex) {
                StoreSelectionUndoState();
                SceneView.RepaintAll();
            }
        }

        public static void SelectSingle(int index)
        {
            var nextSelection = new HashSet<int>();
            ImageLatticeSelectionUtility.SelectSingle(nextSelection, index);
            ApplyPointSelection(nextSelection, index, "Select Lattice Point");
        }

        public static void SetActivePoint(int index)
        {
            if (!SelectedPoints.Contains(index)) {
                SelectSingle(index);
                return;
            }

            ApplyPointSelection(new HashSet<int>(SelectedPoints), index, "Activate Lattice Point");
        }

        public static void ToggleSelection(int index)
        {
            var nextSelection = new HashSet<int>(SelectedPoints);
            var wasSelected = nextSelection.Contains(index);
            ImageLatticeSelectionUtility.ToggleSelection(nextSelection, index);
            ApplyPointSelection(nextSelection, wasSelected ? GetFirstSelectedPoint(nextSelection) : index, "Toggle Lattice Point Selection");
        }

        public static void SelectCell(int cellIndex, int controlPointColumns, int controlPointRows)
        {
            var nextSelection = new HashSet<int> { cellIndex };
            ApplyCellSelection(nextSelection, cellIndex, controlPointColumns, controlPointRows, "Select Lattice Cell");
        }

        public static void SetActiveCell(int cellIndex, int controlPointColumns, int controlPointRows)
        {
            if (!SelectedCells.Contains(cellIndex)) {
                SelectCell(cellIndex, controlPointColumns, controlPointRows);
                return;
            }

            ApplyCellSelection(new HashSet<int>(SelectedCells), cellIndex, controlPointColumns, controlPointRows, "Activate Lattice Cell");
        }

        public static void ToggleCellSelection(int cellIndex, int controlPointColumns, int controlPointRows)
        {
            var nextSelection = new HashSet<int>(SelectedCells);
            var wasSelected = !nextSelection.Add(cellIndex);
            if (wasSelected) {
                nextSelection.Remove(cellIndex);
            }

            ApplyCellSelection(nextSelection, wasSelected ? GetFirstSelectedCell(nextSelection) : cellIndex, controlPointColumns, controlPointRows, "Toggle Lattice Cell Selection");
        }

        public static void SelectCellsInRect(Vector2[] cells, Rect rect, bool additive, int controlPointColumns, int controlPointRows)
        {
            var nextSelection = additive ? new HashSet<int>(SelectedCells) : new HashSet<int>();
            for (var i = 0; i < cells.Length; i++) {
                if (rect.Contains(cells[i], true)) {
                    nextSelection.Add(i);
                }
            }

            var nextActiveCell = activeCellIndex;
            if (nextActiveCell < 0 || !nextSelection.Contains(nextActiveCell)) {
                nextActiveCell = GetFirstSelectedCell(nextSelection);
            }

            ApplyCellSelection(nextSelection, nextActiveCell, controlPointColumns, controlPointRows, "Marquee Lattice Cell Selection");
        }

        public static void SelectInRect(Vector2[] points, Rect rect, bool additive)
        {
            var nextSelection = new HashSet<int>(SelectedPoints);
            ImageLatticeSelectionUtility.SelectInRect(points, rect, nextSelection, additive);

            var nextActivePoint = activePointIndex;
            if (nextActivePoint < 0 || !nextSelection.Contains(nextActivePoint)) {
                nextActivePoint = GetFirstSelectedPoint(nextSelection);
            }

            ApplyPointSelection(nextSelection, nextActivePoint, "Marquee Lattice Selection");
        }

        public static List<int> GetAffectedPoints(int controlPointColumns, int controlPointRows)
        {
            RebuildExpandedCellSelection(controlPointColumns, controlPointRows);
            ImageLatticeSelectionUtility.CollectAffectedPoints(Selection, controlPointColumns, controlPointRows, mirrorMode, AffectedPoints);
            return AffectedPoints;
        }

        public static List<int> GetAffectedCells(int controlPointColumns, int controlPointRows)
        {
            ImageLatticeSelectionUtility.CollectAffectedCells(SelectedCells, controlPointColumns - 1, controlPointRows - 1, mirrorMode, AffectedCells);
            return AffectedCells;
        }

        public static void CycleMirrorMode()
        {
            MirrorMode = ImageLatticeSelectionUtility.GetNextMirrorMode(mirrorMode);
        }

        public static void CycleEditTarget()
        {
            EditTarget = editTarget == ImageLatticeEditTarget.Points ? ImageLatticeEditTarget.Cells : ImageLatticeEditTarget.Points;
        }

        public static void CycleSoftSelectionMode()
        {
            SoftSelectionMode = softSelectionMode switch {
                ImageLatticeSoftSelectionMode.Off => ImageLatticeSoftSelectionMode.Linear,
                ImageLatticeSoftSelectionMode.Linear => ImageLatticeSoftSelectionMode.Smooth,
                _ => ImageLatticeSoftSelectionMode.Off
            };
        }

        public static void RecordTransform(ImageLattice image, string undoName)
        {
            if (!undoActive) {
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(undoName);
                undoActive = true;
            }
        }

        public static void EndUndo()
        {
            if (!undoActive) {
                return;
            }

            Undo.CollapseUndoOperations(undoGroup);
            undoActive = false;
            undoGroup = -1;
        }

        public static void NotifyImageChanged(ImageLattice image)
        {
            if (image == null) {
                return;
            }

            if (activeImage == image) {
                UpdateActiveImageSignature(image);
                NormalizeSelection(activeImage.ControlPointColumns * activeImage.ControlPointRows);
                StoreSelectionUndoState();
            }

            SceneView.RepaintAll();
        }

        public static void ResetSelectedPoints()
        {
            ApplySelectionOperation("Reset Lattice Selection", ImageLatticeSelectionUtility.ResetPoints);
        }

        public static void RelaxSelectedPoints()
        {
            ApplySelectionOperation("Relax Lattice Selection", ImageLatticeSelectionUtility.RelaxPoints);
        }

        public static void RefreshAfterUndoRedo()
        {
            InteractionCancelled?.Invoke();
            EndUndo();
            RestoreSelectionUndoState();

            if (activeImage != null) {
                UpdateActiveImageSignature(activeImage);
                NormalizeSelection(activeImage.ControlPointColumns * activeImage.ControlPointRows);
                activeImage.UpdateRuntimeMaterialPayloadOrDirtyImage();
                EditorUtility.SetDirty(activeImage);
            }

            SceneView.RepaintAll();
        }

        private static void ApplyPointSelection(HashSet<int> nextSelection, int nextActivePoint, string undoName)
        {
            if (activePointIndex == nextActivePoint && PointSelectionEquals(nextSelection)) {
                SceneView.RepaintAll();
                return;
            }

            RecordSelectionUndo(undoName);
            SelectedPoints.Clear();
            foreach (var index in nextSelection) {
                SelectedPoints.Add(index);
            }

            activePointIndex = nextActivePoint;
            StoreSelectionUndoState();
            SceneView.RepaintAll();
        }

        private static void ApplyCellSelection(HashSet<int> nextSelection, int nextActiveCell, int controlPointColumns, int controlPointRows, string undoName)
        {
            if (activeCellIndex == nextActiveCell && CellSelectionEquals(nextSelection)) {
                SceneView.RepaintAll();
                return;
            }

            RecordSelectionUndo(undoName);
            SelectedCells.Clear();
            foreach (var index in nextSelection) {
                SelectedCells.Add(index);
            }

            activeCellIndex = nextActiveCell;
            RebuildExpandedCellSelection(controlPointColumns, controlPointRows);
            StoreSelectionUndoState();
            SceneView.RepaintAll();
        }

        private static void ApplySelectionOperation(string undoName,
            Action<Vector2[], Vector2[], int, int, ICollection<int>, ImageLatticeMirrorMode> operation)
        {
            if (activeImage == null || !HasSelection) {
                return;
            }

            var points = ImageLatticeSceneView.CapturePoints(activeImage);
            var destination = new Vector2[points.Length];
            var affected = GetAffectedPoints(activeImage.ControlPointColumns, activeImage.ControlPointRows);
            if (affected.Count == 0) {
                return;
            }

            RecordTransform(activeImage, undoName);
            operation(points, destination, activeImage.ControlPointColumns, activeImage.ControlPointRows, affected, mirrorMode);
            if (ImageLatticeSerializedPointUtility.ApplyPoints(activeImage, destination)) {
                activeImage.UpdateRuntimeMaterialPayloadOrDirtyImage();
                PrefabUtility.RecordPrefabInstancePropertyModifications(activeImage);
                EditorUtility.SetDirty(activeImage);
            }

            EndUndo();
            SceneView.RepaintAll();
        }

        private static void RecordSelectionUndo(string undoName)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RecordObject(GetSelectionUndoState(), undoName);
        }

        private static ImageLatticeSelectionUndoState GetSelectionUndoState()
        {
            if (selectionUndoState != null) {
                return selectionUndoState;
            }

            selectionUndoState = ScriptableObject.CreateInstance<ImageLatticeSelectionUndoState>();
            selectionUndoState.name = "Image Lattice Selection Undo State";
            selectionUndoState.hideFlags = HideFlags.HideAndDontSave;
            selectionUndoState.Store(activeImage, SelectedPoints, SelectedCells, activePointIndex, activeCellIndex);
            return selectionUndoState;
        }

        private static void StoreSelectionUndoState()
        {
            if (selectionUndoState != null) {
                selectionUndoState.Store(activeImage, SelectedPoints, SelectedCells, activePointIndex, activeCellIndex);
            }
        }

        private static void RestoreSelectionUndoState()
        {
            if (selectionUndoState == null || selectionUndoState.ActiveImage != activeImage) {
                return;
            }

            SelectedPoints.Clear();
            foreach (var index in selectionUndoState.SelectedPoints) {
                SelectedPoints.Add(index);
            }

            SelectedCells.Clear();
            foreach (var index in selectionUndoState.SelectedCells) {
                SelectedCells.Add(index);
            }

            activePointIndex = selectionUndoState.ActivePointIndex;
            activeCellIndex = selectionUndoState.ActiveCellIndex;
            if (activeImage != null) {
                NormalizeSelection(activeImage.ControlPointColumns * activeImage.ControlPointRows);
            }
        }

        private static void CancelInteraction(bool clearSelection)
        {
            InteractionCancelled?.Invoke();
            EndUndo();

            if (clearSelection) {
                SelectedPoints.Clear();
                SelectedCells.Clear();
                ExpandedSelectedCellPoints.Clear();
                AffectedPoints.Clear();
                AffectedCells.Clear();
                activePointIndex = -1;
                activeCellIndex = -1;
            }

            SceneView.RepaintAll();
        }

        private static bool HasActiveImageSignatureChanged(ImageLattice image)
        {
            return image != null &&
                   (activeControlPointColumns != image.ControlPointColumns ||
                    activeControlPointRows != image.ControlPointRows ||
                    activeSegmentsPerCell != image.SegmentsPerCell ||
                    activeImageType != image.GetComponent<Image>().type);
        }

        private static void UpdateActiveImageSignature(ImageLattice image)
        {
            if (image == null) {
                activeControlPointColumns = -1;
                activeControlPointRows = -1;
                activeSegmentsPerCell = -1;
                activeImageType = default;
                return;
            }

            activeControlPointColumns = image.ControlPointColumns;
            activeControlPointRows = image.ControlPointRows;
            activeSegmentsPerCell = image.SegmentsPerCell;
            activeImageType = image.GetComponent<Image>().type;
            RebuildExpandedCellSelection();
            SoftSelectionRadius = softSelectionRadius;
        }

        private static void RebuildExpandedCellSelection()
        {
            RebuildExpandedCellSelection(activeControlPointColumns, activeControlPointRows);
        }

        private static void RebuildExpandedCellSelection(int controlPointColumns, int controlPointRows)
        {
            ExpandedSelectedCellPoints.Clear();
            if (controlPointColumns < 2 || controlPointRows < 2) {
                return;
            }

            foreach (var cellIndex in SelectedCells) {
                ImageLatticeSelectionUtility.AddCellPoints(ExpandedSelectedCellPoints, cellIndex, controlPointColumns, controlPointRows);
            }
        }

        private static int GetActiveCellCount()
        {
            return activeControlPointColumns > 1 && activeControlPointRows > 1
                ? (activeControlPointColumns - 1) * (activeControlPointRows - 1)
                : 0;
        }

        private static int GetFirstSelectedPoint()
        {
            return GetFirstSelectedPoint(SelectedPoints);
        }

        private static int GetFirstSelectedPoint(ICollection<int> selection)
        {
            foreach (var selectedPoint in selection) {
                return selectedPoint;
            }

            return -1;
        }

        private static int GetFirstSelectedCell(ICollection<int> selection)
        {
            foreach (var selectedCell in selection) {
                return selectedCell;
            }

            return -1;
        }

        private static float SnapRadius(float value)
        {
            return Mathf.Round(value / 0.05f) * 0.05f;
        }

        private static bool PointSelectionEquals(HashSet<int> nextSelection)
        {
            return SelectionEquals(SelectedPoints, nextSelection);
        }

        private static bool CellSelectionEquals(HashSet<int> nextSelection)
        {
            return SelectionEquals(SelectedCells, nextSelection);
        }

        private static bool SelectionEquals(HashSet<int> currentSelection, HashSet<int> nextSelection)
        {
            if (currentSelection.Count != nextSelection.Count) {
                return false;
            }

            foreach (var index in currentSelection) {
                if (!nextSelection.Contains(index)) {
                    return false;
                }
            }

            return true;
        }

        private static void DestroySelectionUndoState()
        {
            if (selectionUndoState == null) {
                return;
            }

            UnityEngine.Object.DestroyImmediate(selectionUndoState);
            selectionUndoState = null;
        }
    }
}