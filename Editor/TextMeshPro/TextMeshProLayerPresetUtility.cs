using UnityEditor;
using TMPro;

namespace Tripledot.CanvasKit.Editor
{
    internal static class TextMeshProLayerPresetUtility
    {
        internal static TextMeshProLayerPreset CreateFromLocalLayers(TextMeshProLayerStack stack, string assetPath)
        {
            if (stack == null || string.IsNullOrEmpty(assetPath)) {
                return null;
            }

            var layerPreset = UnityEngine.ScriptableObject.CreateInstance<TextMeshProLayerPreset>();
            layerPreset.CopyFrom(stack.LocalLayers, GetStackFontAsset(stack));
            AssetDatabase.CreateAsset(layerPreset, assetPath);
            AssetDatabase.SaveAssets();
            stack.Preset = layerPreset;
            EditorUtility.SetDirty(stack);
            return layerPreset;
        }

        internal static TextMeshProLayerPreset DuplicateEffectivePreset(TextMeshProLayerStack stack, string assetPath)
        {
            if (stack == null || string.IsNullOrEmpty(assetPath)) {
                return null;
            }

            var layerPreset = UnityEngine.ScriptableObject.CreateInstance<TextMeshProLayerPreset>();
            stack.CopyEffectivePresetLayersTo(layerPreset.MutableLayers);
            layerPreset.SetFontAsset(GetStackFontAsset(stack));
            AssetDatabase.CreateAsset(layerPreset, assetPath);
            AssetDatabase.SaveAssets();
            stack.Preset = layerPreset;
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
            return preset != null
                && text != null
                && preset.FontAsset != null
                && text.font != preset.FontAsset;
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
                stack.SetLayerStackDirty();
                EditorUtility.SetDirty(stack);
            }
        }
    }
}
