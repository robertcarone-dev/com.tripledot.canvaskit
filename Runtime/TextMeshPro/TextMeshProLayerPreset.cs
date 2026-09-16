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

    public sealed class TextMeshProLayerPreset : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField]
        private TMP_FontAsset fontAsset;
        [SerializeField]
        private List<TextMeshProLayerData> layers = new List<TextMeshProLayerData>();

        [NonSerialized]
        private int version;

        public static event Action<TextMeshProLayerPreset, TextMeshProLayerChange> Changed;

        public TMP_FontAsset FontAsset => fontAsset;
        // Snapshots prevent callers from mutating a shared preset without invalidation.
        public IReadOnlyList<TextMeshProLayerData> Layers {
            get {
                var result = new TextMeshProLayerData[layers.Count];
                for (var i = 0; i < result.Length; i++) {
                    result[i] = layers[i].Clone();
                }
                return result;
            }
        }

        public int LayerCount => layers.Count;
        internal int Version => version;

        public TextMeshProLayerData GetLayerCopy(int index)
        {
            if (index < 0 || index >= layers.Count) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return layers[index].Clone();
        }

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
                var copy = data.Clone();
                copy.SetId(Guid.NewGuid().ToString("N"));
                layers.Add(copy);
            }

            NotifyChanged();
        }

        internal void AddLayer(TextMeshProLayerData layer)
        {
            var copy = layer.Clone();
            copy.SetId(Guid.NewGuid().ToString("N"));
            layers.Add(copy);
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

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            EnsureLayerIds();
        }

        private void EnsureLayerIds()
        {
            for (var i = 0; i < layers.Count; i++) {
                if (string.IsNullOrEmpty(layers[i].Id)) {
                    layers[i].SetId("legacy:" + i);
                }
                for (var j = 0; j < i; j++) {
                    if (layers[j].Id == layers[i].Id) {
                        layers[i].SetId(Guid.NewGuid().ToString("N"));
                        break;
                    }
                }
            }
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            EnsureLayerIds();
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
