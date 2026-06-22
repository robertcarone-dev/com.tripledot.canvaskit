using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tripledot.CanvasKit.Editor
{
    [EditorToolbarElement(ToolbarId, typeof(SceneView))]
    internal sealed class ImageLatticeMirrorDropdown : EditorToolbarDropdown
    {
        public const string ToolbarId = "Canvas Kit/Image Lattice/Mirror";
        private const string IconRoot = "Packages/com.tripledot.canvaskit/Editor Default Resources/Icons/ImageLattice/";

        private static readonly string[] IconPaths = {
            IconRoot + "MirrorNoneIcon.png",
            IconRoot + "MirrorXIcon.png",
            IconRoot + "MirrorYIcon.png",
            IconRoot + "MirrorXYIcon.png"
        };
        private static readonly Texture2D[] Icons = new Texture2D[4];

        public ImageLatticeMirrorDropdown()
        {
            clicked += ShowMenu;
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            UpdateContent();
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            ImageLatticeToolState.MirrorModeChanged += UpdateContent;
            UpdateContent();
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            ImageLatticeToolState.MirrorModeChanged -= UpdateContent;
        }

        private void ShowMenu()
        {
            var menu = new GenericMenu();
            AddMenuItem(menu, ImageLatticeMirrorMode.Off, "Mirror Off");
            AddMenuItem(menu, ImageLatticeMirrorMode.Horizontal, "Horizontal");
            AddMenuItem(menu, ImageLatticeMirrorMode.Vertical, "Vertical");
            AddMenuItem(menu, ImageLatticeMirrorMode.Both, "Both");
            menu.DropDown(worldBound);
        }

        private static void AddMenuItem(GenericMenu menu, ImageLatticeMirrorMode mode, string label)
        {
            menu.AddItem(new GUIContent(label), ImageLatticeToolState.MirrorMode == mode, () => ImageLatticeToolState.MirrorMode = mode);
        }

        private void UpdateContent()
        {
            text = string.Empty;
            icon = GetIcon(ImageLatticeToolState.MirrorMode);
            tooltip = GetTooltip(ImageLatticeToolState.MirrorMode);
        }

        private static string GetTooltip(ImageLatticeMirrorMode mode)
        {
            return mode switch {
                ImageLatticeMirrorMode.Vertical => "Lattice mirror: Vertical. Press M to cycle.",
                ImageLatticeMirrorMode.Horizontal => "Lattice mirror: Horizontal. Press M to cycle.",
                ImageLatticeMirrorMode.Both => "Lattice mirror: Both. Press M to cycle.",
                _ => "Lattice mirror: Off. Press M to cycle."
            };
        }

        private static Texture2D GetIcon(ImageLatticeMirrorMode mode)
        {
            var index = (int)mode;
            if (Icons[index] == null) {
                Icons[index] = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPaths[index]);
            }

            return Icons[index];
        }
    }
}