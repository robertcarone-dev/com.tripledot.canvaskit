using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tripledot.CanvasKit
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
                hashCode = (hashCode * 397) ^ LayerIndex;
                hashCode = (hashCode * 397) ^ MaterialContext.GetHashCode();
                return hashCode;
            }
        }
    }

    internal static class TextMeshProLayerMaterialCache
    {
        private static readonly Dictionary<TextMeshProLayerMaterialCacheKey, Entry> Entries = new Dictionary<TextMeshProLayerMaterialCacheKey, Entry>();

        public static Entry Acquire(TextMeshProLayerMaterialCacheKey key, TextMeshProLayerData layer, TextMeshProLayerMaterialContext context)
        {
            if (Entries.TryGetValue(key, out var entry)) {
                if (entry.Material != null) {
                    if (entry.PresetVersion != key.PresetVersion) {
                        layer.ApplyMaterial(entry.Material, context, entry.GradientState);
                        entry.PresetVersion = key.PresetVersion;
                    }

                    entry.ReferenceCount++;
                    return entry;
                }

                entry.GradientState.Release();
                Entries.Remove(key);
            }

            var material = TextMeshProLayerMaterial.CreateMaterial(TextMeshProLayerMaterial.ResolveCoreShader());
            material.name = (key.Preset != null ? key.Preset.name : "Preset") + " (TextLayerCore Shared)";

            entry = new Entry(key, material);
            layer.ApplyMaterial(material, context, entry.GradientState);
            Entries.Add(key, entry);
            return entry;
        }

        public static void Release(Entry entry)
        {
            if (entry == null) {
                return;
            }

            entry.ReferenceCount--;
            if (entry.ReferenceCount <= 0) {
                Entries.Remove(entry.Key);
                entry.GradientState.Release();
                CoreUtils.Destroy(entry.Material);
            }
        }

        public sealed class Entry
        {
            public readonly TextMeshProLayerMaterialCacheKey Key;
            public readonly Material Material;
            public readonly TextMeshProLayerMaterialGradientState GradientState;
            public int PresetVersion;
            public int ReferenceCount;

            public Entry(TextMeshProLayerMaterialCacheKey key, Material material)
            {
                Key = key;
                Material = material;
                GradientState = new TextMeshProLayerMaterialGradientState();
                PresetVersion = key.PresetVersion;
                ReferenceCount = 1;
            }
        }
    }
}