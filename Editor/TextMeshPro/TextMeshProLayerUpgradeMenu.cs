using TMPro;
using UnityEditor;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    internal static class TextMeshProLayerUpgradeMenu
    {
        private const string UpgradeSelectedMenu = "GameObject/UI (Canvas)/TextMeshPro - Upgrade To Layer Stack";
        private const string CreatePresetMenu = "Assets/TextMeshPro/Upgrade TMP Material To Layer Stack Preset";

        [MenuItem("CONTEXT/TextMeshProUGUI/Upgrade to TMP Layer Stack")]
        private static void UpgradeContext(MenuCommand command)
        {
            if (command.context is TextMeshProUGUI text) {
                UpgradeText(text);
            }
        }

        [MenuItem(UpgradeSelectedMenu)]
        private static void UpgradeSelected()
        {
            var texts = Selection.GetFiltered<TextMeshProUGUI>(SelectionMode.Editable | SelectionMode.Deep);
            for (int i = 0; i < texts.Length; i++) {
                UpgradeText(texts[i]);
            }
        }

        [MenuItem(UpgradeSelectedMenu, true)]
        private static bool CanUpgradeSelected()
        {
            return Selection.GetFiltered<TextMeshProUGUI>(SelectionMode.Editable | SelectionMode.Deep).Length > 0;
        }

        [MenuItem(CreatePresetMenu)]
        private static void CreatePresetFromSelectedMaterial()
        {
            var material = Selection.activeObject as Material;
            if (material == null) {
                return;
            }

            var preset = TextMeshProLayerUpgradeUtility.CreatePresetFromMaterial(material, TextMeshProLayerUpgradeUtility.GetPresetAssetPath(material));
            if (preset == null) {
                return;
            }

            Selection.activeObject = preset;
            EditorGUIUtility.PingObject(preset);
        }

        [MenuItem(CreatePresetMenu, true)]
        private static bool CanCreatePresetFromSelectedMaterial()
        {
            return Selection.activeObject is Material material && material.HasProperty("_FaceColor");
        }

        private static void UpgradeText(TextMeshProUGUI text)
        {
            if (text == null) {
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Upgrade TextMeshPro Layer Stack");
            Undo.RecordObject(text, "Upgrade TextMeshPro Layer Stack");
            var stack = text.GetComponent<TextMeshProLayerStack>();
            if (stack == null) {
                Undo.AddComponent<TextMeshProLayerStack>(text.gameObject);
            } else {
                Undo.RecordObject(stack, "Upgrade TextMeshPro Layer Stack");
            }

            TextMeshProLayerUpgradeUtility.UpgradeText(text);
        }
    }
}
