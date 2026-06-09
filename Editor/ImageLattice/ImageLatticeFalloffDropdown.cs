using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tripledot.CanvasKit.Editor
{
    [EditorToolbarElement(ToolbarId, typeof(SceneView))]
    internal sealed class ImageLatticeFalloffDropdown : EditorToolbarDropdown
    {
        public const string ToolbarId = "Canvas Kit/Image Lattice/Falloff";
        private const string IconRoot = "Packages/com.tripledot.canvaskit/Editor Default Resources/Icons/ImageLattice/";

        private static readonly string[] IconPaths = {
            IconRoot + "FalloffOffIcon.png",
            IconRoot + "FalloffLinearIcon.png",
            IconRoot + "FalloffSmoothIcon.png"
        };
        private static readonly Texture2D[] Icons = new Texture2D[3];

        public ImageLatticeFalloffDropdown()
        {
            clicked += ShowMenu;
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            UpdateContent();
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            ImageLatticeToolState.SoftSelectionChanged += UpdateContent;
            UpdateContent();
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            ImageLatticeToolState.SoftSelectionChanged -= UpdateContent;
        }

        private void ShowMenu()
        {
            var menu = new GenericMenu();
            AddModeItem(menu, ImageLatticeSoftSelectionMode.Off, "Off");
            AddModeItem(menu, ImageLatticeSoftSelectionMode.Linear, "Linear");
            AddModeItem(menu, ImageLatticeSoftSelectionMode.Smooth, "Smooth");
            menu.DropDown(worldBound);
        }

        private static void AddModeItem(GenericMenu menu, ImageLatticeSoftSelectionMode mode, string label)
        {
            menu.AddItem(new GUIContent(label), ImageLatticeToolState.SoftSelectionMode == mode, () => ImageLatticeToolState.SoftSelectionMode = mode);
        }

        private void UpdateContent()
        {
            text = string.Empty;
            icon = GetIcon(ImageLatticeToolState.SoftSelectionMode);
            tooltip = ImageLatticeToolState.SoftSelectionMode switch {
                ImageLatticeSoftSelectionMode.Linear => $"Falloff: Linear, radius {ImageLatticeToolState.SoftSelectionRadius:0.##}.",
                ImageLatticeSoftSelectionMode.Smooth => $"Falloff: Smooth, radius {ImageLatticeToolState.SoftSelectionRadius:0.##}.",
                _ => "Falloff: Off."
            };
        }

        private static Texture2D GetIcon(ImageLatticeSoftSelectionMode mode)
        {
            var index = (int)mode;
            if (Icons[index] == null) {
                Icons[index] = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPaths[index]);
            }

            return Icons[index];
        }
    }
}
