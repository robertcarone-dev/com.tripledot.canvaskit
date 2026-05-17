#ifndef TRIPLEDOT_CANVASKIT_TEXT_LAYER_CORE_PASS_INCLUDED
#define TRIPLEDOT_CANVASKIT_TEXT_LAYER_CORE_PASS_INCLUDED

#include "Packages/com.tripledot.canvaskit/ShaderLibrary/Canvas.hlsl"
#include "Packages/com.tripledot.canvaskit/ShaderLibrary/TextSDF.hlsl"

float _UIMaskSoftnessX;
float _UIMaskSoftnessY;

struct Attributes
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    half4 color : COLOR;
    float4 texCoord0 : TEXCOORD0;
    float4 texCoord1 : TEXCOORD1;
    float4 texCoord2 : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    half4 color : COLOR;
    float2 atlasUV : TEXCOORD0;
    float2 localPosition : TEXCOORD1;
    float4 mask : TEXCOORD2;
    half3 sdfParams : TEXCOORD3;
    float4 atlasSafeRect : TEXCOORD4;
    float2 paintUV : TEXCOORD5;
    float2 paintBoundsSize : TEXCOORD6;
    UNITY_VERTEX_OUTPUT_STEREO
};

// ----------------------------------------------------------------------------
// Vertex
// ----------------------------------------------------------------------------

Varyings BuildTextLayerVaryings(Attributes input, float faceDilate)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float4 positionCS = TransformObjectToHClip(input.positionOS);
    output.positionCS = positionCS;
    output.color = input.color;
    output.atlasUV = input.texCoord0.xy;
    output.atlasSafeRect = input.texCoord1;
    output.localPosition = input.positionOS.xy;
    output.paintUV = input.texCoord2.xy;
    output.paintBoundsSize = float2(input.texCoord0.z, input.texCoord2.z);
    output.mask = GetCanvasMask(input.positionOS.xy, positionCS, _ClipRect,
        _MaskSoftnessX, _MaskSoftnessY, _UIMaskSoftnessX, _UIMaskSoftnessY, _ScreenParams, UNITY_MATRIX_P);

    TextSDFParams sdfParams = GetTextSDFParams(input.positionOS, input.normalOS, input.texCoord0, faceDilate,
        _ScaleX, _ScaleY, _GradientScale, _Sharpness, _PerspectiveFilter, _WeightNormal, _WeightBold, _ScaleRatioA);
    output.sdfParams = half3(sdfParams.scale, sdfParams.bias, sdfParams.weight);
    return output;
}

Varyings Vertex(Attributes input)
{
    return BuildTextLayerVaryings(input, _FaceDilate);
}

TextSDFParams UnpackTextSDFParams(half3 packedParams)
{
    TextSDFParams sdfParams;
    sdfParams.scale = packedParams.x;
    sdfParams.bias = packedParams.y;
    sdfParams.weight = packedParams.z;
    return sdfParams;
}

// ----------------------------------------------------------------------------
// Canvas Paint
// ----------------------------------------------------------------------------

half4 SampleFaceCanvasPaint(float2 paintUV, float2 paintBoundsSize)
{
    if (_FacePaintMode == 1) {
        float t = SampleCanvasLinearGradientT(paintUV, _FacePaintTransform0, _FacePaintTransform1, _FaceGradientAngle, paintBoundsSize);
#if defined(GRADIENT_ATLAS_ON)
        if (_FaceGradientAtlasRect.w > 0.5) {
            return ApplyCanvasGradientAtlasOpacity(SampleCanvasGradientAtlas(TEXTURE2D_ARGS(_GradientAtlas, sampler_GradientAtlas), _FaceGradientAtlasRect, t, _GradientAtlas_TexelSize), half4(_FaceColor).a);
        }
#endif
        return lerp(half4(_FaceColor), half4(_FaceColorB), saturate(t));
    }

    if (_FacePaintMode == 2) {
        float t = SampleCanvasRadialGradientT(paintUV, _FacePaintTransform0, _FacePaintTransform1);
#if defined(GRADIENT_ATLAS_ON)
        if (_FaceGradientAtlasRect.w > 0.5) {
            return ApplyCanvasGradientAtlasOpacity(SampleCanvasGradientAtlas(TEXTURE2D_ARGS(_GradientAtlas, sampler_GradientAtlas), _FaceGradientAtlasRect, t, _GradientAtlas_TexelSize), half4(_FaceColor).a);
        }
#endif
        return lerp(half4(_FaceColor), half4(_FaceColorB), saturate(t));
    }

#if defined(FACE_TEXTURE_ON)
    if (_FacePaintMode == 3) {
        float2 transformedUV = TransformCanvasPaintTextureUV(paintUV, _FacePaintTransform0, _FacePaintTransform1);
        return half4(SAMPLE_TEXTURE2D(_FaceTexture, sampler_FaceTexture, transformedUV * _FaceTextureTransform.zw + _FaceTextureTransform.xy)) * half4(_FaceColor);
    }
#endif

    return half4(_FaceColor);
}

half4 SampleStrokeCanvasPaint(float2 paintUV, float2 paintBoundsSize)
{
    if (_StrokePaintMode == 1) {
        float t = SampleCanvasLinearGradientT(paintUV, _StrokePaintTransform0, _StrokePaintTransform1, _StrokeGradientAngle, paintBoundsSize);
#if defined(GRADIENT_ATLAS_ON)
        if (_StrokeGradientAtlasRect.w > 0.5) {
            return ApplyCanvasGradientAtlasOpacity(SampleCanvasGradientAtlas(TEXTURE2D_ARGS(_GradientAtlas, sampler_GradientAtlas), _StrokeGradientAtlasRect, t, _GradientAtlas_TexelSize), half4(_StrokeColor).a);
        }
#endif
        return lerp(half4(_StrokeColor), half4(_StrokeColorB), saturate(t));
    }

    if (_StrokePaintMode == 2) {
        float t = SampleCanvasRadialGradientT(paintUV, _StrokePaintTransform0, _StrokePaintTransform1);
#if defined(GRADIENT_ATLAS_ON)
        if (_StrokeGradientAtlasRect.w > 0.5) {
            return ApplyCanvasGradientAtlasOpacity(SampleCanvasGradientAtlas(TEXTURE2D_ARGS(_GradientAtlas, sampler_GradientAtlas), _StrokeGradientAtlasRect, t, _GradientAtlas_TexelSize), half4(_StrokeColor).a);
        }
#endif
        return lerp(half4(_StrokeColor), half4(_StrokeColorB), saturate(t));
    }

#if defined(STROKE_TEXTURE_ON)
    if (_StrokePaintMode == 3) {
        float2 transformedUV = TransformCanvasPaintTextureUV(paintUV, _StrokePaintTransform0, _StrokePaintTransform1);
        return half4(SAMPLE_TEXTURE2D(_StrokeTexture, sampler_StrokeTexture, transformedUV * _StrokeTextureTransform.zw + _StrokeTextureTransform.xy)) * half4(_StrokeColor);
    }
#endif

    return half4(_StrokeColor);
}

half4 SampleShadowCanvasPaint(float2 paintUV, float2 paintBoundsSize)
{
    if (_ShadowPaintMode == 1) {
        float t = SampleCanvasLinearGradientT(paintUV, _ShadowPaintTransform0, _ShadowPaintTransform1, _ShadowGradientAngle, paintBoundsSize);
#if defined(GRADIENT_ATLAS_ON)
        if (_ShadowGradientAtlasRect.w > 0.5) {
            return ApplyCanvasGradientAtlasOpacity(SampleCanvasGradientAtlas(TEXTURE2D_ARGS(_GradientAtlas, sampler_GradientAtlas), _ShadowGradientAtlasRect, t, _GradientAtlas_TexelSize), half4(_ShadowColor).a);
        }
#endif
        return lerp(half4(_ShadowColor), half4(_ShadowColorB), saturate(t));
    }

    if (_ShadowPaintMode == 2) {
        float t = SampleCanvasRadialGradientT(paintUV, _ShadowPaintTransform0, _ShadowPaintTransform1);
#if defined(GRADIENT_ATLAS_ON)
        if (_ShadowGradientAtlasRect.w > 0.5) {
            return ApplyCanvasGradientAtlasOpacity(SampleCanvasGradientAtlas(TEXTURE2D_ARGS(_GradientAtlas, sampler_GradientAtlas), _ShadowGradientAtlasRect, t, _GradientAtlas_TexelSize), half4(_ShadowColor).a);
        }
#endif
        return lerp(half4(_ShadowColor), half4(_ShadowColorB), saturate(t));
    }

#if defined(SHADOW_TEXTURE_ON)
    if (_ShadowPaintMode == 3) {
        float2 transformedUV = TransformCanvasPaintTextureUV(paintUV, _ShadowPaintTransform0, _ShadowPaintTransform1);
        return half4(SAMPLE_TEXTURE2D(_ShadowTexture, sampler_ShadowTexture, transformedUV * _ShadowTextureTransform.zw + _ShadowTextureTransform.xy)) * half4(_ShadowColor);
    }
#endif

    return half4(_ShadowColor);
}

// ----------------------------------------------------------------------------
// Fragment
// ----------------------------------------------------------------------------

half4 ApplyTextLayerOpacity(half4 premultiplied)
{
    return premultiplied * saturate(_LayerOpacity);
}

half3 UnpremultiplyTextLayerRgb(half4 premultiplied)
{
    half alpha = saturate(premultiplied.a);
    return alpha > 0.0001 ? premultiplied.rgb / alpha : half3(0.0, 0.0, 0.0);
}

half4 GetTextLayerBlendOutput(half4 premultiplied)
{
    const int BlendModePremultipliedAlpha = 0;
    const int BlendModeStraightAlpha = 1;
    const int BlendModeAdditive = 2;
    const int BlendModeMultiply = 3;
    const int BlendModeScreen = 4;
    half alpha = saturate(premultiplied.a);

    if (_BlendMode == BlendModeStraightAlpha || _BlendMode == BlendModeAdditive) {
        return half4(UnpremultiplyTextLayerRgb(premultiplied), alpha);
    }

    if (_BlendMode == BlendModeMultiply) {
        return half4(lerp(half3(1.0, 1.0, 1.0), UnpremultiplyTextLayerRgb(premultiplied), alpha), alpha);
    }

    if (_BlendMode == BlendModePremultipliedAlpha || _BlendMode == BlendModeScreen) {
        return half4(premultiplied.rgb, alpha);
    }

    return half4(premultiplied.rgb, alpha);
}

#if defined(FACE_LIGHTING_ON)
float SampleTextLightingSignedDistance(float2 atlasUV, float4 atlasSafeRect, TextSDFParams sdfParams)
{
    half sdf = SampleTextAtlasSDF(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), atlasUV, atlasSafeRect);
    return GetTextSignedDistance(sdf, sdfParams);
}

float2 GetTextFaceLightingAtlasNormal(float2 atlasUV, float4 atlasSafeRect, TextSDFParams sdfParams, out float confidence)
{
    float2 halfTexel = _MainTex_TexelSize.xy * 0.5;
    float left = SampleTextLightingSignedDistance(atlasUV - float2(halfTexel.x, 0.0), atlasSafeRect, sdfParams);
    float right = SampleTextLightingSignedDistance(atlasUV + float2(halfTexel.x, 0.0), atlasSafeRect, sdfParams);
    float down = SampleTextLightingSignedDistance(atlasUV - float2(0.0, halfTexel.y), atlasSafeRect, sdfParams);
    float up = SampleTextLightingSignedDistance(atlasUV + float2(0.0, halfTexel.y), atlasSafeRect, sdfParams);

    float2 atlasGradient = float2(right - left, up - down);
    float gradientLengthSq = dot(atlasGradient, atlasGradient);
    float gradientLength = sqrt(gradientLengthSq);

    confidence = smoothstep(0.0001, 0.01, gradientLength);

    return atlasGradient * rsqrt(max(gradientLengthSq, 0.0000000001));
}

half4 ApplyTextFaceLighting(half4 facePaint, float signedDistance, float2 atlasUV, float4 atlasSafeRect, TextSDFParams sdfParams)
{
    if (_FaceBevelWidth <= 0.0) {
        return facePaint;
    }

    float usableInteriorDistance = max((0.5 + sdfParams.weight) * sdfParams.scale, 0.0001);
    float rawInsideDistance = max(-signedDistance, 0.0) / usableInteriorDistance;
    float insideDistance = saturate(rawInsideDistance);

    float bevelFeather = max(max(_FaceBevelWidth * _FaceBevelSoftness, fwidth(rawInsideDistance)), 0.0001);
    float bevelStart = _FaceBevelWidth - bevelFeather;
    float bevelMask = 1.0 - smoothstep(bevelStart, _FaceBevelWidth, insideDistance);

    float normalConfidence;
    float2 edgeNormal = GetTextFaceLightingAtlasNormal(atlasUV, atlasSafeRect, sdfParams, normalConfidence);

    float lightLengthSq = dot(_FaceLightDirection.xy, _FaceLightDirection.xy);
    float2 lightDirection = lightLengthSq > 0.0 ? _FaceLightDirection.xy * rsqrt(lightLengthSq) : float2(-0.7071068, 0.7071068);

    float facing = dot(edgeNormal, lightDirection);
    half lightingMask = bevelMask * normalConfidence;

    half highlight = saturate(facing) * lightingMask * half(_FaceHighlightColor.a);
    half shadow = saturate(-facing) * lightingMask * half(_FaceShadowColor.a);

    facePaint.rgb = lerp(facePaint.rgb, half3(_FaceHighlightColor.rgb), highlight);
    facePaint.rgb = lerp(facePaint.rgb, half3(_FaceShadowColor.rgb), shadow);

    return facePaint;
}
#endif

half4 Fragment(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half sdf = SampleTextAtlasSDF(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), input.atlasUV, input.atlasSafeRect);
    TextSDFParams sdfParams = UnpackTextSDFParams(input.sdfParams);
    float signedDistance = GetTextSignedDistance(sdf, sdfParams);
    float2 paintUV = input.paintUV;
    half4 result = half4(0.0, 0.0, 0.0, 0.0);

    if (_ShadowEnabled != 0) {
        float2 shadowUV = OffsetTextAtlasUV(input.atlasUV, input.localPosition, _ShadowOffset.xy);
        half shadowSDF = SampleTextAtlasSDF(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), shadowUV, input.atlasSafeRect);
        float shadowSignedDistance = GetTextSignedDistance(shadowSDF, sdfParams);
        half shadowCoverage = GetTextOutsideRamp(shadowSignedDistance, _ShadowSpread, _ShadowWeight, sdfParams, _GradientScale, _ScaleRatioA);
        shadowCoverage = GetTextCoverageWithAtlasSafeMask(shadowCoverage, shadowUV, input.atlasSafeRect);
        if (shadowCoverage > 0.0) {
            half4 shadowPaint = SampleShadowCanvasPaint(paintUV, input.paintBoundsSize);
            shadowPaint *= input.color;
            result = CompositePremultiplied(result, shadowPaint, shadowCoverage);
        }
    }

    if (_FaceEnabled != 0) {
        half faceCoverage = GetTextFaceCoverageFromDistance(signedDistance);
        faceCoverage = GetTextCoverageWithAtlasSafeMask(faceCoverage, input.atlasUV, input.atlasSafeRect);
        if (faceCoverage > 0.0) {
            half4 facePaint = SampleFaceCanvasPaint(paintUV, input.paintBoundsSize);
            facePaint *= input.color;
#if defined(FACE_LIGHTING_ON)
            facePaint = ApplyTextFaceLighting(facePaint, signedDistance, input.atlasUV, input.atlasSafeRect, sdfParams);
#endif
            result = CompositePremultiplied(result, facePaint, faceCoverage);
        }
    }

    if (_StrokeEnabled != 0) {
        float2 strokeUV = OffsetTextAtlasUV(input.atlasUV, input.localPosition, _StrokeOffset.xy);
        half strokeSDF = SampleTextAtlasSDF(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), strokeUV, input.atlasSafeRect);
        half strokeCoverage = GetTextStrokeCoverage(strokeSDF, sdfParams, _GradientScale, _ScaleRatioA, _StrokeWeight, _StrokeSoftness, _StrokePosition);
        strokeCoverage = GetTextCoverageWithAtlasSafeMask(strokeCoverage, strokeUV, input.atlasSafeRect);
        if (strokeCoverage > 0.0) {
            half4 strokePaint = SampleStrokeCanvasPaint(paintUV, input.paintBoundsSize);
            strokePaint *= input.color;
            result = CompositePremultiplied(result, strokePaint, strokeCoverage);
        }
    }

#if UNITY_UI_CLIP_RECT
    result *= GetCanvasMaskFactor(input.mask, _ClipRect);
#endif

    result = ApplyTextLayerOpacity(result);

#if UNITY_UI_ALPHACLIP
    clip(result.a - 0.001);
#endif

    return GetTextLayerBlendOutput(result);
}

#endif // TRIPLEDOT_CANVASKIT_TEXT_LAYER_CORE_PASS_INCLUDED
