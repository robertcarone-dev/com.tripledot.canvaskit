using System;

namespace Tripledot.CanvasKit.TextMeshPro
{
    internal readonly struct TextMeshProLayerMaterialCacheKey : IEquatable<TextMeshProLayerMaterialCacheKey>
    {
        public readonly TextMeshProLayerPreset Preset;
        public readonly int PresetVersion;
        public readonly int LayerIndex;
        public readonly TextMeshProLayerMaterialContext MaterialContext;

        public TextMeshProLayerMaterialCacheKey(
            TextMeshProLayerPreset preset,
            int presetVersion,
            int layerIndex,
            TextMeshProLayerMaterialContext materialContext)
        {
            Preset = preset;
            PresetVersion = presetVersion;
            LayerIndex = layerIndex;
            MaterialContext = materialContext;
        }

        public bool Equals(TextMeshProLayerMaterialCacheKey other)
        {
            return ReferenceEquals(Preset, other.Preset)
                   && PresetVersion == other.PresetVersion
                   && LayerIndex == other.LayerIndex
                   && MaterialContext.Equals(other.MaterialContext);
        }

        public override bool Equals(object obj)
        {
            return obj is TextMeshProLayerMaterialCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked {
                var hashCode = Preset != null ? Preset.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ PresetVersion;
                hashCode = (hashCode * 397) ^ LayerIndex;
                hashCode = (hashCode * 397) ^ MaterialContext.GetHashCode();
                return hashCode;
            }
        }
    }
}