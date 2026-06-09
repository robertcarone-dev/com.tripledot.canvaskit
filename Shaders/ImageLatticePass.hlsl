#ifndef CANVASKIT_IMAGE_LATTICE_PASS_INCLUDED
#define CANVASKIT_IMAGE_LATTICE_PASS_INCLUDED

#include "Packages/com.tripledot.canvaskit/ShaderLibrary/Canvas.hlsl"

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_AlphaTex);
SAMPLER(sampler_AlphaTex);

CBUFFER_START(UnityPerMaterial)
    float4 _TextureSampleAdd;
    float4 _LatticeGrid;
    float4 _ClipRect;
    float _UIMaskSoftnessX;
    float _UIMaskSoftnessY;
CBUFFER_END

#include "Packages/com.tripledot.canvaskit/Shaders/ImageLatticeDeform.hlsl"

struct Attributes
{
    float3 positionOS : POSITION;
    half4 color : COLOR;
    float4 texCoord0 : TEXCOORD0;
    float4 texCoord1 : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    half4 color : COLOR;
    float2 uv : TEXCOORD0;
    float4 mask : TEXCOORD1;
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings Vertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionOS = input.positionOS;
    positionOS.xy = ApplyImageLatticeDeformation(positionOS.xy, input.texCoord1);

    float4 positionCS = TransformObjectToHClip(positionOS);
    output.positionCS = positionCS;
    output.color = input.color;
    output.uv = input.texCoord0.xy;
    output.mask = GetCanvasMask(positionOS.xy, positionCS, _ClipRect, _UIMaskSoftnessX, _UIMaskSoftnessY, _ScreenParams, UNITY_MATRIX_P);
    return output;
}

half4 Fragment(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 color = half4(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv)) + half4(_TextureSampleAdd);
    color.a *= half(SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, input.uv).r);
    color *= input.color;

#if UNITY_UI_CLIP_RECT
    color.a *= GetCanvasMaskFactor(input.mask, _ClipRect);
#endif

#if UNITY_UI_ALPHACLIP
    clip(color.a - 0.001);
#endif

    return color;
}

#endif // CANVASKIT_IMAGE_LATTICE_PASS_INCLUDED
