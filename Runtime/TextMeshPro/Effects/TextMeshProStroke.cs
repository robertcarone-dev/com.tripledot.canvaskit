using System;
using UnityEngine;
using UnityEngine.Animations;

namespace Tripledot.CanvasKit.TextMeshPro
{
    public enum TextMeshProStrokePosition
    {
        Outside = 0,
        Center = 1,
        Inside = 2
    }

    [Serializable]
    public struct TextMeshProStroke
    {
        public static TextMeshProStroke Default =>
            new TextMeshProStroke {
                Enabled = true,
                Paint = CanvasPaint.Solid(Color.black),
                Position = TextMeshProStrokePosition.Outside,
                Width = 1f
            };
        
        [NotKeyable]
        public bool Enabled;
        public CanvasPaint Paint;

        [NotKeyable]
        public TextMeshProStrokePosition Position;
        [NotKeyable]
        public float Width;

        [NotKeyable]
        public float Feather;

        [NotKeyable]
        public Vector2 Offset;

        internal float GetSdfRange()
        {
            return !Enabled ? 0f : Width * TextMeshProUtility.GetStrokeVisualPaddingFactor(Position) + Feather;
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
    }
}
