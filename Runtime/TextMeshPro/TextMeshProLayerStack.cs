using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UI;

namespace Tripledot.CanvasKit.TextMeshPro
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    [AddComponentMenu("UI (Canvas)/TextMeshPro - Layer Stack", 11)]
    [MovedFrom("Tripledot.CanvasKit")]
    public sealed class TextMeshProLayerStack : UIBehaviour
    {
        [SerializeField]
        private TextMeshProLayerPreset preset;
        [SerializeField]
        private List<TextMeshProLayerData> localLayers = new List<TextMeshProLayerData>();
        [SerializeField]
        private List<LayerOverride> presetLayerOverrides = new List<LayerOverride>();

        private readonly List<ResolvedLayer> resolvedLayers = new List<ResolvedLayer>();
        private readonly List<TextMeshProLayerData> resolvedLayerData = new List<TextMeshProLayerData>();
        private readonly List<LayerRuntimeState> layerRuntimeStates = new List<LayerRuntimeState>();
        private readonly TextMeshProLayerMeshBuilder meshBuilder = new TextMeshProLayerMeshBuilder();

        private TextMeshProUGUI text;
        private Mesh mesh;
        private Vector4 paintBounds;
        private float appliedEffectPaddingBudget;
        private float appliedGeometryPaddingLimit;
        private TextMeshProLayerChange pendingChange = TextMeshProLayerChange.Geometry;
        private bool hasPaintBounds;
        private bool hasAssignedMesh;
        private bool rebuildQueued;
        private bool rebuilding;
        
        private const AdditionalCanvasShaderChannels RequiredCanvasChannels =
            AdditionalCanvasShaderChannels.TexCoord1 |
            AdditionalCanvasShaderChannels.TexCoord2;

        public TextMeshProLayerPreset Preset {
            get => preset;
            set {
                if (value != preset) {
                    preset = value;
                    NotifyChanged(TextMeshProLayerChange.Geometry);
                }
            }
        }

        public void SetLayerStackDirty()
        {
            NotifyChanged(TextMeshProLayerChange.Geometry);
        }

        internal void SetLayerStackDirty(TextMeshProLayerChange change)
        {
            NotifyChanged(change);
        }

        internal void SetLayerCompositionChanged()
        {
            NotifyChanged(TextMeshProLayerChange.Geometry);
        }

        internal void SetLayerMaterialChanged()
        {
            NotifyChanged(TextMeshProLayerChange.Material);
        }

        internal void NotifyChanged(TextMeshProLayerChange change)
        {
            pendingChange = CombineChanges(pendingChange, change);
            QueueRebuild();
        }

        internal bool TryGetCurrentPaintBounds(out Vector4 bounds)
        {
            bounds = paintBounds;
            return hasPaintBounds && paintBounds.z > 0f && paintBounds.w > 0f;
        }

        internal void ReplaceLocalLayers(IList<TextMeshProLayerData> layers)
        {
            preset = null;
            localLayers.Clear();
            foreach (var layer in layers) {
                localLayers.Add(layer.Clone());
            }

            NotifyChanged(TextMeshProLayerChange.Geometry);
        }

        internal void AddLocalLayer(TextMeshProLayerData layer)
        {
            localLayers.Add(layer);
            NotifyChanged(TextMeshProLayerChange.Geometry);
        }

        internal bool IsPresetLayerInstance(int index)
        {
            EnsurePresetOverrideSlots();
            return index >= 0 && index < presetLayerOverrides.Count && presetLayerOverrides[index].OverrideLayer;
        }

        internal void SetPresetLayerInstance(int index, bool instance)
        {
            EnsurePresetOverrideSlots();
            if (index < 0 || index >= presetLayerOverrides.Count) {
                return;
            }

            var layerOverride = presetLayerOverrides[index];
            if (layerOverride.OverrideLayer == instance) {
                return;
            }

            if (instance) {
                layerOverride.CopyFromPreset(preset.GetLayer(index));
                layerOverride.OverrideLayer = true;
            } else {
                layerOverride.OverrideLayer = false;
                layerOverride.EnsureLayerCopy(preset.GetLayer(index));
            }

            NotifyChanged(TextMeshProLayerChange.Geometry);
        }

        internal TextMeshProLayerData GetEffectivePresetLayer(int index)
        {
            if (preset == null || index < 0 || index >= preset.LayerCount) {
                return null;
            }

            EnsurePresetOverrideSlots();
            return presetLayerOverrides[index].OverrideLayer
                ? presetLayerOverrides[index].Layer
                : preset.GetLayer(index);
        }

        internal void CopyEffectivePresetLayersTo(IList<TextMeshProLayerData> destination)
        {
            destination.Clear();
            if (preset == null) {
                for (var i = 0; i < localLayers.Count; i++) {
                    destination.Add(localLayers[i].Clone());
                }

                return;
            }

            EnsurePresetOverrideSlots();
            for (var i = 0; i < preset.LayerCount; i++) {
                destination.Add(GetEffectivePresetLayer(i).Clone());
            }
        }

        internal void ClearPresetLayerInstances()
        {
            EnsurePresetOverrideSlots();
            for (var i = 0; i < presetLayerOverrides.Count; i++) {
                presetLayerOverrides[i].OverrideLayer = false;
                presetLayerOverrides[i].EnsureLayerCopy(preset.GetLayer(i));
            }

            NotifyChanged(TextMeshProLayerChange.Geometry);
        }

        internal Mesh CurrentMesh => mesh;

        protected override void OnEnable()
        {
            base.OnEnable();

            text = GetComponent<TextMeshProUGUI>();
            RegisterCallbacks();
            TextMeshProLayerPreset.Changed += OnPresetChanged;
            NotifyChanged(TextMeshProLayerChange.Geometry);
        }

        protected override void OnDisable()
        {
            CancelQueuedRebuild();
            UnregisterCallbacks();
            TextMeshProLayerPreset.Changed -= OnPresetChanged;
            RestoreDefaultRendering();
            ReleaseResources();
            pendingChange = TextMeshProLayerChange.Geometry;

            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            CancelQueuedRebuild();
            UnregisterCallbacks();
            TextMeshProLayerPreset.Changed -= OnPresetChanged;
            ReleaseResources();

            base.OnDestroy();
        }

        protected override void OnDidApplyAnimationProperties()
        {
            NotifyChanged(TextMeshProLayerChange.Material);
            base.OnDidApplyAnimationProperties();
        }

        protected override void OnTransformParentChanged()
        {
            NotifyChanged(TextMeshProLayerChange.Material);
            base.OnTransformParentChanged();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            NotifyChanged(TextMeshProLayerChange.Geometry);
            base.OnRectTransformDimensionsChange();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            NotifyChanged(TextMeshProLayerChange.Material);
            base.OnCanvasHierarchyChanged();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            EnsurePresetOverrideSlots();
            NotifyChanged(TextMeshProLayerChange.Geometry);
        }

        protected override void Reset()
        {
            base.Reset();

            if (preset == null && localLayers.Count == 0) {
                localLayers.Add(TextMeshProLayerData.Default());
            }

            NotifyChanged(TextMeshProLayerChange.Geometry);
        }
#endif

        private void RegisterCallbacks()
        {
            UnregisterCallbacks();
            text.RegisterDirtyVerticesCallback(OnTextVerticesDirty);
            text.RegisterDirtyMaterialCallback(OnTextMaterialDirty);
            text.RegisterDirtyLayoutCallback(OnTextLayoutDirty);
        }

        private void UnregisterCallbacks()
        {
            if (text == null) {
                return;
            }

            text.UnregisterDirtyVerticesCallback(OnTextVerticesDirty);
            text.UnregisterDirtyMaterialCallback(OnTextMaterialDirty);
            text.UnregisterDirtyLayoutCallback(OnTextLayoutDirty);
        }

        private void OnTextVerticesDirty()
        {
            NotifyChanged(TextMeshProLayerChange.Geometry);
        }

        private void OnTextMaterialDirty()
        {
            NotifyChanged(TextMeshProLayerChange.Material);
        }

        private void OnTextLayoutDirty()
        {
            NotifyChanged(TextMeshProLayerChange.Geometry);
        }

        private void OnPresetChanged(TextMeshProLayerPreset changedPreset, TextMeshProLayerChange change)
        {
            if (changedPreset == preset) {
                NotifyChanged(change);
            }
        }

        private void QueueRebuild()
        {
            if (!isActiveAndEnabled || text == null || !text.enabled || rebuilding || rebuildQueued) {
                return;
            }

            rebuildQueued = true;
            Canvas.willRenderCanvases += OnWillRenderCanvases;
        }

        private void CancelQueuedRebuild()
        {
            if (!rebuildQueued) {
                return;
            }

            rebuildQueued = false;
            Canvas.willRenderCanvases -= OnWillRenderCanvases;
        }

        private void OnWillRenderCanvases()
        {
            CancelQueuedRebuild();
            if (!isActiveAndEnabled || !text.enabled || rebuilding) {
                return;
            }

            Rebuild();
        }

        private void Rebuild()
        {
            var change = pendingChange;
            if (mesh == null || !hasAssignedMesh) {
                change = CombineChanges(change, TextMeshProLayerChange.Geometry);
            }

            pendingChange = TextMeshProLayerChange.None;
            rebuilding = true;
            try {
                if ((change & TextMeshProLayerChange.Geometry) != 0) {
                    RebuildGeometry();
                }

                if (resolvedLayers.Count == 0) {
                    ClearLayerRendering();
                    return;
                }

                if ((change & (TextMeshProLayerChange.Geometry | TextMeshProLayerChange.Material)) != 0) {
                    ApplyMaterials();
                }
            } finally {
                rebuilding = false;
            }

            if (pendingChange != TextMeshProLayerChange.None) {
                QueueRebuild();
            }
        }

        private void RebuildGeometry()
        {
            ResolveRenderableLayers();
            EnsureLayerRuntimeStates(resolvedLayers.Count);
            if (resolvedLayers.Count == 0) {
                return;
            }

            CanvasUtility.EnsureChannels(text.canvas, RequiredCanvasChannels);
            text.ForceMeshUpdate();
            RefreshLayerPaddingBudget();

            var targetMesh = GetMesh();
            meshBuilder.Build(targetMesh, text.textInfo, GetResolvedLayerData(), appliedGeometryPaddingLimit);
            RefreshPaintBounds(targetMesh);
        }

        private void ApplyMaterials()
        {
            var renderMaterial = text.materialForRendering;
            var sourceMaterial = text.fontSharedMaterial != null ? text.fontSharedMaterial : renderMaterial;
            var materialContext = TextMeshProLayerMaterialContext.Capture(text, sourceMaterial, renderMaterial, appliedEffectPaddingBudget);
            var canvasRenderer = text.canvasRenderer;

            canvasRenderer.cull = false;
            canvasRenderer.SetMesh(mesh);
            canvasRenderer.materialCount = resolvedLayers.Count;
            canvasRenderer.SetTexture(materialContext.FontAtlas);
            hasAssignedMesh = true;

            for (var materialSlot = 0; materialSlot < resolvedLayers.Count; materialSlot++) {
                var resolvedLayer = GetRenderLayerForSlot(materialSlot);
                var material = layerRuntimeStates[materialSlot].GetOrCreateMaterial(resolvedLayer, materialContext, name);
                canvasRenderer.SetMaterial(material, materialSlot);
            }
        }

        private void ResolveRenderableLayers()
        {
            resolvedLayers.Clear();
            if (preset == null) {
                for (var i = 0; i < localLayers.Count; i++) {
                    var layer = localLayers[i];
                    if (layer.Enabled) {
                        resolvedLayers.Add(ResolvedLayer.Unique(layer));
                    }
                }

                return;
            }

            EnsurePresetOverrideSlots();
            for (var i = 0; i < preset.LayerCount; i++) {
                var presetLayer = preset.GetLayer(i);
                var layerOverride = presetLayerOverrides[i];
                var layer = layerOverride.OverrideLayer ? layerOverride.Layer : presetLayer;
                if (layer.Enabled) {
                    resolvedLayers.Add(layerOverride.OverrideLayer
                        ? ResolvedLayer.Unique(layer)
                        : ResolvedLayer.Shared(layer, preset, i));
                }
            }
        }

        private IList<TextMeshProLayerData> GetResolvedLayerData()
        {
            resolvedLayerData.Clear();
            for (var i = 0; i < resolvedLayers.Count; i++) {
                resolvedLayerData.Add(resolvedLayers[i].Layer);
            }

            return resolvedLayerData;
        }

        private void EnsurePresetOverrideSlots()
        {
            var count = preset != null ? preset.LayerCount : 0;
            while (presetLayerOverrides.Count < count) {
                presetLayerOverrides.Add(new LayerOverride());
            }

            for (var i = 0; i < count; i++) {
                presetLayerOverrides[i] ??= new LayerOverride();
                presetLayerOverrides[i].EnsureLayerCopy(preset.GetLayer(i));
            }

            if (presetLayerOverrides.Count > count) {
                presetLayerOverrides.RemoveRange(count, presetLayerOverrides.Count - count);
            }
        }

        private void RefreshLayerPaddingBudget()
        {
            var requiredPadding = CalculateMaxSdfPadding();
            var availableSdfPadding = TextMeshProUtility.CalculateAvailablePadding(text);

            appliedEffectPaddingBudget = Mathf.Min(requiredPadding, availableSdfPadding);
            appliedGeometryPaddingLimit = TextMeshProUtility.GetGeometryPaddingLimit(appliedEffectPaddingBudget);
        }

        private float CalculateMaxSdfPadding()
        {
            var padding = 0f;
            for (var i = 0; i < resolvedLayers.Count; i++) {
                padding = Mathf.Max(padding, resolvedLayers[i].Layer.GetSdfPadding());
            }

            return padding;
        }

        private void EnsureLayerRuntimeStates(int count)
        {
            while (layerRuntimeStates.Count < count) {
                layerRuntimeStates.Add(new LayerRuntimeState());
            }

            for (var i = count; i < layerRuntimeStates.Count; i++) {
                layerRuntimeStates[i].ReleaseMaterial();
            }

            if (layerRuntimeStates.Count > count) {
                layerRuntimeStates.RemoveRange(count, layerRuntimeStates.Count - count);
            }
        }

        private void ReleaseLayerRuntimeStates()
        {
            for (var i = 0; i < layerRuntimeStates.Count; i++) {
                layerRuntimeStates[i].ReleaseMaterial();
            }

            layerRuntimeStates.Clear();
        }

        private void ReleaseResources()
        {
            ReleaseLayerRuntimeStates();
            ReleaseMesh();
            resolvedLayers.Clear();
            hasPaintBounds = false;
            hasAssignedMesh = false;
        }

        private Mesh GetMesh()
        {
            if (mesh == null) {
                mesh = new Mesh {
                    name = name + " (TextMeshPro Layer Mesh)",
                    hideFlags = HideFlags.HideAndDontSave
                };
                mesh.MarkDynamic();
            }

            return mesh;
        }

        private void ReleaseMesh()
        {
            CoreUtils.Destroy(mesh);
            mesh = null;
        }

        private void RefreshPaintBounds(Mesh layerMesh)
        {
            paintBounds = TryGetMeshPaintBounds(layerMesh, out var meshBounds)
                ? meshBounds
                : TextMeshProUtility.CalculateFrameBounds(text);
            hasPaintBounds = true;
        }

        private static bool TryGetMeshPaintBounds(Mesh layerMesh, out Vector4 bounds)
        {
            bounds = default;
            if (layerMesh.vertexCount == 0) {
                return false;
            }

            var meshBounds = layerMesh.bounds;
            if (meshBounds.size.x <= 0f || meshBounds.size.y <= 0f) {
                return false;
            }

            bounds = CanvasUtility.BoundsFromMinMax(meshBounds.min, meshBounds.max);
            return true;
        }

        private void RestoreDefaultRendering()
        {
            if (text == null) {
                return;
            }

            text.SetVerticesDirty();
            text.SetMaterialDirty();
        }

        private void ClearLayerRendering()
        {
            ReleaseResources();
            RestoreDefaultRendering();
            pendingChange = TextMeshProLayerChange.Geometry;
        }

        private ResolvedLayer GetRenderLayerForSlot(int materialSlot)
        {
            return resolvedLayers[resolvedLayers.Count - 1 - materialSlot];
        }

        private static TextMeshProLayerChange CombineChanges(TextMeshProLayerChange current, TextMeshProLayerChange next)
        {
            if ((next & TextMeshProLayerChange.Geometry) != 0) {
                return TextMeshProLayerChange.Geometry | TextMeshProLayerChange.Material;
            }

            if ((current & TextMeshProLayerChange.Geometry) != 0) {
                return current | TextMeshProLayerChange.Material;
            }

            return current | next;
        }

        [Serializable]
        private sealed class LayerOverride
        {
            [SerializeField]
            private bool overrideLayer;

            [SerializeField]
            private TextMeshProLayerData layer = TextMeshProLayerData.Default();

            public bool OverrideLayer {
                get => overrideLayer;
                set => overrideLayer = value;
            }

            public TextMeshProLayerData Layer => layer;

            internal void EnsureLayerCopy(TextMeshProLayerData source)
            {
                if (layer == null) {
                    layer = source.Clone();
                } else {
                    if (!overrideLayer) {
                        layer.CopyFrom(source);
                    }
                }
            }

            internal void CopyFromPreset(TextMeshProLayerData source)
            {
                if (layer == null) {
                    layer = source.Clone();
                } else {
                    layer.CopyFrom(source);
                }
            }
        }

        private readonly struct ResolvedLayer
        {
            public readonly TextMeshProLayerData Layer;
            public readonly LayerMaterialScope MaterialScope;

            private ResolvedLayer(TextMeshProLayerData layer, LayerMaterialScope materialScope)
            {
                Layer = layer;
                MaterialScope = materialScope;
            }

            public static ResolvedLayer Unique(TextMeshProLayerData layer)
            {
                return new ResolvedLayer(layer, LayerMaterialScope.Unique);
            }

            public static ResolvedLayer Shared(TextMeshProLayerData layer, TextMeshProLayerPreset preset, int layerIndex)
            {
                return new ResolvedLayer(layer, LayerMaterialScope.Shared(preset, layerIndex));
            }
        }

        private sealed class LayerRuntimeState
        {
            private Material uniqueMaterial;
            private readonly TextMeshProLayerMaterialGradientState uniqueGradientState = new TextMeshProLayerMaterialGradientState();

            private TextMeshProLayerMaterialCache.Entry sharedMaterialEntry;
            private TextMeshProLayerMaterialCacheKey sharedMaterialKey;
            private bool hasSharedMaterialKey;

            public Material GetOrCreateMaterial(ResolvedLayer resolvedLayer, TextMeshProLayerMaterialContext context, string ownerName)
            {
                if (resolvedLayer.MaterialScope.CanShare) {
                    return GetOrCreateSharedMaterial(resolvedLayer, context);
                }

                ReleaseSharedMaterial();
                return GetOrCreateUniqueMaterial(resolvedLayer.Layer, context, ownerName);
            }

            private Material GetOrCreateSharedMaterial(ResolvedLayer resolvedLayer, TextMeshProLayerMaterialContext context)
            {
                ReleaseUniqueMaterial();

                var key = resolvedLayer.MaterialScope.CreateCacheKey(context);
                if (sharedMaterialEntry == null || !hasSharedMaterialKey || !sharedMaterialKey.Equals(key)) {
                    ReleaseSharedMaterial();
                    sharedMaterialEntry = TextMeshProLayerMaterialCache.Acquire(key, resolvedLayer.Layer, context);
                    sharedMaterialKey = key;
                    hasSharedMaterialKey = true;
                }

                return sharedMaterialEntry.Material;
            }

            private Material GetOrCreateUniqueMaterial(TextMeshProLayerData layer, TextMeshProLayerMaterialContext context, string ownerName)
            {
                if (uniqueMaterial == null) {
                    uniqueMaterial = TextMeshProLayerMaterial.CreateRuntimeMaterial();
                    uniqueMaterial.name = ownerName + " (TextMeshPro Layer Material)";
                }

                TextMeshProLayerMaterial.ApplyLayer(uniqueMaterial, layer, context, uniqueGradientState);
                return uniqueMaterial;
            }

            public void ReleaseMaterial()
            {
                ReleaseUniqueMaterial();
                ReleaseSharedMaterial();
            }

            private void ReleaseUniqueMaterial()
            {
                uniqueGradientState.Release();
                CoreUtils.Destroy(uniqueMaterial);
                uniqueMaterial = null;
            }

            private void ReleaseSharedMaterial()
            {
                TextMeshProLayerMaterialCache.Release(sharedMaterialEntry);
                sharedMaterialEntry = null;
                sharedMaterialKey = default;
                hasSharedMaterialKey = false;
            }
        }

        private readonly struct LayerMaterialScope
        {
            public static readonly LayerMaterialScope Unique = new LayerMaterialScope(null, -1);

            private readonly TextMeshProLayerPreset preset;
            private readonly int layerIndex;

            private LayerMaterialScope(TextMeshProLayerPreset preset, int layerIndex)
            {
                this.preset = preset;
                this.layerIndex = layerIndex;
            }

            public bool CanShare => preset != null;

            public static LayerMaterialScope Shared(TextMeshProLayerPreset preset, int layerIndex)
            {
                return new LayerMaterialScope(preset, layerIndex);
            }

            public TextMeshProLayerMaterialCacheKey CreateCacheKey(TextMeshProLayerMaterialContext context)
            {
                return new TextMeshProLayerMaterialCacheKey(preset, preset.Version, layerIndex, context);
            }
        }
    }
}
