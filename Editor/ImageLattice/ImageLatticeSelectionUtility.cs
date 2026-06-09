using System.Collections.Generic;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    internal enum ImageLatticeMirrorMode
    {
        Off = 0,
        Horizontal = 1,
        Vertical = 2,
        Both = 3
    }

    internal static class ImageLatticeSelectionUtility
    {
        public static int GetPointIndex(int x, int y, int controlPointColumns)
        {
            return y * controlPointColumns + x;
        }

        public static int GetCellIndex(int x, int y, int cellColumns)
        {
            return y * cellColumns + x;
        }

        public static Vector2 GetCellCenterUv(int cellIndex, int controlPointColumns, int controlPointRows)
        {
            var cellColumns = Mathf.Max(1, controlPointColumns - 1);
            var x = cellIndex % cellColumns;
            var y = cellIndex / cellColumns;
            return new Vector2(
                (x + 0.5f) / Mathf.Max(1, controlPointColumns - 1),
                (y + 0.5f) / Mathf.Max(1, controlPointRows - 1));
        }

        public static Vector2 GetIdentityPoint(int index, int controlPointColumns, int controlPointRows)
        {
            var x = index % controlPointColumns;
            var y = index / controlPointColumns;
            return new Vector2(
                controlPointColumns > 1 ? x / (float)(controlPointColumns - 1) : 0f,
                controlPointRows > 1 ? y / (float)(controlPointRows - 1) : 0f);
        }

        public static void SelectSingle(HashSet<int> selection, int index)
        {
            selection.Clear();
            selection.Add(index);
        }

        public static void ToggleSelection(HashSet<int> selection, int index)
        {
            if (!selection.Add(index)) {
                selection.Remove(index);
            }
        }

        public static void SelectInRect(Vector2[] points, Rect rect, HashSet<int> selection, bool additive)
        {
            if (!additive) {
                selection.Clear();
            }

            for (var i = 0; i < points.Length; i++) {
                if (rect.Contains(points[i], true)) {
                    selection.Add(i);
                }
            }
        }

        public static void AddCellPoints(ICollection<int> selection, int cellIndex, int controlPointColumns, int controlPointRows)
        {
            var cellColumns = controlPointColumns - 1;
            var cellRows = controlPointRows - 1;
            if (cellColumns <= 0 || cellRows <= 0 || cellIndex < 0 || cellIndex >= cellColumns * cellRows) {
                return;
            }

            var x = cellIndex % cellColumns;
            var y = cellIndex / cellColumns;
            AddUnique(selection, GetPointIndex(x, y, controlPointColumns));
            AddUnique(selection, GetPointIndex(x + 1, y, controlPointColumns));
            AddUnique(selection, GetPointIndex(x, y + 1, controlPointColumns));
            AddUnique(selection, GetPointIndex(x + 1, y + 1, controlPointColumns));
        }

        public static bool IsCellSelected(ICollection<int> selection, int cellIndex)
        {
            return selection.Contains(cellIndex);
        }

        public static void CollectAffectedPoints(ICollection<int> selected, int controlPointColumns, int controlPointRows, ImageLatticeMirrorMode mirrorMode, List<int> result)
        {
            result.Clear();
            foreach (var index in selected) {
                AddUnique(result, index);
                AddMirrorIndexes(index, controlPointColumns, controlPointRows, mirrorMode, result);
            }
        }

        public static void CollectAffectedCells(ICollection<int> selected, int cellColumns, int cellRows, ImageLatticeMirrorMode mirrorMode, List<int> result)
        {
            result.Clear();
            if (cellColumns <= 0 || cellRows <= 0) {
                return;
            }

            foreach (var index in selected) {
                AddUnique(result, index);
                if (mirrorMode is ImageLatticeMirrorMode.Horizontal or ImageLatticeMirrorMode.Both) {
                    AddUnique(result, MirrorCellIndex(index, cellColumns, cellRows, true, false));
                }

                if (mirrorMode is ImageLatticeMirrorMode.Vertical or ImageLatticeMirrorMode.Both) {
                    AddUnique(result, MirrorCellIndex(index, cellColumns, cellRows, false, true));
                }

                if (mirrorMode == ImageLatticeMirrorMode.Both) {
                    AddUnique(result, MirrorCellIndex(index, cellColumns, cellRows, true, true));
                }
            }
        }

        public static Rect CalculateBounds(Vector2[] points, ICollection<int> indexes)
        {
            var initialized = false;
            var min = Vector2.zero;
            var max = Vector2.zero;

            foreach (var index in indexes) {
                var point = points[index];
                if (!initialized) {
                    min = point;
                    max = point;
                    initialized = true;
                    continue;
                }

                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            return initialized ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : default;
        }

        public static Vector2 CalculateCenter(Vector2[] points, ICollection<int> indexes)
        {
            if (indexes.Count == 0) {
                return default;
            }

            var sum = Vector2.zero;
            foreach (var index in indexes) {
                sum += points[index];
            }

            return sum / indexes.Count;
        }

        public static void MovePoints(Vector2[] source, Vector2[] destination, int controlPointColumns, int controlPointRows, ICollection<int> selected,
            Vector2 delta, ImageLatticeMirrorMode mirrorMode, ImageLatticeEditTarget editTarget = ImageLatticeEditTarget.Points,
            ImageLatticeSoftSelectionMode softSelectionMode = ImageLatticeSoftSelectionMode.Off, float softSelectionRadius = 1.5f, ICollection<int> selectedCells = null)
        {
            TransformPointsWithFalloff(source, destination, controlPointColumns, controlPointRows, selected, selectedCells, mirrorMode, editTarget, softSelectionMode, softSelectionRadius,
                (_, point) => point + delta);
        }

        public static void ScalePoints(Vector2[] source, Vector2[] destination, int controlPointColumns, int controlPointRows, ICollection<int> selected,
            Vector2 pivot, Vector2 scale, ImageLatticeMirrorMode mirrorMode, ImageLatticeEditTarget editTarget = ImageLatticeEditTarget.Points,
            ImageLatticeSoftSelectionMode softSelectionMode = ImageLatticeSoftSelectionMode.Off, float softSelectionRadius = 1.5f, ICollection<int> selectedCells = null)
        {
            TransformPointsWithFalloff(source, destination, controlPointColumns, controlPointRows, selected, selectedCells, mirrorMode, editTarget, softSelectionMode, softSelectionRadius,
                (_, point) => pivot + Vector2.Scale(point - pivot, scale));
        }

        public static void RotatePoints(Vector2[] source, Vector2[] destination, int controlPointColumns, int controlPointRows, ICollection<int> selected,
            Vector2 pivot, float angleDegrees, ImageLatticeMirrorMode mirrorMode, ImageLatticeEditTarget editTarget = ImageLatticeEditTarget.Points,
            ImageLatticeSoftSelectionMode softSelectionMode = ImageLatticeSoftSelectionMode.Off, float softSelectionRadius = 1.5f, ICollection<int> selectedCells = null)
        {
            var radians = angleDegrees * Mathf.Deg2Rad;
            var sin = Mathf.Sin(radians);
            var cos = Mathf.Cos(radians);
            TransformPointsWithFalloff(source, destination, controlPointColumns, controlPointRows, selected, selectedCells, mirrorMode, editTarget, softSelectionMode, softSelectionRadius,
                (_, point) => pivot + Rotate(point - pivot, sin, cos));
        }

        public static void ResizePoints(Vector2[] source, Vector2[] destination, int controlPointColumns, int controlPointRows, ICollection<int> selected,
            Rect sourceRect, Rect targetRect, ImageLatticeMirrorMode mirrorMode, ImageLatticeEditTarget editTarget = ImageLatticeEditTarget.Points,
            ImageLatticeSoftSelectionMode softSelectionMode = ImageLatticeSoftSelectionMode.Off, float softSelectionRadius = 1.5f, ICollection<int> selectedCells = null)
        {
            TransformPointsWithFalloff(source, destination, controlPointColumns, controlPointRows, selected, selectedCells, mirrorMode, editTarget, softSelectionMode, softSelectionRadius,
                (_, point) => MapRectPoint(point, sourceRect, targetRect));
        }

        public static void TransformPoints(Vector2[] source, Vector2[] destination, int controlPointColumns, int controlPointRows, ICollection<int> selected,
            Vector2 pivot, Vector2 translation, float angleDegrees, Vector2 scale, ImageLatticeMirrorMode mirrorMode,
            ImageLatticeEditTarget editTarget = ImageLatticeEditTarget.Points, ImageLatticeSoftSelectionMode softSelectionMode = ImageLatticeSoftSelectionMode.Off,
            float softSelectionRadius = 1.5f, ICollection<int> selectedCells = null)
        {
            var radians = angleDegrees * Mathf.Deg2Rad;
            var sin = Mathf.Sin(radians);
            var cos = Mathf.Cos(radians);
            TransformPointsWithFalloff(source, destination, controlPointColumns, controlPointRows, selected, selectedCells, mirrorMode, editTarget, softSelectionMode, softSelectionRadius,
                (_, point) => pivot + Rotate(Vector2.Scale(point - pivot, scale), sin, cos) + translation);
        }

        public static void ResetPoints(Vector2[] source, Vector2[] destination, int controlPointColumns, int controlPointRows, ICollection<int> selected,
            ImageLatticeMirrorMode mirrorMode)
        {
            System.Array.Copy(source, destination, source.Length);
            foreach (var index in selected) {
                destination[index] = ConstrainCenterline(index, controlPointColumns, controlPointRows, GetIdentityPoint(index, controlPointColumns, controlPointRows), mirrorMode);
            }
        }

        public static void RelaxPoints(Vector2[] source, Vector2[] destination, int controlPointColumns, int controlPointRows, ICollection<int> selected,
            ImageLatticeMirrorMode mirrorMode)
        {
            System.Array.Copy(source, destination, source.Length);
            foreach (var index in selected) {
                if (!TryCalculateOrthogonalNeighborAverage(source, index, controlPointColumns, controlPointRows, out var average)) {
                    continue;
                }

                destination[index] = ConstrainCenterline(index, controlPointColumns, controlPointRows, Vector2.LerpUnclamped(source[index], average, 0.5f), mirrorMode);
            }
        }

        public static float GetSoftSelectionWeight(int index, int controlPointColumns, int controlPointRows, ICollection<int> selected,
            ImageLatticeMirrorMode mirrorMode, ImageLatticeEditTarget editTarget, ImageLatticeSoftSelectionMode softSelectionMode, float softSelectionRadius,
            ICollection<int> selectedCells = null)
        {
            return TryGetSoftInfluence(index, controlPointColumns, controlPointRows, selected, selectedCells, mirrorMode, editTarget, softSelectionMode, softSelectionRadius, out var influence)
                ? influence.Weight
                : 0f;
        }

        public static float GetCellSoftSelectionWeight(int cellIndex, int controlPointColumns, int controlPointRows, ICollection<int> selectedCells,
            ImageLatticeMirrorMode mirrorMode, ImageLatticeSoftSelectionMode softSelectionMode, float softSelectionRadius)
        {
            return TryGetCellInfluence(cellIndex, controlPointColumns - 1, controlPointRows - 1, selectedCells, mirrorMode, softSelectionMode, softSelectionRadius, out var influence)
                ? influence.Weight
                : 0f;
        }

        public static int MirrorIndex(int index, int controlPointColumns, int controlPointRows, bool mirrorX, bool mirrorY)
        {
            var x = index % controlPointColumns;
            var y = index / controlPointColumns;

            if (mirrorX) { x = controlPointColumns - 1 - x; }
            if (mirrorY) { y = controlPointRows - 1 - y; }

            return GetPointIndex(x, y, controlPointColumns);
        }

        public static int MirrorCellIndex(int index, int cellColumns, int cellRows, bool mirrorX, bool mirrorY)
        {
            var x = index % cellColumns;
            var y = index / cellColumns;

            if (mirrorX) { x = cellColumns - 1 - x; }
            if (mirrorY) { y = cellRows - 1 - y; }

            return GetCellIndex(x, y, cellColumns);
        }

        public static Vector2 ReflectDelta(Vector2 delta, bool mirrorX, bool mirrorY)
        {
            if (mirrorX) { delta.x = -delta.x; }
            if (mirrorY) { delta.y = -delta.y; }
            return delta;
        }

        public static Vector2 ConstrainUniformScale(Vector2 scale, bool scaleX, bool scaleY)
        {
            var uniform = scaleX && scaleY
                ? Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y))
                : scaleX ? scale.x : scale.y;
            return new Vector2(uniform, uniform);
        }

        public static ImageLatticeMirrorMode GetNextMirrorMode(ImageLatticeMirrorMode mode)
        {
            return mode switch {
                ImageLatticeMirrorMode.Off => ImageLatticeMirrorMode.Horizontal,
                ImageLatticeMirrorMode.Horizontal => ImageLatticeMirrorMode.Vertical,
                ImageLatticeMirrorMode.Vertical => ImageLatticeMirrorMode.Both,
                _ => ImageLatticeMirrorMode.Off
            };
        }

        public static void TransformSelectedPoints(Vector2[] source, Vector2[] destination, int controlPointColumns, int controlPointRows, ICollection<int> selected,
            ImageLatticeMirrorMode mirrorMode, System.Func<int, Vector2, Vector2> transform)
        {
            System.Array.Copy(source, destination, source.Length);
            if (selected.Count == 0) {
                return;
            }

            var explicitSelection = new HashSet<int>(selected);
            foreach (var index in selected) {
                destination[index] = ConstrainCenterline(index, controlPointColumns, controlPointRows, transform(index, source[index]), mirrorMode);
            }

            foreach (var index in selected) {
                ApplyMirrorDelta(source, destination, explicitSelection, index, controlPointColumns, controlPointRows, mirrorMode, false, false);

                if (mirrorMode is ImageLatticeMirrorMode.Horizontal or ImageLatticeMirrorMode.Both) {
                    ApplyMirrorDelta(source, destination, explicitSelection, index, controlPointColumns, controlPointRows, mirrorMode, true, false);
                }

                if (mirrorMode is ImageLatticeMirrorMode.Vertical or ImageLatticeMirrorMode.Both) {
                    ApplyMirrorDelta(source, destination, explicitSelection, index, controlPointColumns, controlPointRows, mirrorMode, false, true);
                }

                if (mirrorMode == ImageLatticeMirrorMode.Both) {
                    ApplyMirrorDelta(source, destination, explicitSelection, index, controlPointColumns, controlPointRows, mirrorMode, true, true);
                }
            }
        }

        private static void TransformPointsWithFalloff(Vector2[] source, Vector2[] destination, int controlPointColumns, int controlPointRows, ICollection<int> selected,
            ICollection<int> selectedCells, ImageLatticeMirrorMode mirrorMode, ImageLatticeEditTarget editTarget, ImageLatticeSoftSelectionMode softSelectionMode, float softSelectionRadius,
            System.Func<int, Vector2, Vector2> transform)
        {
            if (softSelectionMode == ImageLatticeSoftSelectionMode.Off) {
                TransformSelectedPoints(source, destination, controlPointColumns, controlPointRows, selected, mirrorMode, transform);
                return;
            }

            System.Array.Copy(source, destination, source.Length);
            if (selected.Count == 0) {
                return;
            }

            for (var i = 0; i < source.Length; i++) {
                if (!TryGetSoftInfluence(i, controlPointColumns, controlPointRows, selected, selectedCells, mirrorMode, editTarget, softSelectionMode, softSelectionRadius, out var influence)) {
                    continue;
                }

                var sourceIndex = influence.MirrorX || influence.MirrorY
                    ? MirrorIndex(i, controlPointColumns, controlPointRows, influence.MirrorX, influence.MirrorY)
                    : i;
                var delta = transform(sourceIndex, source[sourceIndex]) - source[sourceIndex];
                delta = ReflectDelta(delta, influence.MirrorX, influence.MirrorY);
                var next = source[i] + delta * influence.Weight;
                destination[i] = ConstrainCenterline(i, controlPointColumns, controlPointRows, next, mirrorMode);
            }
        }

        private static bool TryGetSoftInfluence(int index, int controlPointColumns, int controlPointRows, ICollection<int> selected, ICollection<int> selectedCells,
            ImageLatticeMirrorMode mirrorMode, ImageLatticeEditTarget editTarget, ImageLatticeSoftSelectionMode softSelectionMode, float softSelectionRadius,
            out SoftInfluence influence)
        {
            influence = default;
            if (softSelectionMode == ImageLatticeSoftSelectionMode.Off || selected.Count == 0) {
                return false;
            }

            if (selected.Contains(index)) {
                influence = new SoftInfluence(1f, false, false);
                return true;
            }

            if (editTarget == ImageLatticeEditTarget.Cells && selectedCells != null && selectedCells.Count > 0) {
                return TryGetCellPointInfluence(index, controlPointColumns, controlPointRows, selectedCells, mirrorMode, softSelectionMode, softSelectionRadius, out influence);
            }

            var bestDistance = float.PositiveInfinity;
            var bestMirrorX = false;
            var bestMirrorY = false;
            foreach (var selectedIndex in selected) {
                ConsiderPointAnchor(index, selectedIndex, controlPointColumns, false, false, ref bestDistance, ref bestMirrorX, ref bestMirrorY);

                if (mirrorMode is ImageLatticeMirrorMode.Horizontal or ImageLatticeMirrorMode.Both) {
                    ConsiderPointAnchor(index, MirrorIndex(selectedIndex, controlPointColumns, controlPointRows, true, false),
                        controlPointColumns, true, false, ref bestDistance, ref bestMirrorX, ref bestMirrorY);
                }

                if (mirrorMode is ImageLatticeMirrorMode.Vertical or ImageLatticeMirrorMode.Both) {
                    ConsiderPointAnchor(index, MirrorIndex(selectedIndex, controlPointColumns, controlPointRows, false, true),
                        controlPointColumns, false, true, ref bestDistance, ref bestMirrorX, ref bestMirrorY);
                }

                if (mirrorMode == ImageLatticeMirrorMode.Both) {
                    ConsiderPointAnchor(index, MirrorIndex(selectedIndex, controlPointColumns, controlPointRows, true, true),
                        controlPointColumns, true, true, ref bestDistance, ref bestMirrorX, ref bestMirrorY);
                }
            }

            return TryCreateInfluence(bestDistance, bestMirrorX, bestMirrorY, softSelectionMode, softSelectionRadius, out influence);
        }

        private static bool TryGetCellPointInfluence(int pointIndex, int controlPointColumns, int controlPointRows, ICollection<int> selectedCells,
            ImageLatticeMirrorMode mirrorMode, ImageLatticeSoftSelectionMode softSelectionMode, float softSelectionRadius, out SoftInfluence influence)
        {
            influence = default;
            var cellColumns = controlPointColumns - 1;
            var cellRows = controlPointRows - 1;
            if (cellColumns <= 0 || cellRows <= 0) {
                return false;
            }

            var bestDistance = float.PositiveInfinity;
            var bestMirrorX = false;
            var bestMirrorY = false;
            foreach (var selectedCell in selectedCells) {
                ConsiderCellPointAnchor(pointIndex, selectedCell, controlPointColumns, cellColumns, cellRows, false, false, ref bestDistance, ref bestMirrorX, ref bestMirrorY);

                if (mirrorMode is ImageLatticeMirrorMode.Horizontal or ImageLatticeMirrorMode.Both) {
                    ConsiderCellPointAnchor(pointIndex, MirrorCellIndex(selectedCell, cellColumns, cellRows, true, false),
                        controlPointColumns, cellColumns, cellRows, true, false, ref bestDistance, ref bestMirrorX, ref bestMirrorY);
                }

                if (mirrorMode is ImageLatticeMirrorMode.Vertical or ImageLatticeMirrorMode.Both) {
                    ConsiderCellPointAnchor(pointIndex, MirrorCellIndex(selectedCell, cellColumns, cellRows, false, true),
                        controlPointColumns, cellColumns, cellRows, false, true, ref bestDistance, ref bestMirrorX, ref bestMirrorY);
                }

                if (mirrorMode == ImageLatticeMirrorMode.Both) {
                    ConsiderCellPointAnchor(pointIndex, MirrorCellIndex(selectedCell, cellColumns, cellRows, true, true),
                        controlPointColumns, cellColumns, cellRows, true, true, ref bestDistance, ref bestMirrorX, ref bestMirrorY);
                }
            }

            return TryCreateInfluence(bestDistance, bestMirrorX, bestMirrorY, softSelectionMode, softSelectionRadius, out influence);
        }

        private static bool TryGetCellInfluence(int cellIndex, int cellColumns, int cellRows, ICollection<int> selectedCells,
            ImageLatticeMirrorMode mirrorMode, ImageLatticeSoftSelectionMode softSelectionMode, float softSelectionRadius, out SoftInfluence influence)
        {
            influence = default;
            if (softSelectionMode == ImageLatticeSoftSelectionMode.Off || selectedCells == null || selectedCells.Count == 0 || cellColumns <= 0 || cellRows <= 0) {
                return false;
            }

            if (selectedCells.Contains(cellIndex)) {
                influence = new SoftInfluence(1f, false, false);
                return true;
            }

            var bestDistance = float.PositiveInfinity;
            var bestMirrorX = false;
            var bestMirrorY = false;
            foreach (var selectedCell in selectedCells) {
                ConsiderCellAnchor(cellIndex, selectedCell, cellColumns, false, false, ref bestDistance, ref bestMirrorX, ref bestMirrorY);

                if (mirrorMode is ImageLatticeMirrorMode.Horizontal or ImageLatticeMirrorMode.Both) {
                    ConsiderCellAnchor(cellIndex, MirrorCellIndex(selectedCell, cellColumns, cellRows, true, false),
                        cellColumns, true, false, ref bestDistance, ref bestMirrorX, ref bestMirrorY);
                }

                if (mirrorMode is ImageLatticeMirrorMode.Vertical or ImageLatticeMirrorMode.Both) {
                    ConsiderCellAnchor(cellIndex, MirrorCellIndex(selectedCell, cellColumns, cellRows, false, true),
                        cellColumns, false, true, ref bestDistance, ref bestMirrorX, ref bestMirrorY);
                }

                if (mirrorMode == ImageLatticeMirrorMode.Both) {
                    ConsiderCellAnchor(cellIndex, MirrorCellIndex(selectedCell, cellColumns, cellRows, true, true),
                        cellColumns, true, true, ref bestDistance, ref bestMirrorX, ref bestMirrorY);
                }
            }

            return TryCreateInfluence(bestDistance, bestMirrorX, bestMirrorY, softSelectionMode, softSelectionRadius, out influence);
        }

        private static void ConsiderPointAnchor(int index, int anchorIndex, int controlPointColumns, bool mirrorX, bool mirrorY,
            ref float bestDistance, ref bool bestMirrorX, ref bool bestMirrorY)
        {
            var distance = GetPointGridDistance(index, anchorIndex, controlPointColumns);
            if (distance >= bestDistance) {
                return;
            }

            bestDistance = distance;
            bestMirrorX = mirrorX;
            bestMirrorY = mirrorY;
        }

        private static void ConsiderCellAnchor(int cellIndex, int anchorCellIndex, int cellColumns, bool mirrorX, bool mirrorY,
            ref float bestDistance, ref bool bestMirrorX, ref bool bestMirrorY)
        {
            var distance = GetCellGridDistance(cellIndex, anchorCellIndex, cellColumns);
            if (distance >= bestDistance) {
                return;
            }

            bestDistance = distance;
            bestMirrorX = mirrorX;
            bestMirrorY = mirrorY;
        }

        private static void ConsiderCellPointAnchor(int pointIndex, int anchorCellIndex, int controlPointColumns, int cellColumns, int cellRows, bool mirrorX, bool mirrorY,
            ref float bestDistance, ref bool bestMirrorX, ref bool bestMirrorY)
        {
            var pointX = pointIndex % controlPointColumns;
            var pointY = pointIndex / controlPointColumns;

            ConsiderAdjacentCell(pointX - 1, pointY - 1, anchorCellIndex, cellColumns, cellRows, mirrorX, mirrorY, ref bestDistance, ref bestMirrorX, ref bestMirrorY);
            ConsiderAdjacentCell(pointX, pointY - 1, anchorCellIndex, cellColumns, cellRows, mirrorX, mirrorY, ref bestDistance, ref bestMirrorX, ref bestMirrorY);
            ConsiderAdjacentCell(pointX - 1, pointY, anchorCellIndex, cellColumns, cellRows, mirrorX, mirrorY, ref bestDistance, ref bestMirrorX, ref bestMirrorY);
            ConsiderAdjacentCell(pointX, pointY, anchorCellIndex, cellColumns, cellRows, mirrorX, mirrorY, ref bestDistance, ref bestMirrorX, ref bestMirrorY);
        }

        private static void ConsiderAdjacentCell(int cellX, int cellY, int anchorCellIndex, int cellColumns, int cellRows, bool mirrorX, bool mirrorY,
            ref float bestDistance, ref bool bestMirrorX, ref bool bestMirrorY)
        {
            if (cellX < 0 || cellX >= cellColumns || cellY < 0 || cellY >= cellRows) {
                return;
            }

            ConsiderCellAnchor(GetCellIndex(cellX, cellY, cellColumns), anchorCellIndex, cellColumns, mirrorX, mirrorY, ref bestDistance, ref bestMirrorX, ref bestMirrorY);
        }

        private static bool TryCreateInfluence(float distance, bool mirrorX, bool mirrorY, ImageLatticeSoftSelectionMode softSelectionMode, float softSelectionRadius,
            out SoftInfluence influence)
        {
            influence = default;
            softSelectionRadius = Mathf.Max(ImageLatticeToolState.MinSoftSelectionRadius, softSelectionRadius);
            if (distance >= softSelectionRadius) {
                return false;
            }

            var weight = 1f - distance / softSelectionRadius;
            if (softSelectionMode == ImageLatticeSoftSelectionMode.Smooth) {
                weight = weight * weight * (3f - 2f * weight);
            }

            if (weight <= 0f) {
                return false;
            }

            influence = new SoftInfluence(weight, mirrorX, mirrorY);
            return true;
        }

        private static float GetPointGridDistance(int index, int anchorIndex, int controlPointColumns)
        {
            var x = index % controlPointColumns;
            var y = index / controlPointColumns;
            var anchorX = anchorIndex % controlPointColumns;
            var anchorY = anchorIndex / controlPointColumns;
            return Vector2.Distance(new Vector2(x, y), new Vector2(anchorX, anchorY));
        }

        private static float GetCellGridDistance(int index, int anchorIndex, int cellColumns)
        {
            var x = index % cellColumns;
            var y = index / cellColumns;
            var anchorX = anchorIndex % cellColumns;
            var anchorY = anchorIndex / cellColumns;
            return Vector2.Distance(new Vector2(x, y), new Vector2(anchorX, anchorY));
        }

        private static bool TryCalculateOrthogonalNeighborAverage(Vector2[] points, int index, int controlPointColumns, int controlPointRows, out Vector2 average)
        {
            var x = index % controlPointColumns;
            var y = index / controlPointColumns;
            var sum = Vector2.zero;
            var count = 0;

            if (x > 0) {
                sum += points[GetPointIndex(x - 1, y, controlPointColumns)];
                count++;
            }

            if (x < controlPointColumns - 1) {
                sum += points[GetPointIndex(x + 1, y, controlPointColumns)];
                count++;
            }

            if (y > 0) {
                sum += points[GetPointIndex(x, y - 1, controlPointColumns)];
                count++;
            }

            if (y < controlPointRows - 1) {
                sum += points[GetPointIndex(x, y + 1, controlPointColumns)];
                count++;
            }

            average = count > 0 ? sum / count : default;
            return count > 0;
        }

        private readonly struct SoftInfluence
        {
            public readonly float Weight;
            public readonly bool MirrorX;
            public readonly bool MirrorY;

            public SoftInfluence(float weight, bool mirrorX, bool mirrorY)
            {
                Weight = weight;
                MirrorX = mirrorX;
                MirrorY = mirrorY;
            }
        }

        private static Vector2 Rotate(Vector2 point, float sin, float cos)
        {
            return new Vector2(
                point.x * cos - point.y * sin,
                point.x * sin + point.y * cos);
        }

        private static Vector2 MapRectPoint(Vector2 point, Rect sourceRect, Rect targetRect)
        {
            var normalized = new Vector2(
                Mathf.Approximately(sourceRect.width, 0f) ? 0.5f : Mathf.InverseLerp(sourceRect.xMin, sourceRect.xMax, point.x),
                Mathf.Approximately(sourceRect.height, 0f) ? 0.5f : Mathf.InverseLerp(sourceRect.yMin, sourceRect.yMax, point.y));
            
            return new Vector2(
                Mathf.LerpUnclamped(targetRect.xMin, targetRect.xMax, normalized.x),
                Mathf.LerpUnclamped(targetRect.yMin, targetRect.yMax, normalized.y));
        }

        private static void ApplyMirrorDelta(Vector2[] source, Vector2[] destination, HashSet<int> explicitSelection, int sourceIndex,
            int controlPointColumns, int controlPointRows, ImageLatticeMirrorMode mirrorMode, bool mirrorX, bool mirrorY)
        {
            var targetIndex = MirrorIndex(sourceIndex, controlPointColumns, controlPointRows, mirrorX, mirrorY);
            if (targetIndex == sourceIndex || explicitSelection.Contains(targetIndex)) {
                return;
            }

            var delta = destination[sourceIndex] - source[sourceIndex];
            var mirrored = source[targetIndex] + ReflectDelta(delta, mirrorX, mirrorY);
            destination[targetIndex] = ConstrainCenterline(targetIndex, controlPointColumns, controlPointRows, mirrored, mirrorMode);
        }

        private static void AddMirrorIndexes(int index, int controlPointColumns, int controlPointRows, ImageLatticeMirrorMode mirrorMode, List<int> result)
        {
            if (mirrorMode is ImageLatticeMirrorMode.Horizontal or ImageLatticeMirrorMode.Both) {
                AddUnique(result, MirrorIndex(index, controlPointColumns, controlPointRows, true, false));
            }

            if (mirrorMode is ImageLatticeMirrorMode.Vertical or ImageLatticeMirrorMode.Both) {
                AddUnique(result, MirrorIndex(index, controlPointColumns, controlPointRows, false, true));
            }

            if (mirrorMode == ImageLatticeMirrorMode.Both) {
                AddUnique(result, MirrorIndex(index, controlPointColumns, controlPointRows, true, true));
            }
        }

        private static void AddUnique(ICollection<int> indexes, int index)
        {
            if (!indexes.Contains(index)) {
                indexes.Add(index);
            }
        }

        private static Vector2 ConstrainCenterline(int index, int controlPointColumns, int controlPointRows, Vector2 point, ImageLatticeMirrorMode mirrorMode)
        {
            var identity = GetIdentityPoint(index, controlPointColumns, controlPointRows);
            if ((mirrorMode is ImageLatticeMirrorMode.Horizontal or ImageLatticeMirrorMode.Both) &&
                IsCenterColumn(index, controlPointColumns)) {
                point.x = identity.x;
            }

            if ((mirrorMode is ImageLatticeMirrorMode.Vertical or ImageLatticeMirrorMode.Both) &&
                IsCenterRow(index, controlPointColumns, controlPointRows)) {
                point.y = identity.y;
            }

            return point;
        }

        private static bool IsCenterColumn(int index, int controlPointColumns)
        {
            return controlPointColumns % 2 == 1 && index % controlPointColumns == controlPointColumns / 2;
        }

        private static bool IsCenterRow(int index, int controlPointColumns, int controlPointRows)
        {
            return controlPointRows % 2 == 1 && index / controlPointColumns == controlPointRows / 2;
        }
    }
}
