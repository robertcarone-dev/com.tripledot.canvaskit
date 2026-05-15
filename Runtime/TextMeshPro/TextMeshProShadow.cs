using System;
using UnityEngine;

namespace Tripledot.CanvasKit
{
    [Serializable]
    public struct TextMeshProShadow : IEquatable<TextMeshProShadow>
    {
        public bool Enabled;
        public CanvasPaint Paint;
        public Vector2 Offset;
        
        public float Spread;
        public TextMeshProSdfLengthUnit SpreadUnit;
        
        public float Blur;
        public TextMeshProSdfLengthUnit BlurUnit;

        public static TextMeshProShadow Default => new TextMeshProShadow {
            Enabled = true,
            Paint = CanvasPaint.Solid(new Color(0f, 0f, 0f, 0.5f)),
            Offset = new Vector2(0f, -2f),
            Blur = 4f
        };

        internal float GetSdfRange()
        {
            return !Enabled ? 0f : TextMeshProUtility.GetShadowSdfRange(Spread, Blur);
        }

        internal Vector4 GetVisualPadding(float sdfPaddingLimit, float baseSdfRange, bool includeDirectionalOffset, Vector2 localUnitsPerAtlasPixel)
        {
            if (!Enabled) {
                return Vector4.zero;
            }
            
            var effectRange = baseSdfRange + TextMeshProUtility.GetShadowOutwardRange(Spread, Blur);
            var range = Mathf.Min(TextMeshProUtility.SdfPixelsToPaddingPixels(effectRange), sdfPaddingLimit);
            var padding = range > 0f ? TextMeshProUtility.PaddingUniform(range) : Vector4.zero;
            
            return includeDirectionalOffset ? TextMeshProUtility.PaddingWithDirectionalOffset(padding, Offset, localUnitsPerAtlasPixel) : padding;
        }

        public bool Equals(TextMeshProShadow other)
        {
            return Enabled == other.Enabled
                && Paint.Equals(other.Paint)
                && Offset == other.Offset
                && Blur == other.Blur
                && BlurUnit == other.BlurUnit
                && Spread == other.Spread
                && SpreadUnit == other.SpreadUnit;
        }

        public override bool Equals(object obj)
        {
            return obj is TextMeshProShadow other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Enabled.GetHashCode();
                hashCode = (hashCode * 397) ^ Paint.GetHashCode();
                hashCode = (hashCode * 397) ^ Offset.GetHashCode();
                hashCode = (hashCode * 397) ^ Blur.GetHashCode();
                hashCode = (hashCode * 397) ^ BlurUnit.GetHashCode();
                hashCode = (hashCode * 397) ^ Spread.GetHashCode();
                hashCode = (hashCode * 397) ^ SpreadUnit.GetHashCode();
                return hashCode;
            }
        }
    }
}
