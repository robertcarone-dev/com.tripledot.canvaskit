using UnityEngine;

namespace Tripledot.CanvasKit
{
    internal static class CanvasUtility
    {
        internal static void EnsureChannels(Canvas canvas, AdditionalCanvasShaderChannels channels)
        {
            var rootCanvas = canvas != null ? canvas.rootCanvas : null;
            if (rootCanvas == null) {
                return;
            }

            rootCanvas.additionalShaderChannels |= channels;
        }

        internal static Vector4 BoundsFromMinMax(Vector3 min, Vector3 max)
        {
            return new Vector4(min.x, min.y, Mathf.Max(0f, max.x - min.x), Mathf.Max(0f, max.y - min.y));
        }
    }
}
