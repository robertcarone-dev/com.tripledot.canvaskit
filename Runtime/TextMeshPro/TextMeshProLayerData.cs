using System;
using UnityEngine;
using UnityEngine.Animations;

namespace Tripledot.CanvasKit
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
        public Vector3 offset = Vector3.zero;
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
            face = source.face;
            stroke = source.stroke;
            shadow = source.shadow;
        }

        #region Internal

        internal Vector2 GeometryOffset => offset;

        internal Vector4 GetVisualPadding(float sdfPaddingLimit, Vector2 localUnitsPerAtlasPixel)
        {
            if (!enabled) {
                return Vector4.zero;
            }

            var padding = Vector4.zero;
            padding = TextMeshProUtility.PaddingMax(padding, face.GetVisualPadding(sdfPaddingLimit));
            padding = TextMeshProUtility.PaddingMax(padding, stroke.GetVisualPadding(sdfPaddingLimit, face.Dilate, true, localUnitsPerAtlasPixel));
            padding = TextMeshProUtility.PaddingMax(padding, shadow.GetVisualPadding(sdfPaddingLimit, face.Dilate, true, localUnitsPerAtlasPixel));
            return padding;
        }

        internal float GetSdfPadding()
        {
            if (!enabled) {
                return 0f;
            }

            return face.Dilate + Mathf.Max(stroke.GetSdfRange(), shadow.GetSdfRange());
        }
        
        #endregion

        #region Material

        internal void ApplyMaterial(Material material, TextMeshProLayerMaterialContext context, TextMeshProLayerMaterialGradientState gradientState)
        {
            material.SetInteger(ShaderIds.FaceEnabled, face.Enabled ? 1 : 0);
            var faceUsesGradientAtlas = TextMeshProLayerMaterial.ApplyPaint(
                material, face.Paint, ShaderIds.FacePaint, ShaderKeywords.FaceTexture, face.Enabled, gradientState.Face);
            material.SetFloat(ShaderIds.FaceDilate, TextMeshProUtility.PixelsToFaceDilate(face.Dilate, context.GradientScale));
            var faceLightingEnabled = face.Enabled && face.Lighting.Enabled;
            var faceBevelWidth = Mathf.Clamp01(face.Lighting.BevelWidth);
            material.SetInteger(ShaderIds.FaceLightingEnabled, faceLightingEnabled ? 1 : 0);
            material.SetFloat(ShaderIds.FaceBevelWidth, faceBevelWidth);
            material.SetFloat(ShaderIds.FaceBevelSoftness, Mathf.Clamp01(face.Lighting.BevelSoftness));
            material.SetVector(ShaderIds.FaceLightDirection, AngleToDirection(face.Lighting.LightAngle));
            material.SetColor(ShaderIds.FaceHighlightColor, face.Lighting.HighlightColor);
            material.SetColor(ShaderIds.FaceShadowColor, face.Lighting.ShadowColor);
            CanvasUtility.SetKeyword(material, ShaderKeywords.FaceLighting, faceLightingEnabled && faceBevelWidth > 0f);

            TextMeshProUtility.ClampStrokeEffect(
                stroke.Width, stroke.Feather, stroke.Position, context.AppliedSdfPadding, face.Dilate, 
                out var strokeWidth, out var strokeFeather);
            material.SetInteger(ShaderIds.StrokeEnabled, stroke.Enabled ? 1 : 0);
            material.SetFloat(ShaderIds.StrokeWeight, strokeWidth);
            material.SetFloat(ShaderIds.StrokeSoftness, strokeFeather);
            material.SetInteger(ShaderIds.StrokePosition, (int)stroke.Position);
            material.SetVector(ShaderIds.StrokeOffset, new Vector4(stroke.Offset.x, stroke.Offset.y, 0f, 0f));
            var strokeUsesGradientAtlas = TextMeshProLayerMaterial.ApplyPaint(
                material, stroke.Paint, ShaderIds.StrokePaint, ShaderKeywords.StrokeTexture, stroke.Enabled, gradientState.Stroke);

            TextMeshProUtility.ClampShadowEffect(
                shadow.Spread, shadow.Blur, context.AppliedSdfPadding, face.Dilate,
                out var shadowSpread, out var shadowBlur);
            material.SetInteger(ShaderIds.ShadowEnabled, shadow.Enabled ? 1 : 0);
            material.SetFloat(ShaderIds.ShadowWeight, shadowBlur);
            material.SetFloat(ShaderIds.ShadowSpread, shadowSpread);
            material.SetVector(ShaderIds.ShadowOffset, new Vector4(shadow.Offset.x, shadow.Offset.y, 0f, 0f));
            var shadowUsesGradientAtlas = TextMeshProLayerMaterial.ApplyPaint(
                material, shadow.Paint, ShaderIds.ShadowPaint, ShaderKeywords.ShadowTexture, shadow.Enabled, gradientState.Shadow);

            CanvasUtility.SetKeyword(material, ShaderKeywords.GradientAtlas, 
                faceUsesGradientAtlas || strokeUsesGradientAtlas || shadowUsesGradientAtlas);
            
            TextMeshProLayerMaterial.ApplySharedTextProperties(material, context, blendMode, Opacity);
        }

        private static Vector4 AngleToDirection(float angle)
        {
            var radians = angle * Mathf.Deg2Rad;
            return new Vector4(Mathf.Cos(radians), Mathf.Sin(radians), 0f, 0f);
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
            public const string FaceTexture = "FACE_TEXTURE_ON";
            public const string StrokeTexture = "STROKE_TEXTURE_ON";
            public const string ShadowTexture = "SHADOW_TEXTURE_ON";
            public const string GradientAtlas = "GRADIENT_ATLAS_ON";
            public const string FaceLighting = "FACE_LIGHTING_ON";
        }

        private static class ShaderIds
        {
            public static readonly int FaceEnabled = Shader.PropertyToID("_FaceEnabled");
            public static readonly int FaceColor = Shader.PropertyToID("_FaceColor");
            public static readonly int FaceColorB = Shader.PropertyToID("_FaceColorB");
            public static readonly int FaceDilate = Shader.PropertyToID("_FaceDilate");

            public static readonly int FaceLightingEnabled = Shader.PropertyToID("_FaceLightingEnabled");
            public static readonly int FaceBevelWidth = Shader.PropertyToID("_FaceBevelWidth");
            public static readonly int FaceBevelSoftness = Shader.PropertyToID("_FaceBevelSoftness");
            public static readonly int FaceLightDirection = Shader.PropertyToID("_FaceLightDirection");
            public static readonly int FaceHighlightColor = Shader.PropertyToID("_FaceHighlightColor");
            public static readonly int FaceShadowColor = Shader.PropertyToID("_FaceShadowColor");

            public static readonly TextMeshProLayerMaterial.PaintShaderIds FacePaint = new TextMeshProLayerMaterial.PaintShaderIds(
                Shader.PropertyToID("_FacePaintMode"),
                FaceColor,
                FaceColorB,
                Shader.PropertyToID("_FaceTexture"),
                Shader.PropertyToID("_FaceGradientAngle"),
                Shader.PropertyToID("_FaceGradientAtlasRect"),
                Shader.PropertyToID("_FaceTextureTransform"),
                Shader.PropertyToID("_FacePaintTransform0"),
                Shader.PropertyToID("_FacePaintTransform1"));

            public static readonly int StrokeEnabled = Shader.PropertyToID("_StrokeEnabled");
            public static readonly int StrokeWeight = Shader.PropertyToID("_StrokeWeight");
            public static readonly int StrokeSoftness = Shader.PropertyToID("_StrokeSoftness");
            public static readonly int StrokePosition = Shader.PropertyToID("_StrokePosition");
            public static readonly int StrokeOffset = Shader.PropertyToID("_StrokeOffset");

            public static readonly TextMeshProLayerMaterial.PaintShaderIds StrokePaint = new TextMeshProLayerMaterial.PaintShaderIds(
                Shader.PropertyToID("_StrokePaintMode"),
                Shader.PropertyToID("_StrokeColor"),
                Shader.PropertyToID("_StrokeColorB"),
                Shader.PropertyToID("_StrokeTexture"),
                Shader.PropertyToID("_StrokeGradientAngle"),
                Shader.PropertyToID("_StrokeGradientAtlasRect"),
                Shader.PropertyToID("_StrokeTextureTransform"),
                Shader.PropertyToID("_StrokePaintTransform0"),
                Shader.PropertyToID("_StrokePaintTransform1"));

            public static readonly int ShadowEnabled = Shader.PropertyToID("_ShadowEnabled");
            public static readonly int ShadowWeight = Shader.PropertyToID("_ShadowWeight");
            public static readonly int ShadowSpread = Shader.PropertyToID("_ShadowSpread");
            public static readonly int ShadowOffset = Shader.PropertyToID("_ShadowOffset");

            public static readonly TextMeshProLayerMaterial.PaintShaderIds ShadowPaint = new TextMeshProLayerMaterial.PaintShaderIds(
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
