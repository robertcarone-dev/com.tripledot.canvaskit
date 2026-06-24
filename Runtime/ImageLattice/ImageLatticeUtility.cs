using UnityEngine;

namespace Tripledot.CanvasKit
{
    internal static class ImageLatticeUtility
    {
        public static Vector2 Evaluate(ImageLattice image, int controlPointColumns, int controlPointRows, Vector2 uv)
        {
            var x = GetCellCoordinate(uv.x, controlPointColumns, out var tx);
            var y = GetCellCoordinate(uv.y, controlPointRows, out var ty);

            var p0 = SampleRow(image, controlPointColumns, controlPointRows, x, y - 1, tx);
            var p1 = SampleRow(image, controlPointColumns, controlPointRows, x, y, tx);
            var p2 = SampleRow(image, controlPointColumns, controlPointRows, x, y + 1, tx);
            var p3 = SampleRow(image, controlPointColumns, controlPointRows, x, y + 2, tx);

            return CatmullRom(p0, p1, p2, p3, ty);
        }

        private static Vector2 SampleRow(ImageLattice image, int controlPointColumns, int controlPointRows, int x, int y, float t)
        {
            var p0 = GetPoint(image, controlPointColumns, controlPointRows, x - 1, y);
            var p1 = GetPoint(image, controlPointColumns, controlPointRows, x, y);
            var p2 = GetPoint(image, controlPointColumns, controlPointRows, x + 1, y);
            var p3 = GetPoint(image, controlPointColumns, controlPointRows, x + 2, y);
            
            return CatmullRom(p0, p1, p2, p3, t);
        }

        private static Vector2 GetPoint(ImageLattice image, int controlPointColumns, int controlPointRows, int x, int y)
        {
            var inRangeX = x >= 0 && x < controlPointColumns;
            var inRangeY = y >= 0 && y < controlPointRows;
            if (inRangeX && inRangeY) {
                return GetStoredPoint(image, controlPointColumns, x, y);
            }

            if (inRangeX) {
                GetExtrapolationAxis(y, controlPointRows, out var extrapolatedY0, out var extrapolatedY1, out var extrapolatedTy);
                return Vector2.LerpUnclamped(
                    GetStoredPoint(image, controlPointColumns, x, extrapolatedY0),
                    GetStoredPoint(image, controlPointColumns, x, extrapolatedY1),
                    extrapolatedTy);
            }

            if (inRangeY) {
                GetExtrapolationAxis(x, controlPointColumns, out var extrapolatedX0, out var extrapolatedX1, out var extrapolatedTx);
                return Vector2.LerpUnclamped(
                    GetStoredPoint(image, controlPointColumns, extrapolatedX0, y),
                    GetStoredPoint(image, controlPointColumns, extrapolatedX1, y),
                    extrapolatedTx);
            }

            GetExtrapolationAxis(x, controlPointColumns, out var x0, out var x1, out var tx);
            GetExtrapolationAxis(y, controlPointRows, out var y0, out var y1, out var ty);
            var row0 = Vector2.LerpUnclamped(GetStoredPoint(image, controlPointColumns, x0, y0), GetStoredPoint(image, controlPointColumns, x1, y0), tx);
            var row1 = Vector2.LerpUnclamped(GetStoredPoint(image, controlPointColumns, x0, y1), GetStoredPoint(image, controlPointColumns, x1, y1), tx);
            return Vector2.LerpUnclamped(row0, row1, ty);
        }

        private static void GetExtrapolationAxis(int value, int size, out int lower, out int upper, out float t)
        {
            if (value < 0) {
                lower = 0;
                upper = 1;
                t = value;
                return;
            }

            if (value >= size) {
                lower = size - 2;
                upper = size - 1;
                t = value - (size - 2);
                return;
            }

            lower = value;
            upper = value;
            t = 0f;
        }

        private static Vector2 GetStoredPoint(ImageLattice image, int controlPointColumns, int x, int y)
        {
            return image.GetStoredLatticePointUnchecked(GetPointIndex(x, y, controlPointColumns));
        }

        private static int GetCellCoordinate(float value, int size, out float t)
        {
            var scaled = Mathf.Clamp01(value) * (size - 1);
            var cell = Mathf.Min(Mathf.FloorToInt(scaled), size - 2);
            t = scaled - cell;
            return cell;
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static int GetPointIndex(int x, int y, int controlPointColumns)
        {
            return y * controlPointColumns + x;
        }
    }
}