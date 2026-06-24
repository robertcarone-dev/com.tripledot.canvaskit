using System;
using UnityEngine;
using UnityEngine.Animations;

namespace Tripledot.CanvasKit.TextMeshPro
{
    [Serializable]
    public struct TextMeshProFaceLighting
    {
        public static TextMeshProFaceLighting Default =>
            new TextMeshProFaceLighting {
                Enabled = false,
                BevelWidth = 0.35f,
                BevelSoftness = 0.35f,
                LightAngle = 135f,
                HighlightColor = new Color(1f, 1f, 1f, 0.65f),
                ShadowColor = new Color(0.45f, 0.24f, 0.05f, 0.35f)
            };
        
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
    }
}
