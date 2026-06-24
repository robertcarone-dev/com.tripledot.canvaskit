using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tripledot.CanvasKit.TextMeshPro
{
    internal static class TextMeshProLayerMaterialCache
    {
        public sealed class Entry
        {
            public readonly TextMeshProLayerMaterialCacheKey Key;
            public readonly Material Material;
            public readonly TextMeshProLayerMaterialGradientState GradientState;
            public int ReferenceCount;

            public Entry(TextMeshProLayerMaterialCacheKey key, Material material)
            {
                Key = key;
                Material = material;
                GradientState = new TextMeshProLayerMaterialGradientState();
                ReferenceCount = 1;
            }
        }
        
        private static readonly Dictionary<TextMeshProLayerMaterialCacheKey, Entry> Entries = new Dictionary<TextMeshProLayerMaterialCacheKey, Entry>();

        public static Entry Acquire(TextMeshProLayerMaterialCacheKey key, TextMeshProLayerData layer, TextMeshProLayerMaterialContext context)
        {
            if (Entries.TryGetValue(key, out var entry)) {
                if (entry.Material != null) {
                    entry.ReferenceCount++;
                    return entry;
                }

                entry.GradientState.Release();
                Entries.Remove(key);
            }

            var material = TextMeshProLayerMaterial.CreateRuntimeMaterial();
            material.name = (key.Preset != null ? key.Preset.name : "Preset") + " (TextLayerCore Shared)";

            entry = new Entry(key, material);
            TextMeshProLayerMaterial.ApplyLayer(material, layer, context, entry.GradientState);
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
    }
}
