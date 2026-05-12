using System;
using UnityEngine;

namespace Tripledot.CanvasKit
{
    public enum CanvasPaintType
    {
        Solid = 0,
        LinearGradient = 1,
        RadialGradient = 2,
        Texture = 3
    }
    
    public enum CanvasGradientMode
    {
        Simple = 0,
        Texture = 1
    }
    
    [Serializable]
    public struct CanvasPaint : IEquatable<CanvasPaint>
    {
        public CanvasPaintType Type;
        public float Opacity;
        
        public Color Color;
        public bool ColorUsesHdrPicker;
        public Color SecondaryColor;
        public bool SecondaryColorUsesHdrPicker;
        
        public Gradient Gradient;
        public CanvasGradientMode GradientMode;
        public Texture2D Texture;
        public CanvasPaintTransform Transform;

        public static CanvasPaint Solid(Color color)
        {
            return new CanvasPaint { Type = CanvasPaintType.Solid, Color = color, SecondaryColor = color, Opacity = 1f, Transform = CanvasPaintTransform.Default };
        }

        internal bool IsGradientPaint => Type is CanvasPaintType.LinearGradient or CanvasPaintType.RadialGradient;

        internal bool HasFullGradient => Gradient != null && IsGradientPaint && GradientMode == CanvasGradientMode.Texture;

        public bool Equals(CanvasPaint other)
        {
            return Type == other.Type
                && GradientMode == other.GradientMode
                && Color == other.Color
                && SecondaryColor == other.SecondaryColor
                && Opacity == other.Opacity
                && Equals(Gradient, other.Gradient)
                && Texture == other.Texture
                && Transform.Equals(other.Transform);
        }

        public override bool Equals(object obj)
        {
            return obj is CanvasPaint other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Type.GetHashCode();
                hashCode = (hashCode * 397) ^ GradientMode.GetHashCode();
                hashCode = (hashCode * 397) ^ Color.GetHashCode();
                hashCode = (hashCode * 397) ^ SecondaryColor.GetHashCode();
                hashCode = (hashCode * 397) ^ Opacity.GetHashCode();
                hashCode = (hashCode * 397) ^ (Gradient != null ? Gradient.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Texture != null ? Texture.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Transform.GetHashCode();
                return hashCode;
            }
        }
    }
}
