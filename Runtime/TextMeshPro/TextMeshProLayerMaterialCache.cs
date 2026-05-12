using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tripledot.CanvasKit
{
    internal readonly struct TextMeshProLayerMaterialCacheKey : IEquatable<TextMeshProLayerMaterialCacheKey>
    {
        internal readonly TextMeshProLayerPreset Preset;
        internal readonly int PresetVersion;
        internal readonly int LayerIndex;
        internal readonly TextMeshProLayerMaterialContext MaterialContext;

        internal TextMeshProLayerMaterialCacheKey(
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
            unchecked
            {
                var hashCode = Preset != null ? Preset.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ PresetVersion;
                hashCode = (hashCode * 397) ^ LayerIndex;
                hashCode = (hashCode * 397) ^ MaterialContext.GetHashCode();
                return hashCode;
            }
        }
    }

    internal static class TextMeshProLayerMaterialCache
    {
        private static readonly Dictionary<TextMeshProLayerMaterialCacheKey, Entry> Entries = new Dictionary<TextMeshProLayerMaterialCacheKey, Entry>();

        internal static Entry Acquire(TextMeshProLayerMaterialCacheKey key, TextMeshProLayerData layer, TextMeshProLayerMaterialContext context)
        {
            if (Entries.TryGetValue(key, out var entry)) {
                if (entry.Material != null) {
                    entry.ReferenceCount++;
                    return entry;
                }

                Entries.Remove(key);
            }

            var material = TextMeshProLayerMaterial.CreateMaterial(TextMeshProLayerMaterial.ResolveCoreShader(), layer.MaterialName);
            material.name = GetSharedMaterialName(key, layer);
            layer.ApplyMaterial(material, context);

            entry = new Entry(key, material);
            Entries.Add(key, entry);
            return entry;
        }

        internal static void Release(Entry entry)
        {
            if (entry == null) {
                return;
            }

            entry.ReferenceCount--;
            if (entry.ReferenceCount > 0) {
                return;
            }

            Entries.Remove(entry.Key);
            CoreUtils.Destroy(entry.Material);
        }

        internal static void ClearForTests()
        {
            foreach (var entry in Entries.Values) {
                CoreUtils.Destroy(entry.Material);
            }

            Entries.Clear();
        }

        private static string GetSharedMaterialName(TextMeshProLayerMaterialCacheKey key, TextMeshProLayerData layer)
        {
            var presetName = key.Preset != null ? key.Preset.name : "Preset";
            return presetName + " (" + layer.MaterialName + " Shared)";
        }

        internal sealed class Entry
        {
            internal readonly TextMeshProLayerMaterialCacheKey Key;
            internal readonly Material Material;
            internal int ReferenceCount;

            internal Entry(TextMeshProLayerMaterialCacheKey key, Material material)
            {
                Key = key;
                Material = material;
                ReferenceCount = 1;
            }
        }
    }
}
