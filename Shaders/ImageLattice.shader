Shader "UI/Tripledot/Image Lattice"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _AlphaTex ("Alpha Texture", 2D) = "white" {}
        _TextureSampleAdd ("Texture Sample Add", Vector) = (0,0,0,0)
        _LatticeGrid ("Lattice Grid", Vector) = (3,3,0,0)

        _ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
        _UIMaskSoftnessX ("UI Mask Softness X", Float) = 1
        _UIMaskSoftnessY ("UI Mask Softness Y", Float) = 1
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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Image Lattice"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.tripledot.canvaskit/Shaders/ImageLatticePass.hlsl"
            ENDHLSL
        }
    }

    Fallback "UI/Default"
}
