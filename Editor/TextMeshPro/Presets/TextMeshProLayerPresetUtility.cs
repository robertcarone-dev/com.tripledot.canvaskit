using UnityEditor;
using TMPro;
using System.Collections.Generic;

namespace Tripledot.CanvasKit.TextMeshPro.Editor
{
    internal static class TextMeshProLayerPresetUtility
    {
        private static readonly List<TextMeshProLayerData> LayerBuffer = new List<TextMeshProLayerData>();

        internal static bool ApplyProperties(SerializedObject serializedPreset)
        {
            if (!serializedPreset.hasModifiedProperties) {
                return false;
            }
            var preset = (TextMeshProLayerPreset)serializedPreset.targetObject;
            if (!ChangesLayerIdentities(serializedPreset, preset)) {
                return serializedPreset.ApplyModifiedProperties();
            }
            var stacks = UnityEngine.Resources.FindObjectsOfTypeAll<TextMeshProLayerStack>();
            foreach (var stack in stacks) {
                if (stack.Preset == preset) {
                    Undo.RegisterCompleteObjectUndo(stack, "Edit TextMeshPro Layer Preset");
                }
            }
            var changed = serializedPreset.ApplyModifiedProperties();
            foreach (var stack in stacks) {
                if (stack.Preset == preset) {
                    if (stack.EnsurePresetOverrideSlots()) {
                        EditorUtility.SetDirty(stack);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(stack);
                    }
                }
            }
            return changed;
        }

        private static bool ChangesLayerIdentities(SerializedObject serializedPreset, TextMeshProLayerPreset preset)
        {
            var layers = serializedPreset.FindProperty("layers");
            if (layers.arraySize != preset.LayerCount) {
                return true;
            }
            // Reordering changes rendering order, but does not move identity-based override slots.
            var ids = new HashSet<string>();
            for (var i = 0; i < layers.arraySize; i++) {
                if (!ids.Add(layers.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue)) {
                    return true;
                }
            }
            for (var i = 0; i < preset.LayerCount; i++) {
                if (!ids.Contains(preset.GetLayer(i).Id)) {
                    return true;
                }
            }
            return false;
        }

        internal static TextMeshProLayerPreset CreateFromLocalLayers(TextMeshProLayerStack stack, string assetPath)
        {
            if (stack == null || string.IsNullOrEmpty(assetPath)) {
                return null;
            }

            var layerPreset = UnityEngine.ScriptableObject.CreateInstance<TextMeshProLayerPreset>();
            stack.CopyEffectivePresetLayersTo(LayerBuffer);
            layerPreset.CopyFrom(LayerBuffer, GetStackFontAsset(stack));
            AssetDatabase.CreateAsset(layerPreset, assetPath);
            AssetDatabase.SaveAssets();
            Undo.RecordObject(stack, "Assign TextMeshPro Layer Preset");
            stack.Preset = layerPreset;
            stack.EnsurePresetOverrideSlots();
            PrefabUtility.RecordPrefabInstancePropertyModifications(stack);
            EditorUtility.SetDirty(stack);
            return layerPreset;
        }

        internal static TextMeshProLayerPreset DuplicateEffectivePreset(TextMeshProLayerStack stack, string assetPath)
        {
            if (stack == null || string.IsNullOrEmpty(assetPath)) {
                return null;
            }

            var layerPreset = UnityEngine.ScriptableObject.CreateInstance<TextMeshProLayerPreset>();
            stack.CopyEffectivePresetLayersTo(LayerBuffer);
            layerPreset.CopyFrom(LayerBuffer, GetStackFontAsset(stack));

            AssetDatabase.CreateAsset(layerPreset, assetPath);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(stack, "Assign TextMeshPro Layer Preset");
            stack.Preset = layerPreset;
            stack.EnsurePresetOverrideSlots();
            PrefabUtility.RecordPrefabInstancePropertyModifications(stack);
            stack.ClearPresetLayerInstances();
            EditorUtility.SetDirty(stack);

            return layerPreset;
        }

        internal static TMP_FontAsset GetStackFontAsset(TextMeshProLayerStack stack)
        {
            return stack != null && stack.TryGetComponent(out TextMeshProUGUI text) ? text.font : null;
        }

        internal static bool HasFontMismatch(TextMeshProLayerPreset preset, TextMeshProUGUI text)
        {
            return preset != null && 
                   text != null && 
                   preset.FontAsset != null && 
                   text.font != preset.FontAsset;
        }

        internal static void ApplyPresetFont(TextMeshProLayerPreset preset, TextMeshProUGUI text, TextMeshProLayerStack stack)
        {
            if (preset == null || text == null || preset.FontAsset == null) {
                return;
            }

            Undo.RecordObject(text, "Apply TextMeshPro Layer Preset Font");
            text.font = preset.FontAsset;
            text.SetAllDirty();
            EditorUtility.SetDirty(text);

            if (stack != null) {
                Undo.RecordObject(stack, "Apply TextMeshPro Layer Preset Font");
                stack.SetLayerStackDirty(TextMeshProLayerChange.Geometry);
                EditorUtility.SetDirty(stack);
            }
        }
    }
}
