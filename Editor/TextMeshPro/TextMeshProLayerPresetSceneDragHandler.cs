using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tripledot.CanvasKit.Editor
{
    [InitializeOnLoad]
    internal static class TextMeshProLayerPresetSceneDragHandler
    {
        private const string UiLayerName = "UI";
        private static readonly Vector2 DefaultTextSize = new Vector2(220f, 80f);

        static TextMeshProLayerPresetSceneDragHandler()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
        }

        public static GameObject CreateTextObjectForPreset(TextMeshProLayerPreset preset, Vector2? sceneMousePosition = null, SceneView sceneView = null)
        {
            return CreateTextObjectForPreset(preset, GetOrCreateCanvas().transform, sceneMousePosition, sceneView);
        }

        public static GameObject CreateTextObjectForPresetInHierarchy(TextMeshProLayerPreset preset, GameObject target)
        {
            return CreateTextObjectForPreset(preset, GetHierarchyParent(target), null, null);
        }

        private static GameObject CreateTextObjectForPreset(TextMeshProLayerPreset preset, Transform parent, Vector2? sceneMousePosition, SceneView sceneView)
        {
            if (preset == null) {
                return null;
            }

            var canvas = parent != null ? parent.GetComponentInParent<Canvas>() : GetOrCreateCanvas();
            parent ??= canvas.transform;
            
            var textObject = ObjectFactory.CreateGameObject(preset.name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(TextMeshProLayerStack));
            Undo.RegisterCreatedObjectUndo(textObject, "Create TextMeshPro Layer Stack");
            GameObjectUtility.SetParentAndAlign(textObject, parent.gameObject);

            var rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = DefaultTextSize;
            rectTransform.anchoredPosition = GetAnchoredPosition(canvas, sceneMousePosition, sceneView);

            var text = textObject.GetComponent<TextMeshProUGUI>();
            if (preset.FontAsset != null) {
                text.font = preset.FontAsset;
            }

            text.text = preset.GetPreviewText();
            text.fontSize = 36f;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.color = Color.white;

            var stack = textObject.GetComponent<TextMeshProLayerStack>();
            stack.Preset = preset;
            stack.SetLayerStackDirty();

            Selection.activeGameObject = textObject;
            EditorSceneManager.MarkSceneDirty(textObject.scene);
            
            return textObject;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            var current = Event.current;
            if (current == null || current.type is not (EventType.DragUpdated or EventType.DragPerform)) {
                return;
            }

            if (!TryGetDraggedPreset(out var preset)) {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (current.type == EventType.DragPerform) {
                DragAndDrop.AcceptDrag();
                CreateTextObjectForPreset(preset, current.mousePosition, sceneView);
            }

            current.Use();
        }

        private static void OnHierarchyGUI(int instanceId, Rect selectionRect)
        {
            var current = Event.current;
            if (current == null
                || current.type is not (EventType.DragUpdated or EventType.DragPerform)
                || !selectionRect.Contains(current.mousePosition)) {
                return;
            }

            if (!TryGetDraggedPreset(out var preset)) {
                return;
            }

#pragma warning disable CS0618
            var target = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
#pragma warning restore CS0618
            if (target == null) {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (current.type == EventType.DragPerform) {
                DragAndDrop.AcceptDrag();
                CreateTextObjectForPresetInHierarchy(preset, target);
            }

            current.Use();
        }

        private static bool TryGetDraggedPreset(out TextMeshProLayerPreset preset)
        {
            preset = null;
            var references = DragAndDrop.objectReferences;
            if (references == null) {
                return false;
            }

            for (var i = 0; i < references.Length; i++) {
                if (references[i] is TextMeshProLayerPreset layerPreset) {
                    preset = layerPreset;
                    return true;
                }
            }

            return false;
        }

        private static Canvas GetOrCreateCanvas()
        {
            var selected = Selection.activeGameObject;
            if (selected != null) {
                var parentCanvas = selected.GetComponentInParent<Canvas>();
                if (parentCanvas != null) {
                    return parentCanvas;
                }
            }

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null) {
                return canvas;
            }

            var canvasObject = ObjectFactory.CreateGameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Canvas");
            canvasObject.layer = LayerMask.NameToLayer(UiLayerName);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            if (Object.FindFirstObjectByType<EventSystem>() == null) {
                var eventSystem = ObjectFactory.CreateGameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
            }

            return canvas;
        }

        private static Transform GetHierarchyParent(GameObject target)
        {
            if (target == null) {
                return GetOrCreateCanvas().transform;
            }

            if (target.GetComponentInParent<Canvas>() != null && target.transform is RectTransform) {
                return target.transform;
            }

            if (target.TryGetComponent(out Canvas targetCanvas)) {
                return targetCanvas.transform;
            }

            var canvasObject = ObjectFactory.CreateGameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Canvas");
            GameObjectUtility.SetParentAndAlign(canvasObject, target);
            canvasObject.layer = LayerMask.NameToLayer(UiLayerName);
            
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            if (Object.FindFirstObjectByType<EventSystem>() == null) {
                var eventSystem = ObjectFactory.CreateGameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
            }

            return canvas.transform;
        }

        private static Vector2 GetAnchoredPosition(Canvas canvas, Vector2? sceneMousePosition, SceneView sceneView)
        {
            if (canvas == null || sceneMousePosition == null || sceneView == null || sceneView.camera == null) {
                return Vector2.zero;
            }

            var canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null) {
                return Vector2.zero;
            }

            var mouse = sceneMousePosition.Value;
            mouse.y = sceneView.camera.pixelHeight - mouse.y;
            
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mouse, sceneView.camera, out var localPoint)
                ? localPoint
                : Vector2.zero;
        }
    }
}
