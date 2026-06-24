using System;
using UnityEngine;
using UnityEngine.Animations;

namespace Tripledot.CanvasKit.TextMeshPro
{
    [Serializable]
    public sealed class TextMeshProLayerData
    {
        [SerializeField]
        [NotKeyable]
        private bool enabled = true;
        [SerializeField]
        private string label = "Layer";
        [SerializeField]
        private CanvasBlendMode blendMode = CanvasBlendMode.PremultipliedAlpha;
        [SerializeField]
        private float opacity = 1f;
        [SerializeField]
        [NotKeyable]
        private Vector3 offset = Vector3.zero;
        [SerializeField]
        private TextMeshProFace face = TextMeshProFace.Default;
        [SerializeField]
        private TextMeshProStroke stroke = DisabledStroke();
        [SerializeField]
        private TextMeshProShadow shadow = DisabledShadow();

        public bool Enabled {
            get => enabled;
            set => enabled = value;
        }

        public string Label {
            get => label;
            set => label = value ?? string.Empty;
        }

        public CanvasBlendMode BlendMode {
            get => blendMode;
            set => blendMode = value;
        }

        public float Opacity {
            get => Mathf.Clamp01(opacity);
            set => opacity = Mathf.Clamp01(value);
        }

        public Vector3 Offset {
            get => offset;
            set => offset = value;
        }

        public TextMeshProFace Face {
            get => face;
            set => face = value;
        }

        public TextMeshProStroke Stroke {
            get => stroke;
            set => stroke = value;
        }

        public TextMeshProShadow Shadow {
            get => shadow;
            set => shadow = value;
        }

        public static TextMeshProLayerData Default()
        {
            return new TextMeshProLayerData {
                label = "Layer",
                face = TextMeshProFace.Default,
                stroke = DisabledStroke(),
                shadow = DisabledShadow()
            };
        }

        public static TextMeshProLayerData StrokePreset()
        {
            return new TextMeshProLayerData {
                label = "Stroke",
                face = DisabledFace(),
                stroke = TextMeshProStroke.Default,
                shadow = DisabledShadow()
            };
        }

        public static TextMeshProLayerData ShadowPreset()
        {
            return new TextMeshProLayerData {
                label = "Shadow",
                face = DisabledFace(),
                stroke = DisabledStroke(),
                shadow = TextMeshProShadow.Default
            };
        }

        public static TextMeshProLayerData GlowPreset()
        {
            var glow = TextMeshProShadow.Default;
            glow.Paint = CanvasPaint.Solid(new Color(1f, 1f, 1f, 0.5f));
            glow.Offset = Vector2.zero;

            return new TextMeshProLayerData {
                label = "Glow",
                blendMode = CanvasBlendMode.Additive,
                face = DisabledFace(),
                stroke = DisabledStroke(),
                shadow = glow
            };
        }

        public TextMeshProLayerData Clone()
        {
            var copy = new TextMeshProLayerData();
            copy.CopyFrom(this);
            return copy;
        }

        public void CopyFrom(TextMeshProLayerData source)
        {
            if (source == null) {
                return;
            }

            enabled = source.enabled;
            label = source.label;
            blendMode = source.blendMode;
            opacity = source.Opacity;
            offset = source.offset;
            face = source.face;
            stroke = source.stroke;
            shadow = source.shadow;
        }

        internal Vector2 GeometryOffset => offset;

        internal Vector4 GetVisualPadding(float sdfPaddingLimit, Vector2 localUnitsPerAtlasPixel)
        {
            if (!enabled) {
                return Vector4.zero;
            }

            var faceSdfRange = face.GetSdfRange();
            var padding = Vector4.zero;
            padding = TextMeshProUtility.PaddingMax(padding, face.GetVisualPadding(sdfPaddingLimit));
            padding = TextMeshProUtility.PaddingMax(padding, stroke.GetVisualPadding(sdfPaddingLimit, faceSdfRange, true, localUnitsPerAtlasPixel));
            padding = TextMeshProUtility.PaddingMax(padding, shadow.GetVisualPadding(sdfPaddingLimit, faceSdfRange, true, localUnitsPerAtlasPixel));
            return padding;
        }

        internal float GetSdfPadding()
        {
            return !enabled ? 0f : face.GetSdfRange() + Mathf.Max(stroke.GetSdfRange(), shadow.GetSdfRange());
        }

        private static TextMeshProFace DisabledFace()
        {
            var value = TextMeshProFace.Default;
            value.Enabled = false;
            return value;
        }

        private static TextMeshProStroke DisabledStroke()
        {
            var value = TextMeshProStroke.Default;
            value.Enabled = false;
            return value;
        }

        private static TextMeshProShadow DisabledShadow()
        {
            var value = TextMeshProShadow.Default;
            value.Enabled = false;
            return value;
        }
    }
}
