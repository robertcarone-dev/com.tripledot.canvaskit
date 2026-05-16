using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tripledot.CanvasKit
{
    internal static class CanvasGradientAtlas
    {
        private const int InitialHeight = 16;

        private static readonly Dictionary<Gradient, EntryData> Entries = new Dictionary<Gradient, EntryData>(ReferenceComparer<Gradient>.Instance);
        private static readonly List<EntryData> EntryList = new List<EntryData>();
        private static readonly List<int> FreeRows = new List<int>();

        private static RenderTexture _atlasTexture;
        private static Texture2D _rowTexture;
        private static Color32[] _rowPixels;
        private static int _atlasWidth;
        private static int _atlasHeight;
        private static int _nextRow;

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

        internal sealed class Lease
        {
            internal EntryData Data;
        }

        internal sealed class EntryData
        {
            public Gradient Gradient;
            public int Row;
            public int ReferenceCount;
        }

        internal static bool TryGetEntry(CanvasPaint paint, bool resourcesEnabled, Lease lease, out Entry entry)
        {
            if (!resourcesEnabled || !paint.HasFullGradient) {
                Release(lease);
                entry = default;
                return false;
            }

            EnsureWidth(CanvasKitSettings.CurrentGradientAtlasWidth);

            var data = Acquire(paint.Gradient, lease);
            UploadRow(data);

            var rect = new Vector4(0.5f / Mathf.Max(1, _atlasWidth), data.Row, Mathf.Max(0, _atlasWidth - 1) / (float)Mathf.Max(1, _atlasWidth), 1f);
            entry = new Entry(_atlasTexture, rect);
            return true;
        }

        internal static void Release(Lease lease)
        {
            if (lease?.Data == null) {
                return;
            }

            var data = lease.Data;
            lease.Data = null;
            data.ReferenceCount--;
            if (data.ReferenceCount > 0) {
                return;
            }

            Entries.Remove(data.Gradient);
            EntryList[data.Row] = null;
            FreeRows.Add(data.Row);

            if (Entries.Count == 0) {
                ReleaseTextures();
                EntryList.Clear();
                FreeRows.Clear();
                _nextRow = 0;
            }
        }

        private static EntryData Acquire(Gradient gradient, Lease lease)
        {
            if (lease.Data != null && ReferenceEquals(lease.Data.Gradient, gradient)) {
                return lease.Data;
            }

            Release(lease);

            if (!Entries.TryGetValue(gradient, out var data)) {
                data = new EntryData {
                    Gradient = gradient,
                    Row = AllocateRow()
                };
                EntryList[data.Row] = data;
                Entries.Add(gradient, data);
                EnsureHeight(data.Row + 1);
            }

            data.ReferenceCount++;
            lease.Data = data;
            return data;
        }

        private static int AllocateRow()
        {
            if (FreeRows.Count > 0) {
                var index = FreeRows.Count - 1;
                var row = FreeRows[index];
                FreeRows.RemoveAt(index);
                return row;
            }

            var next = _nextRow++;
            while (EntryList.Count <= next) {
                EntryList.Add(null);
            }

            return next;
        }

        private static void EnsureWidth(int width)
        {
            width = CanvasKitSettings.ClampGradientAtlasWidth(width);
            if (_atlasTexture != null && _atlasWidth == width) {
                return;
            }

            _atlasWidth = width;
            _atlasHeight = 0;
            ReleaseTextures();
            
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
                if (EntryList[i] != null) {
                    UploadRow(EntryList[i]);
                }
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

        private static void ReleaseTextures()
        {
            CoreUtils.Destroy(_atlasTexture);
            CoreUtils.Destroy(_rowTexture);
            _atlasTexture = null;
            _rowTexture = null;
            _rowPixels = null;
            _atlasHeight = 0;
        }

        private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();

            public bool Equals(T x, T y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(T obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
