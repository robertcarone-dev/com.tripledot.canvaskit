using System;
using UnityEngine;

namespace Tripledot.CanvasKit
{
    public enum TextMeshProStrokePosition
    {
        Outside = 0,
        Center = 1,
        Inside = 2
    }

    [Serializable]
    public struct TextMeshProStroke : IEquatable<TextMeshProStroke>
    {
        public bool Enabled;
        public CanvasPaint Paint;
        public TextMeshProStrokePosition Position;
        public float Width;
        public TextMeshProSdfLengthUnit WidthUnit;
        public float Feather;
        public TextMeshProSdfLengthUnit FeatherUnit;
        public Vector2 Offset;

        public static TextMeshProStroke Default => new TextMeshProStroke {
            Enabled = true,
            Paint = CanvasPaint.Solid(Color.black),
            Position = TextMeshProStrokePosition.Outside,
            Width = 1f
        };

        internal float GetSdfRange()
        {
            if (!Enabled) {
                return 0f;
            }

            return Width * TextMeshProUtility.GetStrokeVisualPaddingFactor(Position) + Feather;
        }

        internal Vector4 GetVisualPadding(float sdfPaddingLimit, float baseSdfRange, bool includeDirectionalOffset, Vector2 localUnitsPerAtlasPixel)
        {
            if (!Enabled) {
                return Vector4.zero;
            }
            
            var effectRange = baseSdfRange + GetSdfRange();
            var range = Mathf.Min(TextMeshProUtility.SdfPixelsToPaddingPixels(effectRange), sdfPaddingLimit);
            var padding = range > 0f ? TextMeshProUtility.PaddingUniform(range) : Vector4.zero;
            
            return includeDirectionalOffset ? TextMeshProUtility.PaddingWithDirectionalOffset(padding, Offset, localUnitsPerAtlasPixel) : padding;
        }

        public bool Equals(TextMeshProStroke other)
        {
            return Enabled == other.Enabled
                && Paint.Equals(other.Paint)
                && Position == other.Position
                && Width == other.Width
                && WidthUnit == other.WidthUnit
                && Feather == other.Feather
                && FeatherUnit == other.FeatherUnit
                && Offset == other.Offset;
        }

        public override bool Equals(object obj)
        {
            return obj is TextMeshProStroke other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Enabled.GetHashCode();
                hashCode = (hashCode * 397) ^ Paint.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Position;
                hashCode = (hashCode * 397) ^ Width.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)WidthUnit;
                hashCode = (hashCode * 397) ^ Feather.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)FeatherUnit;
                hashCode = (hashCode * 397) ^ Offset.GetHashCode();
                return hashCode;
            }
        }
    }
}
