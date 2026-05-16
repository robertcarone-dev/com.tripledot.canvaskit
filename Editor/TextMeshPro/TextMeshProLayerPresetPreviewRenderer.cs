using TMPro;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    internal static class TextMeshProLayerPresetPreviewRenderer
    {
        private const int PreviewLayer = 31;
        private const int PreviewPaddingX = 12;
        private const int PreviewPaddingY = 8;
        
        private const int MinFontSize = 16;
        private const int MaxFontSize = 64;
        
        private static readonly Color PreviewBackgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);

        public static bool CanPreview(TextMeshProLayerPreset preset)
        {
            return preset != null && preset.FontAsset != null && preset.LayerCount > 0;
        }

        public static Texture2D RenderPreviewTexture(TextMeshProLayerPreset preset, int width, int height)
        {
            if (!CanPreview(preset) || width <= 0 || height <= 0) {
                return null;
            }
            
            var renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previousActiveRT = RenderTexture.active;
            
            var cameraObject = new GameObject("Preset Preview Camera") { hideFlags = HideFlags.HideAndDontSave };
            var root = new GameObject("Preset Preview") {
                hideFlags = HideFlags.HideAndDontSave,
                layer = PreviewLayer
            };

            Texture2D texture = null;
            try {
                var camera = cameraObject.AddComponent<Camera>();
                camera.hideFlags = HideFlags.HideAndDontSave;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = PreviewBackgroundColor;
                camera.orthographic = true;
                camera.orthographicSize = height * 0.5f;
                camera.aspect = width / (float)height;
                camera.cullingMask = 1 << PreviewLayer;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 100f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.targetTexture = renderTexture;

                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                
                var canvasRect = root.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(width, height);

                var text = CreateStackPreview(root.transform, preset, height);
                text.ForceMeshUpdate();
                
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture.active = renderTexture;
                texture = new Texture2D(width, height, TextureFormat.ARGB32, false) {
                    hideFlags = HideFlags.HideAndDontSave
                };
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply(false, false);
                return texture;
            } catch {
                if (texture != null) {
                    Object.DestroyImmediate(texture);
                }

                return null;
            } finally {
                RenderTexture.active = previousActiveRT;
                RenderTexture.ReleaseTemporary(renderTexture);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraObject);
            }
        }

        public static void DrawPreview(Rect rect, Texture texture, GUIStyle background)
        {
            if (Event.current.type != EventType.Repaint) {
                return;
            }

            background?.Draw(rect, false, false, false, false);
            if (texture != null) {
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
            }
        }

        private static TextMeshProUGUI CreateStackPreview(Transform parent, TextMeshProLayerPreset preset, int height)
        {
            var textObject = new GameObject("Preview Text") {
                hideFlags = HideFlags.HideAndDontSave,
                layer = PreviewLayer
            };
            textObject.transform.SetParent(parent, false);
            
            var textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(PreviewPaddingX, PreviewPaddingY);
            textRect.offsetMax = new Vector2(-PreviewPaddingX, -PreviewPaddingY);

            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = preset.FontAsset;
            text.text = preset.GetPreviewText();
            text.fontSize = Mathf.Clamp(height * 0.52f, MinFontSize, MaxFontSize);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.color = Color.white;

            var stack = textObject.AddComponent<TextMeshProLayerStack>();
            stack.Preset = preset;
            
            return text;
        }
    }
}
