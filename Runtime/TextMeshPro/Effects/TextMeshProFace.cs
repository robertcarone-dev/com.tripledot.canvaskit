using System;
using UnityEngine;
using UnityEngine.Animations;

namespace Tripledot.CanvasKit.TextMeshPro
{
    [Serializable]
    public struct TextMeshProFace
    {
        public static TextMeshProFace Default =>
            new TextMeshProFace {
                Enabled = true,
                Paint = CanvasPaint.Solid(Color.white),
                Lighting = TextMeshProFaceLighting.Default
            };
        
        [NotKeyable]
        public bool Enabled;
        public CanvasPaint Paint;
        [NotKeyable]
        public float Dilate;
        public TextMeshProFaceLighting Lighting;

        internal float GetSdfRange()
        {
            return Enabled ? Dilate : 0f;
        }

        internal Vector4 GetVisualPadding(float sdfPaddingLimit)
        {
            var range = Mathf.Min(TextMeshProUtility.SdfPixelsToPaddingPixels(GetSdfRange()), sdfPaddingLimit);
            return range > 0f ? TextMeshProUtility.PaddingUniform(range) : Vector4.zero;
        }
    }
}
