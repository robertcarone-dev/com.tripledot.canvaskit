using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Tripledot.CanvasKit
{
    public sealed class TextMeshProLayerPreset : ScriptableObject
    {
        [SerializeField]
        private TMP_FontAsset fontAsset;
        [SerializeField]
        private List<TextMeshProLayerData> layers = new List<TextMeshProLayerData>();

        [NonSerialized]
        private readonly List<int> layerVersions = new List<int>();
        [NonSerialized]
        private int version;
#if UNITY_EDITOR
        [NonSerialized]
        private int suppressOnValidateNotifications;
#endif

        public static event Action<TextMeshProLayerPreset> Changed;
        internal static event Action<TextMeshProLayerPreset, TextMeshProLayerStack.DirtyFlags, int> ChangedWithDirtyFlags;

        public TMP_FontAsset FontAsset => fontAsset;
        public IReadOnlyList<TextMeshProLayerData> Layers => layers;

        internal List<TextMeshProLayerData> MutableLayers => layers;
        internal int LayerCount => layers.Count;
        internal int Version => version;

        internal int GetLayerVersion(int index)
        {
            EnsureLayerVersionSlots();
            return index >= 0 && index < layerVersions.Count ? layerVersions[index] : version;
        }

        internal TextMeshProLayerData GetLayer(int index)
        {
            return index >= 0 && index < layers.Count ? layers[index] : null;
        }

        internal void CopyFrom(IList<TextMeshProLayerData> sourceLayers)
        {
            CopyFrom(sourceLayers, fontAsset);
        }

        internal void CopyFrom(IList<TextMeshProLayerData> sourceLayers, TMP_FontAsset sourceFontAsset)
        {
            layers.Clear();
            fontAsset = sourceFontAsset;

            if (sourceLayers == null) {
                NotifyChanged();
                return;
            }

            foreach (var data in sourceLayers) {
                layers.Add(data?.Clone());
            }

            NotifyChanged();
        }

        internal void SetFontAsset(TMP_FontAsset value)
        {
            if (fontAsset == value) {
                return;
            }

            fontAsset = value;
            NotifyChanged(TextMeshProLayerStack.MaterialDirtyFlags);
        }

        private void OnValidate()
        {
            EnsureLayerVersionSlots();
#if UNITY_EDITOR
            if (suppressOnValidateNotifications > 0) {
                return;
            }

            NotifyChanged();
#endif
        }

        internal void NotifyChanged(TextMeshProLayerStack.DirtyFlags flags = TextMeshProLayerStack.CompositionDirtyFlags)
        {
            NotifyChanged(flags, -1);
        }

        internal void NotifyChanged(TextMeshProLayerStack.DirtyFlags flags, int layerIndex)
        {
            if (flags == TextMeshProLayerStack.DirtyFlags.None) {
                return;
            }

            unchecked {
                version++;
            }

            IncrementLayerVersion(flags, layerIndex);

            Changed?.Invoke(this);
            ChangedWithDirtyFlags?.Invoke(this, flags, layerIndex);
        }

        private void IncrementLayerVersion(TextMeshProLayerStack.DirtyFlags flags, int layerIndex)
        {
            EnsureLayerVersionSlots();

            if ((flags & TextMeshProLayerStack.DirtyFlags.Layers) == 0 && layerIndex >= 0 && layerIndex < layerVersions.Count) {
                unchecked {
                    layerVersions[layerIndex]++;
                }

                return;
            }

            for (var i = 0; i < layerVersions.Count; i++) {
                unchecked {
                    layerVersions[i]++;
                }
            }
        }

        private void EnsureLayerVersionSlots()
        {
            while (layerVersions.Count < layers.Count) {
                layerVersions.Add(version);
            }

            if (layerVersions.Count > layers.Count) {
                layerVersions.RemoveRange(layers.Count, layerVersions.Count - layers.Count);
            }
        }

#if UNITY_EDITOR
        internal void BeginSuppressingOnValidateNotifications()
        {
            suppressOnValidateNotifications++;
        }

        internal void EndSuppressingOnValidateNotifications()
        {
            suppressOnValidateNotifications = Mathf.Max(0, suppressOnValidateNotifications - 1);
        }

        internal const string DefaultPreviewText = "AaBbYy 123";

        [SerializeField]
        private string previewText = string.Empty;

        internal string PreviewText {
            get => previewText;
            set => previewText = value ?? string.Empty;
        }

        internal string GetPreviewText()
        {
            return string.IsNullOrWhiteSpace(previewText) ? DefaultPreviewText : previewText;
        }

#endif
    }
}