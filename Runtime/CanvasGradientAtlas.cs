using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tripledot.CanvasKit
{
    internal static class CanvasGradientAtlas
    {
        private const int InitialHeight = 16;

        private static readonly Dictionary<int, EntryData> Entries = new Dictionary<int, EntryData>();
        private static readonly List<EntryData> EntryList = new List<EntryData>();
        private static readonly Dictionary<GradientSnapshot, int> GradientIds = new Dictionary<GradientSnapshot, int>();
        private static readonly ConditionalWeakTable<Gradient, CachedGradientId> ObjectGradientIds = new ConditionalWeakTable<Gradient, CachedGradientId>();

        private static RenderTexture _atlasTexture;
        private static Texture2D _rowTexture;
        private static Color32[] _rowPixels;
        private static int _atlasWidth;
        private static int _atlasHeight;
        private static int _nextRow;
        private static int _nextGradientId;

        private sealed class CachedGradientId
        {
            public int Id;
        }

        private sealed class GradientSnapshot : IEquatable<GradientSnapshot>
        {
            private readonly GradientColorKey[] colorKeys;
            private readonly GradientAlphaKey[] alphaKeys;
            private readonly int hashCode;

            public readonly GradientMode Mode;
            public readonly ColorSpace ColorSpace;

            public GradientSnapshot(Gradient gradient)
            {
                Mode = gradient.mode;
                ColorSpace = gradient.colorSpace;
                colorKeys = gradient.colorKeys;
                alphaKeys = gradient.alphaKeys;
                hashCode = ComputeHashCode();
            }

            public Gradient CreateGradient()
            {
                var gradient = new Gradient {
                    mode = Mode
                };
                gradient.SetKeys(colorKeys, alphaKeys);
                return gradient;
            }

            public bool Equals(GradientSnapshot other)
            {
                if (other == null) {
                    return false;
                }

                if (hashCode != other.hashCode || Mode != other.Mode || ColorSpace != other.ColorSpace) {
                    return false;
                }

                return ColorKeysEqual(colorKeys, other.colorKeys) && 
                       AlphaKeysEqual(alphaKeys, other.alphaKeys);
            }

            public override bool Equals(object obj)
            {
                return obj is GradientSnapshot other && Equals(other);
            }

            public override int GetHashCode()
            {
                return hashCode;
            }

            private int ComputeHashCode()
            {
                var hash = new HashCode();
                hash.Add(Mode);
                hash.Add(ColorSpace);
                for (int i = 0; i < colorKeys.Length; i++) {
                    hash.Add(colorKeys[i].color);
                    hash.Add(colorKeys[i].time);
                }

                for (int i = 0; i < alphaKeys.Length; i++) {
                    hash.Add(alphaKeys[i].alpha);
                    hash.Add(alphaKeys[i].time);
                }

                return hash.ToHashCode();
            }

            private static bool ColorKeysEqual(GradientColorKey[] lhs, GradientColorKey[] rhs)
            {
                if (lhs.Length != rhs.Length) {
                    return false;
                }

                for (int i = 0; i < lhs.Length; i++) {
                    if (lhs[i].color != rhs[i].color || lhs[i].time != rhs[i].time) {
                        return false;
                    }
                }

                return true;
            }

            private static bool AlphaKeysEqual(GradientAlphaKey[] lhs, GradientAlphaKey[] rhs)
            {
                if (lhs.Length != rhs.Length) {
                    return false;
                }

                for (int i = 0; i < lhs.Length; i++) {
                    if (lhs[i].alpha != rhs[i].alpha || lhs[i].time != rhs[i].time) {
                        return false;
                    }
                }

                return true;
            }
        }

        internal readonly struct Entry
        {
            public readonly Texture Texture;
            public readonly Vector4 Rect;

            public Entry(Texture texture, Vector4 rect)
            {
                Texture = texture;
                Rect = rect;
            }
        }

        private sealed class EntryData
        {
            public Gradient Gradient;
            public int Row;
        }

        internal static bool TryGetEntry(CanvasPaint paint, out Entry entry)
        {
            if (!paint.HasFullGradient) {
                entry = default;
                return false;
            }

            EnsureWidth(CanvasKitSettings.CurrentGradientAtlasWidth);

            var gradientId = GetGradientId(paint.Gradient, out var snapshot);
            if (!Entries.TryGetValue(gradientId, out var data)) {
                snapshot ??= new GradientSnapshot(paint.Gradient);
                data = new EntryData {
                    Gradient = snapshot.CreateGradient(),
                    Row = _nextRow++
                };
                Entries[gradientId] = data;
                EntryList.Add(data);
                EnsureHeight(data.Row + 1);
                UploadRow(data);
            }

            var rect = new Vector4(0.5f / Mathf.Max(1, _atlasWidth), data.Row, Mathf.Max(0, _atlasWidth - 1) / (float)Mathf.Max(1, _atlasWidth), 1f);
            entry = new Entry(_atlasTexture, rect);
            return true;
        }

        private static void EnsureWidth(int width)
        {
            width = CanvasKitSettings.ClampGradientAtlasWidth(width);
            if (_atlasTexture != null && _atlasWidth == width) {
                return;
            }

            _atlasWidth = width;
            _atlasHeight = 0;
            
            CoreUtils.Destroy(_atlasTexture);
            CoreUtils.Destroy(_rowTexture);
            
            _atlasTexture = null;
            _rowTexture = null;
            _rowPixels = null;
            
            EnsureHeight(Mathf.Max(1, _nextRow));
        }

        private static void EnsureHeight(int requiredRows)
        {
            var requiredHeight = Mathf.NextPowerOfTwo(Mathf.Max(InitialHeight, requiredRows));
            if (_atlasTexture != null && _atlasHeight >= requiredHeight) {
                return;
            }

            _atlasHeight = requiredHeight;
            if (_atlasTexture == null) {
                _atlasTexture = new RenderTexture(_atlasWidth, _atlasHeight, 0, RenderTextureFormat.ARGB32) {
                    name = "Style Gradient Atlas",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                    autoGenerateMips = false
                };
            } else {
                _atlasTexture.Release();
                _atlasTexture.width = _atlasWidth;
                _atlasTexture.height = _atlasHeight;
            }

            _atlasTexture.Create();
            
            EnsureRowTexture();
            for (int i = 0; i < EntryList.Count; i++) {
                UploadRow(EntryList[i]);
            }
        }

        private static void EnsureRowTexture()
        {
            if (_rowTexture != null && _rowTexture.width == _atlasWidth) {
                return;
            }

            CoreUtils.Destroy(_rowTexture);
            _rowTexture = new Texture2D(_atlasWidth, 1, TextureFormat.ARGB32, false) {
                name = "Style Gradient Atlas Row",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _rowPixels = new Color32[_atlasWidth];
        }

        private static void UploadRow(EntryData data)
        {
            EnsureRowTexture();
            var max = Mathf.Max(1, _atlasWidth - 1);
            for (int i = 0; i < _atlasWidth; i++) {
                var color = data.Gradient.Evaluate(i / (float)max);
                _rowPixels[i] = color;
            }

            _rowTexture.SetPixels32(_rowPixels);
            _rowTexture.Apply(false, false);
            Graphics.CopyTexture(_rowTexture, 0, 0, 0, 0, _atlasWidth, 1, _atlasTexture, 0, 0, 0, data.Row);
        }

        private static int GetGradientId(Gradient gradient, out GradientSnapshot snapshot)
        {
            if (ObjectGradientIds.TryGetValue(gradient, out var cached)) {
                snapshot = null;
                return cached.Id;
            }

            snapshot = new GradientSnapshot(gradient);
            
            if (!GradientIds.TryGetValue(snapshot, out var id)) {
                id = _nextGradientId++;
                GradientIds[snapshot] = id;
            }

            ObjectGradientIds.Add(gradient, new CachedGradientId {
                Id = id
            });

            return id;
        }
    }
}
