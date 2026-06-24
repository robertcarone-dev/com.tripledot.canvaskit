using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tripledot.CanvasKit.TextMeshPro
{
    internal static class TextMeshProLayerMaterial
    {
        public static Material CreateRuntimeMaterial()
        {
            var shader = ResolveCoreShader();
            return new Material(shader) {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        public static Shader ResolveCoreShader()
        {
            GraphicsSettings.TryGetRenderPipelineSettings<CanvasKitResourcesURP>(out var resources);
            if (resources == null) {
                throw new InvalidOperationException(
                    $"Failed to resolve required text shader: render pipeline resources are missing a {nameof(CanvasKitResourcesURP)} asset.");
            }

            var shader = resources.TextMeshProCoreShader;

            if (shader == null) {
                throw new InvalidOperationException(
                    "Failed to resolve required text shader from render pipeline resources. " +
                    "Make sure the core TextMeshPro shader is assigned in the CanvasKitResourcesURP asset.");
            }

            return shader;
        }

        public static void ApplyLayer(Material material, TextMeshProLayerData layer, TextMeshProLayerMaterialContext context, TextMeshProLayerMaterialGradientState gradientState)
        {
            var face = layer.Face;
            var stroke = layer.Stroke;
            var shadow = layer.Shadow;

            material.SetInteger(ShaderIds.FaceEnabled, face.Enabled ? 1 : 0);
            var faceUsesGradientAtlas = ApplyPaint(material, face.Paint, ShaderIds.FacePaint, ShaderKeywords.FaceTexture, face.Enabled, gradientState.Face);
            var faceSdfRange = face.GetSdfRange();
            material.SetFloat(ShaderIds.FaceDilate, TextMeshProUtility.PixelsToFaceDilate(faceSdfRange, context.GradientScale));

            var faceLightingEnabled = face.Enabled && face.Lighting.Enabled;
            var faceBevelWidth = Mathf.Clamp01(face.Lighting.BevelWidth);
            material.SetInteger(ShaderIds.FaceLightingEnabled, faceLightingEnabled ? 1 : 0);
            material.SetFloat(ShaderIds.FaceBevelWidth, faceBevelWidth);
            material.SetFloat(ShaderIds.FaceBevelSoftness, Mathf.Clamp01(face.Lighting.BevelSoftness));
            material.SetVector(ShaderIds.FaceLightDirection, AngleToDirection(face.Lighting.LightAngle));
            material.SetColor(ShaderIds.FaceHighlightColor, face.Lighting.HighlightColor);
            material.SetColor(ShaderIds.FaceShadowColor, face.Lighting.ShadowColor);
            CoreUtils.SetKeyword(material, ShaderKeywords.FaceLighting, faceLightingEnabled && faceBevelWidth > 0f);

            TextMeshProUtility.ClampStrokeEffect(stroke.Width, stroke.Feather, stroke.Position, context.AppliedSdfPadding, faceSdfRange, out var strokeWidth, out var strokeFeather);
            material.SetInteger(ShaderIds.StrokeEnabled, stroke.Enabled ? 1 : 0);
            material.SetFloat(ShaderIds.StrokeWeight, strokeWidth);
            material.SetFloat(ShaderIds.StrokeSoftness, strokeFeather);
            material.SetInteger(ShaderIds.StrokePosition, (int)stroke.Position);
            material.SetVector(ShaderIds.StrokeOffset, new Vector4(stroke.Offset.x, stroke.Offset.y, 0f, 0f));
            var strokeUsesGradientAtlas = ApplyPaint(material, stroke.Paint, ShaderIds.StrokePaint, ShaderKeywords.StrokeTexture, stroke.Enabled, gradientState.Stroke);

            TextMeshProUtility.ClampShadowEffect(shadow.Spread, shadow.Blur, context.AppliedSdfPadding, faceSdfRange, out var shadowSpread, out var shadowBlur);
            material.SetInteger(ShaderIds.ShadowEnabled, shadow.Enabled ? 1 : 0);
            material.SetFloat(ShaderIds.ShadowWeight, shadowBlur);
            material.SetFloat(ShaderIds.ShadowSpread, shadowSpread);
            material.SetVector(ShaderIds.ShadowOffset, new Vector4(shadow.Offset.x, shadow.Offset.y, 0f, 0f));
            var shadowUsesGradientAtlas = ApplyPaint(material, shadow.Paint, ShaderIds.ShadowPaint, ShaderKeywords.ShadowTexture, shadow.Enabled, gradientState.Shadow);
            CoreUtils.SetKeyword(material, ShaderKeywords.GradientAtlas, faceUsesGradientAtlas || strokeUsesGradientAtlas || shadowUsesGradientAtlas);

            ApplySharedTextProperties(material, context, layer.BlendMode, layer.Opacity);
        }

        public static void ApplySharedTextProperties(Material material, TextMeshProLayerMaterialContext context, CanvasBlendMode blendPreset, float layerOpacity)
        {
            material.SetTexture(ShaderIds.MainTex, context.FontAtlas);
            material.SetFloat(ShaderIds.LayerOpacity, Mathf.Clamp01(layerOpacity));
            material.SetFloat(ShaderIds.ScaleRatioA, context.ScaleRatioA);
            material.SetFloat(ShaderIds.GradientScale, context.GradientScale);
            material.SetFloat(ShaderIds.Sharpness, context.Sharpness);
            material.SetFloat(ShaderIds.ScaleX, context.ScaleX);
            material.SetFloat(ShaderIds.ScaleY, context.ScaleY);
            material.SetFloat(ShaderIds.PerspectiveFilter, context.PerspectiveFilter);
            material.SetFloat(ShaderIds.WeightNormal, context.WeightNormal);
            material.SetFloat(ShaderIds.WeightBold, context.WeightBold);
            material.SetVector(CanvasShaderIds.ClipRect, context.ClipRect);
            material.SetFloat(CanvasShaderIds.UIMaskSoftnessX, context.MaskSoftnessX);
            material.SetFloat(CanvasShaderIds.UIMaskSoftnessY, context.MaskSoftnessY);
            material.SetFloat(CanvasShaderIds.StencilComp, context.StencilComp);
            material.SetFloat(CanvasShaderIds.Stencil, context.Stencil);
            material.SetFloat(CanvasShaderIds.StencilOp, context.StencilOp);
            material.SetFloat(CanvasShaderIds.StencilWriteMask, context.StencilWriteMask);
            material.SetFloat(CanvasShaderIds.StencilReadMask, context.StencilReadMask);
            material.SetFloat(CanvasShaderIds.CullMode, context.CullMode);
            material.SetFloat(CanvasShaderIds.ColorMask, context.ColorMask);
            ApplyBlend(material, blendPreset);
        }

        public static bool ApplyPaint(Material material, CanvasPaint paint, PaintShaderIds ids, string textureKeyword, bool resourcesEnabled, CanvasGradientAtlas.Lease gradientLease)
        {
            var transform = paint.Transform;
            var textureEnabled = resourcesEnabled && paint.Type == CanvasPaintType.Texture && paint.Texture != null;
            var paintMode = paint.Type == CanvasPaintType.Texture && !textureEnabled ? CanvasPaintType.Solid : paint.Type;
            var primaryColor = paint.HasFullGradient ? Color.white : GetPrimaryPaintColor(paint, paintMode);
            var secondaryColor = paint.HasFullGradient ? Color.white : GetSecondaryPaintColor(paint, paintMode);

            material.SetInteger(ids.PaintMode, (int)paintMode);
            material.SetColor(ids.Color, CanvasUtility.WithOpacity(primaryColor, paint.Opacity));
            material.SetColor(ids.ColorB, CanvasUtility.WithOpacity(secondaryColor, paint.Opacity));
            material.SetTexture(ids.Texture, textureEnabled ? paint.Texture : Texture2D.whiteTexture);
            material.SetFloat(ids.GradientAngle, transform.Rotation);
            material.SetVector(ids.TextureTransform, new Vector4(transform.Offset.x, transform.Offset.y, transform.Scale.x, transform.Scale.y));
            SetPaintTransform(material, ids.PaintTransform0, ids.PaintTransform1, transform);

            var gradientAtlasEnabled = SetGradientAtlas(material, ids.GradientAtlasRect, paint, resourcesEnabled, gradientLease);
            if (!string.IsNullOrEmpty(textureKeyword)) {
                CoreUtils.SetKeyword(material, textureKeyword, textureEnabled);
            }

            return gradientAtlasEnabled;
        }

        private static void ApplyBlend(Material material, CanvasBlendMode preset)
        {
            var src = BlendMode.One;
            var dst = BlendMode.OneMinusSrcAlpha;
            switch (preset) {
                case CanvasBlendMode.StraightAlpha:
                    src = BlendMode.SrcAlpha;
                    break;
                case CanvasBlendMode.Additive:
                    src = BlendMode.SrcAlpha;
                    dst = BlendMode.One;
                    break;
                case CanvasBlendMode.Multiply:
                    src = BlendMode.DstColor;
                    dst = BlendMode.Zero;
                    break;
                case CanvasBlendMode.Screen:
                    src = BlendMode.OneMinusDstColor;
                    dst = BlendMode.One;
                    break;
            }

            material.SetInteger(ShaderIds.BlendMode, (int)preset);
            material.SetInteger(ShaderIds.StyleBlendSrc, (int)src);
            material.SetInteger(ShaderIds.StyleBlendDst, (int)dst);
            material.SetInteger(ShaderIds.StyleBlendSrcAlpha, (int)BlendMode.One);
            material.SetInteger(ShaderIds.StyleBlendDstAlpha, (int)BlendMode.OneMinusSrcAlpha);
            material.SetInteger(ShaderIds.StyleBlendOp, (int)BlendOp.Add);
        }

        private static Vector4 AngleToDirection(float angle)
        {
            var radians = angle * Mathf.Deg2Rad;
            return new Vector4(Mathf.Cos(radians), Mathf.Sin(radians), 0f, 0f);
        }

        private static void SetPaintTransform(Material material, int transform0Id, int transform1Id, CanvasPaintTransform transform)
        {
            material.SetVector(transform0Id, new Vector4(transform.Center.x, transform.Center.y, transform.Offset.x, transform.Offset.y));
            material.SetVector(transform1Id, new Vector4(transform.Scale.x, transform.Scale.y, transform.Rotation, (float)transform.WrapMode));
        }

        private static bool SetGradientAtlas(Material material, int rectId, CanvasPaint paint, bool resourcesEnabled,
            CanvasGradientAtlas.Lease gradientLease)
        {
            if (CanvasGradientAtlas.TryGetEntry(paint, resourcesEnabled, gradientLease, out var entry)) {
                material.SetTexture(ShaderIds.GradientAtlas, entry.Texture);
                material.SetVector(rectId, entry.Rect);
                return true;
            }

            material.SetVector(rectId, Vector4.zero);
            return false;
        }

        private static Color GetPrimaryPaintColor(CanvasPaint paint, CanvasPaintType paintMode)
        {
            return paintMode == CanvasPaintType.Texture ? Color.white : paint.Color;
        }

        private static Color GetSecondaryPaintColor(CanvasPaint paint, CanvasPaintType paintMode)
        {
            return paintMode == CanvasPaintType.Texture ? Color.white : paint.SecondaryColor;
        }

        internal readonly struct PaintShaderIds
        {
            public readonly int PaintMode;
            public readonly int Color;
            public readonly int ColorB;
            public readonly int Texture;
            public readonly int GradientAngle;
            public readonly int GradientAtlasRect;
            public readonly int TextureTransform;
            public readonly int PaintTransform0;
            public readonly int PaintTransform1;

            public PaintShaderIds(
                int paintMode, int color, int colorB, int texture, int gradientAngle, int gradientAtlasRect,
                int textureTransform, int paintTransform0, int paintTransform1)
            {
                PaintMode = paintMode;
                Color = color;
                ColorB = colorB;
                Texture = texture;
                GradientAngle = gradientAngle;
                GradientAtlasRect = gradientAtlasRect;
                TextureTransform = textureTransform;
                PaintTransform0 = paintTransform0;
                PaintTransform1 = paintTransform1;
            }
        }

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
            public static readonly int MainTex = Shader.PropertyToID("_MainTex");
            public static readonly int GradientAtlas = Shader.PropertyToID("_GradientAtlas");
            public static readonly int LayerOpacity = Shader.PropertyToID("_LayerOpacity");
            public static readonly int BlendMode = Shader.PropertyToID("_BlendMode");
            public static readonly int StyleBlendSrc = Shader.PropertyToID("_StyleBlendSrc");
            public static readonly int StyleBlendDst = Shader.PropertyToID("_StyleBlendDst");
            public static readonly int StyleBlendSrcAlpha = Shader.PropertyToID("_StyleBlendSrcAlpha");
            public static readonly int StyleBlendDstAlpha = Shader.PropertyToID("_StyleBlendDstAlpha");
            public static readonly int StyleBlendOp = Shader.PropertyToID("_StyleBlendOp");
            public static readonly int ScaleRatioA = Shader.PropertyToID("_ScaleRatioA");
            public static readonly int GradientScale = Shader.PropertyToID("_GradientScale");
            public static readonly int Sharpness = Shader.PropertyToID("_Sharpness");
            public static readonly int ScaleX = Shader.PropertyToID("_ScaleX");
            public static readonly int ScaleY = Shader.PropertyToID("_ScaleY");
            public static readonly int PerspectiveFilter = Shader.PropertyToID("_PerspectiveFilter");
            public static readonly int WeightNormal = Shader.PropertyToID("_WeightNormal");
            public static readonly int WeightBold = Shader.PropertyToID("_WeightBold");

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

            public static readonly PaintShaderIds FacePaint = new PaintShaderIds(
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

            public static readonly PaintShaderIds StrokePaint = new PaintShaderIds(
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

            public static readonly PaintShaderIds ShadowPaint = new PaintShaderIds(
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
    }
}
