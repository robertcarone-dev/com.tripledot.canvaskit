using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    internal static class ImageLatticeSceneView
    {
        private const float PointHandleSize = 0.07f;
        private const float SelectedPointHandleSize = 0.085f;
        private const float CellHandleSize = 0.12f;
        private const float PointHitDistance = 13f;
        private const float CellHitDistance = 17f;
        private const float SurfaceLineWidth = 1.25f;
        private const float ControlLineWidth = 2.35f;
        private const float BoundaryControlLineWidth = 3f;
        private const float RadiusPixelsPerUnit = 80f;

        private static readonly Color SurfaceLineColor = new Color(0.13f, 0.68f, 1f, 0.32f);
        private static readonly Color ControlLineColor = new Color(0.13f, 0.68f, 1f, 0.95f);
        private static readonly Color AxisColor = new Color(1f, 1f, 1f, 0.72f);
        private static readonly Color PointColor = new Color(1f, 1f, 1f, 0.88f);
        private static readonly Color HoverColor = new Color(1f, 0.86f, 0.35f, 0.95f);
        private static readonly Color SelectedColor = new Color(1f, 0.55f, 0.14f, 0.98f);
        private static readonly Color MirrorColor = new Color(1f, 0.55f, 0.14f, 0.46f);
        private static readonly Color FalloffColor = new Color(0.38f, 0.86f, 1f, 0.46f);
        private static readonly Color CellFalloffFillColor = new Color(0.13f, 0.68f, 1f, 0.15f);
        private static readonly Color CellSelectedFillColor = new Color(1f, 0.55f, 0.14f, 0.16f);
        private static readonly Color CellMirrorFillColor = new Color(1f, 0.55f, 0.14f, 0.08f);
        private static readonly Color RadiusColor = new Color(0.38f, 0.86f, 1f, 0.88f);
        private static readonly Color RadiusDisabledColor = new Color(0.72f, 0.78f, 0.82f, 0.48f);
        private static readonly Color BoundsColor = new Color(1f, 0.55f, 0.14f, 0.95f);
        private static readonly Color BoundsFillColor = new Color(1f, 0.55f, 0.14f, 0.08f);
        private static readonly Color MarqueeFillColor = new Color(0.13f, 0.68f, 1f, 0.12f);
        private static readonly Color MarqueeLineColor = new Color(0.13f, 0.68f, 1f, 0.85f);

        private static readonly List<int> DrawPoints = new List<int>(64);
        private static readonly List<int> DrawCells = new List<int>(64);

        private static Vector3[] curvePoints = new Vector3[64];
        private static Vector2[] cellCenters = Array.Empty<Vector2>();
        private static Vector2[] guiPoints = Array.Empty<Vector2>();
        private static readonly Vector3[] SelectionCorners = new Vector3[5];
        private static readonly Vector3[] CellFillCorners = new Vector3[4];
        private static int marqueeControl;
        private static Vector2 marqueeStartGui;
        private static Vector2 marqueeCurrentGui;
        private static int cellDragControl;
        private static Vector2[] cellDragSourcePoints;
        private static Vector2[] cellDragDestinationPoints;
        private static Vector3 cellDragStartWorld;
        private static int radiusDragControl;
        private static Vector2 radiusDragStartGui;
        private static float radiusDragStartValue;

        public static bool TryGetLocalRect(ImageLattice image, out Rect localRect)
        {
            if (image.GetComponent<UnityEngine.UI.Image>().type != UnityEngine.UI.Image.Type.Simple) {
                localRect = default;
                return false;
            }

            localRect = image.GetLatticeLocalRect();
            return localRect.width > 0f &&
                   localRect.height > 0f;
        }

        public static bool HandleSelectionInput(ImageLattice image, Rect localRect)
        {
            var evt = Event.current;
            if (evt.type == EventType.Layout) {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                return false;
            }

            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape) {
                EndCellDrag();
                EndRadiusDrag();
                ImageLatticeToolState.ClearSelection();
                evt.Use();
                return true;
            }

            if (HandleRadiusInput(evt)) {
                return true;
            }

            if (cellDragControl != 0 && GUIUtility.hotControl == cellDragControl) {
                return ContinueCellDrag(image, localRect, evt);
            }

            if (marqueeControl != 0 && GUIUtility.hotControl == marqueeControl) {
                return ContinueMarquee(image, localRect, evt);
            }

            if (GUIUtility.hotControl != 0 || evt.type != EventType.MouseDown || evt.button != 0 || evt.alt) {
                return false;
            }

            if (ImageLatticeToolState.EditTarget == ImageLatticeEditTarget.Cells) {
                var cellIndex = FindNearestCell(image, localRect, evt.mousePosition);
                if (cellIndex >= 0) {
                    return BeginCellInteraction(image, cellIndex, evt);
                }

                BeginMarquee(evt);
                return true;
            }

            var pointIndex = FindNearestPoint(image, localRect, evt.mousePosition);
            if (pointIndex >= 0) {
                if (evt.shift) {
                    ImageLatticeToolState.ToggleSelection(pointIndex);
                } else if (ImageLatticeToolState.IsSelected(pointIndex)) {
                    ImageLatticeToolState.SetActivePoint(pointIndex);
                } else {
                    ImageLatticeToolState.SelectSingle(pointIndex);
                }

                evt.Use();
                return true;
            }

            BeginMarquee(evt);
            return true;
        }

        public static bool HandlePriorityInput()
        {
            return HandleRadiusInput(Event.current);
        }

        public static void Draw(ImageLattice image, Rect localRect)
        {
            var hoveredPoint = GUIUtility.hotControl == 0 && ImageLatticeToolState.EditTarget == ImageLatticeEditTarget.Points
                ? FindNearestPoint(image, localRect, Event.current.mousePosition)
                : -1;
            var hoveredCell = GUIUtility.hotControl == 0 && ImageLatticeToolState.EditTarget == ImageLatticeEditTarget.Cells
                ? FindNearestCell(image, localRect, Event.current.mousePosition)
                : -1;

            DrawGrid(image, localRect);
            DrawCellFalloffOverlay(image, localRect);
            DrawRadiusOverlay(image, localRect, hoveredPoint, hoveredCell);
            DrawSelectionOverlay(image, localRect);
            if (ImageLatticeToolState.EditTarget == ImageLatticeEditTarget.Cells) {
                DrawCellHandles(image, localRect, hoveredCell);
            } else {
                DrawPointHandles(image, localRect, hoveredPoint);
            }
            DrawRadiusHud();
            DrawMarquee();
        }

        public static Vector2[] CapturePoints(ImageLattice image)
        {
            var points = new Vector2[image.ControlPointColumns * image.ControlPointRows];
            for (var y = 0; y < image.ControlPointRows; y++) {
                for (var x = 0; x < image.ControlPointColumns; x++) {
                    points[ImageLatticeSelectionUtility.GetPointIndex(x, y, image.ControlPointColumns)] = image.GetLatticePoint(x, y);
                }
            }

            return points;
        }

        public static bool TryGetAffectedBounds(ImageLattice image, Vector2[] points, out Rect bounds)
        {
            bounds = default;
            if (!ImageLatticeToolState.HasSelection) {
                return false;
            }

            var affected = ImageLatticeToolState.GetAffectedPoints(image.ControlPointColumns, image.ControlPointRows);
            bounds = ImageLatticeSelectionUtility.CalculateBounds(points, affected);
            return true;
        }

        public static bool TryGetSelectionCenter(ImageLattice image, Vector2[] points, out Vector2 center)
        {
            center = default;
            if (!ImageLatticeToolState.HasSelection) {
                return false;
            }

            center = ImageLatticeSelectionUtility.CalculateCenter(points, ImageLatticeToolState.Selection);
            return true;
        }

        public static Vector3 LatticeUvToWorld(ImageLattice image, Rect localRect, Vector2 uv)
        {
            var local = new Vector3(
                localRect.xMin + uv.x * localRect.width,
                localRect.yMin + uv.y * localRect.height,
                0f);
            return ((RectTransform)image.transform).TransformPoint(local);
        }

        public static Vector2 WorldToLatticeUv(ImageLattice image, Rect localRect, Vector3 world)
        {
            var local = ((RectTransform)image.transform).InverseTransformPoint(world);
            return new Vector2(
                localRect.width > 0f ? (local.x - localRect.xMin) / localRect.width : 0f,
                localRect.height > 0f ? (local.y - localRect.yMin) / localRect.height : 0f);
        }

        private static bool BeginCellInteraction(ImageLattice image, int cellIndex, Event evt)
        {
            if (evt.shift) {
                ImageLatticeToolState.ToggleCellSelection(cellIndex, image.ControlPointColumns, image.ControlPointRows);
            } else if (ImageLatticeToolState.IsCellSelected(cellIndex)) {
                ImageLatticeToolState.SetActiveCell(cellIndex, image.ControlPointColumns, image.ControlPointRows);
            } else {
                ImageLatticeToolState.SelectCell(cellIndex, image.ControlPointColumns, image.ControlPointRows);
            }

            if (!ImageLatticeToolState.IsCellSelected(cellIndex) ||
                !TryGuiPointToWorld(image, evt.mousePosition, out cellDragStartWorld)) {
                evt.Use();
                return true;
            }

            cellDragControl = GUIUtility.GetControlID(FocusType.Passive);
            GUIUtility.hotControl = cellDragControl;
            cellDragSourcePoints = null;
            cellDragDestinationPoints = null;
            evt.Use();
            return true;
        }

        private static bool ContinueCellDrag(ImageLattice image, Rect localRect, Event evt)
        {
            var eventType = evt.GetTypeForControl(cellDragControl);
            switch (eventType) {
                case EventType.MouseDrag: {
                    if (!TryGuiPointToWorld(image, evt.mousePosition, out var currentWorld)) {
                        return false;
                    }

                    if (cellDragSourcePoints == null) {
                        cellDragSourcePoints = CapturePoints(image);
                        cellDragDestinationPoints = new Vector2[cellDragSourcePoints.Length];
                        ImageLatticeToolState.RecordTransform(image, "Move Lattice Cell");
                    }

                    var delta = WorldToLatticeUv(image, localRect, currentWorld) - WorldToLatticeUv(image, localRect, cellDragStartWorld);
                    ImageLatticeSelectionUtility.MovePoints(
                        source: cellDragSourcePoints,
                        destination: cellDragDestinationPoints,
                        controlPointColumns: image.ControlPointColumns,
                        controlPointRows: image.ControlPointRows,
                        selected: ImageLatticeToolState.Selection,
                        delta: delta,
                        mirrorMode: ImageLatticeToolState.MirrorMode,
                        editTarget: ImageLatticeToolState.EditTarget,
                        softSelectionMode: ImageLatticeToolState.SoftSelectionMode,
                        softSelectionRadius: ImageLatticeToolState.SoftSelectionRadius,
                        selectedCells: ImageLatticeToolState.CellSelection);
                    ApplyDraggedPoints(image, cellDragDestinationPoints);
                    evt.Use();
                    return false;
                }
                case EventType.MouseUp:
                case EventType.Ignore: {
                    EndCellDrag();
                    evt.Use();
                    SceneView.RepaintAll();
                    return true;
                }
            }

            return false;
        }

        private static bool HandleRadiusInput(Event evt)
        {
            if (radiusDragControl != 0 && GUIUtility.hotControl == radiusDragControl) {
                if (!ImageLatticeToolState.IsRadiusClutchActive) {
                    EndRadiusDrag();
                    return false;
                }

                var eventType = evt.GetTypeForControl(radiusDragControl);
                switch (eventType) {
                    case EventType.MouseDrag: {
                        ImageLatticeToolState.SoftSelectionRadius = radiusDragStartValue + (evt.mousePosition.x - radiusDragStartGui.x) / RadiusPixelsPerUnit;
                        evt.Use();
                        SceneView.RepaintAll();
                        return true;
                    }
                    case EventType.MouseUp:
                    case EventType.Ignore: {
                        EndRadiusDrag();
                        evt.Use();
                        SceneView.RepaintAll();
                        return true;
                    }
                }

                return false;
            }

            if (!ImageLatticeToolState.IsRadiusClutchActive ||
                evt.type != EventType.MouseDown ||
                evt.button != 0 ||
                evt.alt) {
                return false;
            }

            radiusDragControl = GUIUtility.GetControlID(FocusType.Passive);
            GUIUtility.hotControl = radiusDragControl;
            radiusDragStartGui = evt.mousePosition;
            radiusDragStartValue = ImageLatticeToolState.SoftSelectionRadius;
            evt.Use();
            SceneView.RepaintAll();
            return true;
        }

        private static bool ContinueMarquee(ImageLattice image, Rect localRect, Event evt)
        {
            var eventType = evt.GetTypeForControl(marqueeControl);
            switch (eventType) {
                case EventType.MouseDrag: {
                    marqueeCurrentGui = evt.mousePosition;
                    evt.Use();
                    SceneView.RepaintAll();
                    return false;
                }
                case EventType.MouseUp: {
                    ApplyMarqueeSelection(image, localRect, evt.shift);
                    EndMarquee();
                    evt.Use();
                    SceneView.RepaintAll();
                    return true;
                }
            }

            return false;
        }

        private static void BeginMarquee(Event evt)
        {
            marqueeControl = GUIUtility.GetControlID(FocusType.Passive);
            GUIUtility.hotControl = marqueeControl;
            marqueeStartGui = evt.mousePosition;
            marqueeCurrentGui = evt.mousePosition;
            evt.Use();
        }

        private static void EndMarquee()
        {
            if (GUIUtility.hotControl == marqueeControl) {
                GUIUtility.hotControl = 0;
            }

            marqueeControl = 0;
        }

        private static void ApplyMarqueeSelection(ImageLattice image, Rect localRect, bool additive)
        {
            var marquee = GetMarqueeRect();
            if (ImageLatticeToolState.EditTarget == ImageLatticeEditTarget.Cells) {
                var cells = CaptureCellCenters(image);
                var guiCells = GetGuiPointBuffer(cells.Length);
                for (var i = 0; i < cells.Length; i++) {
                    guiCells[i] = HandleUtility.WorldToGUIPoint(LatticeUvToWorld(image, localRect, cells[i]));
                }

                ImageLatticeToolState.SelectCellsInRect(guiCells, marquee, additive, image.ControlPointColumns, image.ControlPointRows);
                return;
            }

            var points = CapturePoints(image);
            var guiPoints = GetGuiPointBuffer(points.Length);
            for (var i = 0; i < points.Length; i++) {
                guiPoints[i] = HandleUtility.WorldToGUIPoint(LatticeUvToWorld(image, localRect, points[i]));
            }

            ImageLatticeToolState.SelectInRect(guiPoints, marquee, additive);
        }

        private static void DrawGrid(ImageLattice image, Rect localRect)
        {
            var xSegments = GetSurfaceSegmentCount(image.ControlPointColumns, image.SegmentsPerCell);
            var ySegments = GetSurfaceSegmentCount(image.ControlPointRows, image.SegmentsPerCell);
            DrawSurfaceGrid(image, localRect, xSegments, ySegments);
            DrawControlGrid(image, localRect, xSegments, ySegments);
        }

        private static void DrawSurfaceGrid(ImageLattice image, Rect localRect, int xSegments, int ySegments)
        {
            Handles.color = SurfaceLineColor;

            for (var y = 0; y <= ySegments; y++) {
                DrawHorizontalGridLine(image, localRect, y / (float)ySegments, xSegments + 1, SurfaceLineWidth);
            }

            for (var x = 0; x <= xSegments; x++) {
                DrawVerticalGridLine(image, localRect, x / (float)xSegments, ySegments + 1, SurfaceLineWidth);
            }
        }

        private static void DrawControlGrid(ImageLattice image, Rect localRect, int xSegments, int ySegments)
        {
            var controlPointColumns = image.ControlPointColumns;
            var controlPointRows = image.ControlPointRows;

            Handles.color = ControlLineColor;
            for (var y = 0; y < controlPointRows; y++) {
                var width = y == 0 || y == controlPointRows - 1 ? BoundaryControlLineWidth : ControlLineWidth;
                DrawHorizontalGridLine(image, localRect, y / (float)(controlPointRows - 1), xSegments + 1, width);
            }

            for (var x = 0; x < controlPointColumns; x++) {
                var width = x == 0 || x == controlPointColumns - 1 ? BoundaryControlLineWidth : ControlLineWidth;
                DrawVerticalGridLine(image, localRect, x / (float)(controlPointColumns - 1), ySegments + 1, width);
            }
        }

        private static void DrawHorizontalGridLine(ImageLattice image, Rect localRect, float v, int pointCount, float width)
        {
            EnsureCurveCapacity(pointCount);
            for (var i = 0; i < pointCount; i++) {
                var u = i / (float)(pointCount - 1);
                curvePoints[i] = LatticeUvToWorld(image, localRect, image.EvaluateLattice(new Vector2(u, v)));
            }

            Handles.DrawAAPolyLine(width, pointCount, curvePoints);
        }

        private static void DrawVerticalGridLine(ImageLattice image, Rect localRect, float u, int pointCount, float width)
        {
            EnsureCurveCapacity(pointCount);
            for (var i = 0; i < pointCount; i++) {
                var v = i / (float)(pointCount - 1);
                curvePoints[i] = LatticeUvToWorld(image, localRect, image.EvaluateLattice(new Vector2(u, v)));
            }

            Handles.DrawAAPolyLine(width, pointCount, curvePoints);
        }

        private static int GetSurfaceSegmentCount(int controlPointCount, int segmentsPerCell)
        {
            return Mathf.Max(1, (controlPointCount - 1) * segmentsPerCell);
        }

        private static void DrawCellFalloffOverlay(ImageLattice image, Rect localRect)
        {
            if (ImageLatticeToolState.EditTarget != ImageLatticeEditTarget.Cells ||
                Event.current.type != EventType.Repaint) {
                return;
            }

            var cellCount = (image.ControlPointColumns - 1) * (image.ControlPointRows - 1);
            var affectedCells = ImageLatticeToolState.GetAffectedCells(image.ControlPointColumns, image.ControlPointRows);
            for (var i = 0; i < cellCount; i++) {
                var selected = ImageLatticeToolState.CellSelection.Contains(i);
                var affected = !selected && affectedCells.Contains(i);
                var weight = GetCellSoftSelectionWeight(i, image.ControlPointColumns, image.ControlPointRows);
                if (!selected && !affected && weight <= 0f) {
                    continue;
                }

                var color = selected ? CellSelectedFillColor :
                    affected ? CellMirrorFillColor :
                    new Color(CellFalloffFillColor.r, CellFalloffFillColor.g, CellFalloffFillColor.b, CellFalloffFillColor.a * Mathf.Clamp01(weight));
                Handles.color = color;
                DrawCellFill(image, localRect, i);
            }
        }

        private static void DrawSelectionOverlay(ImageLattice image, Rect localRect)
        {
            var points = CapturePoints(image);
            if (!TryGetAffectedBounds(image, points, out var bounds)) {
                return;
            }

            var min = new Vector2(bounds.xMin, bounds.yMin);
            var max = new Vector2(bounds.xMax, bounds.yMax);
            
            SelectionCorners[0] = LatticeUvToWorld(image, localRect, min);
            SelectionCorners[1] = LatticeUvToWorld(image, localRect, new Vector2(max.x, min.y));
            SelectionCorners[2] = LatticeUvToWorld(image, localRect, max);
            SelectionCorners[3] = LatticeUvToWorld(image, localRect, new Vector2(min.x, max.y));
            SelectionCorners[4] = SelectionCorners[0];

            if (Event.current.type == EventType.Repaint && bounds.width > 0f && bounds.height > 0f) {
                Handles.color = BoundsFillColor;
                Handles.DrawAAConvexPolygon(SelectionCorners[0], SelectionCorners[1], SelectionCorners[2], SelectionCorners[3]);
            }

            Handles.color = BoundsColor;
            Handles.DrawAAPolyLine(3f, SelectionCorners);
        }

        private static void DrawPointHandles(ImageLattice image, Rect localRect, int hovered)
        {
            var pointCount = image.ControlPointColumns * image.ControlPointRows;
            var affectedPoints = ImageLatticeToolState.GetAffectedPoints(image.ControlPointColumns, image.ControlPointRows);

            DrawPoints.Clear();
            for (var i = 0; i < pointCount; i++) {
                if (!ImageLatticeToolState.Selection.Contains(i) && !affectedPoints.Contains(i)) {
                    DrawPoints.Add(i);
                }
            }

            foreach (var point in DrawPoints) {
                DrawPointHandle(image, localRect, point, hovered, affectedPoints);
            }

            foreach (var index in affectedPoints) {
                if (!ImageLatticeToolState.Selection.Contains(index)) {
                    DrawPointHandle(image, localRect, index, hovered, affectedPoints);
                }
            }

            foreach (var index in ImageLatticeToolState.Selection) {
                DrawPointHandle(image, localRect, index, hovered, affectedPoints);
            }
        }

        private static void DrawPointHandle(ImageLattice image, Rect localRect, int index, int hovered, List<int> affectedPoints)
        {
            var x = index % image.ControlPointColumns;
            var y = index / image.ControlPointColumns;
            
            var point = image.GetLatticePoint(x, y);
            var world = LatticeUvToWorld(image, localRect, point);
            
            var selected = ImageLatticeToolState.Selection.Contains(index);
            var affected = !selected && affectedPoints.Contains(index);
            
            var falloffWeight = GetSoftSelectionWeight(index, image.ControlPointColumns, image.ControlPointRows);
            var handleSize = HandleUtility.GetHandleSize(world) * (selected ? SelectedPointHandleSize : PointHandleSize + PointHandleSize * 0.25f * falloffWeight);

            if (selected) {
                Handles.color = SelectedColor;
            } else if (affected) {
                Handles.color = MirrorColor;
            } else if (hovered == index) {
                Handles.color = HoverColor;
            } else if (falloffWeight > 0f) {
                Handles.color = Color.Lerp(PointColor, FalloffColor, falloffWeight);
            } else if (IsBoundaryPoint(image, x, y)) {
                Handles.color = AxisColor;
            } else {
                Handles.color = PointColor;
            }

            Handles.CircleHandleCap(0, world, Quaternion.identity, handleSize, EventType.Repaint);
        }

        private static void DrawCellHandles(ImageLattice image, Rect localRect, int hovered)
        {
            if (ImageLatticeToolState.EditTarget != ImageLatticeEditTarget.Cells) {
                return;
            }

            var cellCenters = CaptureCellCenters(image);
            var affectedCells = ImageLatticeToolState.GetAffectedCells(image.ControlPointColumns, image.ControlPointRows);

            DrawCells.Clear();
            for (var i = 0; i < cellCenters.Length; i++) {
                if (!ImageLatticeToolState.CellSelection.Contains(i) && !affectedCells.Contains(i)) {
                    DrawCells.Add(i);
                }
            }

            foreach (var cell in DrawCells) {
                DrawCellHandle(image, localRect, cellCenters[cell], cell, hovered, affectedCells);
            }

            foreach (var index in affectedCells) {
                if (!ImageLatticeToolState.CellSelection.Contains(index)) {
                    DrawCellHandle(image, localRect, cellCenters[index], index, hovered, affectedCells);
                }
            }

            foreach (var index in ImageLatticeToolState.CellSelection) {
                DrawCellHandle(image, localRect, cellCenters[index], index, hovered, affectedCells);
            }
        }

        private static void DrawCellHandle(ImageLattice image, Rect localRect, Vector2 center, int cellIndex, int hovered, List<int> affectedCells)
        {
            var world = LatticeUvToWorld(image, localRect, center);
            var selected = ImageLatticeToolState.CellSelection.Contains(cellIndex);
            var affected = !selected && affectedCells.Contains(cellIndex);
            var falloffWeight = GetCellSoftSelectionWeight(cellIndex, image.ControlPointColumns, image.ControlPointRows);
            var size = HandleUtility.GetHandleSize(world) * (CellHandleSize + CellHandleSize * 0.2f * falloffWeight);

            if (selected) {
                Handles.color = SelectedColor;
            } else if (affected) {
                Handles.color = MirrorColor;
            } else if (hovered == cellIndex) {
                Handles.color = HoverColor;
            } else if (falloffWeight > 0f) {
                Handles.color = Color.Lerp(PointColor, FalloffColor, falloffWeight);
            } else {
                Handles.color = PointColor;
            }

            Handles.RectangleHandleCap(0, world, Quaternion.identity, size, EventType.Repaint);
        }

        private static void DrawRadiusOverlay(ImageLattice image, Rect localRect, int hoveredPoint, int hoveredCell)
        {
            if (ImageLatticeToolState.SoftSelectionMode == ImageLatticeSoftSelectionMode.Off &&
                !ImageLatticeToolState.IsRadiusClutchActive) {
                return;
            }

            if (ImageLatticeToolState.EditTarget == ImageLatticeEditTarget.Cells) {
                var cellIndex = GetRadiusAnchorCell(hoveredCell);
                if (cellIndex >= 0) {
                    DrawCellRadiusContour(image, localRect, cellIndex);
                }
                return;
            }

            var pointIndex = hoveredPoint >= 0 ? hoveredPoint : ImageLatticeToolState.ActivePointIndex;
            if (pointIndex >= 0) {
                DrawPointRadiusContour(image, localRect, pointIndex);
            }
        }

        private static void DrawPointRadiusContour(ImageLattice image, Rect localRect, int pointIndex)
        {
            var x = pointIndex % image.ControlPointColumns;
            var y = pointIndex / image.ControlPointColumns;
            DrawGridRadiusContour(image, localRect, new Vector2(x, y), false);
        }

        private static void DrawCellRadiusContour(ImageLattice image, Rect localRect, int cellIndex)
        {
            var cellColumns = image.ControlPointColumns - 1;
            var x = cellIndex % cellColumns;
            var y = cellIndex / cellColumns;
            DrawGridRadiusContour(image, localRect, new Vector2(x, y), true);
        }

        private static void DrawGridRadiusContour(ImageLattice image, Rect localRect, Vector2 center, bool cellSpace)
        {
            const int sampleCount = 80;
            EnsureCurveCapacity(sampleCount + 1);

            for (var i = 0; i <= sampleCount; i++) {
                var t = i / (float)sampleCount * Mathf.PI * 2f;
                var grid = center + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * ImageLatticeToolState.SoftSelectionRadius;
                Vector2 uv;
                if (cellSpace) {
                    uv = new Vector2(
                        (grid.x + 0.5f) / Mathf.Max(1, image.ControlPointColumns - 1),
                        (grid.y + 0.5f) / Mathf.Max(1, image.ControlPointRows - 1));
                } else {
                    uv = new Vector2(
                        grid.x / Mathf.Max(1, image.ControlPointColumns - 1),
                        grid.y / Mathf.Max(1, image.ControlPointRows - 1));
                }

                uv.x = Mathf.Clamp01(uv.x);
                uv.y = Mathf.Clamp01(uv.y);
                curvePoints[i] = LatticeUvToWorld(image, localRect, image.EvaluateLattice(uv));
            }

            Handles.color = ImageLatticeToolState.SoftSelectionMode == ImageLatticeSoftSelectionMode.Off
                ? RadiusDisabledColor
                : RadiusColor;
            Handles.DrawAAPolyLine(2.75f, sampleCount + 1, curvePoints);
        }

        private static void DrawRadiusHud()
        {
            if (radiusDragControl == 0 ||
                GUIUtility.hotControl != radiusDragControl ||
                Event.current.type != EventType.Repaint) {
                return;
            }

            Handles.BeginGUI();
            {
                var label = ImageLatticeToolState.SoftSelectionMode == ImageLatticeSoftSelectionMode.Off
                    ? $"Radius {ImageLatticeToolState.SoftSelectionRadius:0.##} (Off)"
                    : $"Radius {ImageLatticeToolState.SoftSelectionRadius:0.##}";
            
                var rect = new Rect(
                    Event.current.mousePosition.x + 14f, 
                    Event.current.mousePosition.y + 14f, 
                    ImageLatticeToolState.SoftSelectionMode == ImageLatticeSoftSelectionMode.Off ? 120f : 88f,
                    22f);
            
                EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.72f));
                GUI.Label(rect, label, EditorStyles.whiteLabel);
            }
            Handles.EndGUI();
        }

        private static void DrawMarquee()
        {
            if (marqueeControl != 0) {
                Handles.BeginGUI();
                var rect = GetMarqueeRect();
                EditorGUI.DrawRect(rect, MarqueeFillColor);
                DrawGuiRectOutline(rect, MarqueeLineColor);
                Handles.EndGUI();
            }
        }

        private static void DrawGuiRectOutline(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMax, rect.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, 1f, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax, rect.yMin, 1f, rect.height + 1f), Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private static int FindNearestPoint(ImageLattice image, Rect localRect, Vector2 mousePosition)
        {
            var bestIndex = -1;
            var bestDistance = PointHitDistance;
            
            for (var y = 0; y < image.ControlPointRows; y++) {
                for (var x = 0; x < image.ControlPointColumns; x++) {
                    var index = ImageLatticeSelectionUtility.GetPointIndex(x, y, image.ControlPointColumns);
                    var world = LatticeUvToWorld(image, localRect, image.GetLatticePoint(x, y));
                    var distance = Vector2.Distance(mousePosition, HandleUtility.WorldToGUIPoint(world));
                    if (distance > bestDistance) {
                        continue;
                    }

                    bestIndex = index;
                    bestDistance = distance;
                }
            }

            return bestIndex;
        }

        private static int FindNearestCell(ImageLattice image, Rect localRect, Vector2 mousePosition)
        {
            var cellCenters = CaptureCellCenters(image);
            var bestIndex = -1;
            var bestDistance = CellHitDistance;
            
            for (var i = 0; i < cellCenters.Length; i++) {
                var world = LatticeUvToWorld(image, localRect, cellCenters[i]);
                var distance = Vector2.Distance(mousePosition, HandleUtility.WorldToGUIPoint(world));
                if (distance > bestDistance) {
                    continue;
                }

                bestIndex = i;
                bestDistance = distance;
            }

            return bestIndex;
        }

        private static Vector2[] CaptureCellCenters(ImageLattice image)
        {
            var cellColumns = image.ControlPointColumns - 1;
            var cellRows = image.ControlPointRows - 1;
            var cellCount = cellColumns * cellRows;
            
            if (cellCenters.Length != cellCount) {
                cellCenters = new Vector2[cellCount];
            }

            for (var y = 0; y < cellRows; y++) {
                for (var x = 0; x < cellColumns; x++) {
                    var index = ImageLatticeSelectionUtility.GetCellIndex(x, y, cellColumns);
                    cellCenters[index] = image.EvaluateLattice(ImageLatticeSelectionUtility.GetCellCenterUv(index, image.ControlPointColumns, image.ControlPointRows));
                }
            }

            return cellCenters;
        }

        private static void DrawCellFill(ImageLattice image, Rect localRect, int cellIndex)
        {
            var cellColumns = image.ControlPointColumns - 1;
            var x = cellIndex % cellColumns;
            var y = cellIndex / cellColumns;
            
            CellFillCorners[0] = LatticeUvToWorld(image, localRect, image.GetLatticePoint(x, y));
            CellFillCorners[1] = LatticeUvToWorld(image, localRect, image.GetLatticePoint(x + 1, y));
            CellFillCorners[2] = LatticeUvToWorld(image, localRect, image.GetLatticePoint(x + 1, y + 1));
            CellFillCorners[3] = LatticeUvToWorld(image, localRect, image.GetLatticePoint(x, y + 1));

            Handles.DrawAAConvexPolygon(CellFillCorners);
        }

        private static int GetRadiusAnchorCell(int hoveredCell)
        {
            return hoveredCell >= 0 ? hoveredCell : ImageLatticeToolState.ActiveCellIndex;
        }

        private static float GetSoftSelectionWeight(int index, int controlPointColumns, int controlPointRows)
        {
            return ImageLatticeSelectionUtility.GetSoftSelectionWeight(
                index,
                controlPointColumns,
                controlPointRows,
                ImageLatticeToolState.Selection,
                ImageLatticeToolState.MirrorMode,
                ImageLatticeToolState.EditTarget,
                ImageLatticeToolState.SoftSelectionMode,
                ImageLatticeToolState.SoftSelectionRadius,
                ImageLatticeToolState.CellSelection);
        }

        private static float GetCellSoftSelectionWeight(int index, int controlPointColumns, int controlPointRows)
        {
            return ImageLatticeSelectionUtility.GetCellSoftSelectionWeight(
                index,
                controlPointColumns,
                controlPointRows,
                ImageLatticeToolState.CellSelection,
                ImageLatticeToolState.MirrorMode,
                ImageLatticeToolState.SoftSelectionMode,
                ImageLatticeToolState.SoftSelectionRadius);
        }

        private static bool TryGuiPointToWorld(ImageLattice image, Vector2 mousePosition, out Vector3 world)
        {
            var rectTransform = (RectTransform)image.transform;
            var ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            
            var plane = new Plane(rectTransform.forward, rectTransform.position);
            if (plane.Raycast(ray, out var enter)) {
                world = ray.GetPoint(enter);
                return true;
            }

            world = default;
            return false;
        }

        private static void ApplyDraggedPoints(ImageLattice image, Vector2[] points)
        {
            if (ImageLatticeSerializedPointUtility.ApplyPoints(image, points)) {
                image.UpdateRuntimeMaterialPayloadOrDirtyImage();
                PrefabUtility.RecordPrefabInstancePropertyModifications(image);
                EditorUtility.SetDirty(image);
                SceneView.RepaintAll();
            }
        }

        private static void EndCellDrag()
        {
            if (GUIUtility.hotControl == cellDragControl) {
                GUIUtility.hotControl = 0;
            }

            cellDragControl = 0;
            cellDragSourcePoints = null;
            cellDragDestinationPoints = null;
            ImageLatticeToolState.EndUndo();
        }

        private static void EndRadiusDrag()
        {
            if (GUIUtility.hotControl == radiusDragControl) {
                GUIUtility.hotControl = 0;
            }

            radiusDragControl = 0;
        }

        private static bool IsBoundaryPoint(ImageLattice image, int x, int y)
        {
            return x == 0 || y == 0 || x == image.ControlPointColumns - 1 || y == image.ControlPointRows - 1;
        }

        private static Rect GetMarqueeRect()
        {
            return Rect.MinMaxRect(
                Mathf.Min(marqueeStartGui.x, marqueeCurrentGui.x),
                Mathf.Min(marqueeStartGui.y, marqueeCurrentGui.y),
                Mathf.Max(marqueeStartGui.x, marqueeCurrentGui.x),
                Mathf.Max(marqueeStartGui.y, marqueeCurrentGui.y));
        }

        private static void EnsureCurveCapacity(int capacity)
        {
            if (curvePoints.Length < capacity) {
                curvePoints = new Vector3[Mathf.NextPowerOfTwo(capacity)];
            }
        }

        private static Vector2[] GetGuiPointBuffer(int count)
        {
            if (guiPoints.Length != count) {
                guiPoints = new Vector2[count];
            }

            return guiPoints;
        }
    }
}