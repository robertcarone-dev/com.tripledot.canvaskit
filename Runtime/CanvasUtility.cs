using UnityEngine;

namespace Tripledot.CanvasKit
{
    internal static class CanvasUtility
    {
        public static void EnsureChannels(Canvas canvas, AdditionalCanvasShaderChannels channels)
        {
            var rootCanvas = canvas != null ? canvas.rootCanvas : null;
            if (rootCanvas != null) {
                rootCanvas.additionalShaderChannels |= channels;
            }
        }

        public static Vector4 BoundsFromMinMax(Vector3 min, Vector3 max)
        {
            return new Vector4(min.x, min.y, Mathf.Max(0f, max.x - min.x), Mathf.Max(0f, max.y - min.y));
        }

        public static Color WithOpacity(Color color, float opacity)
        {
            color.a *= opacity;
            return color;
        }

        public static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled) {
                material.EnableKeyword(keyword);
            } else {
                material.DisableKeyword(keyword);
            }
        }
    }
}