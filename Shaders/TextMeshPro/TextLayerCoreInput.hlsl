#ifndef TRIPLEDOT_CANVASKIT_TEXT_LAYER_CORE_INPUT_INCLUDED
#define TRIPLEDOT_CANVASKIT_TEXT_LAYER_CORE_INPUT_INCLUDED

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

#if defined(FACE_TEXTURE_ON)
TEXTURE2D(_FaceTexture);
SAMPLER(sampler_FaceTexture);
#endif

#if defined(GRADIENT_ATLAS_ON)
TEXTURE2D(_GradientAtlas);
SAMPLER(sampler_GradientAtlas);
float4 _GradientAtlas_TexelSize;
#endif

#if defined(STROKE_TEXTURE_ON)
TEXTURE2D(_StrokeTexture);
SAMPLER(sampler_StrokeTexture);
#endif

#if defined(SHADOW_TEXTURE_ON)
TEXTURE2D(_ShadowTexture);
SAMPLER(sampler_ShadowTexture);
#endif

CBUFFER_START(UnityPerMaterial)
    float4 _FaceColor;
    float4 _FaceColorB;
    int _FacePaintMode;
    float _FaceGradientAngle;
    float4 _FaceGradientAtlasRect;
    float4 _FaceTextureTransform;
    float4 _FacePaintTransform0;
    float4 _FacePaintTransform1;
    float _FaceDilate;
    float4 _PaintBounds;
    float _LayerOpacity;
    int _BlendMode;
    float _StrokeWeight;
    float _StrokeSoftness;
    int _StrokePosition;
    float4 _StrokeOffset;
    int _StrokePaintMode;
    float4 _StrokeColor;
    float4 _StrokeColorB;
    float _StrokeGradientAngle;
    float4 _StrokeGradientAtlasRect;
    float4 _StrokeTextureTransform;
    float4 _StrokePaintTransform0;
    float4 _StrokePaintTransform1;
    float _ShadowWeight;
    float _ShadowSpread;
    float4 _ShadowOffset;
    int _ShadowPaintMode;
    float4 _ShadowColor;
    float4 _ShadowColorB;
    float _ShadowGradientAngle;
    float4 _ShadowGradientAtlasRect;
    float4 _ShadowTextureTransform;
    float4 _ShadowPaintTransform0;
    float4 _ShadowPaintTransform1;
    float _ScaleRatioA;
    float _GradientScale;
    float _Sharpness;
    float _ScaleX;
    float _ScaleY;
    float _PerspectiveFilter;
    float _WeightNormal;
    float _WeightBold;
    float4 _ClipRect;
    float _MaskSoftnessX;
    float _MaskSoftnessY;
CBUFFER_END

#endif // TRIPLEDOT_CANVASKIT_TEXT_LAYER_CORE_INPUT_INCLUDED
