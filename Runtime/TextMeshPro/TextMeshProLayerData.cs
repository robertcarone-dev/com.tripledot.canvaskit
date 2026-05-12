using System;
using UnityEngine;

namespace Tripledot.CanvasKit
{
    [Serializable]
    public sealed class TextMeshProLayerData
    {
        [SerializeField]
        private bool enabled = true;
        [SerializeField]
        private string label = string.Empty;
        [SerializeField]
        private CanvasBlendMode blendMode = CanvasBlendMode.PremultipliedAlpha;
        [SerializeField]
        private float opacity = 1f;
        [SerializeField]
        private TextMeshProFace face = TextMeshProFace.Default;
        [SerializeField]
        private TextMeshProStroke stroke = DisabledStroke();
        [SerializeField]
        private TextMeshProShadow shadow = DisabledShadow();

        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        public string Label
        {
            get => label;
            set => label = value ?? string.Empty;
        }

        public CanvasBlendMode BlendMode
        {
            get => blendMode;
            set => blendMode = value;
        }

        public float Opacity
        {
            get => Mathf.Clamp01(opacity);
            set => opacity = Mathf.Clamp01(value);
        }

        public TextMeshProFace Face
        {
            get => face;
            set => face = value;
        }

        public TextMeshProStroke Stroke
        {
            get => stroke;
            set => stroke = value;
        }

        public TextMeshProShadow Shadow
        {
            get => shadow;
            set => shadow = value;
        }

        public static TextMeshProLayerData Layer()
        {
            return new TextMeshProLayerData {
                label = "Layer"
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
            face = source.face;
            stroke = source.stroke;
            shadow = source.shadow;
        }

        #region Internal

        internal Vector2 GeometryOffset => Vector2.zero;

        internal string MaterialName => "TextMeshPro Layer";

        internal Vector4 GetVisualPadding(float sdfPaddingLimit)
        {
            return GetVisualPadding(sdfPaddingLimit, Vector2.one);
        }

        internal Vector4 GetVisualPadding(float sdfPaddingLimit, Vector2 localUnitsPerAtlasPixel)
        {
            if (!enabled) {
                return Vector4.zero;
            }

            var faceRange = face.Enabled ? Mathf.Max(0f, face.Dilate) : 0f;
            var padding = Vector4.zero;
            padding = TextMeshProUtility.PaddingMax(padding, face.GetVisualPadding(sdfPaddingLimit));
            padding = TextMeshProUtility.PaddingMax(padding, stroke.GetVisualPadding(sdfPaddingLimit, faceRange, true, localUnitsPerAtlasPixel));
            padding = TextMeshProUtility.PaddingMax(padding, shadow.GetVisualPadding(sdfPaddingLimit, faceRange, true, localUnitsPerAtlasPixel));
            return padding;
        }

        internal float GetSdfPadding()
        {
            if (!enabled) {
                return 0f;
            }

            var faceRange = face.Enabled ? Mathf.Max(0f, face.Dilate) : 0f;
            return faceRange + Mathf.Max(stroke.GetEffectSdfRange(), shadow.GetSdfRange());
        }

        internal void ApplyMaterial(Material material, TextMeshProLayerMaterialContext context)
        {
            ApplyLayerMaterial(material, context);
        }

        #endregion

        #region Material

        private void ApplyLayerMaterial(Material material, TextMeshProLayerMaterialContext context)
        {
            material.SetInteger(ShaderIds.FaceEnabled, face.Enabled ? 1 : 0);
            var faceUsesGradientAtlas = TextMeshProLayerMaterial.ApplyPaint(
                material,
                face.Paint,
                ShaderIds.FacePaint,
                ShaderKeywords.FaceTexture,
                ShaderKeywords.FaceGradientAtlas,
                face.Enabled);

            if (!face.Enabled) {
                material.SetColor(ShaderIds.FaceColor, default);
                material.SetColor(ShaderIds.FaceColorB, default);
            }

            material.SetFloat(ShaderIds.FaceDilate, TextMeshProUtility.PixelsToFaceDilate(face.Dilate, context.GradientScale));

            var facePadding = face.Enabled ? Mathf.Max(0f, face.Dilate) : 0f;
            TextMeshProUtility.ClampStrokeEffect(stroke.EffectiveWidth, stroke.EffectiveFeather, stroke.Position, context.AppliedSdfPadding, facePadding, out var strokeWidth, out var strokeFeather);
            material.SetInteger(ShaderIds.StrokeEnabled, stroke.Enabled ? 1 : 0);
            material.SetFloat(ShaderIds.StrokeWeight, strokeWidth);
            material.SetFloat(ShaderIds.StrokeSoftness, strokeFeather);
            material.SetInteger(ShaderIds.StrokePosition, (int)stroke.Position);
            material.SetVector(ShaderIds.StrokeOffset, new Vector4(stroke.Offset.x, stroke.Offset.y, 0f, 0f));
            var strokeUsesGradientAtlas = TextMeshProLayerMaterial.ApplyPaint(
                material,
                stroke.Paint,
                ShaderIds.StrokePaint,
                ShaderKeywords.StrokeTexture,
                ShaderKeywords.StrokeGradientAtlas,
                stroke.Enabled);

            TextMeshProUtility.ClampShadowEffect(shadow.Spread, shadow.EffectiveBlur, context.AppliedSdfPadding, facePadding, out var shadowSpread, out var shadowBlur);
            material.SetInteger(ShaderIds.ShadowEnabled, shadow.Enabled ? 1 : 0);
            material.SetFloat(ShaderIds.ShadowWeight, shadowBlur);
            material.SetFloat(ShaderIds.ShadowSpread, shadowSpread);
            material.SetVector(ShaderIds.ShadowOffset, new Vector4(shadow.Offset.x, shadow.Offset.y, 0f, 0f));
            var shadowUsesGradientAtlas = TextMeshProLayerMaterial.ApplyPaint(
                material,
                shadow.Paint,
                ShaderIds.ShadowPaint,
                ShaderKeywords.ShadowTexture,
                ShaderKeywords.ShadowGradientAtlas,
                shadow.Enabled);

            TextMeshProLayerMaterial.SetKeyword(material, ShaderKeywords.Face, face.Enabled);
            TextMeshProLayerMaterial.SetKeyword(material, ShaderKeywords.Stroke, stroke.Enabled);
            TextMeshProLayerMaterial.SetKeyword(material, ShaderKeywords.Shadow, shadow.Enabled);
            TextMeshProLayerMaterial.SetKeyword(material, ShaderKeywords.GradientAtlas, faceUsesGradientAtlas || strokeUsesGradientAtlas || shadowUsesGradientAtlas);
            TextMeshProLayerMaterial.ApplySharedTextProperties(material, context, blendMode, Opacity);
        }

        #endregion

        #region Utility

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

        #endregion

        #region Shader Properties

        private static class ShaderKeywords
        {
            internal const string Face = "FACE_ON";
            internal const string Stroke = "STROKE_ON";
            internal const string Shadow = "SHADOW_ON";
            internal const string FaceTexture = "FACE_TEXTURE_ON";
            internal const string StrokeTexture = "STROKE_TEXTURE_ON";
            internal const string ShadowTexture = "SHADOW_TEXTURE_ON";
            internal const string FaceGradientAtlas = "FACE_GRADIENT_ATLAS_ON";
            internal const string StrokeGradientAtlas = "STROKE_GRADIENT_ATLAS_ON";
            internal const string ShadowGradientAtlas = "SHADOW_GRADIENT_ATLAS_ON";
            internal const string GradientAtlas = "GRADIENT_ATLAS_ON";
        }

        private static class ShaderIds
        {
            internal static readonly int FaceEnabled = Shader.PropertyToID("_FaceEnabled");
            internal static readonly int FaceColor = Shader.PropertyToID("_FaceColor");
            internal static readonly int FaceColorB = Shader.PropertyToID("_FaceColorB");
            internal static readonly int FaceDilate = Shader.PropertyToID("_FaceDilate");

            internal static readonly TextMeshProLayerMaterial.PaintShaderIds FacePaint = new TextMeshProLayerMaterial.PaintShaderIds(
                Shader.PropertyToID("_FacePaintMode"),
                FaceColor,
                FaceColorB,
                Shader.PropertyToID("_FaceTexture"),
                Shader.PropertyToID("_FaceGradientAngle"),
                Shader.PropertyToID("_FaceGradientAtlasRect"),
                Shader.PropertyToID("_FaceTextureTransform"),
                Shader.PropertyToID("_FacePaintTransform0"),
                Shader.PropertyToID("_FacePaintTransform1"));

            internal static readonly int StrokeEnabled = Shader.PropertyToID("_StrokeEnabled");
            internal static readonly int StrokeWeight = Shader.PropertyToID("_StrokeWeight");
            internal static readonly int StrokeSoftness = Shader.PropertyToID("_StrokeSoftness");
            internal static readonly int StrokePosition = Shader.PropertyToID("_StrokePosition");
            internal static readonly int StrokeOffset = Shader.PropertyToID("_StrokeOffset");

            internal static readonly TextMeshProLayerMaterial.PaintShaderIds StrokePaint = new TextMeshProLayerMaterial.PaintShaderIds(
                Shader.PropertyToID("_StrokePaintMode"),
                Shader.PropertyToID("_StrokeColor"),
                Shader.PropertyToID("_StrokeColorB"),
                Shader.PropertyToID("_StrokeTexture"),
                Shader.PropertyToID("_StrokeGradientAngle"),
                Shader.PropertyToID("_StrokeGradientAtlasRect"),
                Shader.PropertyToID("_StrokeTextureTransform"),
                Shader.PropertyToID("_StrokePaintTransform0"),
                Shader.PropertyToID("_StrokePaintTransform1"));

            internal static readonly int ShadowEnabled = Shader.PropertyToID("_ShadowEnabled");
            internal static readonly int ShadowWeight = Shader.PropertyToID("_ShadowWeight");
            internal static readonly int ShadowSpread = Shader.PropertyToID("_ShadowSpread");
            internal static readonly int ShadowOffset = Shader.PropertyToID("_ShadowOffset");

            internal static readonly TextMeshProLayerMaterial.PaintShaderIds ShadowPaint = new TextMeshProLayerMaterial.PaintShaderIds(
                Shader.PropertyToID("_ShadowPaintMode"),
                Shader.PropertyToID("_ShadowColor"),
                Shader.PropertyToID("_ShadowColorB"),
                Shader.PropertyToID("_ShadowTexture"),
                Shader.PropertyToID("_ShadowGradientAngle"),
                Shader.PropertyToID("_ShadowGradientAtlasRect"),
                Shader.PropertyToID("_ShadowTextureTransform"),
                Shader.PropertyToID("_ShadowPaintTransform0"),
                Shader.PropertyToID("_ShadowPaintTransform1"));
        }

        #endregion
    }
}
