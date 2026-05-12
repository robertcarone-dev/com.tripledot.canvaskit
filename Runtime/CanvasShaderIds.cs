using UnityEngine;

namespace Tripledot.CanvasKit
{
    internal static class CanvasShaderIds
    {
        public static readonly int ClipRect = Shader.PropertyToID("_ClipRect");
        public static readonly int MaskSoftnessX = Shader.PropertyToID("_MaskSoftnessX");
        public static readonly int MaskSoftnessY = Shader.PropertyToID("_MaskSoftnessY");
        public static readonly int StencilComp = Shader.PropertyToID("_StencilComp");
        public static readonly int Stencil = Shader.PropertyToID("_Stencil");
        public static readonly int StencilOp = Shader.PropertyToID("_StencilOp");
        public static readonly int StencilWriteMask = Shader.PropertyToID("_StencilWriteMask");
        public static readonly int StencilReadMask = Shader.PropertyToID("_StencilReadMask");
        public static readonly int CullMode = Shader.PropertyToID("_CullMode");
        public static readonly int ColorMask = Shader.PropertyToID("_ColorMask");
    }
}
