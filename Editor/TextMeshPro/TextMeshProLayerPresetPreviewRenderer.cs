using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    internal static class TextMeshProLayerPresetPreviewRenderer
    {
        internal const string PreviewText = TextMeshProLayerPreset.DefaultPreviewText;

        private const int PreviewLayer = 31;
        private const int PreviewPaddingX = 12;
        private const int PreviewPaddingY = 8;
        private static readonly Color PreviewBackgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        private static readonly int StrokeEnabledId = Shader.PropertyToID("_StrokeEnabled");
        private static readonly int StrokeWeightId = Shader.PropertyToID("_StrokeWeight");

        internal static bool CanPreview(TextMeshProLayerPreset preset)
        {
            return preset != null && preset.FontAsset != null && preset.LayerCount > 0;
        }

        internal static Texture2D RenderPreviewTexture(TextMeshProLayerPreset preset, int width, int height)
        {
            if (!CanPreview(preset) || width <= 0 || height <= 0) {
                return null;
            }

            return RenderPreviewTexture(preset, width, height, out _);
        }

        internal static PreviewDiagnostics RenderPreviewDiagnosticsForTests(TextMeshProLayerPreset preset, int width, int height)
        {
            if (!CanPreview(preset) || width <= 0 || height <= 0) {
                return PreviewDiagnostics.Empty;
            }

            var texture = RenderPreviewTexture(preset, width, height, out var diagnostics);
            if (texture != null) {
                Object.DestroyImmediate(texture);
            }

            return diagnostics;
        }

        private static Texture2D RenderPreviewTexture(TextMeshProLayerPreset preset, int width, int height, out PreviewDiagnostics diagnostics)
        {
            diagnostics = PreviewDiagnostics.Empty;
            var renderTexture = UnityEngine.RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previousActive = UnityEngine.RenderTexture.active;
            Texture2D texture = null;
            var cameraObject = new GameObject("TextMeshPro Layer Preset Preview Camera") {
                hideFlags = HideFlags.HideAndDontSave
            };
            var root = new GameObject("TextMeshPro Layer Preset Preview") {
                hideFlags = HideFlags.HideAndDontSave,
                layer = PreviewLayer
            };

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
                Canvas.ForceUpdateCanvases();
                diagnostics = CaptureDiagnostics(canvas, text);
                camera.Render();

                UnityEngine.RenderTexture.active = renderTexture;
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
                UnityEngine.RenderTexture.active = previousActive;
                UnityEngine.RenderTexture.ReleaseTemporary(renderTexture);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraObject);
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
            text.fontSize = Mathf.Clamp(height * 0.52f, 16f, 48f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.color = Color.white;

            var stack = textObject.AddComponent<TextMeshProLayerStack>();
            stack.Preset = preset;
            return text;
        }

        private static PreviewDiagnostics CaptureDiagnostics(Canvas canvas, TextMeshProUGUI text)
        {
            var canvasRenderer = text.canvasRenderer;
            var materialCount = canvasRenderer.materialCount;
            var mesh = canvasRenderer.GetMesh();
            var shaderNames = new List<string>(materialCount);
            var strokeEnabledValues = new List<int>(materialCount);
            var strokeWeightValues = new List<float>(materialCount);

            for (int i = 0; i < materialCount; i++) {
                var material = canvasRenderer.GetMaterial(i);
                shaderNames.Add(material != null && material.shader != null ? material.shader.name : string.Empty);
                if (material == null) {
                    continue;
                }

                if (material.HasProperty(StrokeEnabledId)) {
                    strokeEnabledValues.Add(material.GetInteger(StrokeEnabledId));
                }

                if (material.HasProperty(StrokeWeightId)) {
                    strokeWeightValues.Add(material.GetFloat(StrokeWeightId));
                }
            }

            return new PreviewDiagnostics(
                materialCount,
                mesh != null ? mesh.vertexCount : 0,
                canvas != null ? canvas.additionalShaderChannels : default,
                shaderNames.ToArray(),
                strokeEnabledValues.ToArray(),
                strokeWeightValues.ToArray());
        }

        internal static void DrawPreview(Rect rect, Texture texture, GUIStyle background)
        {
            if (Event.current.type != EventType.Repaint) {
                return;
            }

            background?.Draw(rect, false, false, false, false);
            if (texture != null) {
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
            }
        }

        internal readonly struct PreviewDiagnostics
        {
            internal static readonly PreviewDiagnostics Empty = new PreviewDiagnostics(
                0,
                0,
                default,
                System.Array.Empty<string>(),
                System.Array.Empty<int>(),
                System.Array.Empty<float>());

            internal readonly int MaterialCount;
            internal readonly int MeshVertexCount;
            internal readonly AdditionalCanvasShaderChannels CanvasChannels;
            internal readonly string[] ShaderNames;
            internal readonly int[] StrokeEnabledValues;
            internal readonly float[] StrokeWeightValues;

            internal PreviewDiagnostics(
                int materialCount,
                int meshVertexCount,
                AdditionalCanvasShaderChannels canvasChannels,
                string[] shaderNames,
                int[] strokeEnabledValues,
                float[] strokeWeightValues)
            {
                MaterialCount = materialCount;
                MeshVertexCount = meshVertexCount;
                CanvasChannels = canvasChannels;
                ShaderNames = shaderNames;
                StrokeEnabledValues = strokeEnabledValues;
                StrokeWeightValues = strokeWeightValues;
            }
        }
    }
}
