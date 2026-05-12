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
        private int version;

        public static event Action<TextMeshProLayerPreset> Changed;

        public TMP_FontAsset FontAsset => fontAsset;
        public IReadOnlyList<TextMeshProLayerData> Layers => layers;

        internal List<TextMeshProLayerData> MutableLayers => layers;
        internal int LayerCount => layers.Count;
        internal int Version => version;

        internal TextMeshProLayerData GetLayer(int index)
        {
            return index >= 0 && index < layers.Count ? layers[index] : null;
        }

        internal void CopyFrom(IList<TextMeshProLayerData> source)
        {
            CopyFrom(source, fontAsset);
        }

        internal void CopyFrom(IList<TextMeshProLayerData> source, TMP_FontAsset sourceFontAsset)
        {
            layers.Clear();
            fontAsset = sourceFontAsset;
            if (source == null) {
                NotifyChanged();
                return;
            }

            for (int i = 0; i < source.Count; i++) {
                layers.Add(source[i]?.Clone());
            }

            NotifyChanged();
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
            NotifyChanged();
        }

        internal void NotifyChanged()
        {
            unchecked {
                version++;
            }

            Changed?.Invoke(this);
        }

#if UNITY_EDITOR
        internal const string DefaultPreviewText = "AaBbYy 123";
        
        [SerializeField]
        private string previewText = string.Empty;
        
        internal string PreviewText
        {
            get => previewText;
            set => previewText = value ?? string.Empty;
        }

        internal string GetPreviewText()
        {
            return string.IsNullOrWhiteSpace(previewText) ? DefaultPreviewText : previewText;
        }

        internal void SetPreviewTextForTests(string value)
        {
            PreviewText = value;
        }
#endif
    }
}
