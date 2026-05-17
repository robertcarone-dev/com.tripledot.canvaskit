Shader "TextMeshPro/Tripledot/Text Core"
{
    Properties
    {
        _MainTex ("Font Atlas", 2D) = "white" {}
        _FaceColor ("Fill Color", Color) = (1,1,1,1)
        _FaceColorB ("Fill Color B", Color) = (1,1,1,1)
        _FacePaintMode ("Fill Paint Mode", Integer) = 0
        _FaceTexture ("Fill Texture", 2D) = "white" {}
        _GradientAtlas ("Gradient Atlas", 2D) = "white" {}
        _FaceGradientAngle ("Fill Gradient Angle", Float) = 0
        _FaceGradientAtlasRect ("Fill Gradient Atlas Rect", Vector) = (0,0,0,0)
        _FaceTextureTransform ("Fill Texture Transform", Vector) = (0,0,1,1)
        _FacePaintTransform0 ("Fill Paint Transform 0", Vector) = (0.5,0.5,0,0)
        _FacePaintTransform1 ("Fill Paint Transform 1", Vector) = (1,1,0,0)
        _FaceEnabled ("Face Enabled", Integer) = 1
        _FaceDilate ("Face Dilate", Float) = 0
        _FaceLightingEnabled ("Fill Lighting Enabled", Integer) = 0
        _FaceBevelWidth ("Fill Bevel Width", Float) = 0.35
        _FaceBevelSoftness ("Fill Bevel Softness", Float) = 0.35
        _FaceLightDirection ("Fill Light Direction", Vector) = (-0.7071,0.7071,0,0)
        _FaceHighlightColor ("Fill Highlight Color", Color) = (1,1,1,0.65)
        _FaceShadowColor ("Fill Shadow Color", Color) = (0.45,0.24,0.05,0.35)
        _PaintBounds ("Paint Bounds", Vector) = (0,0,1,1)
        _LayerOpacity ("Layer Opacity", Range(0,1)) = 1
        [HideInInspector] _BlendMode ("Blend Mode", Integer) = 0
        [HideInInspector] _StyleBlendSrc ("Style Blend Source", Integer) = 1
        [HideInInspector] _StyleBlendDst ("Style Blend Destination", Integer) = 10
        [HideInInspector] _StyleBlendSrcAlpha ("Style Blend Source Alpha", Integer) = 1
        [HideInInspector] _StyleBlendDstAlpha ("Style Blend Destination Alpha", Integer) = 10
        [HideInInspector] _StyleBlendOp ("Style Blend Operation", Integer) = 0

        _StrokeEnabled ("Stroke Enabled", Integer) = 0
        _StrokeWeight ("Stroke Width", Float) = 0
        _StrokeSoftness ("Stroke Feather", Float) = 0
        _StrokePosition ("Stroke Position", Integer) = 0
        _StrokeOffset ("Stroke Offset", Vector) = (0,0,0,0)
        _StrokePaintMode ("Stroke Paint Mode", Integer) = 0
        _StrokeColor ("Stroke Color", Color) = (0,0,0,1)
        _StrokeColorB ("Stroke Color B", Color) = (0,0,0,1)
        _StrokeTexture ("Stroke Texture", 2D) = "white" {}
        _StrokeGradientAngle ("Stroke Gradient Angle", Float) = 0
        _StrokeGradientAtlasRect ("Stroke Gradient Atlas Rect", Vector) = (0,0,0,0)
        _StrokeTextureTransform ("Stroke Texture Transform", Vector) = (0,0,1,1)
        _StrokePaintTransform0 ("Stroke Paint Transform 0", Vector) = (0.5,0.5,0,0)
        _StrokePaintTransform1 ("Stroke Paint Transform 1", Vector) = (1,1,0,0)

        _ShadowEnabled ("Shadow Enabled", Integer) = 0
        _ShadowWeight ("Shadow Blur", Float) = 0
        _ShadowSpread ("Shadow Spread", Float) = 0
        _ShadowOffset ("Shadow Offset", Vector) = (0,0,0,0)
        _ShadowPaintMode ("Shadow Paint Mode", Integer) = 0
        _ShadowColor ("Shadow Color", Color) = (0,0,0,0.5)
        _ShadowColorB ("Shadow Color B", Color) = (0,0,0,0.5)
        _ShadowTexture ("Shadow Texture", 2D) = "white" {}
        _ShadowGradientAngle ("Shadow Gradient Angle", Float) = 0
        _ShadowGradientAtlasRect ("Shadow Gradient Atlas Rect", Vector) = (0,0,0,0)
        _ShadowTextureTransform ("Shadow Texture Transform", Vector) = (0,0,1,1)
        _ShadowPaintTransform0 ("Shadow Paint Transform 0", Vector) = (0.5,0.5,0,0)
        _ShadowPaintTransform1 ("Shadow Paint Transform 1", Vector) = (1,1,0,0)

        _ScaleRatioA ("Scale Ratio A", Float) = 1
        _GradientScale ("Gradient Scale", Float) = 5
        _Sharpness ("Sharpness", Range(-1,1)) = 0
        _ScaleX ("Scale X", Float) = 1
        _ScaleY ("Scale Y", Float) = 1
        _PerspectiveFilter ("Perspective Correction", Range(0,1)) = 0.875
        _WeightNormal ("Weight Normal", Float) = 0
        _WeightBold ("Weight Bold", Float) = 0.5

        _ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
        _MaskSoftnessX ("Mask Softness X", Float) = 0
        _MaskSoftnessY ("Mask Softness Y", Float) = 0
        _StencilComp ("Stencil Comparison", Integer) = 8
        _Stencil ("Stencil ID", Integer) = 0
        _StencilOp ("Stencil Operation", Integer) = 0
        _StencilWriteMask ("Stencil Write Mask", Integer) = 255
        _StencilReadMask ("Stencil Read Mask", Integer) = 255
        _CullMode ("Cull Mode", Integer) = 0
        _ColorMask ("Color Mask", Integer) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull [_CullMode]
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend [_StyleBlendSrc] [_StyleBlendDst], [_StyleBlendSrcAlpha] [_StyleBlendDstAlpha]
        BlendOp [_StyleBlendOp]
        ColorMask [_ColorMask]

        Pass
        {
            Name "Text"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma multi_compile_local_fragment _ FACE_TEXTURE_ON
            #pragma multi_compile_local_fragment _ STROKE_TEXTURE_ON
            #pragma multi_compile_local_fragment _ SHADOW_TEXTURE_ON
            #pragma multi_compile_local_fragment _ GRADIENT_ATLAS_ON
            #pragma multi_compile_local_fragment _ FACE_LIGHTING_ON
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.tripledot.canvaskit/Shaders/TextMeshPro/TextLayerCoreInput.hlsl"
            #include "Packages/com.tripledot.canvaskit/Shaders/TextMeshPro/TextLayerCorePass.hlsl"
            ENDHLSL
        }
    }

    Fallback "TextMeshPro/Distance Field"
}
