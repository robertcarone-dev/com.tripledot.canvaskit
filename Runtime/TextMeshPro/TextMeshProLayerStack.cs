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
    public sealed class TextMeshProLayerStack : UIBehaviour, ISerializationCallbackReceiver, IClippable
    {
        [SerializeField]
        private TextMeshProLayerPreset preset;
        [SerializeField]
        private List<TextMeshProLayerData> localLayers = new List<TextMeshProLayerData>();
        [SerializeField]
        private List<LayerOverride> presetLayerOverrides = new List<LayerOverride>();

        [SerializeField]
        [HideInInspector]
        private TextMeshProLayerPreset overridePreset;
        [SerializeField]
        [HideInInspector]
        private bool overrideIdsInitialized;

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
        private bool hasSourceGeometry;
        private float sourceScaleY;
        private RectMask2D clipParent;
        private Rect clipRect;
        private bool validClipRect;
        private Vector2 clipSoftness;
        private bool wasMaskable;
        private bool rebuilding;
        
        private const AdditionalCanvasShaderChannels RequiredCanvasChannels =
            AdditionalCanvasShaderChannels.TexCoord1 |
            AdditionalCanvasShaderChannels.TexCoord2;

        public TextMeshProLayerPreset Preset {
            get => preset;
            set {
                if (value != preset) {
                    presetLayerOverrides.Clear();
                    overridePreset = value;
                    overrideIdsInitialized = true;
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
        }

        internal bool TryGetCurrentPaintBounds(out Vector4 bounds)
        {
            bounds = paintBounds;
            return hasPaintBounds && paintBounds.z > 0f && paintBounds.w > 0f;
        }

        public void ReplaceLocalLayers(IList<TextMeshProLayerData> layers)
        {
            if (layers == null) {
                throw new ArgumentNullException(nameof(layers));
            }
            for (var i = 0; i < layers.Count; i++) {
                if (layers[i] == null) {
                    throw new ArgumentException("Layers must not contain null entries.", nameof(layers));
                }
            }
            Preset = null;
            localLayers.Clear();
            foreach (var layer in layers) {
                localLayers.Add(layer.Clone());
            }

            NotifyChanged(TextMeshProLayerChange.Geometry);
        }

        public void AddLocalLayer(TextMeshProLayerData layer)
        {
            RequireLocalLayers();
            if (layer == null) {
                throw new ArgumentNullException(nameof(layer));
            }
            localLayers.Add(layer.Clone());
            NotifyChanged(TextMeshProLayerChange.Geometry);
        }

        public int LayerCount => preset != null ? preset.LayerCount : localLayers.Count;

        public TextMeshProLayerData GetLayerCopy(int index)
        {
            ValidateLayerIndex(index);
            return (preset != null ? GetEffectivePresetLayer(index) : localLayers[index]).Clone();
        }

        /// <summary>Copies a layer into this object. Preset rows become local overrides; the asset is not modified.</summary>
        public void SetLayer(int index, TextMeshProLayerData layer)
        {
            ValidateLayerIndex(index);
            if (layer == null) {
                throw new ArgumentNullException(nameof(layer));
            }
            var previous = preset != null ? GetEffectivePresetLayer(index) : localLayers[index];
            var change = previous.HasSameGeometry(layer) ? TextMeshProLayerChange.Material : TextMeshProLayerChange.Geometry;
            if (preset == null) {
                localLayers[index] = layer.Clone();
            } else {
                EnsurePresetOverrideSlots();
                var slot = presetLayerOverrides[GetPresetLayerOverrideIndex(index)];
                slot.CopyFromPreset(layer);
                slot.OverrideLayer = true;
            }
            NotifyChanged(change);
        }

        /// <summary>Updates opacity without cloning layer data each frame. A shared row becomes an instance on first edit.</summary>
        public void SetLayerOpacity(int index, float opacity)
        {
            ValidateLayerIndex(index);
            var layer = preset != null ? GetEffectivePresetLayer(index) : localLayers[index];
            opacity = Mathf.Clamp01(opacity);
            if (layer.Opacity == opacity) {
                return;
            }
            if (preset != null) {
                var slot = presetLayerOverrides[GetPresetLayerOverrideIndex(index)];
                if (!slot.OverrideLayer) {
                    slot.CopyFromPreset(layer);
                    slot.OverrideLayer = true;
                }
                layer = slot.Layer;
            }
            layer.Opacity = opacity;
            NotifyChanged(TextMeshProLayerChange.Material);
        }

        public void RemoveLocalLayer(int index)
        {
            RequireLocalLayers();
            ValidateLayerIndex(index);
            localLayers.RemoveAt(index);
            NotifyChanged(TextMeshProLayerChange.Geometry);
        }

        private void RequireLocalLayers()
        {
            if (preset != null) {
                throw new InvalidOperationException("Replace the preset with local layers before changing layer composition.");
            }
        }

        private void ValidateLayerIndex(int index)
        {
            if (index < 0 || index >= LayerCount) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public bool IsPresetLayerInstance(int index)
        {
            ValidateLayerIndex(index);
            EnsurePresetOverrideSlots();
            return preset != null && presetLayerOverrides[GetPresetLayerOverrideIndex(index)].OverrideLayer;
        }

        public void SetPresetLayerInstance(int index, bool instance)
        {
            EnsurePresetOverrideSlots();
            if (preset == null || index < 0 || index >= preset.LayerCount) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var layerOverride = presetLayerOverrides[GetPresetLayerOverrideIndex(index)];
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
            var slot = presetLayerOverrides[GetPresetLayerOverrideIndex(index)];
            return slot.OverrideLayer ? slot.Layer : preset.GetLayer(index);
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

        public void ClearPresetLayerInstances()
        {
            EnsurePresetOverrideSlots();
            for (var i = 0; i < (preset != null ? preset.LayerCount : 0); i++) {
                var slot = presetLayerOverrides[GetPresetLayerOverrideIndex(i)];
                slot.OverrideLayer = false;
                slot.EnsureLayerCopy(preset.GetLayer(i));
            }

            NotifyChanged(TextMeshProLayerChange.Geometry);
        }

        internal Mesh CurrentMesh => mesh;

        protected override void OnEnable()
        {
            base.OnEnable();

            text = GetComponent<TextMeshProUGUI>();
            RegisterCallbacks();
            hasSourceGeometry = false;
            UpdateClipParent();
            // Initialize/register both owning update managers before our render callback.
            text.SetVerticesDirty();
            TMP_UpdateManager.RegisterTextElementForGraphicRebuild(text);
            Canvas.willRenderCanvases += OnWillRenderCanvases;
            TextMeshProLayerPreset.Changed += OnPresetChanged;
            NotifyChanged(TextMeshProLayerChange.Geometry);
        }

        protected override void OnDisable()
        {
            Canvas.willRenderCanvases -= OnWillRenderCanvases;
            UnregisterCallbacks();
            TextMeshProLayerPreset.Changed -= OnPresetChanged;
            RestoreDefaultRendering();
            RemoveClipParent();
            ReleaseResources();
            pendingChange = TextMeshProLayerChange.Geometry;

            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            Canvas.willRenderCanvases -= OnWillRenderCanvases;
            UnregisterCallbacks();
            RemoveClipParent();
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
            UpdateClipParent();
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
            UpdateClipParent();
            NotifyChanged(TextMeshProLayerChange.Material);
            base.OnCanvasHierarchyChanged();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
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
            text.OnPreRenderText += OnTextGenerated;
        }

        private void UnregisterCallbacks()
        {
            if (text == null) {
                return;
            }

            text.UnregisterDirtyVerticesCallback(OnTextVerticesDirty);
            text.UnregisterDirtyMaterialCallback(OnTextMaterialDirty);
            text.UnregisterDirtyLayoutCallback(OnTextLayoutDirty);
            text.OnPreRenderText -= OnTextGenerated;
        }

        private void OnTextVerticesDirty()
        {
            NotifyChanged(TextMeshProLayerChange.Geometry);
        }

        private void OnTextMaterialDirty()
        {
            if (wasMaskable != text.maskable) {
                UpdateClipParent();
            }
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

        private void OnTextGenerated(TMP_TextInfo textInfo)
        {
            hasSourceGeometry = true;
            NotifyChanged(TextMeshProLayerChange.Geometry);
        }

        private void OnWillRenderCanvases()
        {
            if (!isActiveAndEnabled || text == null || !text.isActiveAndEnabled || text.canvas == null || rebuilding) {
                return;
            }
            if (text.font == null) {
                RestoreDefaultRendering();
                ReleaseResources();
                return;
            }

            if (!hasSourceGeometry || text.havePropertiesChanged) {
                text.ForceMeshUpdate();
                hasSourceGeometry = true;
                NotifyChanged(TextMeshProLayerChange.Geometry);
            }

            if (pendingChange != TextMeshProLayerChange.None) {
                Rebuild();
            }
            // TMP's scale-only upload has no notification. Copy just its updated SDF
            // scale channel, without rebuilding layer geometry, materials or text layout.
            var scaleY = text.rectTransform.lossyScale.y;
            if (hasAssignedMesh && sourceScaleY != scaleY) {
                meshBuilder.UpdateSdfScale(mesh, text.textInfo);
                text.canvasRenderer.SetMesh(mesh);
            }
            sourceScaleY = scaleY;
            if (clipParent != null) {
                CullText();
            }
        }

        RectTransform IClippable.rectTransform => text.rectTransform;
        void IClippable.RecalculateClipping()
        {
            UpdateClipParent();
        }

        void IClippable.Cull(Rect value, bool validRect)
        {
        }

        void IClippable.SetClipRect(Rect value, bool validRect)
        {
            clipRect = value;
            validClipRect = validRect;
            // RectMask2D dispatches ordinary IClippable targets before MaskableGraphics.
            // Remove TMP from the latter set before that iteration starts, including when
            // TMP re-registers itself after a hierarchy or mask change. This stack forwards
            // clipping and runs TMP's culling once, after the styled bounds are available.
            if (text != null) {
                clipParent.RemoveClippable(text);
                text.SetClipRect(value, validRect);
            }
        }

        void IClippable.SetClipSoftness(Vector2 value)
        {
            if (text != null) {
                text.SetClipSoftness(value);
            }
            if (clipSoftness != value) {
                clipSoftness = value;
                NotifyChanged(TextMeshProLayerChange.Material);
            }
        }

        private void UpdateClipParent()
        {
            wasMaskable = text != null && text.maskable;
            var next = isActiveAndEnabled && text != null && text.maskable ? MaskUtilities.GetRectMaskForClippable(text) : null;
            if (next == clipParent) {
                return;
            }
            RemoveClipParent();
            clipParent = next;
            if (clipParent != null) {
                clipParent.AddClippable(this);
            }
        }

        private void RemoveClipParent()
        {
            if (clipParent != null) {
                clipParent.RemoveClippable(this);
                if (text != null) {
                    text.RecalculateClipping();
                }
            }
            clipParent = null;
            validClipRect = false;
        }

        private void CullText()
        {
            if (!hasAssignedMesh) {
                text.Cull(clipRect, validClipRect);
                return;
            }

            // TMP remains the authority for culling events, rebuild notification and
            // submesh propagation. Supply the styled primary bounds only for this call.
            var sourceMesh = text.mesh;
            var sourceBounds = sourceMesh.bounds;
            try {
                sourceMesh.bounds = mesh.bounds;
                text.Cull(clipRect, validClipRect);
            } finally {
                if (sourceMesh != null) {
                    sourceMesh.bounds = sourceBounds;
                }
            }
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
                ResolveRenderableLayers();
                EnsureLayerRuntimeStates(resolvedLayers.Count);
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
            } catch {
                RestoreDefaultRendering();
                ReleaseResources();
                throw;
            } finally {
                rebuilding = false;
            }
        }

        private void RebuildGeometry()
        {
            if (resolvedLayers.Count == 0) {
                return;
            }

            CanvasUtility.EnsureChannels(text.canvas, RequiredCanvasChannels);
            RefreshLayerPaddingBudget();

            var targetMesh = GetMesh();
            meshBuilder.Build(targetMesh, text.textInfo, GetResolvedLayerData(), appliedGeometryPaddingLimit, text.geometrySortingOrder == VertexSortingOrder.Reverse);
            RefreshPaintBounds(targetMesh);
        }

        private void ApplyMaterials()
        {
            var renderMaterial = text.materialForRendering;
            var sourceMaterial = text.fontSharedMaterial != null ? text.fontSharedMaterial : renderMaterial;
            var materialContext = TextMeshProLayerMaterialContext.Capture(text, sourceMaterial, renderMaterial, appliedEffectPaddingBudget);
            var canvasRenderer = text.canvasRenderer;

            canvasRenderer.SetMesh(mesh);
            canvasRenderer.materialCount = resolvedLayers.Count + (meshBuilder.HasDecorations ? 1 : 0);
            canvasRenderer.SetTexture(materialContext.FontAtlas);
            hasAssignedMesh = true;

            for (var materialSlot = 0; materialSlot < resolvedLayers.Count; materialSlot++) {
                var resolvedLayer = GetRenderLayerForSlot(materialSlot);
                var material = layerRuntimeStates[materialSlot].GetOrCreateMaterial(resolvedLayer, materialContext, name);
                canvasRenderer.SetMaterial(material, materialSlot);
            }
            if (meshBuilder.HasDecorations) {
                canvasRenderer.SetMaterial(renderMaterial, resolvedLayers.Count);
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
                var layerOverride = presetLayerOverrides[GetPresetLayerOverrideIndex(i)];
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

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            // Legacy IDs are deterministic so unloaded scenes still map to their original
            // preset layers after the preset has been reordered and saved.
            if (!overrideIdsInitialized) {
                overridePreset = preset;
                for (var i = 0; i < presetLayerOverrides.Count; i++) {
                    presetLayerOverrides[i].LayerId = "legacy:" + i;
                }
                overrideIdsInitialized = true;
            }
        }

        internal bool EnsurePresetOverrideSlots()
        {
            var changed = !overrideIdsInitialized;
            if (overridePreset != preset) {
                presetLayerOverrides.Clear();
                overridePreset = preset;
                changed = true;
            }
            overrideIdsInitialized = true;
            var count = preset != null ? preset.LayerCount : 0;
            for (var i = 0; i < count; i++) {
                var source = preset.GetLayer(i);
                if (GetPresetLayerOverrideIndex(i) < 0) {
                    var slot = new LayerOverride { LayerId = source.Id };
                    slot.CopyFromPreset(source);
                    presetLayerOverrides.Add(slot);
                    changed = true;
                }
            }
            foreach (var slot in presetLayerOverrides) {
                if (string.IsNullOrEmpty(slot.LayerId)) {
                    continue;
                }
                var exists = false;
                for (var i = 0; i < count; i++) {
                    exists |= preset.GetLayer(i).Id == slot.LayerId;
                }
                if (!exists) {
                    // Keep an empty slot so animation paths for surviving rows never shift.
                    slot.LayerId = null;
                    slot.OverrideLayer = false;
                    slot.CopyFromPreset(TextMeshProLayerData.Default());
                    changed = true;
                }
            }
            return changed;
        }

        internal int GetPresetLayerOverrideIndex(int layerIndex)
        {
            var id = preset.GetLayer(layerIndex).Id;
            for (var i = 0; i < presetLayerOverrides.Count; i++) {
                if (presetLayerOverrides[i].LayerId == id) {
                    return i;
                }
            }
            return -1;
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
            resolvedLayerData.Clear();
            meshBuilder.Clear();
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

            if (!hasAssignedMesh) {
                return;
            }
            var renderer = text.canvasRenderer;
            if (!text.isActiveAndEnabled) {
                renderer.Clear();
                hasAssignedMesh = false;
                return;
            }
            renderer.SetMesh(text.textInfo.characterCount == 0 ? null : text.mesh);
            renderer.materialCount = 1;
            renderer.SetMaterial(text.materialForRendering, 0);
            renderer.SetTexture(text.mainTexture);
            hasAssignedMesh = false;
            if (clipParent != null) {
                text.Cull(clipRect, validClipRect);
            }
        }

        private void ClearLayerRendering()
        {
            RestoreDefaultRendering();
            ReleaseResources();
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
            private string layerId;

            public string LayerId {
                get => layerId;
                set => layerId = value;
            }

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
