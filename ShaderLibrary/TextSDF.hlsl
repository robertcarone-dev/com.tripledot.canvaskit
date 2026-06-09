#ifndef CANVASKIT_TEXT_SDF_INCLUDED
#define CANVASKIT_TEXT_SDF_INCLUDED

// ----------------------------------------------------------------------------
// SDF Parameters
// ----------------------------------------------------------------------------

struct TextSDFParams
{
    float scale;
    float bias;
    float weight;
};

TextSDFParams GetTextSDFParams(float3 positionOS, float3 normalOS, float4 atlasAndWeight, float faceDilate,
    float scaleX, float scaleY, float gradientScale, float sharpness, float perspectiveFilter,
    float weightNormal, float weightBold, float scaleRatio)
{
    float4 positionCS = TransformObjectToHClip(positionOS);
    float2 pixelSize = positionCS.w;
    pixelSize /= float2(scaleX, scaleY) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

    float scale = rsqrt(max(dot(pixelSize, pixelSize), 0.000001));
    scale *= abs(atlasAndWeight.w) * gradientScale * max(sharpness + 1.0, 0.001);

    if (UNITY_MATRIX_P[3][3] == 0) {
        float3 normalWS = TransformObjectToWorldNormal(normalOS);
        float3 viewDirWS = GetWorldSpaceNormalizeViewDir(TransformObjectToWorld(positionOS));
        scale = lerp(abs(scale) * (1.0 - perspectiveFilter), scale, abs(dot(normalWS, viewDirWS)));
    }

    float bold = step(atlasAndWeight.w, 0.0);
    float weight = lerp(weightNormal, weightBold, bold) / 4.0;
    weight = (weight + faceDilate) * scaleRatio * 0.5;

    TextSDFParams sdfParams;
    sdfParams.scale = max(scale, 0.0001);
    sdfParams.bias = (0.5 - weight) + (0.5 / sdfParams.scale);
    sdfParams.weight = weight;
    return sdfParams;
}

// ----------------------------------------------------------------------------
// SDF Sampling
// ----------------------------------------------------------------------------

float GetTextSignedDistance(float sdf, TextSDFParams sdfParams)
{
    return (sdfParams.bias - sdf) * sdfParams.scale - 0.5;
}

half GetTextAtlasSafeMask(float2 atlasUV, float4 safeRect)
{
    half2 insideMin = half2(step(safeRect.xy, atlasUV));
    half2 insideMax = half2(step(atlasUV, safeRect.zw));
    return insideMin.x * insideMin.y * insideMax.x * insideMax.y;
}

float2 ClampTextAtlasUV(float2 atlasUV, float4 safeRect)
{
    return clamp(atlasUV, safeRect.xy, safeRect.zw);
}

half SampleTextAtlasSDF(TEXTURE2D_PARAM(atlasTexture, atlasSampler), float2 atlasUV, float4 safeRect)
{
    half mask = GetTextAtlasSafeMask(atlasUV, safeRect);
    half sdf = SAMPLE_TEXTURE2D(atlasTexture, atlasSampler, ClampTextAtlasUV(atlasUV, safeRect)).a;
    return lerp(0.5, sdf, mask);
}

half GetTextCoverageWithAtlasSafeMask(half coverage, float2 atlasUV, float4 safeRect)
{
    return coverage * GetTextAtlasSafeMask(atlasUV, safeRect);
}

half GetTextFaceCoverageFromDistance(float signedDistance)
{
    return saturate(0.5 - signedDistance);
}

float2 OffsetTextAtlasUV(float2 atlasUV, float2 localPosition, float2 localOffset)
{
    float2 uvDx = ddx(atlasUV);
    float2 uvDy = ddy(atlasUV);
    float2 localDx = ddx(localPosition);
    float2 localDy = ddy(localPosition);
    float determinant = localDx.x * localDy.y - localDy.x * localDx.y;
    if (abs(determinant) < 0.000001) {
        return atlasUV;
    }

    float invDeterminant = rcp(determinant);
    float screenOffsetX = (localOffset.x * localDy.y - localDy.x * localOffset.y) * invDeterminant;
    float screenOffsetY = (localDx.x * localOffset.y - localOffset.x * localDx.y) * invDeterminant;
    float2 uvDelta = uvDx * screenOffsetX + uvDy * screenOffsetY;
    return atlasUV - uvDelta;
}

// ----------------------------------------------------------------------------
// SDF Type Utilities
// ----------------------------------------------------------------------------

half GetTextFaceCoverage(float sdf, TextSDFParams sdfParams)
{
    return GetTextFaceCoverageFromDistance(GetTextSignedDistance(sdf, sdfParams));
}

float GetTextPixelsToScaledDistance(float pixels, TextSDFParams sdfParams, float gradientScale, float scaleRatio)
{
    return pixels * sdfParams.scale * scaleRatio / gradientScale;
}

void GetTextSignedDistanceBand(half position, half weight, out half inner, out half outer)
{
    if (position > 1.5) {
        inner = -weight;
        outer = 0.0;
        return;
    }

    if (position > 0.5) {
        inner = -weight * 0.5;
        outer = weight * 0.5;
        return;
    }

    inner = 0.0;
    outer = weight;
}

half GetTextSignedDistanceBandCoverage(float distanceValue, half inner, half outer, half feather)
{
    half innerAlpha = smoothstep(inner - feather, inner + feather, distanceValue);
    half outerAlpha = 1.0 - smoothstep(outer - feather, outer + feather, distanceValue);
    return saturate(innerAlpha * outerAlpha);
}

half GetTextStrokeCoverageFromDistance(float signedDistance, TextSDFParams sdfParams, float gradientScale,
    float scaleRatio, float weightPixels, float softnessPixels, int position)
{
    if (weightPixels <= 0.0) {
        return 0.0;
    }

    half weight = GetTextPixelsToScaledDistance(weightPixels, sdfParams, gradientScale, scaleRatio);
    half softness = max(GetTextPixelsToScaledDistance(softnessPixels, sdfParams, gradientScale, scaleRatio), 0.5);
    
    half inner;
    half outer;
    GetTextSignedDistanceBand(position, weight, inner, outer);
    
    return GetTextSignedDistanceBandCoverage(signedDistance, inner, outer, softness);
}

half GetTextStrokeCoverage(float sdf, TextSDFParams sdfParams, float gradientScale, float scaleRatio, float weightPixels, float softnessPixels, int position)
{
    return GetTextStrokeCoverageFromDistance(GetTextSignedDistance(sdf, sdfParams), sdfParams, gradientScale, scaleRatio, weightPixels, softnessPixels, position);
}

float GetTextEffectFeather(float weightPixels, TextSDFParams sdfParams, float gradientScale, float scaleRatio)
{
    return max(GetTextPixelsToScaledDistance(weightPixels, sdfParams, gradientScale, scaleRatio), 1.0);
}

half GetTextOutsideRamp(float signedDistance, float spreadPixels, float weightPixels, TextSDFParams sdfParams, float gradientScale, float scaleRatio)
{
    float spread = GetTextPixelsToScaledDistance(spreadPixels, sdfParams, gradientScale, scaleRatio);
    float feather = GetTextEffectFeather(weightPixels, sdfParams, gradientScale, scaleRatio);
    return 1.0 - smoothstep(spread, spread + feather, signedDistance);
}

#endif // CANVASKIT_TEXT_SDF_INCLUDED
