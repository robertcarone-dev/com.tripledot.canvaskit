using UnityEditor;
using UnityEngine;
using TMPro;
using Tripledot.CanvasKit.Editor;
using UnityObject = UnityEngine.Object;

namespace Tripledot.CanvasKit.TextMeshPro.Editor
{
    internal enum TextMeshProLayerSource
    {
        Preset,
        Local,
        LinkedPreset
    }

    internal readonly struct TextMeshProLayerInspectorContext
    {
        public readonly TextMeshProLayerSource Source;
        public readonly string ContextKey;
        public readonly float AvailablePadding;
        public readonly UnityObject SceneTarget;
        public readonly bool ShowPresetModeMarker;

        public TextMeshProLayerInspectorContext(
            TextMeshProLayerSource source,
            string contextKey,
            float availablePadding,
            UnityObject sceneTarget,
            bool showPresetModeMarker)
        {
            Source = source;
            ContextKey = contextKey;
            AvailablePadding = availablePadding;
            SceneTarget = sceneTarget;
            ShowPresetModeMarker = showPresetModeMarker;
        }
    }

    internal readonly struct TextMeshProLayerListRowRects
    {
        public readonly Rect Enabled;
        public readonly Rect Swatch;
        public readonly Rect Label;
        public readonly Rect Trailing;

        public TextMeshProLayerListRowRects(Rect rect, bool hasTrailingControl)
        {
            Enabled = new Rect(rect.x, rect.y, TextMeshProLayerInspectorStyles.EnabledToggleSize, rect.height);
            var swatchSize = Mathf.Min(TextMeshProLayerInspectorStyles.LayerSwatchSize, rect.height);
            Swatch = new Rect(
                Enabled.xMax + TextMeshProLayerInspectorStyles.HeaderControlGap,
                rect.y + (rect.height - swatchSize) * 0.5f,
                swatchSize,
                swatchSize);

            var trailingWidth = hasTrailingControl ? TextMeshProLayerInspectorStyles.TrailingControlWidth : 0f;
            var labelStart = Swatch.xMax + TextMeshProLayerInspectorStyles.HeaderControlGap;
            Label = new Rect(labelStart, rect.y, rect.xMax - labelStart - trailingWidth - 8f, rect.height);
            Trailing = new Rect(rect.xMax - trailingWidth, rect.y, trailingWidth, rect.height);
        }
    }

    internal readonly struct TextMeshProLayerHeaderRects
    {
        public readonly Rect Foldout;
        public readonly Rect Enabled;
        public readonly Rect Swatch;
        public readonly Rect InstanceMarker;
        public readonly Rect Label;

        public TextMeshProLayerHeaderRects(Rect rect, bool showPresetModeMarker)
        {
            Foldout = rect;
            Foldout.x += 2f;
            Foldout.y += Mathf.Floor((rect.height - TextMeshProLayerInspectorStyles.FoldoutSize) * 0.5f);
            Foldout.width = TextMeshProLayerInspectorStyles.FoldoutSize;
            Foldout.height = TextMeshProLayerInspectorStyles.FoldoutSize;

            Enabled = rect;
            Enabled.x = Foldout.xMax + 4f;
            Enabled.y += Mathf.Floor((rect.height - TextMeshProLayerInspectorStyles.EnabledToggleSize) * 0.5f);
            Enabled.width = TextMeshProLayerInspectorStyles.EnabledToggleSize;
            Enabled.height = TextMeshProLayerInspectorStyles.EnabledToggleSize;

            Swatch = rect;
            Swatch.x = Enabled.xMax + TextMeshProLayerInspectorStyles.HeaderControlGap;
            Swatch.y += Mathf.Floor((rect.height - TextMeshProLayerInspectorStyles.LayerSwatchSize) * 0.5f);
            Swatch.width = TextMeshProLayerInspectorStyles.LayerSwatchSize;
            Swatch.height = TextMeshProLayerInspectorStyles.LayerSwatchSize;

            InstanceMarker = showPresetModeMarker
                ? new Rect(
                    rect.xMax - 8f - TextMeshProLayerInspectorStyles.InstanceMarkerSize,
                    rect.y + Mathf.Floor((rect.height - TextMeshProLayerInspectorStyles.InstanceMarkerSize) * 0.5f),
                    TextMeshProLayerInspectorStyles.InstanceMarkerSize,
                    TextMeshProLayerInspectorStyles.InstanceMarkerSize)
                : Rect.zero;

            var labelStart = Swatch.xMax + 8f;
            var labelEnd = showPresetModeMarker
                ? InstanceMarker.xMin - TextMeshProLayerInspectorStyles.HeaderControlGap
                : rect.xMax - 8f;
            Label = new Rect(labelStart, rect.y, Mathf.Max(0f, labelEnd - labelStart), rect.height);
        }
    }

    internal sealed class TextMeshProLayerInspectorState
    {
        private readonly SerializedObject serializedObject;
        private readonly TextMeshProLayerStack stack;
        private readonly TextMeshProLayerPreset preset;

        private TextMeshProLayerPreset linkedPreset;
        private SerializedObject linkedPresetObject;
        private SerializedProperty linkedPresetLayers;

        private TextMeshProLayerInspectorState(
            TextMeshProLayerStack stack,
            TextMeshProLayerPreset preset,
            SerializedObject serializedObject,
            SerializedProperty presetProperty,
            SerializedProperty layers,
            SerializedProperty presetLayerOverrides)
        {
            this.stack = stack;
            this.preset = preset;
            this.serializedObject = serializedObject;
            PresetProperty = presetProperty;
            Layers = layers;
            PresetLayerOverrides = presetLayerOverrides;
        }

        public SerializedProperty Layers { get; }
        public SerializedProperty PresetProperty { get; }
        public SerializedProperty PresetLayerOverrides { get; }
        public SerializedObject LinkedPresetObject => linkedPresetObject;
        public SerializedProperty LinkedPresetLayers => linkedPresetLayers;

        public static TextMeshProLayerInspectorState ForPreset(
            TextMeshProLayerPreset preset,
            SerializedObject presetObject,
            SerializedProperty presetLayers)
        {
            return new TextMeshProLayerInspectorState(null, preset, presetObject, null, presetLayers, null);
        }

        public static TextMeshProLayerInspectorState ForStack(
            TextMeshProLayerStack stack,
            SerializedObject stackObject,
            SerializedProperty presetProperty,
            SerializedProperty localLayers,
            SerializedProperty presetLayerOverrides)
        {
            return new TextMeshProLayerInspectorState(stack, null, stackObject, presetProperty, localLayers, presetLayerOverrides);
        }

        public void ClearLinkedPresetCache()
        {
            linkedPreset = null;
            linkedPresetObject = null;
            linkedPresetLayers = null;
        }

        public void EnsureLinkedPresetCache(TextMeshProLayerPreset assignedPreset)
        {
            if (linkedPreset == assignedPreset && linkedPresetObject != null && linkedPresetLayers != null) {
                return;
            }

            linkedPreset = assignedPreset;
            linkedPresetObject = new SerializedObject(assignedPreset);
            linkedPresetLayers = linkedPresetObject.FindProperty("layers");
        }

        public void EnsureOverrideArraySize(int size)
        {
            if (PresetLayerOverrides.arraySize == size) {
                return;
            }

            PresetLayerOverrides.arraySize = size;
            serializedObject.ApplyModifiedProperties();
        }

        public void ApplyLinkedPresetProperties(TextMeshProLayerPreset assignedPreset)
        {
            if (linkedPresetObject.ApplyModifiedProperties()) {
                EditorUtility.SetDirty(assignedPreset);
            }
        }

        public SerializedProperty GetLayer(SerializedProperty layers, TextMeshProLayerSource source, int index)
        {
            var sourceLayer = layers.GetArrayElementAtIndex(index);
            return source == TextMeshProLayerSource.LinkedPreset ? GetLinkedRowLayer(index, sourceLayer) : sourceLayer;
        }

        public TextMeshProLayerInspectorContext CreateDrawContext(TextMeshProLayerSource source)
        {
            return new TextMeshProLayerInspectorContext(
                source,
                GetContextKey(source),
                GetAvailablePadding(),
                stack,
                source == TextMeshProLayerSource.LinkedPreset);
        }

        public bool IsLinkedPresetInstanceLayer(int index)
        {
            return stack.IsPresetLayerInstance(index);
        }

        public void SetInstanceMode(int index, bool instance)
        {
            var overrideEnabled = PresetLayerOverrides.GetArrayElementAtIndex(index).FindPropertyRelative("overrideLayer");
            if (overrideEnabled.boolValue == instance) {
                return;
            }

            PresetLayerOverrides.serializedObject.ApplyModifiedProperties();
            stack.SetPresetLayerInstance(index, instance);
            PresetLayerOverrides.serializedObject.Update();
        }

        private SerializedProperty GetLinkedRowLayer(int index, SerializedProperty sourceLayer)
        {
            var layerOverride = PresetLayerOverrides.GetArrayElementAtIndex(index);
            var overrideEnabled = layerOverride.FindPropertyRelative("overrideLayer");
            return overrideEnabled.boolValue ? layerOverride.FindPropertyRelative("layer") : sourceLayer;
        }

        private string GetContextKey(TextMeshProLayerSource source)
        {
            if (source == TextMeshProLayerSource.LinkedPreset) {
                return "TextMeshProLayerStack." + stack.GetInstanceID() + ".Linked." + linkedPreset.GetInstanceID();
            }

            if (source == TextMeshProLayerSource.Local) {
                return "TextMeshProLayerStack." + stack.GetInstanceID() + ".Local";
            }

            return "TextMeshProLayerPreset." + preset.GetInstanceID();
        }

        private float GetAvailablePadding()
        {
            if (stack != null && stack.TryGetComponent(out TextMeshProUGUI text)) {
                return TextMeshProUtility.CalculateAvailablePadding(text);
            }

            return CanvasEditorGUI.Styles.DefaultSdfSliderPadding;
        }
    }
}
