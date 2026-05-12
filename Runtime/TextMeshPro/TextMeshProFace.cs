using System;
using UnityEngine;

namespace Tripledot.CanvasKit
{
    [Serializable]
    public struct TextMeshProFace : IEquatable<TextMeshProFace>
    {
        public bool Enabled;
        public CanvasPaint Paint;
        public float Dilate;
        public TextMeshProSdfLengthUnit DilateUnit;

        public static TextMeshProFace Default => new TextMeshProFace {
            Enabled = true,
            Paint = CanvasPaint.Solid(Color.white)
        };

        internal float GetSdfRange()
        {
            return Enabled ? Dilate : 0f;
        }

        internal Vector4 GetVisualPadding(float sdfPaddingLimit)
        {
            var range = Mathf.Min(TextMeshProUtility.SdfPixelsToPaddingPixels(GetSdfRange()), sdfPaddingLimit);
            return range > 0f ? TextMeshProUtility.PaddingUniform(range) : Vector4.zero;
        }

        public bool Equals(TextMeshProFace other)
        {
            return Enabled == other.Enabled
                && Paint.Equals(other.Paint)
                && Dilate == other.Dilate
                && DilateUnit == other.DilateUnit;
        }

        public override bool Equals(object obj)
        {
            return obj is TextMeshProFace other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Enabled.GetHashCode();
                hashCode = (hashCode * 397) ^ Paint.GetHashCode();
                hashCode = (hashCode * 397) ^ Dilate.GetHashCode();
                hashCode = (hashCode * 397) ^ DilateUnit.GetHashCode();
                return hashCode;
            }
        }
    }
}
