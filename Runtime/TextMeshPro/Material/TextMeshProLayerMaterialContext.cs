using System;
using TMPro;
using UnityEngine;

namespace Tripledot.CanvasKit.TextMeshPro
{
    internal readonly struct TextMeshProLayerMaterialContext : IEquatable<TextMeshProLayerMaterialContext>
    {
        public readonly Texture FontAtlas;
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

        private TextMeshProLayerMaterialContext(Texture fontAtlas, Material sourceMaterial, Material renderMaterial, Vector2 maskSoftness, float appliedSdfPadding)
        {
            FontAtlas = fontAtlas;
            ScaleRatioA = sourceMaterial.GetFloat(ShaderUtilities.ID_ScaleRatio_A);
            GradientScale = TextMeshProUtility.GetEffectiveGradientScale(sourceMaterial);
            Sharpness = sourceMaterial.GetFloat(ShaderUtilities.ID_Sharpness);
            ScaleX = sourceMaterial.GetFloat(ShaderUtilities.ID_ScaleX);
            ScaleY = sourceMaterial.GetFloat(ShaderUtilities.ID_ScaleY);
            PerspectiveFilter = sourceMaterial.GetFloat(ShaderUtilities.ID_PerspectiveFilter);
            WeightNormal = sourceMaterial.GetFloat(ShaderUtilities.ID_WeightNormal);
            WeightBold = sourceMaterial.GetFloat(ShaderUtilities.ID_WeightBold);
            AppliedSdfPadding = Mathf.Max(0f, appliedSdfPadding);
            ClipRect = renderMaterial.GetVector(CanvasShaderIds.ClipRect);
            MaskSoftnessX = maskSoftness.x;
            MaskSoftnessY = maskSoftness.y;
            StencilComp = Mathf.RoundToInt(renderMaterial.GetFloat(CanvasShaderIds.StencilComp));
            Stencil = Mathf.RoundToInt(renderMaterial.GetFloat(CanvasShaderIds.Stencil));
            StencilOp = Mathf.RoundToInt(renderMaterial.GetFloat(CanvasShaderIds.StencilOp));
            StencilWriteMask = Mathf.RoundToInt(renderMaterial.GetFloat(CanvasShaderIds.StencilWriteMask));
            StencilReadMask = Mathf.RoundToInt(renderMaterial.GetFloat(CanvasShaderIds.StencilReadMask));
            CullMode = Mathf.RoundToInt(renderMaterial.GetFloat(CanvasShaderIds.CullMode));
            ColorMask = Mathf.RoundToInt(renderMaterial.GetFloat(CanvasShaderIds.ColorMask));
        }

        public static TextMeshProLayerMaterialContext Capture(TextMeshProUGUI text, Material sourceMaterial, Material renderMaterial, float appliedSdfPadding)
        {
            return new TextMeshProLayerMaterialContext(text.font.atlasTexture, sourceMaterial, renderMaterial, text.canvasRenderer.clippingSoftness, appliedSdfPadding);
        }

        public bool Equals(TextMeshProLayerMaterialContext other)
        {
            return FontAtlas == other.FontAtlas
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
    }
}
