using System.Runtime.CompilerServices;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    internal static class CanvasPaintEditorUtility
    {
        internal const float MinScale = 0.01f;

        private static readonly Vector2 Center2D = new Vector2(0.5f, 0.5f);
        
        internal static bool IsEditableGradientPaint(SerializedCanvasPaint paint, out CanvasPaintType paintType)
        {
            paintType = CanvasPaintType.Solid;
            if (paint.Root.serializedObject.isEditingMultipleObjects) {
                return false;
            }

            var type = paint.Type;
            if (type.hasMultipleDifferentValues) {
                return false;
            }

            paintType = (CanvasPaintType)type.enumValueIndex;
            if (paintType is not (CanvasPaintType.LinearGradient or CanvasPaintType.RadialGradient)) {
                return false;
            }

            return !HasMixedTransformValues(paint.Transform);
        }

        internal static bool HasDefaultSpatialTransform(SerializedCanvasPaint paint)
        {
            if (HasMixedTransformValues(paint.Transform)) {
                return false;
            }

            return Approximately(paint.Transform.Center.vector2Value, Center2D)
                && Approximately(paint.Transform.Offset.vector2Value, Vector2.zero)
                && Approximately(paint.Transform.Scale.vector2Value, Vector2.one)
                && Mathf.Approximately(NormalizeDegrees(paint.Transform.Rotation.floatValue), 0f);
        }

        internal static void ResetSpatialTransform(SerializedCanvasPaint paint)
        {
            paint.Transform.Center.vector2Value = Center2D;
            paint.Transform.Offset.vector2Value = Vector2.zero;
            paint.Transform.Scale.vector2Value = Vector2.one;
            paint.Transform.Rotation.floatValue = 0f;
        }

        internal static float SampleLinearGradientT(Vector2 paintUv, Vector2 centerUv, Vector2 offsetUv, Vector2 scale, float rotationDegrees, Vector2 paintBoundsSize)
        {
            var relative = paintUv - centerUv - offsetUv;
            var radians = rotationDegrees * Mathf.Deg2Rad;
            var sin = Mathf.Sin(radians);
            var cos = Mathf.Cos(radians);
            var direction = new Vector2(cos, -sin);
            var axis = Vector2.Scale(direction, paintBoundsSize);
            var axisLengthSq = Vector2.Dot(axis, axis);
            var relativeLocal = Vector2.Scale(relative, paintBoundsSize);
            var gradientX = Vector2.Dot(relativeLocal, axis) / Mathf.Max(axisLengthSq * Mathf.Abs(scale.x), 0.0001f);
            return Mathf.Clamp01(gradientX + 0.5f);
        }

        internal static void CalculateLinearTransformFromEndpoints(Vector2 startUv, Vector2 endUv, out Vector2 center, out Vector2 scale, out float rotation)
        {
            var deltaUv = endUv - startUv;
            center = (startUv + endUv) * 0.5f;
            scale = new Vector2(deltaUv.magnitude, 1f);
            rotation = NormalizeDegrees(Mathf.Atan2(-deltaUv.y, deltaUv.x) * Mathf.Rad2Deg);
        }

        private static bool HasMixedTransformValues(SerializedCanvasPaint.TransformProperties transform)
        {
            return transform.Center is { hasMultipleDifferentValues: true }
                || transform.Offset is { hasMultipleDifferentValues: true }
                || transform.Scale is { hasMultipleDifferentValues: true }
                || transform.Rotation is { hasMultipleDifferentValues: true };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float NormalizeDegrees(float value)
        {
            value %= 360f;
            if (value < 0f) {
                value += 360f;
            }

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector2 DirectionFromRotation(float rotationDegrees)
        {
            var radians = rotationDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), -Mathf.Sin(radians));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector2 PerpendicularFromRotation(float rotationDegrees)
        {
            var direction = DirectionFromRotation(rotationDegrees);
            return new Vector2(-direction.y, direction.x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);
        }
    }
}
