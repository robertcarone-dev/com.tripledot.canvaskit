using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Tripledot.CanvasKit
{
    public enum TextMeshProSdfLengthUnit
    {
        Pixels = 0,
        Percent = 1
    }

    internal static class TextMeshProUtility
    {
        private const float SdfPixelsToPaddingPixelsScale = 2f;
        private const float SdfPixelsToPaddingPixelsScaleRcp = 1f / SdfPixelsToPaddingPixelsScale;

        public static Vector4 PaddingUniform(float value)
        {
            return new Vector4(value, value, value, value);
        }

        public static Vector4 PaddingWithDirectionalOffset(Vector4 padding, Vector2 offset, Vector2 localUnitsPerPaddingPixel)
        {
            var x = LocalOffsetToPaddingPixels(offset.x, localUnitsPerPaddingPixel.x);
            var y = LocalOffsetToPaddingPixels(offset.y, localUnitsPerPaddingPixel.y);
            return new Vector4(
                padding.x + (offset.x < 0f ? x : 0f),
                padding.y + (offset.x > 0f ? x : 0f),
                padding.z + (offset.y > 0f ? y : 0f),
                padding.w + (offset.y < 0f ? y : 0f));
        }

        public static Vector4 PaddingMax(Vector4 a, Vector4 b)
        {
            return new Vector4(
                Mathf.Max(a.x, b.x),
                Mathf.Max(a.y, b.y),
                Mathf.Max(a.z, b.z),
                Mathf.Max(a.w, b.w));
        }

        public static Vector4 CalculateBounds(TextMeshProUGUI text, bool forceMeshUpdate = false)
        {
            if (forceMeshUpdate) {
                text.ForceMeshUpdate();
            }

            return TryCalculateVisibleGlyphBounds(text.textInfo, out var bounds) ? bounds : CalculateFrameBounds(text);
        }

        public static Vector4 CalculateFrameBounds(TextMeshProUGUI text)
        {
            var textRect = text.rectTransform.rect;
            return new Vector4(textRect.xMin, textRect.yMin, Mathf.Max(0f, textRect.width), Mathf.Max(0f, textRect.height));
        }

        public static bool TryCalculateVisibleGlyphBounds(TMP_TextInfo textInfo, out Vector4 bounds)
        {
            return TryCalculateLayerBounds(textInfo, null, 0f, out bounds);
        }

        public static bool TryCalculateLayerBounds(TMP_TextInfo textInfo, IList<TextMeshProLayerData> layers, float sdfPaddingLimit, out Vector4 bounds)
        {
            bounds = default;

            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var found = false;

            for (int i = 0; i < textInfo.characterCount; i++) {
                var character = textInfo.characterInfo[i];
                if (!TryCalculateVisibleCharacterBounds(character, out var characterMin, out var characterMax, out var localUnitsPerAtlasPixel)) {
                    continue;
                }

                if (layers == null || layers.Count == 0) {
                    Encapsulate(characterMin, characterMax, ref min, ref max);
                    found = true;
                    continue;
                }

                for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++) {
                    var layer = layers[layerIndex];
                    if (layer is not { Enabled: true }) {
                        continue;
                    }

                    var padding = layer.GetVisualPadding(sdfPaddingLimit, localUnitsPerAtlasPixel);
                    var offset = layer.GeometryOffset;

                    var paddedMin = new Vector2(
                        characterMin.x - padding.x * localUnitsPerAtlasPixel.x + offset.x,
                        characterMin.y - padding.w * localUnitsPerAtlasPixel.y + offset.y);
                    var paddedMax = new Vector2(
                        characterMax.x + padding.y * localUnitsPerAtlasPixel.x + offset.x,
                        characterMax.y + padding.z * localUnitsPerAtlasPixel.y + offset.y);

                    Encapsulate(paddedMin, paddedMax, ref min, ref max);
                    found = true;
                }
            }

            if (!found) {
                return false;
            }

            bounds = new Vector4(min.x, min.y, Mathf.Max(0f, max.x - min.x), Mathf.Max(0f, max.y - min.y));
            return true;
        }

        public static bool TryCalculateGlyphBounds(TMP_TextInfo textInfo, out Vector4 bounds)
        {
            bounds = default;

            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var found = false;

            for (int i = 0; i < textInfo.characterCount; i++) {
                var character = textInfo.characterInfo[i];
                if (!character.isVisible || character.materialReferenceIndex < 0 || character.materialReferenceIndex >= textInfo.meshInfo.Length) {
                    continue;
                }

                var vertices = textInfo.meshInfo[character.materialReferenceIndex].vertices;
                var vertexIndex = character.vertexIndex;
                if (vertices == null || vertexIndex < 0 || vertices.Length < vertexIndex + 4) {
                    continue;
                }

                Encapsulate(vertices[vertexIndex + 0], ref min, ref max);
                Encapsulate(vertices[vertexIndex + 1], ref min, ref max);
                Encapsulate(vertices[vertexIndex + 2], ref min, ref max);
                Encapsulate(vertices[vertexIndex + 3], ref min, ref max);
                found = true;
            }

            if (!found) {
                return false;
            }

            bounds = new Vector4(min.x, min.y, Mathf.Max(0f, max.x - min.x), Mathf.Max(0f, max.y - min.y));
            return true;
        }

        public static float CalculateAvailablePadding(TextMeshProUGUI text)
        {
            var fontAsset = text.font;
            if (fontAsset != null && fontAsset.atlasPadding > 0) {
                return PaddingPixelsToSdfPixels(Mathf.Max(0f, fontAsset.atlasPadding));
            }

            return 0f;
        }

        public static float GetGradientScale(Material material)
        {
            return material != null && material.HasProperty(ShaderUtilities.ID_GradientScale)
                ? material.GetFloat(ShaderUtilities.ID_GradientScale)
                : 0f;
        }

        public static float PixelsToFaceDilate(float pixels, float gradientScale)
        {
            return pixels / GetEffectiveGradientScale(gradientScale);
        }

        public static float SdfPixelsToPaddingPixels(float pixels)
        {
            return Mathf.Max(0f, pixels) * SdfPixelsToPaddingPixelsScale;
        }

        public static float PaddingPixelsToSdfPixels(float pixels)
        {
            return Mathf.Max(0f, pixels) * SdfPixelsToPaddingPixelsScaleRcp;
        }

        public static void ClampStrokeEffect(float width, float feather, TextMeshProStrokePosition position, float availablePadding, float reservedPadding, out float clampedWidth, out float clampedFeather)
        {
            var available = GetRemainingPadding(availablePadding, reservedPadding);
            var strokeWidthFactor = GetStrokeVisualPaddingFactor(position);
            var maxWidth = strokeWidthFactor > 0.000001f ? available / strokeWidthFactor : available;
            clampedWidth = Mathf.Min(Mathf.Max(0f, width), maxWidth);
            clampedFeather = Mathf.Min(Mathf.Max(0f, feather), Mathf.Max(0f, available - clampedWidth * strokeWidthFactor));
        }

        public static void ClampShadowEffect(float spread, float blur, float availablePadding, float reservedPadding, out float clampedSpread, out float clampedBlur)
        {
            var outwardAvailable = GetRemainingPadding(availablePadding, reservedPadding);
            var signedAvailable = float.IsPositiveInfinity(availablePadding) ? availablePadding : Mathf.Max(0f, availablePadding);
            clampedSpread = Mathf.Clamp(spread, -signedAvailable, outwardAvailable);
            clampedBlur = Mathf.Min(Mathf.Max(0f, blur), Mathf.Max(0f, outwardAvailable - clampedSpread));
        }

        public static float GetStrokeVisualPaddingFactor(TextMeshProStrokePosition position)
        {
            return position switch {
                TextMeshProStrokePosition.Outside => 1f,
                TextMeshProStrokePosition.Center => 0.5f,
                _ => 0f
            };
        }

        public static float GetGeometryPaddingLimit(float effectPaddingBudget)
        {
            return effectPaddingBudget > 0f ? SdfPixelsToPaddingPixels(effectPaddingBudget) : 0f;
        }

        public static float PixelsToPercent(float pixels, float availablePadding)
        {
            return availablePadding <= 0f ? 0f : pixels / availablePadding * 100f;
        }

        public static float PercentToPixels(float percent, float availablePadding)
        {
            return availablePadding <= 0f ? 0f : percent * availablePadding / 100f;
        }

        public static float GetEffectiveGradientScale(Material material)
        {
            return GetEffectiveGradientScale(GetGradientScale(material));
        }

        public static float GetEffectiveGradientScale(float gradientScale)
        {
            return gradientScale > 0f ? gradientScale : 1f;
        }

        private static float GetRemainingPadding(float availablePadding, float reservedPadding)
        {
            return Mathf.Max(0f, availablePadding - Mathf.Max(0f, reservedPadding));
        }

        private static float LocalOffsetToPaddingPixels(float localOffset, float localUnitsPerPaddingPixel)
        {
            return localUnitsPerPaddingPixel > 0.000001f ? Mathf.Abs(localOffset) / localUnitsPerPaddingPixel : Mathf.Abs(localOffset);
        }

        private static void Encapsulate(Vector3 vertex, ref Vector2 min, ref Vector2 max)
        {
            min.x = Mathf.Min(min.x, vertex.x);
            min.y = Mathf.Min(min.y, vertex.y);
            max.x = Mathf.Max(max.x, vertex.x);
            max.y = Mathf.Max(max.y, vertex.y);
        }

        private static void Encapsulate(Vector2 characterMin, Vector2 characterMax, ref Vector2 min, ref Vector2 max)
        {
            min = Vector2.Min(min, characterMin);
            max = Vector2.Max(max, characterMax);
        }

        private static bool TryCalculateVisibleCharacterBounds(TMP_CharacterInfo character, out Vector2 min, out Vector2 max, out Vector2 localUnitsPerAtlasPixel)
        {
            min = default;
            max = default;
            localUnitsPerAtlasPixel = Vector2.zero;

            if (!character.isVisible || character.elementType is not (TMP_TextElementType.Character or TMP_TextElementType.Sprite)) {
                return false;
            }

            var glyph = character.alternativeGlyph ?? character.textElement?.glyph;
            if (glyph == null) {
                return false;
            }

            var metrics = glyph.metrics;
            var glyphRect = glyph.glyphRect;
            if (metrics.width <= 0f || metrics.height <= 0f || glyphRect.width <= 0 || glyphRect.height <= 0) {
                return false;
            }

            var scale = character.scale;
            var width = metrics.width * scale;
            var height = metrics.height * scale;
            var xMin = character.origin + metrics.horizontalBearingX * scale;
            var yMax = character.baseLine + metrics.horizontalBearingY * scale;

            min = new Vector2(xMin, yMax - height);
            max = new Vector2(xMin + width, yMax);
            localUnitsPerAtlasPixel = new Vector2(width / glyphRect.width, height / glyphRect.height);

            return max.x > min.x && max.y > min.y;
        }
    }
}
