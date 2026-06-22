using System;
using UnityEngine;
using UnityEngine.Animations;

namespace Tripledot.CanvasKit
{
    [Serializable]
    public struct TextMeshProShadow : IEquatable<TextMeshProShadow>
    {
        [NotKeyable]
        public bool Enabled;
        public CanvasPaint Paint;
        [NotKeyable]
        public Vector2 Offset;

        [NotKeyable]
        public float Spread;
        [NotKeyable]
        public TextMeshProSdfLengthUnit SpreadUnit;

        [NotKeyable]
        public float Blur;
        [NotKeyable]
        public TextMeshProSdfLengthUnit BlurUnit;

        public static TextMeshProShadow Default =>
            new TextMeshProShadow {
                Enabled = true,
                Paint = CanvasPaint.Solid(new Color(0f, 0f, 0f, 0.5f)),
                Offset = new Vector2(0f, -2f),
                Blur = 4f
            };

        internal float GetSdfRange()
        {
            return !Enabled ? 0f : Spread + Blur;
        }

        internal Vector4 GetVisualPadding(float sdfPaddingLimit, float baseSdfRange, bool includeDirectionalOffset, Vector2 localUnitsPerAtlasPixel)
        {
            if (!Enabled) {
                return Vector4.zero;
            }

            var outerRange = baseSdfRange + Mathf.Max(0f, Spread + Blur);
            var range = Mathf.Min(TextMeshProUtility.SdfPixelsToPaddingPixels(outerRange), sdfPaddingLimit);
            var padding = range > 0f ? TextMeshProUtility.PaddingUniform(range) : Vector4.zero;

            return includeDirectionalOffset
                ? TextMeshProUtility.PaddingWithDirectionalOffset(padding, Offset, localUnitsPerAtlasPixel)
                : padding;
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
            unchecked {
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