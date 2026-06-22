using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tripledot.CanvasKit
{
    [Serializable]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class CanvasKitSettings : IRenderPipelineGraphicsSettings
    {
        public const int DefaultGradientAtlasWidth = 256;
        public const int MinGradientAtlasWidth = 32;
        public const int MaxGradientAtlasWidth = 2048;

        [SerializeField]
        [HideInInspector]
        private int version;
        
        [SerializeField]
        [Range(MinGradientAtlasWidth, MaxGradientAtlasWidth)]
        [Tooltip("The width of the gradient atlas texture. Must be a power of two.")]
        private int gradientAtlasWidth = DefaultGradientAtlasWidth;

        private static int? _gradientAtlasWidthOverride;

        int IRenderPipelineGraphicsSettings.version => version;

        bool IRenderPipelineGraphicsSettings.isAvailableInPlayerBuild => true;

        public int GradientAtlasWidth {
            get => ClampGradientAtlasWidth(gradientAtlasWidth);
            set => this.SetValueAndNotify(ref gradientAtlasWidth, ClampGradientAtlasWidth(value));
        }

        public static int CurrentGradientAtlasWidth {
            get {
                if (_gradientAtlasWidthOverride.HasValue) {
                    return ClampGradientAtlasWidth(_gradientAtlasWidthOverride.Value);
                }

                return GraphicsSettings.TryGetRenderPipelineSettings<CanvasKitSettings>(out var settings)
                    ? settings.GradientAtlasWidth
                    : DefaultGradientAtlasWidth;
            }
        }

        public static int ClampGradientAtlasWidth(int value)
        {
            value = Mathf.Clamp(value, MinGradientAtlasWidth, MaxGradientAtlasWidth);
            return Mathf.ClosestPowerOfTwo(value);
        }
    }
}