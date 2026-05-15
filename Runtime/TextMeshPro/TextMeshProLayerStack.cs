using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Tripledot.CanvasKit
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    [AddComponentMenu("UI (Canvas)/TextMeshPro - Layer Stack", 11)]
    public sealed class TextMeshProLayerStack : UIBehaviour, ICanvasElement
    {
        private const AdditionalCanvasShaderChannels RequiredCanvasChannels =
            AdditionalCanvasShaderChannels.TexCoord1 |
            AdditionalCanvasShaderChannels.TexCoord2;

        #region Types

        [Flags]
        internal enum DirtyFlags
        {
            None = 0,
            Layers = 1 << 0,
            Geometry = 1 << 1,
            Material = 1 << 2,
            Padding = 1 << 3,
            Canvas = 1 << 4,
            SourceGeometry = 1 << 5,
            PaintBounds = 1 << 6,
            All = Layers | Geometry | Material | Padding | Canvas | SourceGeometry | PaintBounds
        }

        [Flags]
        private enum ApplyCanvasRendererFlags
        {
            None = 0,
            Mesh = 1 << 0,
            Materials = 1 << 1,
            Texture = 1 << 2,
            All = Mesh | Materials | Texture
        }

        private readonly struct TextSdfSourceKey : IEquatable<TextSdfSourceKey>
        {
            private readonly int materialId;
            private readonly float gradientScale;
            private readonly float scaleRatioA;

            private TextSdfSourceKey(Material material)
            {
                materialId = material != null ? material.GetInstanceID() : 0;
                gradientScale = material != null && material.HasProperty(ShaderUtilities.ID_GradientScale) ? material.GetFloat(ShaderUtilities.ID_GradientScale) : 0f;
                scaleRatioA = material != null && material.HasProperty(ShaderUtilities.ID_ScaleRatio_A) ? material.GetFloat(ShaderUtilities.ID_ScaleRatio_A) : 0f;
            }

            internal static TextSdfSourceKey Capture(Material material)
            {
                return new TextSdfSourceKey(material);
            }

            public bool Equals(TextSdfSourceKey other)
            {
                return materialId == other.materialId
                    && gradientScale == other.gradientScale
                    && scaleRatioA == other.scaleRatioA;
            }

            public override bool Equals(object obj)
            {
                return obj is TextSdfSourceKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = materialId;
                    hashCode = (hashCode * 397) ^ gradientScale.GetHashCode();
                    hashCode = (hashCode * 397) ^ scaleRatioA.GetHashCode();
                    return hashCode;
                }
            }
        }

        private readonly struct TextState : IEquatable<TextState>
        {
            private readonly string text;
            private readonly int fontId;
            private readonly float fontSize;
            private readonly FontStyles fontStyle;
            private readonly TextAlignmentOptions alignment;
            private readonly TextWrappingModes textWrappingMode;
            private readonly TextOverflowModes overflowMode;
            private readonly bool enableAutoSizing;
            private readonly float fontSizeMin;
            private readonly float fontSizeMax;
            private readonly float characterSpacing;
            private readonly float wordSpacing;
            private readonly float lineSpacing;
            private readonly float paragraphSpacing;
            private readonly Vector4 margin;
            private readonly Vector2 rectSize;

            private TextState(TextMeshProUGUI textComponent)
            {
                text = textComponent.text;
                fontId = textComponent.font != null ? textComponent.font.GetInstanceID() : 0;
                fontSize = textComponent.fontSize;
                fontStyle = textComponent.fontStyle;
                alignment = textComponent.alignment;
                textWrappingMode = textComponent.textWrappingMode;
                overflowMode = textComponent.overflowMode;
                enableAutoSizing = textComponent.enableAutoSizing;
                fontSizeMin = textComponent.fontSizeMin;
                fontSizeMax = textComponent.fontSizeMax;
                characterSpacing = textComponent.characterSpacing;
                wordSpacing = textComponent.wordSpacing;
                lineSpacing = textComponent.lineSpacing;
                paragraphSpacing = textComponent.paragraphSpacing;
                margin = textComponent.margin;
                rectSize = textComponent.rectTransform != null ? textComponent.rectTransform.rect.size : Vector2.zero;
            }

            internal static TextState Capture(TextMeshProUGUI textComponent)
            {
                return new TextState(textComponent);
            }

            public bool Equals(TextState other)
            {
                return text == other.text
                    && fontId == other.fontId
                    && fontSize == other.fontSize
                    && fontStyle == other.fontStyle
                    && alignment == other.alignment
                    && textWrappingMode == other.textWrappingMode
                    && overflowMode == other.overflowMode
                    && enableAutoSizing == other.enableAutoSizing
                    && fontSizeMin == other.fontSizeMin
                    && fontSizeMax == other.fontSizeMax
                    && characterSpacing == other.characterSpacing
                    && wordSpacing == other.wordSpacing
                    && lineSpacing == other.lineSpacing
                    && paragraphSpacing == other.paragraphSpacing
                    && margin == other.margin
                    && rectSize == other.rectSize;
            }

            public override bool Equals(object obj)
            {
                return obj is TextState other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = text != null ? text.GetHashCode() : 0;
                    hashCode = (hashCode * 397) ^ fontId;
                    hashCode = (hashCode * 397) ^ fontSize.GetHashCode();
                    hashCode = (hashCode * 397) ^ fontStyle.GetHashCode();
                    hashCode = (hashCode * 397) ^ alignment.GetHashCode();
                    hashCode = (hashCode * 397) ^ textWrappingMode.GetHashCode();
                    hashCode = (hashCode * 397) ^ overflowMode.GetHashCode();
                    hashCode = (hashCode * 397) ^ enableAutoSizing.GetHashCode();
                    hashCode = (hashCode * 397) ^ fontSizeMin.GetHashCode();
                    hashCode = (hashCode * 397) ^ fontSizeMax.GetHashCode();
                    hashCode = (hashCode * 397) ^ characterSpacing.GetHashCode();
                    hashCode = (hashCode * 397) ^ wordSpacing.GetHashCode();
                    hashCode = (hashCode * 397) ^ lineSpacing.GetHashCode();
                    hashCode = (hashCode * 397) ^ paragraphSpacing.GetHashCode();
                    hashCode = (hashCode * 397) ^ margin.GetHashCode();
                    hashCode = (hashCode * 397) ^ rectSize.GetHashCode();
                    return hashCode;
                }
            }
        }

        #endregion

        #region Serialized Fields

        [SerializeField]
        private TextMeshProLayerPreset preset;
        [SerializeField]
        private List<TextMeshProLayerData> localLayers = new List<TextMeshProLayerData>();
        [SerializeField]
        private List<TextMeshProLayerOverride> presetLayerOverrides = new List<TextMeshProLayerOverride>();

        #endregion

        #region Fields

        private readonly List<TextMeshProLayerData> layers = new List<TextMeshProLayerData>();
        private readonly List<LayerMaterialScope> layerMaterialScopes = new List<LayerMaterialScope>();
        private readonly List<Material> appliedCanvasMaterials = new List<Material>();
        private readonly List<LayerRuntimeState> layerRuntimeStates = new List<LayerRuntimeState>();
        private readonly TextMeshProLayerMeshBuilder meshBuilder = new TextMeshProLayerMeshBuilder();

        private TextMeshProUGUI text;
        private Mesh mesh;
        private Mesh appliedMesh;
        private Texture appliedTexture;
        private DirtyFlags dirtyFlags = DirtyFlags.All;
        private TextState textState;
        private TextSdfSourceKey sdfSourceState;
        private TextMeshProLayerMaterialContext materialContextState;
        private bool hasTextState;
        private bool hasSdfSourceState;
        private bool hasMaterialContextState;
        private bool materialSharingAllowedState = true;
        private Vector4 paintBounds;
        private float appliedEffectPaddingBudget;
        private float appliedGeometryPaddingLimit;
        private int appliedMaterialCount = -1;
        private bool isQueuedForGraphicRebuild;
        private bool hasDeferredGraphicRebuild;
        private bool isRendering;
        private bool hasAssignedMesh;
        private bool hasPaintBounds;
        private bool layerCompositionDirty = true;
        private bool isRegisteredForCanvasValidation;
        private Canvas appliedRootCanvas;
        private Transform appliedParent;
        private int appliedSiblingIndex = -1;
        private int appliedAbsoluteDepth = -1;
        
        private static readonly List<TextMeshProLayerStack> CanvasValidationStacks = new List<TextMeshProLayerStack>();

        #endregion

        #region Public API

        public TextMeshProLayerPreset Preset
        {
            get => preset;
            set {
                if (preset == value) {
                    return;
                }

                preset = value;
                SetLayerCompositionChanged();
            }
        }

        public void SetLayerStackDirty()
        {
            SetLayerStackDirty(DirtyFlags.All);
        }

        #endregion

        #region Internal API

        internal List<TextMeshProLayerData> LocalLayers => localLayers;

        internal List<TextMeshProLayerOverride> PresetLayerOverrides => presetLayerOverrides;

        internal bool TryGetCurrentPaintBounds(out Vector4 bounds)
        {
            bounds = paintBounds;
            return hasPaintBounds && paintBounds.z > 0f && paintBounds.w > 0f;
        }

        internal void GetLocalLayers(List<TextMeshProLayerData> results)
        {
            results.Clear();
            for (int i = 0; i < localLayers.Count; i++) {
                if (localLayers[i] != null) {
                    results.Add(localLayers[i]);
                }
            }
        }

        internal void GetResolvedLayers(List<TextMeshProLayerData> results)
        {
            results.Clear();
            ResolveRenderableLayers(results, null);
        }

        internal Material GetAppliedMaterialForTesting(int materialSlot)
        {
            return materialSlot >= 0 && materialSlot < appliedCanvasMaterials.Count ? appliedCanvasMaterials[materialSlot] : null;
        }

        internal Mesh GetLayerMeshForTesting()
        {
            return mesh;
        }

        internal void SetLayerStackDirty(DirtyFlags flags)
        {
            MarkDirty(flags);
            
            if ((flags & DirtyFlags.Layers) != 0) {
                layerCompositionDirty = true;
            }

            QueueGraphicRebuild();
        }

        internal void SetLayerChanged()
        {
            SetLayerGeometryChanged();
        }

        internal void SetLayerMaterialChanged()
        {
            MarkDirty(DirtyFlags.Material | DirtyFlags.Canvas);
            MarkRuntimeMaterialsDirty();
            QueueGraphicRebuild();
        }

        internal void SetLayerGeometryChanged()
        {
            MarkDirty(DirtyFlags.Geometry | DirtyFlags.Material | DirtyFlags.Padding | DirtyFlags.Canvas | DirtyFlags.PaintBounds);
            QueueGraphicRebuild();
        }

        internal void SetLayerPaddingChanged()
        {
            MarkDirty(DirtyFlags.Geometry | DirtyFlags.Material | DirtyFlags.Padding | DirtyFlags.Canvas | DirtyFlags.PaintBounds);
            QueueGraphicRebuild();
        }

        internal void SetLayerCompositionChanged()
        {
            SetLayerStackDirty(DirtyFlags.All);
        }

        #endregion

        #region Unity Lifecycle

        protected override void OnEnable()
        {
            base.OnEnable();
            
            TryGetComponent(out text);
            RegisterCallbacks();
            TextMeshProLayerPreset.Changed += OnPresetChanged;
            SetLayerStackDirty();
        }

        protected override void OnDisable()
        {
            CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);
            
            UnregisterDeferredGraphicRebuild();
            isQueuedForGraphicRebuild = false;
            
            UnregisterCallbacks();
            TextMeshProLayerPreset.Changed -= OnPresetChanged;
            RestoreTextMeshProRendering();
            ReleaseStackResources();
            ResetRuntimeState();
            
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);
            
            UnregisterDeferredGraphicRebuild();
            isQueuedForGraphicRebuild = false;
            
            UnregisterCallbacks();
            TextMeshProLayerPreset.Changed -= OnPresetChanged;
            ReleaseStackResources();
            ResetRuntimeState();
            
            base.OnDestroy();
        }

        protected override void OnDidApplyAnimationProperties()
        {
            SetLayerStackDirty();
            base.OnDidApplyAnimationProperties();
        }

        protected override void Reset()
        {
            base.Reset();
            
            layerCompositionDirty = true;
            if (preset != null || localLayers.Count > 0) {
                SetLayerStackDirty();
                return;
            }

            localLayers.Add(TextMeshProLayerData.Default());
            SetLayerStackDirty();
        }

        protected override void OnTransformParentChanged()
        {
            SetLayerStackDirty();
            base.OnTransformParentChanged();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            SetLayerStackDirty(DirtyFlags.PaintBounds | DirtyFlags.Material);
            base.OnRectTransformDimensionsChange();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            EnsurePresetOverrideSlots();
            SetLayerStackDirty();
        }
#endif

        #endregion

        #region ICanvasElement

        void ICanvasElement.Rebuild(CanvasUpdate executing)
        {
            if (executing == CanvasUpdate.LatePreRender) {
                isQueuedForGraphicRebuild = false;
                RenderTextMeshProLayers();
            }
        }

        void ICanvasElement.LayoutComplete()
        {
        }

        void ICanvasElement.GraphicUpdateComplete()
        {
            isQueuedForGraphicRebuild = false;
        }

        bool ICanvasElement.IsDestroyed()
        {
            return this == null;
        }

        #endregion

        #region Callbacks and Dirty State

        private void MarkDirty(DirtyFlags flags)
        {
            dirtyFlags |= flags;
        }

        private void RegisterCallbacks()
        {
            UnregisterCallbacks();
            if (text == null) {
                return;
            }

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
            MarkDirty(DirtyFlags.SourceGeometry | DirtyFlags.Geometry | DirtyFlags.PaintBounds | DirtyFlags.Canvas);
            QueueGraphicRebuild();
        }

        private void OnPresetChanged(TextMeshProLayerPreset changedPreset)
        {
            if (changedPreset == preset) {
                SetLayerStackDirty();
            }
        }

        private void OnTextMaterialDirty()
        {
            MarkDirty(DirtyFlags.Material);
            QueueGraphicRebuild();
        }

        private void OnTextLayoutDirty()
        {
            MarkDirty(DirtyFlags.SourceGeometry | DirtyFlags.Geometry | DirtyFlags.PaintBounds | DirtyFlags.Canvas);
            QueueGraphicRebuild();
        }

        private void QueueGraphicRebuild()
        {
            if (!isActiveAndEnabled || !text.enabled || isRendering) {
                return;
            }

            RegisterDeferredGraphicRebuild();

            if (isQueuedForGraphicRebuild) {
                return;
            }

            if (CanvasUpdateRegistry.IsRebuildingGraphics()) {
                return;
            }

            isQueuedForGraphicRebuild = CanvasUpdateRegistry.TryRegisterCanvasElementForGraphicRebuild(this);
        }

        private void RegisterDeferredGraphicRebuild()
        {
            if (hasDeferredGraphicRebuild) {
                return;
            }

            hasDeferredGraphicRebuild = true;
            Canvas.willRenderCanvases += OnWillRenderCanvases;
        }

        private void UnregisterDeferredGraphicRebuild()
        {
            if (!hasDeferredGraphicRebuild) {
                return;
            }

            hasDeferredGraphicRebuild = false;
            Canvas.willRenderCanvases -= OnWillRenderCanvases;
        }

        #endregion

        #region Rendering

        private void OnWillRenderCanvases()
        {
            UnregisterDeferredGraphicRebuild();
            if (!isActiveAndEnabled || !text.enabled || isRendering) {
                return;
            }

            isQueuedForGraphicRebuild = false;
            if (CanReapplyCanvasRenderer()) {
                ReapplyCanvasRenderer();
                return;
            }

            RenderTextMeshProLayers();
        }

        private void RenderTextMeshProLayers()
        {
            if (!isActiveAndEnabled || !text.enabled || isRendering) {
                return;
            }

            var layerCount = RefreshRenderableLayers();
            if (layerCount == 0) {
                UnregisterCanvasValidation();
                ClearLayerRendering();
                return;
            }

            RegisterCanvasValidation();
            EnsureCanvasShaderChannels();
            
            if (TryMarkCanvasStateDirty()) {
                MarkDirty(DirtyFlags.Canvas);
            }

            isRendering = true;
            try {
                var renderMaterial = text.materialForRendering != null ? text.materialForRendering : text.fontSharedMaterial;
                var sourceMaterial = text.fontSharedMaterial != null ? text.fontSharedMaterial : renderMaterial;
                materialSharingAllowedState = renderMaterial == sourceMaterial;

                var nextTextState = TextState.Capture(text);
                UpdateTextAndSourceStates(sourceMaterial, nextTextState);
                
                var sourceGeometryDirty = (dirtyFlags & DirtyFlags.SourceGeometry) != 0;
                var geometryDirty = (dirtyFlags & (DirtyFlags.Geometry | DirtyFlags.Padding | DirtyFlags.Layers | DirtyFlags.SourceGeometry)) != 0;
                var paddingDirty = (dirtyFlags & (DirtyFlags.Padding | DirtyFlags.Layers)) != 0;
                var paintBoundsDirty = (dirtyFlags & DirtyFlags.PaintBounds) != 0 || !hasPaintBounds;

                if (paddingDirty || sourceGeometryDirty || paintBoundsDirty) {
                    text.ForceMeshUpdate();
                }

                if (paddingDirty) {
                    RefreshLayerPaddingBudget(text, layers, sourceMaterial);
                }

                if (geometryDirty) {
                    var targetMesh = GetMesh();
                    meshBuilder.Build(targetMesh, text.textInfo, layers, appliedGeometryPaddingLimit);
                    RefreshPaintBounds(targetMesh, text);
                    dirtyFlags &= ~(DirtyFlags.Geometry | DirtyFlags.Padding | DirtyFlags.SourceGeometry | DirtyFlags.PaintBounds);
                } else if (paintBoundsDirty) {
                    RefreshPaintBounds(mesh, text);
                    dirtyFlags &= ~DirtyFlags.PaintBounds;
                }

                var materialContext = TextMeshProLayerMaterialContext.Capture(text, sourceMaterial, renderMaterial, paintBounds, appliedEffectPaddingBudget);
                UpdateMaterialContextState(materialContext);
                
                var materialDirty = (dirtyFlags & (DirtyFlags.Material | DirtyFlags.Layers)) != 0;
                var canvasDirty = (dirtyFlags & (DirtyFlags.Canvas | DirtyFlags.Layers)) != 0;
                if (materialDirty) {
                    MarkRuntimeMaterialsDirty();
                }

                if (!geometryDirty && !materialDirty && !canvasDirty && hasAssignedMesh) {
                    return;
                }

                if (geometryDirty || materialDirty || canvasDirty || !hasAssignedMesh) {
                    var applyFlags = GetApplyFlags(geometryDirty, materialDirty, canvasDirty);
                    ApplyCanvasRenderer(
                        text,
                        GetMesh(),
                        layers,
                        layerMaterialScopes,
                        materialContext,
                        materialSharingAllowedState,
                        applyFlags);
                    dirtyFlags &= ~(DirtyFlags.Material | DirtyFlags.Canvas | DirtyFlags.Layers);
                }
            } finally {
                isRendering = false;
            }
        }

        private ApplyCanvasRendererFlags GetApplyFlags(bool geometryDirty, bool materialDirty, bool canvasDirty)
        {
            if (!hasAssignedMesh) {
                return ApplyCanvasRendererFlags.All;
            }

            var flags = ApplyCanvasRendererFlags.None;
            if (geometryDirty || canvasDirty) {
                flags |= ApplyCanvasRendererFlags.Mesh | ApplyCanvasRendererFlags.Materials | ApplyCanvasRendererFlags.Texture;
            } else if (materialDirty) {
                flags |= ApplyCanvasRendererFlags.Materials | ApplyCanvasRendererFlags.Texture;
            }

            return flags;
        }

        private void UpdateTextAndSourceStates(Material sourceMaterial, TextState nextTextState)
        {
            if (!hasTextState || !textState.Equals(nextTextState)) {
                textState = nextTextState;
                hasTextState = true;
                MarkDirty(DirtyFlags.SourceGeometry | DirtyFlags.Geometry | DirtyFlags.PaintBounds | DirtyFlags.Canvas);
            }

            var nextSdfSourceState = TextSdfSourceKey.Capture(sourceMaterial);
            if (!hasSdfSourceState || !sdfSourceState.Equals(nextSdfSourceState)) {
                sdfSourceState = nextSdfSourceState;
                hasSdfSourceState = true;
                MarkDirty(DirtyFlags.Padding | DirtyFlags.Geometry | DirtyFlags.PaintBounds | DirtyFlags.Canvas);
            }
        }

        private void UpdateMaterialContextState(TextMeshProLayerMaterialContext nextMaterialContext)
        {
            if (!hasMaterialContextState || !materialContextState.Equals(nextMaterialContext)) {
                materialContextState = nextMaterialContext;
                hasMaterialContextState = true;
                MarkDirty(DirtyFlags.Material | DirtyFlags.Canvas);
            }
        }

        private void ReleaseStackResources()
        {
            UnregisterCanvasValidation();
            ResetCanvasRendererState();
            ReleaseLayerRuntimeStates();
            ReleaseMesh();
        }

        #endregion

        #region Layer Resolution

        private int RefreshRenderableLayers()
        {
            if (!layerCompositionDirty) {
                return layers.Count;
            }

            layerCompositionDirty = false;
            layers.Clear();
            layerMaterialScopes.Clear();
            ResolveRenderableLayers(layers, layerMaterialScopes);
            EnsureLayerRuntimeStates(layers.Count);
            ResetCanvasRendererState();
            MarkRuntimeMaterialsDirty();
            MarkDirty(DirtyFlags.Layers | DirtyFlags.Geometry | DirtyFlags.Material | DirtyFlags.Padding | DirtyFlags.Canvas);

            return layers.Count;
        }

        private void RefreshLayerPaddingBudget(TextMeshProUGUI text, List<TextMeshProLayerData> layers, Material sourceMaterial)
        {
            var requiredPadding = CalculateMaxSdfPadding(layers);
            var availableSdfPadding = TextMeshProUtility.CalculateAvailablePadding(text, sourceMaterial);
            var effectPaddingBudget = Mathf.Min(requiredPadding, availableSdfPadding);
            appliedEffectPaddingBudget = effectPaddingBudget;
            appliedGeometryPaddingLimit = TextMeshProUtility.GetGeometryPaddingLimit(effectPaddingBudget);
        }

        internal static float CalculateMaxSdfPadding(List<TextMeshProLayerData> layers)
        {
            var padding = 0f;
            for (int i = 0; i < layers.Count; i++) {
                if (layers[i] != null && layers[i].Enabled) {
                    padding = Mathf.Max(padding, layers[i].GetSdfPadding());
                }
            }

            return padding;
        }

        private void ResolveRenderableLayers(List<TextMeshProLayerData> results, List<LayerMaterialScope> materialScopes)
        {
            if (preset == null) {
                for (int i = 0; i < localLayers.Count; i++) {
                    if (IsRenderableLayer(localLayers[i])) {
                        results.Add(localLayers[i]);
                        materialScopes?.Add(LayerMaterialScope.Unique);
                    }
                }

                return;
            }

            EnsurePresetOverrideSlots();
            for (int i = 0; i < preset.LayerCount; i++) {
                var presetLayer = preset.GetLayer(i);
                var layerOverride = presetLayerOverrides[i];
                var layer = layerOverride.OverrideLayer ? layerOverride.Layer : presetLayer;
                if (IsRenderableLayer(layer)) {
                    results.Add(layer);
                    materialScopes?.Add(layerOverride.OverrideLayer ? LayerMaterialScope.Unique : LayerMaterialScope.Shared(preset, i, preset.Version));
                }
            }
        }

        private static bool IsRenderableLayer(TextMeshProLayerData layer)
        {
            return layer is { Enabled: true };
        }

        #endregion

        #region Preset Overrides

        private void EnsurePresetOverrideSlots()
        {
            var count = preset != null ? preset.LayerCount : 0;
            while (presetLayerOverrides.Count < count) {
                presetLayerOverrides.Add(new TextMeshProLayerOverride());
            }

            for (int i = 0; i < count; i++) {
                presetLayerOverrides[i] ??= new TextMeshProLayerOverride();
                presetLayerOverrides[i].EnsureLayerCopy(preset.GetLayer(i));
            }

            if (presetLayerOverrides.Count > count) {
                presetLayerOverrides.RemoveRange(count, presetLayerOverrides.Count - count);
            }
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
                layerOverride.CopyFromPreset(preset != null ? preset.GetLayer(index) : null);
                layerOverride.OverrideLayer = true;
            } else {
                layerOverride.OverrideLayer = false;
                layerOverride.EnsureLayerCopy(preset != null ? preset.GetLayer(index) : null);
            }

            SetLayerStackDirty();
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
            if (destination == null) {
                return;
            }

            destination.Clear();
            if (preset == null) {
                for (int i = 0; i < localLayers.Count; i++) {
                    destination.Add(localLayers[i]?.Clone());
                }

                return;
            }

            EnsurePresetOverrideSlots();
            for (int i = 0; i < preset.LayerCount; i++) {
                destination.Add(GetEffectivePresetLayer(i)?.Clone());
            }
        }

        internal void ClearPresetLayerInstances()
        {
            EnsurePresetOverrideSlots();
            for (int i = 0; i < presetLayerOverrides.Count; i++) {
                presetLayerOverrides[i].OverrideLayer = false;
                presetLayerOverrides[i].EnsureLayerCopy(preset != null ? preset.GetLayer(i) : null);
            }

            SetLayerStackDirty();
        }

        #endregion

        #region Runtime Materials

        private void EnsureLayerRuntimeStates(int count)
        {
            while (layerRuntimeStates.Count < count) {
                layerRuntimeStates.Add(new LayerRuntimeState());
            }

            for (int i = count; i < layerRuntimeStates.Count; i++) {
                layerRuntimeStates[i].ReleaseMaterial();
            }

            if (layerRuntimeStates.Count > count) {
                layerRuntimeStates.RemoveRange(count, layerRuntimeStates.Count - count);
            }
        }

        private void ReleaseLayerRuntimeStates()
        {
            for (int i = 0; i < layerRuntimeStates.Count; i++) {
                layerRuntimeStates[i].ReleaseMaterial();
            }

            layerRuntimeStates.Clear();
        }

        private void MarkRuntimeMaterialsDirty()
        {
            for (int i = 0; i < layerRuntimeStates.Count; i++) {
                layerRuntimeStates[i].SetMaterialDirty();
            }
        }

        private void ResetRuntimeState()
        {
            dirtyFlags = DirtyFlags.All;
            textState = default;
            sdfSourceState = default;
            materialContextState = default;
            hasTextState = false;
            hasSdfSourceState = false;
            hasMaterialContextState = false;
            materialSharingAllowedState = true;
            paintBounds = default;
            appliedEffectPaddingBudget = 0f;
            appliedGeometryPaddingLimit = 0f;
            hasPaintBounds = false;
            layerCompositionDirty = true;
        }

        #endregion

        #region Canvas Renderer

        private bool CanReapplyCanvasRenderer()
        {
            return dirtyFlags == DirtyFlags.None
                && !layerCompositionDirty
                && hasAssignedMesh
                && mesh != null
                && hasMaterialContextState
                && layers.Count > 0
                && layerRuntimeStates.Count == layers.Count
                && layerMaterialScopes.Count == layers.Count;
        }

        private void ReapplyCanvasRenderer()
        {
            ApplyCanvasRenderer(text, mesh, layers, layerMaterialScopes, materialContextState, materialSharingAllowedState, ApplyCanvasRendererFlags.All);
        }

        private void ApplyCanvasRenderer(
            TextMeshProUGUI text,
            Mesh mesh,
            IList<TextMeshProLayerData> layers,
            IList<LayerMaterialScope> materialScopes,
            TextMeshProLayerMaterialContext materialContext,
            bool materialSharingAllowed,
            ApplyCanvasRendererFlags applyFlags)
        {
            var canvasRenderer = text.canvasRenderer;
            canvasRenderer.cull = false;
            var forceMeshAssignment = (applyFlags & ApplyCanvasRendererFlags.Mesh) != 0;
            var forceMaterialAssignment = (applyFlags & ApplyCanvasRendererFlags.Materials) != 0;
            var forceTextureAssignment = (applyFlags & ApplyCanvasRendererFlags.Texture) != 0;

            if (forceMeshAssignment || !hasAssignedMesh || appliedMesh != mesh) {
                canvasRenderer.SetMesh(mesh);
                appliedMesh = mesh;
                hasAssignedMesh = true;
            }

            forceMaterialAssignment = forceMaterialAssignment || appliedMaterialCount != layers.Count || canvasRenderer.materialCount != layers.Count;
            if (forceMaterialAssignment) {
                canvasRenderer.materialCount = layers.Count;
                appliedMaterialCount = layers.Count;
            }

            if (forceTextureAssignment || materialContext.FontAtlas != appliedTexture) {
                canvasRenderer.SetTexture(materialContext.FontAtlas);
                appliedTexture = materialContext.FontAtlas;
            }

            EnsureAppliedMaterialSlots(layers.Count);
            var canShareMaterials = materialSharingAllowed && CanSharePresetMaterials(canvasRenderer);
            
            for (int i = 0; i < layers.Count; i++) {
                var layer = GetRenderLayerForSlot(layers, i);
                var materialScope = canShareMaterials ? GetRenderMaterialScopeForSlot(materialScopes, i) : LayerMaterialScope.Unique;
                var material = layerRuntimeStates[i].GetOrCreateMaterial(layer, materialScope, materialContext, name);
                if (forceMaterialAssignment || appliedCanvasMaterials[i] != material) {
                    canvasRenderer.SetMaterial(material, i);
                    appliedCanvasMaterials[i] = material;
                }
            }

            CacheAppliedCanvasState(text, canvasRenderer);
        }

        private void EnsureCanvasShaderChannels()
        {
            CanvasUtility.EnsureChannels(text != null ? text.canvas : null, RequiredCanvasChannels);
        }

        private static bool CanSharePresetMaterials(CanvasRenderer canvasRenderer)
        {
            return canvasRenderer == null
                || (!canvasRenderer.hasRectClipping && canvasRenderer.clippingSoftness == Vector2.zero);
        }

        private void EnsureAppliedMaterialSlots(int count)
        {
            while (appliedCanvasMaterials.Count < count) {
                appliedCanvasMaterials.Add(null);
            }

            for (int i = count; i < appliedCanvasMaterials.Count; i++) {
                appliedCanvasMaterials[i] = null;
            }
        }

        private void ResetCanvasRendererState()
        {
            appliedCanvasMaterials.Clear();
            appliedMaterialCount = -1;
            appliedMesh = null;
            appliedTexture = null;
            hasAssignedMesh = false;
            appliedRootCanvas = null;
            appliedParent = null;
            appliedSiblingIndex = -1;
            appliedAbsoluteDepth = -1;
        }

        #endregion

        #region Canvas Validation

        private static void OnWillRenderCanvasesValidateStacks()
        {
            for (int i = CanvasValidationStacks.Count - 1; i >= 0; i--) {
                var stack = CanvasValidationStacks[i];
                if (stack == null) {
                    CanvasValidationStacks.RemoveAt(i);
                    continue;
                }

                stack.ValidateCanvasRendererForRender();
            }

            if (CanvasValidationStacks.Count == 0) {
                Canvas.willRenderCanvases -= OnWillRenderCanvasesValidateStacks;
            }
        }

        private void RegisterCanvasValidation()
        {
            if (isRegisteredForCanvasValidation) {
                return;
            }

            isRegisteredForCanvasValidation = true;
            CanvasValidationStacks.Add(this);
            if (CanvasValidationStacks.Count == 1) {
                Canvas.willRenderCanvases += OnWillRenderCanvasesValidateStacks;
            }
        }

        private void UnregisterCanvasValidation()
        {
            if (!isRegisteredForCanvasValidation) {
                return;
            }

            isRegisteredForCanvasValidation = false;
            CanvasValidationStacks.Remove(this);
            if (CanvasValidationStacks.Count == 0) {
                Canvas.willRenderCanvases -= OnWillRenderCanvasesValidateStacks;
            }
        }

        private void ValidateCanvasRendererForRender()
        {
            if (!CanReapplyCanvasRenderer() || isQueuedForGraphicRebuild) {
                return;
            }

            if (TryMarkCanvasStateDirty()) {
                MarkDirty(DirtyFlags.Canvas);
                RenderTextMeshProLayers();
                return;
            }

            var canvasRenderer = text != null ? text.canvasRenderer : null;
            if (HasCanvasRendererDrifted(canvasRenderer)) {
                ReapplyCanvasRenderer();
            }
        }

        private bool HasCanvasRendererDrifted(CanvasRenderer canvasRenderer)
        {
            if (canvasRenderer == null) {
                return false;
            }

            if (canvasRenderer.cull || canvasRenderer.materialCount != appliedMaterialCount) {
                return true;
            }

            if (canvasRenderer.GetMesh() != appliedMesh) {
                return true;
            }

            if (appliedCanvasMaterials.Count < appliedMaterialCount) {
                return true;
            }

            for (int i = 0; i < appliedMaterialCount; i++) {
                var appliedMaterial = appliedCanvasMaterials[i];
                if (appliedMaterial == null || canvasRenderer.GetMaterial(i) != appliedMaterial) {
                    return true;
                }
            }

            return false;
        }

        private bool TryMarkCanvasStateDirty()
        {
            if (!hasAssignedMesh || text == null) {
                return false;
            }

            var canvasRenderer = text.canvasRenderer;
            var currentRootCanvas = text.canvas != null ? text.canvas.rootCanvas : null;
            var currentParent = transform.parent;
            var currentSiblingIndex = transform.GetSiblingIndex();
            var currentAbsoluteDepth = canvasRenderer != null ? canvasRenderer.absoluteDepth : -1;

            if (appliedRootCanvas == currentRootCanvas
                && appliedParent == currentParent
                && appliedSiblingIndex == currentSiblingIndex
                && appliedAbsoluteDepth == currentAbsoluteDepth) {
                return false;
            }

            appliedRootCanvas = currentRootCanvas;
            appliedParent = currentParent;
            appliedSiblingIndex = currentSiblingIndex;
            appliedAbsoluteDepth = currentAbsoluteDepth;
            return true;
        }

        private void CacheAppliedCanvasState(TextMeshProUGUI text, CanvasRenderer canvasRenderer)
        {
            appliedRootCanvas = text.canvas != null ? text.canvas.rootCanvas : null;
            appliedParent = transform.parent;
            appliedSiblingIndex = transform.GetSiblingIndex();
            appliedAbsoluteDepth = canvasRenderer != null ? canvasRenderer.absoluteDepth : -1;
        }

        #endregion

        #region Utility

        private static TextMeshProLayerData GetRenderLayerForSlot(IList<TextMeshProLayerData> layers, int materialSlot)
        {
            return layers[layers.Count - 1 - materialSlot];
        }

        private static LayerMaterialScope GetRenderMaterialScopeForSlot(IList<LayerMaterialScope> materialScopes, int materialSlot)
        {
            return materialScopes != null && materialSlot >= 0 && materialSlot < materialScopes.Count
                ? materialScopes[materialScopes.Count - 1 - materialSlot]
                : LayerMaterialScope.Unique;
        }

        private void RestoreTextMeshProRendering()
        {
            if (text == null) {
                return;
            }

            text.SetVerticesDirty();
            text.SetMaterialDirty();
        }

        private void ClearLayerRendering()
        {
            if (!hasAssignedMesh && mesh == null) {
                return;
            }

            ReleaseStackResources();
            RestoreTextMeshProRendering();
            dirtyFlags = DirtyFlags.All;
        }

        #endregion

        #region Mesh and Bounds

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
            hasPaintBounds = false;
            ClearMeshAssignment();
        }

        private void ClearMeshAssignment()
        {
            hasAssignedMesh = false;
            appliedMesh = null;
        }

        private void RefreshPaintBounds(Mesh layerMesh, TextMeshProUGUI text)
        {
            paintBounds = TryGetMeshPaintBounds(layerMesh, out var meshBounds) ? meshBounds : TextMeshProUtility.CalculateBounds(text);
            hasPaintBounds = true;
        }

        private static bool TryGetMeshPaintBounds(Mesh layerMesh, out Vector4 bounds)
        {
            bounds = default;
            if (layerMesh == null || layerMesh.vertexCount == 0) {
                return false;
            }

            var meshBounds = layerMesh.bounds;
            if (meshBounds.size.x <= 0f || meshBounds.size.y <= 0f) {
                return false;
            }

            bounds = CanvasUtility.BoundsFromMinMax(meshBounds.min, meshBounds.max);
            return true;
        }

        #endregion

        #region Nested Types

        [Serializable]
        internal sealed class TextMeshProLayerOverride
        {
            [SerializeField]
            private bool overrideLayer;

            [SerializeField]
            private TextMeshProLayerData layer = TextMeshProLayerData.Default();

            public bool OverrideLayer
            {
                get => overrideLayer;
                set => overrideLayer = value;
            }

            public TextMeshProLayerData Layer => layer;

            internal void EnsureLayerCopy(TextMeshProLayerData source)
            {
                if (layer == null) {
                    layer = source != null ? source.Clone() : TextMeshProLayerData.Default();
                    return;
                }

                if (!overrideLayer && source != null) {
                    layer.CopyFrom(source);
                }
            }

            internal void CopyFromPreset(TextMeshProLayerData source)
            {
                if (layer == null) {
                    layer = source != null ? source.Clone() : TextMeshProLayerData.Default();
                    return;
                }

                if (source != null) {
                    layer.CopyFrom(source);
                }
            }
        }

        private sealed class LayerRuntimeState
        {
            private Material uniqueMaterial;
            private TextMeshProLayerMaterialContext uniqueMaterialContext;
            private bool hasUniqueMaterialContext;
            private bool uniqueMaterialDirty = true;

            private TextMeshProLayerMaterialCache.Entry sharedMaterialEntry;
            private TextMeshProLayerMaterialCacheKey sharedMaterialKey;
            private bool hasSharedMaterialKey;

            internal Material GetOrCreateMaterial(TextMeshProLayerData layer, LayerMaterialScope materialScope, TextMeshProLayerMaterialContext context, string ownerName)
            {
                if (materialScope.CanShare) {
                    return GetOrCreateSharedMaterial(layer, materialScope, context);
                }

                ReleaseSharedMaterial();
                return GetOrCreateUniqueMaterial(layer, context, ownerName);
            }

            private Material GetOrCreateSharedMaterial(TextMeshProLayerData layer, LayerMaterialScope materialScope, TextMeshProLayerMaterialContext context)
            {
                ReleaseUniqueMaterial();

                var key = materialScope.CreateCacheKey(context);
                if (sharedMaterialEntry == null || sharedMaterialEntry.Material == null || !hasSharedMaterialKey || !sharedMaterialKey.Equals(key)) {
                    ReleaseSharedMaterial();
                    sharedMaterialEntry = TextMeshProLayerMaterialCache.Acquire(key, layer, context);
                    sharedMaterialKey = key;
                    hasSharedMaterialKey = true;
                }

                return sharedMaterialEntry.Material;
            }

            private Material GetOrCreateUniqueMaterial(TextMeshProLayerData layer, TextMeshProLayerMaterialContext context, string ownerName)
            {
                if (uniqueMaterial == null) {
                    ReleaseUniqueMaterial();
                    uniqueMaterial = TextMeshProLayerMaterial.CreateMaterial(TextMeshProLayerMaterial.ResolveCoreShader());
                    uniqueMaterial.name = ownerName + " (TextMeshPro Layer Material)";
                }

                if (uniqueMaterialDirty || !hasUniqueMaterialContext || !uniqueMaterialContext.Equals(context)) {
                    layer.ApplyMaterial(uniqueMaterial, context);
                    uniqueMaterialContext = context;
                    hasUniqueMaterialContext = true;
                    uniqueMaterialDirty = false;
                }

                return uniqueMaterial;
            }

            internal void SetMaterialDirty()
            {
                uniqueMaterialDirty = true;
            }

            internal void ReleaseMaterial()
            {
                ReleaseUniqueMaterial();
                ReleaseSharedMaterial();
            }

            private void ReleaseUniqueMaterial()
            {
                CoreUtils.Destroy(uniqueMaterial);
                uniqueMaterial = null;
                uniqueMaterialContext = default;
                hasUniqueMaterialContext = false;
                uniqueMaterialDirty = true;
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
            internal static readonly LayerMaterialScope Unique = new LayerMaterialScope(null, -1, 0);

            private readonly TextMeshProLayerPreset preset;
            private readonly int layerIndex;
            private readonly int presetVersion;

            private LayerMaterialScope(TextMeshProLayerPreset preset, int layerIndex, int presetVersion)
            {
                this.preset = preset;
                this.layerIndex = layerIndex;
                this.presetVersion = presetVersion;
            }

            internal bool CanShare => preset != null && layerIndex >= 0;

            internal static LayerMaterialScope Shared(TextMeshProLayerPreset preset, int layerIndex, int presetVersion)
            {
                return new LayerMaterialScope(preset, layerIndex, presetVersion);
            }

            internal TextMeshProLayerMaterialCacheKey CreateCacheKey(TextMeshProLayerMaterialContext context)
            {
                return new TextMeshProLayerMaterialCacheKey(preset, layerIndex, context);
            }
        }

        #endregion
    }
}
