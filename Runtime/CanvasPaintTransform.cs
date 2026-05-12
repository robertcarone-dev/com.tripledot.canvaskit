using System;
using UnityEngine;

namespace Tripledot.CanvasKit
{
    public enum CanvasPaintWrapMode
    {
        Clamp = 0,
        Repeat = 1,
        Mirror = 2
    }

    [Serializable]
    public struct CanvasPaintTransform : IEquatable<CanvasPaintTransform>
    {
        public Vector2 Center;
        public Vector2 Offset;
        public Vector2 Scale;
        public float Rotation;
        public CanvasPaintWrapMode WrapMode;

        public static CanvasPaintTransform Default => new CanvasPaintTransform {
            Center = new Vector2(0.5f, 0.5f),
            Offset = Vector2.zero,
            Scale = Vector2.one,
            Rotation = 0f,
            WrapMode = CanvasPaintWrapMode.Clamp
        };

        public bool Equals(CanvasPaintTransform other)
        {
            return Center == other.Center
                && Offset == other.Offset
                && Scale == other.Scale
                && Rotation == other.Rotation
                && WrapMode == other.WrapMode;
        }

        public override bool Equals(object obj)
        {
            return obj is CanvasPaintTransform other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Center.GetHashCode();
                hashCode = (hashCode * 397) ^ Offset.GetHashCode();
                hashCode = (hashCode * 397) ^ Scale.GetHashCode();
                hashCode = (hashCode * 397) ^ Rotation.GetHashCode();
                hashCode = (hashCode * 397) ^ WrapMode.GetHashCode();
                return hashCode;
            }
        }
    }
}
