using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tripledot.CanvasKit.Editor
{
    [EditorToolbarElement(ToolbarId, typeof(SceneView))]
    internal sealed class ImageLatticeEditTargetDropdown : EditorToolbarDropdown
    {
        public const string ToolbarId = "Canvas Kit/Image Lattice/Edit Target";
        private const string IconRoot = "Packages/com.tripledot.canvaskit/Editor Default Resources/Icons/ImageLattice/";

        private static Texture2D pointsIcon;
        private static Texture2D cellsIcon;

        public ImageLatticeEditTargetDropdown()
        {
            clicked += ShowMenu;
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            UpdateContent();
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            ImageLatticeToolState.EditTargetChanged += UpdateContent;
            UpdateContent();
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            ImageLatticeToolState.EditTargetChanged -= UpdateContent;
        }

        private void ShowMenu()
        {
            var menu = new GenericMenu();
            AddMenuItem(menu, ImageLatticeEditTarget.Points, "Points");
            AddMenuItem(menu, ImageLatticeEditTarget.Cells, "Cells");
            menu.DropDown(worldBound);
        }

        private static void AddMenuItem(GenericMenu menu, ImageLatticeEditTarget target, string label)
        {
            menu.AddItem(new GUIContent(label), ImageLatticeToolState.EditTarget == target, () => ImageLatticeToolState.EditTarget = target);
        }

        private void UpdateContent()
        {
            var editTarget = ImageLatticeToolState.EditTarget;
            text = editTarget == ImageLatticeEditTarget.Cells ? "Cells" : "Points";
            icon = GetIcon(editTarget);
            tooltip = editTarget switch {
                ImageLatticeEditTarget.Cells => "Lattice edit target: Cells.",
                _ => "Lattice edit target: Points."
            };
        }

        private static Texture2D GetIcon(ImageLatticeEditTarget editTarget)
        {
            if (editTarget == ImageLatticeEditTarget.Cells) {
                if (cellsIcon == null) {
                    cellsIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconRoot + "ModeCellsIcon.png");
                }

                return cellsIcon;
            }

            if (pointsIcon == null) {
                pointsIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconRoot + "ModePointsIcon.png");
            }

            return pointsIcon;
        }
    }
}
