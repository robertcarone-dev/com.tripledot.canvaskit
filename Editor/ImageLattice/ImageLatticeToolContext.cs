using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Overlays;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    [CustomEditor(typeof(ImageLatticeToolContext))]
    internal sealed class ImageLatticeToolContextEditor : UnityEditor.Editor, ICreateToolbar
    {
        public IEnumerable<string> toolbarElements
        {
            get
            {
                yield return ImageLatticeEditTargetDropdown.ToolbarId;
                yield return ImageLatticeFalloffDropdown.ToolbarId;
                yield return ImageLatticeMirrorDropdown.ToolbarId;
                yield return ImageLatticeActionsDropdown.ToolbarId;
            }
        }
    }
    
    internal sealed class ImageLatticeShortcutContext : IShortcutContext
    {
        public bool active => ImageLatticeToolState.IsToolActive;
    }
    
    [EditorToolContext("Image Lattice", typeof(ImageLattice))]
    [Icon("Packages/com.tripledot.canvaskit/Editor Default Resources/Icons/ImageLattice/ComponentIcon.png")]
    internal sealed class ImageLatticeToolContext : EditorToolContext
    {
        [Shortcut("Canvas Kit/Image Lattice/Cycle Mirror Mode", typeof(ImageLatticeShortcutContext), KeyCode.M)]
        private static void CycleMirrorModeShortcut(ShortcutArguments args)
        {
            if (!ImageLatticeToolState.IsToolActive) {
                return;
            }

            ImageLatticeToolState.CycleMirrorMode();
        }

        [Shortcut("Canvas Kit/Image Lattice/Decrease Falloff Radius", typeof(ImageLatticeShortcutContext), KeyCode.LeftBracket)]
        private static void DecreaseFalloffRadiusShortcut(ShortcutArguments args)
        {
            if (!ImageLatticeToolState.IsToolActive) {
                return;
            }

            ImageLatticeToolState.AdjustSoftSelectionRadius(-ImageLatticeToolState.RadiusShortcutStep);
        }

        [Shortcut("Canvas Kit/Image Lattice/Increase Falloff Radius", typeof(ImageLatticeShortcutContext), KeyCode.RightBracket)]
        private static void IncreaseFalloffRadiusShortcut(ShortcutArguments args)
        {
            if (!ImageLatticeToolState.IsToolActive) {
                return;
            }

            ImageLatticeToolState.AdjustSoftSelectionRadius(ImageLatticeToolState.RadiusShortcutStep);
        }

        [ClutchShortcut("Canvas Kit/Image Lattice/Adjust Falloff Radius", typeof(ImageLatticeShortcutContext), KeyCode.B)]
        private static void AdjustFalloffRadiusShortcut(ShortcutArguments args)
        {
            if (args.stage == ShortcutStage.End) {
                ImageLatticeToolState.SetRadiusClutchActive(false);
                return;
            }

            if (!ImageLatticeToolState.IsToolActive) {
                return;
            }

            ImageLatticeToolState.SetRadiusClutchActive(true);
        }
        
        private ImageLatticeShortcutContext shortcutContext;
        
        public override void OnActivated()
        {
            base.OnActivated();
            
            if (target is ImageLattice image) {
                ImageLatticeToolState.SetActiveImage(image);
            }
            
            shortcutContext = new ImageLatticeShortcutContext();
            ShortcutManager.RegisterContext(shortcutContext);

            SceneView.RepaintAll();
        }

        public override void OnWillBeDeactivated()
        {
            if (target is ImageLattice image) {
                ImageLatticeToolState.ClearActiveImage(image);
            } else {
                ImageLatticeToolState.ClearSelection();
                ImageLatticeToolState.EndUndo();
            }
            
            ShortcutManager.UnregisterContext(shortcutContext);

            base.OnWillBeDeactivated();
            SceneView.RepaintAll();
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (target is ImageLattice image) {
                ImageLatticeToolState.SetActiveImage(image);
            }
        }

        internal Type ResolveEditorToolType(Tool tool)
        {
            return GetEditorToolType(tool);
        }

        protected override Type GetEditorToolType(Tool tool)
        {
            return tool switch {
                Tool.Move => typeof(ImageLatticeMoveTool),
                Tool.Rotate => typeof(ImageLatticeRotateTool),
                Tool.Scale => typeof(ImageLatticeScaleTool),
                Tool.Rect => typeof(ImageLatticeRectTool),
                Tool.Transform => typeof(ImageLatticeTransformTool),
                _ => base.GetEditorToolType(tool)
            };
        }
    }
}
