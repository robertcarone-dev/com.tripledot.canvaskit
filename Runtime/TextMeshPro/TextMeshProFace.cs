using System;
using UnityEngine;
using UnityEngine.Animations;

namespace Tripledot.CanvasKit
{
    [Serializable]
    public struct TextMeshProFace : IEquatable<TextMeshProFace>
    {
        [NotKeyable]
        public bool Enabled;
        public CanvasPaint Paint;
        [NotKeyable]
        public float Dilate;
        [NotKeyable]
        public TextMeshProSdfLengthUnit DilateUnit;
        public TextMeshProFaceLighting Lighting;
        
        public static TextMeshProFace Default => new TextMeshProFace {
            Enabled = true,
            Paint = CanvasPaint.Solid(Color.white),
            Lighting = TextMeshProFaceLighting.Default
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
                && DilateUnit == other.DilateUnit
                && Lighting.Equals(other.Lighting);
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
                hashCode = (hashCode * 397) ^ Lighting.GetHashCode();
                return hashCode;
            }
        }
    }

    [Serializable]
    public struct TextMeshProFaceLighting : IEquatable<TextMeshProFaceLighting>
    {
        [NotKeyable]
        public bool Enabled;
        [NotKeyable]
        public float BevelWidth;
        [NotKeyable]
        public float BevelSoftness;
        public float LightAngle;
        public Color HighlightColor;
        public bool HighlightColorUsesHdrPicker;
        public Color ShadowColor;
        public bool ShadowColorUsesHdrPicker;

        public static TextMeshProFaceLighting Default => new TextMeshProFaceLighting {
            Enabled = false,
            BevelWidth = 0.35f,
            BevelSoftness = 0.35f,
            LightAngle = 135f,
            HighlightColor = new Color(1f, 1f, 1f, 0.65f),
            ShadowColor = new Color(0.45f, 0.24f, 0.05f, 0.35f)
        };

        public bool Equals(TextMeshProFaceLighting other)
        {
            return Enabled == other.Enabled
                && BevelWidth == other.BevelWidth
                && BevelSoftness == other.BevelSoftness
                && LightAngle == other.LightAngle
                && HighlightColor.Equals(other.HighlightColor)
                && ShadowColor.Equals(other.ShadowColor);
        }

        public override bool Equals(object obj)
        {
            return obj is TextMeshProFaceLighting other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Enabled.GetHashCode();
                hashCode = (hashCode * 397) ^ BevelWidth.GetHashCode();
                hashCode = (hashCode * 397) ^ BevelSoftness.GetHashCode();
                hashCode = (hashCode * 397) ^ LightAngle.GetHashCode();
                hashCode = (hashCode * 397) ^ HighlightColor.GetHashCode();
                hashCode = (hashCode * 397) ^ ShadowColor.GetHashCode();
                return hashCode;
            }
        }
    }
}
