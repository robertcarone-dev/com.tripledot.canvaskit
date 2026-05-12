#ifndef TRIPLEDOT_CANVASKIT_INCLUDED
#define TRIPLEDOT_CANVASKIT_INCLUDED

// ----------------------------------------------------------------------------
// Transforms
// ----------------------------------------------------------------------------

float ApplyCanvasPaintWrapScalar(float value, int wrapMode)
{
    switch (wrapMode) {
        case 1:
            return frac(value);
        case 2: {
            float repeated = frac(value * 0.5) * 2.0;
            return 1.0 - abs(repeated - 1.0);
        }
        default:
            return saturate(value);
    }
}

float2 ApplyCanvasPaintWrap2D(float2 uv, int wrapMode)
{
    switch (wrapMode) {
        case 1:
            return frac(uv);
        case 2: {
            float2 repeated = frac(uv * 0.5) * 2.0;
            return 1.0 - abs(repeated - 1.0);
        }
        default:
            return saturate(uv);
    }
}

float2 RotateIntoCanvasPaintSpace(float2 relative, float rotationDegrees)
{
    float rotation = rotationDegrees * 0.01745329252;
    float s;
    float c;
    sincos(rotation, s, c);
    return float2(relative.x * c - relative.y * s, relative.x * s + relative.y * c);
}

float2 TransformCanvasPaintTextureUV(float2 paintUV, float4 transform0, float4 transform1)
{
    float2 center = transform0.xy;
    float2 offset = transform0.zw;
    float2 scale = abs(transform1.xy);
    float2 relative = paintUV - center - offset;
    
    float rotation = transform1.z * 0.01745329252;
    float s;
    float c;
    sincos(rotation, s, c);
    
    float2 rotated = float2(relative.x * c - relative.y * s, relative.x * s + relative.y * c);
    return ApplyCanvasPaintWrap2D(rotated / scale + center, (int)(transform1.w + 0.5));
}

float2 TransformCanvasPaintGradientUV(float2 paintUV, float4 transform0, float4 transform1, float rotationDegrees)
{
    float2 origin = transform0.xy + transform0.zw;
    float2 scale = abs(transform1.xy);
    return RotateIntoCanvasPaintSpace(paintUV - origin, rotationDegrees) / scale;
}

float2 GetCanvasPaintUV(float2 localPosition, float4 paintBounds)
{
    return (localPosition - paintBounds.xy) / abs(paintBounds.zw);
}

// ----------------------------------------------------------------------------
// Gradient
// ----------------------------------------------------------------------------

float SampleCanvasLinearGradientT(float2 paintUV, float4 transform0, float4 transform1, float angleDegrees, float2 paintBoundsSize)
{
    float2 origin = transform0.xy + transform0.zw;
    float scale = abs(transform1.x);
    
    float rotation = angleDegrees * 0.01745329252;
    float s;
    float c;
    sincos(rotation, s, c);

    float2 boundsSize = max(abs(paintBoundsSize), float2(0.0001, 0.0001));
    float2 direction = float2(c, -s);
    float2 axis = direction * boundsSize;
    float2 relative = (paintUV - origin) * boundsSize;
    
    float axisLengthSq = max(dot(axis, axis), 0.0001);
    float gradientX = dot(relative, axis) / max((axisLengthSq * scale), 0.0001);
    
    return ApplyCanvasPaintWrapScalar(gradientX + 0.5, (int)(transform1.w + 0.5));
}

float SampleCanvasRadialGradientT(float2 paintUV, float4 transform0, float4 transform1)
{
    float2 gradientUV = TransformCanvasPaintGradientUV(paintUV, transform0, transform1, transform1.z);
    return ApplyCanvasPaintWrapScalar(length(gradientUV) * 2.0, (int)(transform1.w + 0.5));
}

half4 SampleCanvasGradientAtlas(TEXTURE2D_PARAM(gradientAtlas, sampler_gradientAtlas), float4 gradientAtlasRect, float t, float4 gradientAtlasTexelSize)
{
    float rowV = (gradientAtlasRect.y + 0.5) * gradientAtlasTexelSize.y;
    return half4(SAMPLE_TEXTURE2D(gradientAtlas, sampler_gradientAtlas, float2(gradientAtlasRect.x + saturate(t) * gradientAtlasRect.z, rowV)));
}

half4 ApplyCanvasGradientAtlasOpacity(half4 color, half opacity)
{
    color.a *= opacity;
    return color;
}

// ----------------------------------------------------------------------------
// Paint
// ----------------------------------------------------------------------------

half4 SampleCanvasPaint(int paintMode, 
    half4 colorA, half4 colorB, 
    TEXTURE2D_PARAM(paintTexture, sampler_paintTexture),
    TEXTURE2D_PARAM(gradientAtlas, sampler_gradientAtlas), 
    float4 gradientAtlasRect,
    float2 paintUV, 
    float gradientAngle,
    float4 textureTransform,
    float4 transform0,
    float4 transform1, 
    float2 paintBoundsSize,
    float4 gradientAtlasTexelSize)
{
    switch (paintMode) {
        case 1: {
            float t = SampleCanvasLinearGradientT(paintUV, transform0, transform1, gradientAngle, paintBoundsSize);
            if (gradientAtlasRect.w > 0.5) {
                return ApplyCanvasGradientAtlasOpacity(SampleCanvasGradientAtlas(TEXTURE2D_ARGS(gradientAtlas, sampler_gradientAtlas), gradientAtlasRect, t, gradientAtlasTexelSize), colorA.a);
            }

            return lerp(colorA, colorB, saturate(t));
        } case 2: {
            float t = SampleCanvasRadialGradientT(paintUV, transform0, transform1);
            if (gradientAtlasRect.w > 0.5) {
                return ApplyCanvasGradientAtlasOpacity(SampleCanvasGradientAtlas(TEXTURE2D_ARGS(gradientAtlas, sampler_gradientAtlas), gradientAtlasRect, t, gradientAtlasTexelSize), colorA.a);
            }

            return lerp(colorA, colorB, saturate(t));
        } case 3: {
            float2 transformedUV = TransformCanvasPaintTextureUV(paintUV, transform0, transform1);
            return half4(SAMPLE_TEXTURE2D(paintTexture, sampler_paintTexture, transformedUV * textureTransform.zw + textureTransform.xy)) * colorA;
        }
        default:
            return colorA;
    }

    return colorA;
}

// ----------------------------------------------------------------------------
// Compositing
// ----------------------------------------------------------------------------

half4 CompositePremultiplied(half4 dstPremultiplied, half4 srcStraight, half coverage)
{
    half srcAlpha = saturate(srcStraight.a * coverage);
    half dstAlpha = saturate(dstPremultiplied.a);
    half3 srcPremultiplied = srcStraight.rgb * srcAlpha;
    return half4(srcPremultiplied + dstPremultiplied.rgb * (1.0 - srcAlpha), srcAlpha + dstAlpha * (1.0 - srcAlpha));
}

half4 CompositeCanvasPaint(half4 current, half coverage, int paintMode,
    half4 colorA, half4 colorB, TEXTURE2D_PARAM(paintTexture, sampler_paintTexture),
    TEXTURE2D_PARAM(gradientAtlas, sampler_gradientAtlas), float4 gradientAtlasRect,
    float2 paintUV, float gradientAngle, float4 textureTransform, float4 transform0, float4 transform1, float2 paintBoundsSize, float4 gradientAtlasTexelSize)
{
    if (coverage <= 0.0) {
        return current;
    }

    half4 paint = SampleCanvasPaint(paintMode, colorA, colorB, TEXTURE2D_ARGS(paintTexture, sampler_paintTexture),
        TEXTURE2D_ARGS(gradientAtlas, sampler_gradientAtlas), gradientAtlasRect, paintUV, gradientAngle, textureTransform, transform0, transform1, paintBoundsSize, gradientAtlasTexelSize);
    return CompositePremultiplied(current, paint, coverage);
}

// ----------------------------------------------------------------------------
// Masking
// ----------------------------------------------------------------------------

float4 GetCanvasMask(float2 positionOS, float4 positionCS, float4 clipRect,
    float maskSoftnessX, float maskSoftnessY, float uiMaskSoftnessX, float uiMaskSoftnessY,
    float4 screenParams, float4x4 projectionMatrix)
{
    float4 clampedRect = clamp(clipRect, -2e10, 2e10);
    float2 pixelSize = positionCS.w;
    pixelSize /= abs(mul((float2x2)projectionMatrix, screenParams.xy));
    half2 maskSoftness = half2(max(uiMaskSoftnessX, maskSoftnessX), max(uiMaskSoftnessY, maskSoftnessY));
    return float4(positionOS * 2.0 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * maskSoftness + abs(pixelSize.xy)));
}

half GetCanvasMaskFactor(float4 mask, float4 clipRect)
{
    half2 clipped = saturate((clipRect.zw - clipRect.xy - abs(mask.xy)) * mask.zw);
    return clipped.x * clipped.y;
}

#endif // TRIPLEDOT_CANVASKIT_INCLUDED
