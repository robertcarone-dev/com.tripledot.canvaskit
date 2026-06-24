using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Tripledot.CanvasKit.Editor.CanvasPreview
{
    internal enum CanvasPreviewTargetKind
    {
        None,
        Canvas,
        RectTransform
    }

    internal readonly struct CanvasPreviewTarget
    {
        public static readonly CanvasPreviewTarget Empty = default;

        public readonly CanvasPreviewTargetKind Kind;
        public readonly GameObject PrefabRoot;
        public readonly Canvas Canvas;
        public readonly RectTransform RectTransform;

        public CanvasPreviewTarget(CanvasPreviewTargetKind kind, GameObject prefabRoot, Canvas canvas, RectTransform rectTransform)
        {
            Kind = kind;
            PrefabRoot = prefabRoot;
            Canvas = canvas;
            RectTransform = rectTransform;
        }
    }
    
    internal static class CanvasPreviewWindow
    {
        private static readonly GUIContent PreviewTitle = new GUIContent("Canvas Preview");
        private static readonly string[] SizeLabels = CreateSizeLabels();
        private static readonly Dictionary<string, PreviewState> StateCache = new Dictionary<string, PreviewState>();
        private static readonly StaticPreviewCache ProjectPreviewCache = new StaticPreviewCache(32);

        static CanvasPreviewWindow()
        {
            EditorApplication.projectChanged += ClearPreviewCache;
        }

        public static bool CanPreview(UnityObject target)
        {
            return TryGetState(target, out _);
        }

        public static bool CanPreview(UnityObject[] targets)
        {
            foreach (var target in targets) {
                if (!CanPreview(target)) {
                    return false;
                }
            }

            return true;
        }

        public static bool TryGetPreviewTarget(UnityObject target, UnityObject[] targets, out GameObject prefab)
        {
            prefab = null;
            
            if (targets is { Length: > 1 }) {
                if (!CanPreview(targets)) {
                    return false;
                }

                if (target is GameObject activeGameObject && CanPreview(activeGameObject)) {
                    prefab = activeGameObject;
                    return true;
                }

                prefab = targets[0] as GameObject;
                return prefab != null;
            }

            if (target is GameObject gameObject && CanPreview(gameObject)) {
                prefab = gameObject;
                return true;
            }

            return false;
        }

        public static bool UsesScreenSpacePreset(UnityObject target)
        {
            return TryGetState(target, out var state) && CanvasPreviewRoleResolver.UsesPreset(state.Role);
        }

        public static bool HasPreviewGUI(UnityObject target, UnityObject[] targets, bool baseHasPreview)
        {
            return baseHasPreview || CanPreview(targets) || CanPreview(target);
        }

        public static GUIContent GetPreviewTitle(UnityObject target, UnityObject[] targets, GUIContent baseTitle)
        {
            return TryGetPreviewTarget(target, targets, out _) ? PreviewTitle : baseTitle;
        }

        public static string GetInfoString(UnityObject target, UnityObject[] targets, int selectedSizeIndex, string baseInfoString)
        {
            if (!TryGetPreviewTarget(target, targets, out var prefab) || !TryGetState(prefab, out var state)) {
                return baseInfoString;
            }

            var roleName = CanvasPreviewRoleResolver.GetDisplayName(state.Role);
            return CanvasPreviewRoleResolver.UsesPreset(state.Role)
                ? "Canvas Preview - " + roleName + " - " + GetSize(selectedSizeIndex).Label
                : "Canvas Preview - " + roleName;
        }

        public static Texture2D RenderStaticPreview(UnityObject target, int width, int height)
        {
            if (target is not GameObject prefabAsset || !CanPreview(prefabAsset)) {
                return null;
            }

            var previewWidth = Mathf.Max(1, width);
            var previewHeight = Mathf.Max(1, height);
            var key = StaticPreviewKey.Create(prefabAsset, previewWidth, previewHeight);
            if (ProjectPreviewCache.TryGet(key, out var cachedTexture)) {
                return cachedTexture;
            }

            var result = CanvasPreviewRenderer.RenderPreviewTexture(prefabAsset, CanvasPreviewSize.Default, previewWidth, previewHeight, true);
            if (result.Texture != null) {
                result.Texture.hideFlags = HideFlags.HideAndDontSave;
                ProjectPreviewCache.Add(key, result.Texture);
            }

            return result.Texture;
        }

        public static void OnPreviewSettings(UnityObject target, UnityObject[] targets, ref int selectedSizeIndex, Action releasePreview)
        {
            if (!TryGetPreviewTarget(target, targets, out var prefab)
                || !TryGetState(prefab, out var state)
                || !CanvasPreviewRoleResolver.UsesPreset(state.Role)) {
                return;
            }

            var nextIndex = EditorGUILayout.Popup(
                selectedIndex: NormalizeSizeIndex(selectedSizeIndex),
                displayedOptions: SizeLabels,
                style: EditorStyles.toolbarPopup,
                options: GUILayout.Width(96f));

            if (nextIndex != selectedSizeIndex) {
                selectedSizeIndex = nextIndex;
                CanvasPreviewSettings.SaveSelectedReferenceSizeIndex(nextIndex);
                releasePreview?.Invoke();
            }
        }

        public static bool OnPreviewGUI(
            UnityObject target,
            UnityObject[] targets,
            Rect rect,
            GUIStyle background,
            CanvasPreviewCache cache,
            int selectedSizeIndex)
        {
            if (!TryGetPreviewTarget(target, targets, out var prefabAsset)) {
                return false;
            }

            if (Event.current == null || Event.current.type != EventType.Repaint) {
                return true;
            }

            background?.Draw(rect, false, false, false, false);

            var width = Mathf.CeilToInt(rect.width);
            var height = Mathf.CeilToInt(rect.height);
            var previewTexture = cache.EnsurePreviewTexture(prefabAsset, GetSize(selectedSizeIndex), selectedSizeIndex, width, height);
            if (previewTexture != null) {
                GUI.DrawTexture(rect, previewTexture, ScaleMode.ScaleToFit, false);
            }

            return true;
        }

        public static void ClearPreviewCache()
        {
            StateCache.Clear();
            ProjectPreviewCache.Clear();
        }

        private static bool TryGetState(UnityObject target, out PreviewState state)
        {
            state = PreviewState.Empty;
            if (target is not GameObject gameObject) {
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(gameObject);
            if (string.IsNullOrEmpty(assetPath)) {
                return false;
            }

            var settingsRevision = CanvasPreviewSettings.Revision;
            if (StateCache.TryGetValue(assetPath, out state) && state.SettingsRevision == settingsRevision) {
                return state.CanPreview;
            }

            if (!CanvasPreviewEligibility.TryGetPreviewTarget(gameObject, out var targetInfo)) {
                state = new PreviewState(false, CanvasPreviewRole.Element, CanvasPreviewTargetKind.None, RenderMode.ScreenSpaceCamera, settingsRevision);
                StateCache[assetPath] = state;
                return false;
            }

            var sourceRenderMode = targetInfo.Canvas != null ? targetInfo.Canvas.renderMode : RenderMode.ScreenSpaceCamera;
            state = new PreviewState(
                canPreview: true,
                role: CanvasPreviewRoleResolver.Resolve(gameObject, targetInfo),
                targetKind: targetInfo.Kind,
                sourceRenderMode: sourceRenderMode,
                settingsRevision: settingsRevision);
            StateCache[assetPath] = state;
            return true;
        }

        private static CanvasPreviewSize GetSize(int selectedSizeIndex)
        {
            return CanvasPreviewSize.StandardSizes[NormalizeSizeIndex(selectedSizeIndex)];
        }

        private static int NormalizeSizeIndex(int selectedSizeIndex)
        {
            return selectedSizeIndex >= 0 && selectedSizeIndex < CanvasPreviewSize.StandardSizes.Length
                ? selectedSizeIndex
                : CanvasPreviewSize.DefaultIndex;
        }

        private static string[] CreateSizeLabels()
        {
            var sizes = CanvasPreviewSize.StandardSizes;
            var labels = new string[sizes.Length];
            for (var i = 0; i < sizes.Length; i++) {
                labels[i] = sizes[i].Label;
            }

            return labels;
        }

        private readonly struct PreviewState
        {
            public static readonly PreviewState Empty = new PreviewState(
                false,
                CanvasPreviewRole.Element,
                CanvasPreviewTargetKind.None,
                RenderMode.WorldSpace,
                0);

            public readonly bool CanPreview;
            public readonly CanvasPreviewRole Role;
            public readonly CanvasPreviewTargetKind TargetKind;
            public readonly RenderMode SourceRenderMode;
            public readonly int SettingsRevision;

            public PreviewState(bool canPreview, CanvasPreviewRole role, CanvasPreviewTargetKind targetKind, RenderMode sourceRenderMode, int settingsRevision)
            {
                CanPreview = canPreview;
                Role = role;
                TargetKind = targetKind;
                SourceRenderMode = sourceRenderMode;
                SettingsRevision = settingsRevision;
            }
        }

        private readonly struct StaticPreviewKey : IEquatable<StaticPreviewKey>
        {
            private readonly string assetPath;
            private readonly Hash128 assetHash;
            private readonly int width;
            private readonly int height;
            private readonly int settingsRevision;
            private readonly CanvasPreviewEnvironmentKey environmentKey;

            private StaticPreviewKey(
                string assetPath, Hash128 assetHash, int width, int height, int settingsRevision, CanvasPreviewEnvironmentKey environmentKey)
            {
                this.assetPath = assetPath;
                this.assetHash = assetHash;
                this.width = width;
                this.height = height;
                this.settingsRevision = settingsRevision;
                this.environmentKey = environmentKey;
            }

            public static StaticPreviewKey Create(GameObject prefabAsset, int width, int height)
            {
                var assetPath = AssetDatabase.GetAssetPath(prefabAsset);
                var assetHash = !string.IsNullOrEmpty(assetPath) ? AssetDatabase.GetAssetDependencyHash(assetPath) : default;
                return new StaticPreviewKey(assetPath, assetHash, width, height, CanvasPreviewSettings.Revision, CanvasPreviewEnvironment.CreateCacheKey());
            }

            public bool Equals(StaticPreviewKey other)
            {
                return assetPath == other.assetPath
                       && assetHash == other.assetHash
                       && width == other.width
                       && height == other.height
                       && settingsRevision == other.settingsRevision
                       && environmentKey == other.environmentKey;
            }

            public override bool Equals(object obj)
            {
                return obj is StaticPreviewKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked {
                    var hashCode = assetPath != null ? assetPath.GetHashCode() : 0;
                    hashCode = (hashCode * 397) ^ assetHash.GetHashCode();
                    hashCode = (hashCode * 397) ^ width;
                    hashCode = (hashCode * 397) ^ height;
                    hashCode = (hashCode * 397) ^ settingsRevision;
                    hashCode = (hashCode * 397) ^ environmentKey.GetHashCode();
                    return hashCode;
                }
            }
        }

        private sealed class StaticPreviewCache
        {
            private readonly int capacity;
            private readonly Dictionary<StaticPreviewKey, LinkedListNode<Entry>> entries = new Dictionary<StaticPreviewKey, LinkedListNode<Entry>>();
            private readonly LinkedList<Entry> usage = new LinkedList<Entry>();

            public StaticPreviewCache(int capacity)
            {
                this.capacity = Mathf.Max(1, capacity);
            }

            public bool TryGet(StaticPreviewKey key, out Texture2D texture)
            {
                if (entries.TryGetValue(key, out var node) && node.Value.Texture != null) {
                    usage.Remove(node);
                    usage.AddFirst(node);
                    texture = node.Value.Texture;
                    return true;
                }

                texture = null;
                return false;
            }

            public void Add(StaticPreviewKey key, Texture2D texture)
            {
                if (texture == null) {
                    return;
                }

                if (entries.TryGetValue(key, out var existingNode)) {
                    DestroyTexture(existingNode.Value.Texture);
                    existingNode.Value = new Entry(key, texture);
                    usage.Remove(existingNode);
                    usage.AddFirst(existingNode);
                    return;
                }

                var node = new LinkedListNode<Entry>(new Entry(key, texture));
                entries[key] = node;
                usage.AddFirst(node);
                Trim();
            }

            public void Clear()
            {
                for (var node = usage.First; node != null; node = node.Next) {
                    DestroyTexture(node.Value.Texture);
                }

                usage.Clear();
                entries.Clear();
            }

            private void Trim()
            {
                while (usage.Count > capacity && usage.Last != null) {
                    var node = usage.Last;
                    usage.RemoveLast();
                    entries.Remove(node.Value.Key);
                    DestroyTexture(node.Value.Texture);
                }
            }

            private static void DestroyTexture(Texture2D texture)
            {
                if (texture != null) {
                    UnityObject.DestroyImmediate(texture);
                }
            }

            private struct Entry
            {
                internal readonly StaticPreviewKey Key;
                internal readonly Texture2D Texture;

                internal Entry(StaticPreviewKey key, Texture2D texture)
                {
                    Key = key;
                    Texture = texture;
                }
            }
        }
    }
}