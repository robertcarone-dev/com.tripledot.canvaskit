using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Tripledot.CanvasKit.TextMeshPro
{
    [Flags]
    public enum TextMeshProLayerChange
    {
        None = 0,
        Material = 1 << 0,
        Geometry = 1 << 1
    }

    public sealed class TextMeshProLayerPreset : ScriptableObject
    {
        [SerializeField]
        private TMP_FontAsset fontAsset;
        [SerializeField]
        private List<TextMeshProLayerData> layers = new List<TextMeshProLayerData>();

        [NonSerialized]
        private int version;

        public static event Action<TextMeshProLayerPreset, TextMeshProLayerChange> Changed;

        public TMP_FontAsset FontAsset => fontAsset;
        public IReadOnlyList<TextMeshProLayerData> Layers => layers;

        internal int LayerCount => layers.Count;
        internal int Version => version;

        internal TextMeshProLayerData GetLayer(int index)
        {
            return layers[index];
        }

        internal void CopyFrom(IList<TextMeshProLayerData> sourceLayers)
        {
            CopyFrom(sourceLayers, fontAsset);
        }

        internal void CopyFrom(IList<TextMeshProLayerData> sourceLayers, TMP_FontAsset sourceFontAsset)
        {
            layers.Clear();
            fontAsset = sourceFontAsset;

            foreach (var data in sourceLayers) {
                layers.Add(data.Clone());
            }

            NotifyChanged();
        }

        internal void AddLayer(TextMeshProLayerData layer)
        {
            layers.Add(layer);
            NotifyChanged();
        }

        internal void CopyLayersTo(IList<TextMeshProLayerData> destination)
        {
            destination.Clear();
            foreach (var layer in layers) {
                destination.Add(layer.Clone());
            }
        }

        internal void SetFontAsset(TMP_FontAsset value)
        {
            if (fontAsset == value) {
                return;
            }

            fontAsset = value;
            NotifyChanged();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            NotifyChanged();
#endif
        }

        internal void NotifyChanged(TextMeshProLayerChange change = TextMeshProLayerChange.Geometry)
        {
            if (change == TextMeshProLayerChange.None) {
                return;
            }

            unchecked {
                version++;
            }

            Changed?.Invoke(this, change);
        }

#if UNITY_EDITOR
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
