using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    internal static class TextMeshProLayerPresetCreationUtility
    {
        private const string CreatePresetMenu = "Assets/Create/TextMeshPro/Layer Stack Preset";
        private const string DefaultPresetFileName = "TextMeshProLayerPreset.asset";
        private const string PresetFileNameSuffix = " New.asset";

        [MenuItem(CreatePresetMenu, false, 112)]
        private static void CreateLayerPreset()
        {
            var selectedMaterial = GetSelectedMaterialPreset(Selection.objects);
            var selectedFont = selectedMaterial != null
                ? TextMeshProLayerUpgradeUtility.ResolveFontAsset(selectedMaterial) ?? GetSelectedFontAsset(Selection.objects)
                : GetSelectedFontAsset(Selection.objects);

            var action = ScriptableObject.CreateInstance<CreateLayerPresetEndNameEditAction>();
            action.FontAsset = selectedFont;
            action.MaterialPreset = selectedMaterial;

            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                instanceID: action.GetInstanceID(),
                endAction: action,
                pathName: GetDefaultAssetPathForSelection(selectedMaterial != null ? selectedMaterial : selectedFont != null ? selectedFont : Selection.activeObject),
                icon: null,
                resourceFile: null);
        }

        internal static string GetDefaultAssetPathForSelection(Object selection)
        {
            var selectedFont = selection as TMP_FontAsset;
            var selectedMaterial = selection as Material;
            var selectedFontPath = selectedFont != null ? AssetDatabase.GetAssetPath(selectedFont) : null;
            var selectedMaterialPath = selectedMaterial != null ? AssetDatabase.GetAssetPath(selectedMaterial) : null;

            var sourcePath = !string.IsNullOrEmpty(selectedMaterialPath) ? selectedMaterialPath : selectedFontPath;
            var directory = !string.IsNullOrEmpty(sourcePath) ? Path.GetDirectoryName(sourcePath)?.Replace('\\', '/') : GetSelectedDirectory(selection);

            if (string.IsNullOrEmpty(directory)) {
                directory = "Assets";
            }

            return AssetDatabase.GenerateUniqueAssetPath(directory + "/" + GetPresetFileName(selectedFont, selectedMaterial, sourcePath));
        }

        private static string GetSelectedDirectory(Object selection)
        {
            var assetPath = selection != null ? AssetDatabase.GetAssetPath(selection) : null;
            if (string.IsNullOrEmpty(assetPath)) {
                return "Assets";
            }

            return AssetDatabase.IsValidFolder(assetPath)
                ? assetPath
                : Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        }

        private static string GetPresetFileName(TMP_FontAsset selectedFont, Material selectedMaterial, string sourcePath)
        {
            var sourceName = !string.IsNullOrEmpty(sourcePath)
                ? Path.GetFileNameWithoutExtension(sourcePath)
                : selectedMaterial != null
                    ? selectedMaterial.name
                    : selectedFont != null
                        ? selectedFont.name
                        : null;

            return string.IsNullOrWhiteSpace(sourceName)
                ? DefaultPresetFileName
                : sourceName + PresetFileNameSuffix;
        }

        internal static TextMeshProLayerPreset CreatePresetAtPath(string assetPath, TMP_FontAsset selectedFont, Material materialPreset = null)
        {
            if (string.IsNullOrEmpty(assetPath)) {
                return null;
            }

            var preset = ScriptableObject.CreateInstance<TextMeshProLayerPreset>();
            if (materialPreset != null) {
                preset.CopyFrom(TextMeshProLayerUpgradeUtility.ConvertMaterial(materialPreset), TextMeshProLayerUpgradeUtility.ResolveFontAsset(materialPreset) ?? selectedFont);
            } else {
                preset.CopyFrom(new[] { TextMeshProLayerData.Default() }, selectedFont);
            }

            AssetDatabase.CreateAsset(preset, AssetDatabase.GenerateUniqueAssetPath(assetPath));
            AssetDatabase.SaveAssets();
            return preset;
        }

        private static TMP_FontAsset GetSelectedFontAsset(Object[] selections)
        {
            if (selections == null) {
                return null;
            }

            for (var i = 0; i < selections.Length; i++) {
                if (selections[i] is TMP_FontAsset fontAsset) {
                    return fontAsset;
                }
            }

            return null;
        }

        private static Material GetSelectedMaterialPreset(Object[] selections)
        {
            if (selections == null) {
                return null;
            }

            for (var i = 0; i < selections.Length; i++) {
                if (selections[i] is Material material && material.HasProperty("_FaceColor")) {
                    return material;
                }
            }

            return null;
        }

        private sealed class CreateLayerPresetEndNameEditAction : EndNameEditAction
        {
            public TMP_FontAsset FontAsset;
            public Material MaterialPreset;

            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                var preset = CreatePresetAtPath(pathName, FontAsset, MaterialPreset);
                if (preset == null) {
                    return;
                }

                Selection.activeObject = preset;
                ProjectWindowUtil.ShowCreatedAsset(preset);
                EditorGUIUtility.PingObject(preset);
            }
        }
    }
}