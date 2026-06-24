using System;
using UnityEngine;
using UnityEngine.Animations;

namespace Tripledot.CanvasKit.TextMeshPro
{
    [Serializable]
    public struct TextMeshProShadow
    {
        public static TextMeshProShadow Default =>
            new TextMeshProShadow {
                Enabled = true,
                Paint = CanvasPaint.Solid(new Color(0f, 0f, 0f, 0.5f)),
                Offset = new Vector2(0f, -2f),
                Blur = 4f
            };
        
        [NotKeyable]
        public bool Enabled;
        public CanvasPaint Paint;
        [NotKeyable]
        public Vector2 Offset;

        [NotKeyable]
        public float Spread;

        [NotKeyable]
        public float Blur;

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
    }
}
