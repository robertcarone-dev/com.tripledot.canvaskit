using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    internal static class TextMeshProLayerUpgradeUtility
    {
        public static List<TextMeshProLayerData> ConvertMaterial(Material material)
        {
            var layers = new List<TextMeshProLayerData>();
            if (material == null) {
                layers.Add(TextMeshProLayerData.Default());
                return layers;
            }

            var gradientScale = TextMeshProUtility.GetEffectiveGradientScale(material);
            if (HasUnderlay(material)) {
                layers.Add(CreateUnderlayLayer(material, gradientScale));
            }

            if (HasGlow(material)) {
                layers.Add(CreateGlowLayer(material, gradientScale));
            }

            layers.Add(CreateFaceLayer(material, gradientScale));
            
            return layers;
        }

        public static TextMeshProLayerStack UpgradeText(TextMeshProUGUI text)
        {
            if (text == null) {
                return null;
            }

            var stack = text.GetComponent<TextMeshProLayerStack>();
            if (stack == null) {
                stack = text.gameObject.AddComponent<TextMeshProLayerStack>();
            }

            var material = text.fontSharedMaterial != null ? text.fontSharedMaterial : text.materialForRendering;
            var layers = ConvertMaterial(material);
            stack.Preset = null;
            stack.LocalLayers.Clear();
            for (var i = 0; i < layers.Count; i++) {
                stack.LocalLayers.Add(layers[i]);
            }

            stack.SetLayerStackDirty();
            EditorUtility.SetDirty(stack);
            EditorUtility.SetDirty(text);
            
            return stack;
        }

        public static TextMeshProLayerPreset CreatePresetFromMaterial(Material material, string assetPath)
        {
            if (material == null || string.IsNullOrEmpty(assetPath)) {
                return null;
            }

            var preset = ScriptableObject.CreateInstance<TextMeshProLayerPreset>();
            preset.CopyFrom(ConvertMaterial(material), ResolveFontAsset(material));
            AssetDatabase.CreateAsset(preset, AssetDatabase.GenerateUniqueAssetPath(assetPath));
            AssetDatabase.SaveAssets();
            return preset;
        }

        public static TMP_FontAsset ResolveFontAsset(Material material)
        {
            if (material == null) {
                return null;
            }

            var materialAssetPath = AssetDatabase.GetAssetPath(material);
            var mainTexture = GetTexture(material, ShaderUtilities.ID_MainTex);
            var guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            
            for (var i = 0; i < guids.Length; i++) {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (font == null) {
                    continue;
                }

                if (!string.IsNullOrEmpty(materialAssetPath) && font.material == material) {
                    return font;
                }

                if (mainTexture != null && font.atlasTexture == mainTexture) {
                    return font;
                }

                if (font.material != null && GetTexture(font.material, ShaderUtilities.ID_MainTex) == mainTexture && mainTexture != null) {
                    return font;
                }
            }

            return null;
        }

        public static string GetPresetAssetPath(Material material)
        {
            var materialPath = AssetDatabase.GetAssetPath(material);
            if (string.IsNullOrEmpty(materialPath)) {
                return "Assets/" + (material != null ? material.name : "TextMeshPro") + " Layer Preset.asset";
            }

            var directory = System.IO.Path.GetDirectoryName(materialPath)?.Replace('\\', '/');
            var fileName = System.IO.Path.GetFileNameWithoutExtension(materialPath);
            return string.IsNullOrEmpty(directory)
                ? fileName + " Layer Preset.asset"
                : directory + "/" + fileName + " Layer Preset.asset";
        }

        private static TextMeshProLayerData CreateFaceLayer(Material material, float gradientScale)
        {
            var layer = TextMeshProLayerData.Default();
            layer.Face = new TextMeshProFace {
                Enabled = true,
                Paint = CanvasPaint.Solid(GetColor(material, ShaderUtilities.ID_FaceColor, Color.white)),
                Dilate = NormalizedDistanceToPixels(GetFloat(material, ShaderUtilities.ID_FaceDilate, 0f), gradientScale),
                DilateUnit = TextMeshProSdfLengthUnit.Pixels,
                Lighting = CreateFaceLighting(material)
            };

            var outlineWidth = GetFloat(material, ShaderUtilities.ID_OutlineWidth, 0f);
            var outlineSoftness = GetFloat(material, ShaderUtilities.ID_OutlineSoftness, 0f);
            var outlineColor = GetColor(material, ShaderUtilities.ID_OutlineColor, Color.black);
            
            if (outlineWidth > 0f && outlineColor.a > 0f) {
                layer.Stroke = new TextMeshProStroke {
                    Enabled = true,
                    Paint = CanvasPaint.Solid(outlineColor),
                    Position = TextMeshProStrokePosition.Center,
                    Width = NormalizedOutlineWidthToPixels(outlineWidth, gradientScale),
                    WidthUnit = TextMeshProSdfLengthUnit.Pixels,
                    Feather = NormalizedSoftnessToPixels(outlineSoftness, gradientScale),
                    FeatherUnit = TextMeshProSdfLengthUnit.Pixels
                };
            }

            return layer;
        }

        private static TextMeshProLayerData CreateUnderlayLayer(Material material, float gradientScale)
        {
            var layer = TextMeshProLayerData.ShadowPreset();
            layer.Label = "Underlay";
            layer.Shadow = new TextMeshProShadow {
                Enabled = true,
                Paint = CanvasPaint.Solid(GetColor(material, ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.5f))),
                Offset = new Vector2(
                    NormalizedDistanceToPixels(GetFloat(material, ShaderUtilities.ID_UnderlayOffsetX, 0f), gradientScale),
                    NormalizedDistanceToPixels(GetFloat(material, ShaderUtilities.ID_UnderlayOffsetY, 0f), gradientScale)),
                Spread = NormalizedDistanceToPixels(GetFloat(material, ShaderUtilities.ID_UnderlayDilate, 0f), gradientScale),
                SpreadUnit = TextMeshProSdfLengthUnit.Pixels,
                Blur = NormalizedSoftnessToPixels(GetFloat(material, ShaderUtilities.ID_UnderlaySoftness, 0f), gradientScale),
                BlurUnit = TextMeshProSdfLengthUnit.Pixels
            };
            return layer;
        }

        private static TextMeshProLayerData CreateGlowLayer(Material material, float gradientScale)
        {
            var layer = TextMeshProLayerData.GlowPreset();
            
            var spread = NormalizedDistanceToPixels(GetFloat(material, ShaderUtilities.ID_GlowOffset, 0f) - GetFloat(material, ShaderUtilities.ID_GlowInner, 0f), gradientScale);
            var blur = NormalizedDistanceToPixels(GetFloat(material, ShaderUtilities.ID_GlowOuter, 0f) + GetFloat(material, ShaderUtilities.ID_GlowInner, 0f), gradientScale);
            
            layer.Shadow = new TextMeshProShadow {
                Enabled = true,
                Paint = CanvasPaint.Solid(GetColor(material, ShaderUtilities.ID_GlowColor, new Color(1f, 1f, 1f, 0.5f))),
                Offset = Vector2.zero,
                Spread = spread,
                SpreadUnit = TextMeshProSdfLengthUnit.Pixels,
                Blur = blur,
                BlurUnit = TextMeshProSdfLengthUnit.Pixels
            };
            
            return layer;
        }

        private static bool HasUnderlay(Material material)
        {
            return material != null
                && material.HasProperty(ShaderUtilities.ID_UnderlayColor)
                && GetColor(material, ShaderUtilities.ID_UnderlayColor, Color.clear).a > 0f
                && (material.IsKeywordEnabled("UNDERLAY_ON") || material.IsKeywordEnabled("UNDERLAY_INNER"));
        }

        private static bool HasGlow(Material material)
        {
            return material != null
                && material.HasProperty(ShaderUtilities.ID_GlowColor)
                && GetColor(material, ShaderUtilities.ID_GlowColor, Color.clear).a > 0f
                && material.IsKeywordEnabled("GLOW_ON");
        }

        private static TextMeshProFaceLighting CreateFaceLighting(Material material)
        {
            if (!HasBevel(material)) {
                return TextMeshProFaceLighting.Default;
            }

            // TMP bevel also supports offset, clamp, bump maps, reflections, and exact specular exponent.
            // Canvas Kit face lighting keeps only the approximate face-only highlight/shadow intent.
            var specular = GetColor(material, "_SpecularColor", Color.white);
            var highlightColor = new Color(specular.r, specular.g, specular.b, Mathf.Clamp01(GetFloat(material, "_SpecularPower", 0f) / 4f));
            var shadowStrength = Mathf.Clamp01(Mathf.Max(GetFloat(material, "_Diffuse", 0f), 1f - GetFloat(material, "_Ambient", 1f)) * 0.5f);

            return new TextMeshProFaceLighting {
                Enabled = true,
                BevelWidth = Mathf.Clamp01(Mathf.Max(Mathf.Abs(GetFloat(material, "_BevelWidth", 0f)) * 2f, 0.2f)),
                BevelSoftness = Mathf.Clamp01(Mathf.Lerp(0.2f, 0.75f, GetFloat(material, "_BevelRoundness", 0f))),
                LightAngle = Mathf.Repeat(90f - GetFloat(material, ShaderUtilities.ID_LightAngle, Mathf.PI) * Mathf.Rad2Deg, 360f),
                HighlightColor = highlightColor,
                HighlightColorUsesHdrPicker = IsHdrColor(highlightColor),
                ShadowColor = new Color(0f, 0f, 0f, shadowStrength)
            };
        }

        private static bool HasBevel(Material material)
        {
            return material != null
                && material.HasProperty(ShaderUtilities.ID_BevelAmount)
                && material.IsKeywordEnabled(ShaderUtilities.Keyword_Bevel)
                && GetFloat(material, ShaderUtilities.ID_BevelAmount, 0f) > 0f;
        }

        private static bool IsHdrColor(Color color)
        {
            return color.r > 1f || color.g > 1f || color.b > 1f;
        }

        private static float NormalizedOutlineWidthToPixels(float value, float gradientScale)
        {
            return Mathf.Max(0f, value) * gradientScale;
        }

        private static float NormalizedSoftnessToPixels(float value, float gradientScale)
        {
            return Mathf.Max(0f, value) * gradientScale;
        }

        private static float NormalizedDistanceToPixels(float value, float gradientScale)
        {
            return value * gradientScale;
        }

        private static Color GetColor(Material material, int propertyId, Color fallback)
        {
            return material != null && material.HasProperty(propertyId) ? material.GetColor(propertyId) : fallback;
        }

        private static Color GetColor(Material material, string propertyName, Color fallback)
        {
            return material != null && material.HasProperty(propertyName) ? material.GetColor(propertyName) : fallback;
        }

        private static float GetFloat(Material material, int propertyId, float fallback)
        {
            return material != null && material.HasProperty(propertyId) ? material.GetFloat(propertyId) : fallback;
        }

        private static float GetFloat(Material material, string propertyName, float fallback)
        {
            return material != null && material.HasProperty(propertyName) ? material.GetFloat(propertyName) : fallback;
        }

        private static Texture GetTexture(Material material, int propertyId)
        {
            return material != null && material.HasProperty(propertyId) ? material.GetTexture(propertyId) : null;
        }
    }
}
