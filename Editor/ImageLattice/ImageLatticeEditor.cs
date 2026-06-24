using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.UI;

namespace Tripledot.CanvasKit.Editor
{
    [CustomEditor(typeof(ImageLattice))]
    internal sealed class ImageLatticeEditor : UnityEditor.Editor
    {
        private SerializedProperty controlColumns;
        private SerializedProperty controlRows;
        private SerializedProperty segmentsPerCell;
        private SerializedProperty raycastMode;
        private Image[] targetImages;

        private ImageLattice TargetImage => (ImageLattice)target;

        private void OnEnable()
        {
            targetImages = new Image[targets.Length];
            for (var i = 0; i < targets.Length; i++) {
                targetImages[i] = ((ImageLattice)targets[i]).GetComponent<Image>();
            }

            controlColumns = serializedObject.FindProperty("controlColumns");
            controlRows = serializedObject.FindProperty("controlRows");
            segmentsPerCell = serializedObject.FindProperty("segmentsPerCell");
            raycastMode = serializedObject.FindProperty("raycastMode");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawWarnings();
            
            var dirtyFlags = DrawLatticeControls();
            var changed = serializedObject.ApplyModifiedProperties();
            if (changed) {
                MarkTargetsDirty(dirtyFlags);
            }
        }

        private void DrawWarnings()
        {
            if (AnyUnsupportedImageType()) {
                EditorGUILayout.HelpBox(
                    "Image Lattice modifies Simple Image type only. Other Image types render normally until changed to Simple.",
                    MessageType.Warning);
            }

            if (AnyExplicitNonLatticeMaterial()) {
                EditorGUILayout.HelpBox(
                    "The assigned Image material does not use a Canvas Kit lattice shader, so lattice deformation will not render. Use the default material, UI/Tripledot/Image Lattice, or a Canvas Image Lattice Shader Graph.",
                    MessageType.Warning);
            }

            if (AnySpriteMeshEnabled()) {
                EditorGUILayout.HelpBox(
                    "Image Lattice ignores tight sprite mesh rendering and generates its own tessellated mesh.",
                    MessageType.Info);
            }

            if (AnyDeformedRaycastWithAlphaHitTest()) {
                EditorGUILayout.HelpBox(
                    "Deformed Visible Area raycasts and Image alpha hit testing should not be combined. Unity's Image alpha filter samples undeformed UVs before the lattice filter runs.",
                    MessageType.Warning);
            }
        }

        private DirtyFlags DrawLatticeControls()
        {
            var dirtyFlags = DirtyFlags.None;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.IntSlider(controlColumns, ImageLattice.MinControlPointsPerAxis, ImageLattice.MaxControlPointsPerAxis, Styles.ControlColumns);
            EditorGUILayout.IntSlider(controlRows, ImageLattice.MinControlPointsPerAxis, ImageLattice.MaxControlPointsPerAxis, Styles.ControlRows);
            if (EditorGUI.EndChangeCheck()) {
                dirtyFlags |= DirtyFlags.Mesh | DirtyFlags.MaterialPayload;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.IntSlider(segmentsPerCell, ImageLattice.MinSegmentsPerCell, ImageLattice.MaxSegmentsPerCell, Styles.SegmentsPerCell);
            if (EditorGUI.EndChangeCheck()) {
                dirtyFlags |= DirtyFlags.Mesh;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(raycastMode, Styles.RaycastMode);
            EditorGUI.EndChangeCheck();

            using (new EditorGUILayout.HorizontalScope()) {
                using (new EditorGUI.DisabledScope(targets.Length != 1)) {
                    var editing = targets.Length == 1 && ImageLatticeToolState.IsEditing(TargetImage);
                    var label = editing ? Styles.EditingLattice : Styles.EditLattice;
                    if (GUILayout.Button(label, EditorStyles.miniButton)) {
                        serializedObject.ApplyModifiedProperties();
                        if (editing) {
                            ImageLatticeToolState.StopEditing(TargetImage);
                        } else {
                            ToolManager.SetActiveContext<ImageLatticeToolContext>();
                            Tools.current = Tool.Move;
                            ImageLatticeToolState.SetActiveImage(TargetImage);
                        }
                    }
                }

                if (GUILayout.Button(Styles.ResetLattice, EditorStyles.miniButton)) {
                    ResetSelectedLattices();
                }
            }

            return dirtyFlags;
        }

        private void ResetSelectedLattices()
        {
            serializedObject.ApplyModifiedProperties();
            foreach (var target in targets) {
                if (target is ImageLattice image) {
                    Undo.RecordObject(image, "Reset Lattice");
                    image.ResetLattice();
                    ImageLatticeToolState.NotifyImageChanged(image);
                    EditorUtility.SetDirty(image);
                }
            }

            serializedObject.Update();
            SceneView.RepaintAll();
        }

        private void MarkTargetsDirty(DirtyFlags dirtyFlags)
        {
            for (var i = 0; i < targets.Length; i++) {
                if (targets[i] is not ImageLattice image) {
                    continue;
                }

                if ((dirtyFlags & DirtyFlags.Mesh) != 0) {
                    targetImages[i].SetVerticesDirty();
                }

                if ((dirtyFlags & DirtyFlags.MaterialPayload) != 0) {
                    image.UpdateRuntimeMaterialPayloadOrDirtyImage();
                }

                EditorUtility.SetDirty(image);
                ImageLatticeToolState.NotifyImageChanged(image);
            }

            SceneView.RepaintAll();
        }

        private bool AnyUnsupportedImageType()
        {
            foreach (var image in targetImages) {
                if (image.type != Image.Type.Simple) {
                    return true;
                }
            }

            return false;
        }

        private bool AnyExplicitNonLatticeMaterial()
        {
            foreach (var image in targetImages) {
                if (HasExplicitMaterial(image) && !ImageLattice.IsLatticeMaterial(image.material)) {
                    return true;
                }
            }

            return false;
        }

        private bool AnySpriteMeshEnabled()
        {
            foreach (var image in targetImages) {
                if (image.type == Image.Type.Simple && image.useSpriteMesh) {
                    return true;
                }
            }

            return false;
        }

        private bool AnyDeformedRaycastWithAlphaHitTest()
        {
            for (var i = 0; i < targets.Length; i++) {
                if (targets[i] is not ImageLattice image || image.RaycastMode != ImageLatticeRaycastMode.DeformedVisibleArea) {
                    continue;
                }

                var targetImage = targetImages[i];
                if (targetImage.alphaHitTestMinimumThreshold > 0f) {
                    return true;
                }
            }

            return false;
        }

        private static bool HasExplicitMaterial(Image graphicImage)
        {
            var material = graphicImage.material;
            return material != graphicImage.defaultMaterial &&
                   material != Image.defaultETC1GraphicMaterial;
        }

        [System.Flags]
        private enum DirtyFlags
        {
            None = 0,
            Mesh = 1,
            MaterialPayload = 2
        }

        private static class Styles
        {
            public static readonly GUIContent ControlColumns = L10n.TextContent("Control Columns", "Number of editable lattice control-point columns across the image.");
            public static readonly GUIContent ControlRows = L10n.TextContent("Control Rows", "Number of editable lattice control-point rows down the image.");
            public static readonly GUIContent SegmentsPerCell = L10n.TextContent("Segments Per Cell", "Mesh segments generated between neighboring control points. Higher values render smoother curves with more vertices.");
            public static readonly GUIContent RaycastMode = L10n.TextContent("Raycast Mode", "Choose whether raycasts use Unity's Image behavior or the current deformed lattice shape.");
            public static readonly GUIContent EditLattice = L10n.TextContent("Edit Lattice", "Activate the Image Lattice tool context.");
            public static readonly GUIContent EditingLattice = L10n.TextContent("Editing Lattice", "Image Lattice tool context is active.");
            public static readonly GUIContent ResetLattice = L10n.TextContent("Reset", "Reset lattice points to an undeformed grid.");
        }
    }
}