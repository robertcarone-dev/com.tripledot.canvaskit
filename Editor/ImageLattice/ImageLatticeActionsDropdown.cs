using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    [EditorToolbarElement(ToolbarId, typeof(SceneView))]
    internal sealed class ImageLatticeActionsDropdown : EditorToolbarDropdown
    {
        public const string ToolbarId = "Canvas Kit/Image Lattice/Actions";
        private const string IconPath = "Packages/com.tripledot.canvaskit/Editor Default Resources/Icons/ImageLattice/ControlAction.png";

        private static Texture2D actionIcon;

        public ImageLatticeActionsDropdown()
        {
            clicked += ShowMenu;
            text = string.Empty;
            icon = GetIcon();
            tooltip = "Lattice selection actions.";
        }

        private static Texture2D GetIcon()
        {
            if (actionIcon == null) {
                actionIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            }

            return actionIcon;
        }

        private void ShowMenu()
        {
            var menu = new GenericMenu();
            if (ImageLatticeToolState.HasSelection) {
                menu.AddItem(new GUIContent("Reset Selected"), false, ImageLatticeToolState.ResetSelectedPoints);
                menu.AddItem(new GUIContent("Relax Selected"), false, ImageLatticeToolState.RelaxSelectedPoints);
            } else {
                menu.AddDisabledItem(new GUIContent("Reset Selected"));
                menu.AddDisabledItem(new GUIContent("Relax Selected"));
            }

            menu.DropDown(worldBound);
        }
    }
}
