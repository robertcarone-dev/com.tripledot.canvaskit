using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tripledot.CanvasKit
{
    internal static class TextMeshProLayerMaterial
    {
        public static Material CreateMaterial(Shader shader)
        {
            return new Material(shader) {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        public static void ApplySharedTextProperties(Material material, TextMeshProLayerMaterialContext context, CanvasBlendMode blendPreset, float layerOpacity)
        {
            material.SetTexture(ShaderIds.MainTex, context.FontAtlas);
            material.SetVector(ShaderIds.PaintBounds, context.PaintBounds);
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
            material.SetInteger(CanvasShaderIds.StencilComp, context.StencilComp);
            material.SetInteger(CanvasShaderIds.Stencil, context.Stencil);
            material.SetInteger(CanvasShaderIds.StencilOp, context.StencilOp);
            material.SetInteger(CanvasShaderIds.StencilWriteMask, context.StencilWriteMask);
            material.SetInteger(CanvasShaderIds.StencilReadMask, context.StencilReadMask);
            material.SetInteger(CanvasShaderIds.CullMode, context.CullMode);
            material.SetInteger(CanvasShaderIds.ColorMask, context.ColorMask);
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
                CanvasUtility.SetKeyword(material, textureKeyword, textureEnabled);
            }

            return gradientAtlasEnabled;
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

            public PaintShaderIds(int paintMode, int color, int colorB, int texture, int gradientAngle, int gradientAtlasRect,
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

        private static class ShaderIds
        {
            public static readonly int MainTex = Shader.PropertyToID("_MainTex");
            public static readonly int GradientAtlas = Shader.PropertyToID("_GradientAtlas");
            public static readonly int PaintBounds = Shader.PropertyToID("_PaintBounds");
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
        }
    }

    internal sealed class TextMeshProLayerMaterialGradientState
    {
        public readonly CanvasGradientAtlas.Lease Face = new CanvasGradientAtlas.Lease();
        public readonly CanvasGradientAtlas.Lease Stroke = new CanvasGradientAtlas.Lease();
        public readonly CanvasGradientAtlas.Lease Shadow = new CanvasGradientAtlas.Lease();

        public void Release()
        {
            CanvasGradientAtlas.Release(Face);
            CanvasGradientAtlas.Release(Stroke);
            CanvasGradientAtlas.Release(Shadow);
        }
    }

    internal readonly struct TextMeshProLayerMaterialContext : IEquatable<TextMeshProLayerMaterialContext>
    {
        public readonly Texture FontAtlas;
        public readonly Vector4 PaintBounds;
        public readonly float ScaleRatioA;
        public readonly float GradientScale;
        public readonly float Sharpness;
        public readonly float ScaleX;
        public readonly float ScaleY;
        public readonly float PerspectiveFilter;
        public readonly float WeightNormal;
        public readonly float WeightBold;
        public readonly float AppliedSdfPadding;
        public readonly Vector4 ClipRect;
        public readonly float MaskSoftnessX;
        public readonly float MaskSoftnessY;
        public readonly int StencilComp;
        public readonly int Stencil;
        public readonly int StencilOp;
        public readonly int StencilWriteMask;
        public readonly int StencilReadMask;
        public readonly int CullMode;
        public readonly int ColorMask;

        private TextMeshProLayerMaterialContext(Texture fontAtlas, Vector4 paintBounds, Material sourceMaterial, Material renderMaterial, float appliedSdfPadding)
        {
            FontAtlas = fontAtlas;
            PaintBounds = paintBounds;
            ScaleRatioA = GetFloat(sourceMaterial, ShaderUtilities.ID_ScaleRatio_A, 1f);
            GradientScale = TextMeshProUtility.GetEffectiveGradientScale(sourceMaterial);
            Sharpness = GetFloat(sourceMaterial, ShaderUtilities.ID_Sharpness, 0f);
            ScaleX = GetFloat(sourceMaterial, ShaderUtilities.ID_ScaleX, 1f);
            ScaleY = GetFloat(sourceMaterial, ShaderUtilities.ID_ScaleY, 1f);
            PerspectiveFilter = GetFloat(sourceMaterial, ShaderUtilities.ID_PerspectiveFilter, 0.875f);
            WeightNormal = GetFloat(sourceMaterial, ShaderUtilities.ID_WeightNormal, 0f);
            WeightBold = GetFloat(sourceMaterial, ShaderUtilities.ID_WeightBold, 0.5f);
            AppliedSdfPadding = Mathf.Max(0f, appliedSdfPadding);
            ClipRect = GetVector(renderMaterial, CanvasShaderIds.ClipRect, new Vector4(-32767f, -32767f, 32767f, 32767f));
            MaskSoftnessX = GetFloat(renderMaterial, CanvasShaderIds.UIMaskSoftnessX, GetFloat(renderMaterial, CanvasShaderIds.LegacyMaskSoftnessX, 0f));
            MaskSoftnessY = GetFloat(renderMaterial, CanvasShaderIds.UIMaskSoftnessY, GetFloat(renderMaterial, CanvasShaderIds.LegacyMaskSoftnessY, 0f));
            StencilComp = GetInteger(renderMaterial, CanvasShaderIds.StencilComp, 8);
            Stencil = GetInteger(renderMaterial, CanvasShaderIds.Stencil, 0);
            StencilOp = GetInteger(renderMaterial, CanvasShaderIds.StencilOp, 0);
            StencilWriteMask = GetInteger(renderMaterial, CanvasShaderIds.StencilWriteMask, 255);
            StencilReadMask = GetInteger(renderMaterial, CanvasShaderIds.StencilReadMask, 255);
            CullMode = GetInteger(renderMaterial, CanvasShaderIds.CullMode, 0);
            ColorMask = GetInteger(renderMaterial, CanvasShaderIds.ColorMask, 15);
        }

        public static TextMeshProLayerMaterialContext Capture(TextMeshProUGUI text, Material sourceMaterial, Material renderMaterial, Vector4 paintBounds, float appliedSdfPadding)
        {
            var fontAtlas = text?.font?.atlasTexture
                            ?? GetTexture(sourceMaterial, ShaderUtilities.ID_MainTex)
                            ?? GetTexture(renderMaterial, ShaderUtilities.ID_MainTex);
            return new TextMeshProLayerMaterialContext(fontAtlas, paintBounds, sourceMaterial, renderMaterial, appliedSdfPadding);
        }

        public bool Equals(TextMeshProLayerMaterialContext other)
        {
            return FontAtlas == other.FontAtlas
                   && PaintBounds == other.PaintBounds
                   && ScaleRatioA == other.ScaleRatioA
                   && GradientScale == other.GradientScale
                   && Sharpness == other.Sharpness
                   && ScaleX == other.ScaleX
                   && ScaleY == other.ScaleY
                   && PerspectiveFilter == other.PerspectiveFilter
                   && WeightNormal == other.WeightNormal
                   && WeightBold == other.WeightBold
                   && AppliedSdfPadding == other.AppliedSdfPadding
                   && ClipRect == other.ClipRect
                   && MaskSoftnessX == other.MaskSoftnessX
                   && MaskSoftnessY == other.MaskSoftnessY
                   && StencilComp == other.StencilComp
                   && Stencil == other.Stencil
                   && StencilOp == other.StencilOp
                   && StencilWriteMask == other.StencilWriteMask
                   && StencilReadMask == other.StencilReadMask
                   && CullMode == other.CullMode
                   && ColorMask == other.ColorMask;
        }

        public override bool Equals(object obj)
        {
            return obj is TextMeshProLayerMaterialContext other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked {
                var hashCode = FontAtlas != null ? FontAtlas.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ PaintBounds.GetHashCode();
                hashCode = (hashCode * 397) ^ ScaleRatioA.GetHashCode();
                hashCode = (hashCode * 397) ^ GradientScale.GetHashCode();
                hashCode = (hashCode * 397) ^ Sharpness.GetHashCode();
                hashCode = (hashCode * 397) ^ ScaleX.GetHashCode();
                hashCode = (hashCode * 397) ^ ScaleY.GetHashCode();
                hashCode = (hashCode * 397) ^ PerspectiveFilter.GetHashCode();
                hashCode = (hashCode * 397) ^ WeightNormal.GetHashCode();
                hashCode = (hashCode * 397) ^ WeightBold.GetHashCode();
                hashCode = (hashCode * 397) ^ AppliedSdfPadding.GetHashCode();
                hashCode = (hashCode * 397) ^ ClipRect.GetHashCode();
                hashCode = (hashCode * 397) ^ MaskSoftnessX.GetHashCode();
                hashCode = (hashCode * 397) ^ MaskSoftnessY.GetHashCode();
                hashCode = (hashCode * 397) ^ StencilComp;
                hashCode = (hashCode * 397) ^ Stencil;
                hashCode = (hashCode * 397) ^ StencilOp;
                hashCode = (hashCode * 397) ^ StencilWriteMask;
                hashCode = (hashCode * 397) ^ StencilReadMask;
                hashCode = (hashCode * 397) ^ CullMode;
                hashCode = (hashCode * 397) ^ ColorMask;
                return hashCode;
            }
        }

        private static float GetFloat(Material material, int id, float fallback)
        {
            return material != null && material.HasProperty(id) ? material.GetFloat(id) : fallback;
        }

        private static int GetInteger(Material material, int id, int fallback)
        {
            if (material == null || !material.HasProperty(id)) {
                return fallback;
            }

            return material.HasInteger(id) ? material.GetInteger(id) : Mathf.RoundToInt(material.GetFloat(id));
        }

        private static Vector4 GetVector(Material material, int id, Vector4 fallback)
        {
            return material != null && material.HasProperty(id) ? material.GetVector(id) : fallback;
        }

        private static Texture GetTexture(Material material, int id)
        {
            return material != null && material.HasProperty(id) ? material.GetTexture(id) : null;
        }
    }
}