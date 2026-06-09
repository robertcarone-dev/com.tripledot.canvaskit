using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tripledot.CanvasKit.Editor
{
    internal static class ImageLatticeMenu
    {
        private const int MenuPriority = 2002;
        private const string UiLayerName = "UI";
        private const string StandardSpritePath = "UI/Skin/UISprite.psd";

        [MenuItem("GameObject/UI/Image Lattice", false, MenuPriority)]
        private static void CreateImageLattice(MenuCommand command)
        {
            var parent = ResolveParent(command);
            var gameObject = ObjectFactory.CreateGameObject("Image Lattice", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ImageLattice));
            gameObject.layer = LayerMask.NameToLayer(UiLayerName);

            var rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(100f, 100f);

            var image = gameObject.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(StandardSpritePath);

            Undo.RegisterCreatedObjectUndo(gameObject, "Create Image Lattice");
            GameObjectUtility.SetParentAndAlign(gameObject, parent);
            Selection.activeGameObject = gameObject;
        }

        private static GameObject ResolveParent(MenuCommand command)
        {
            var parent = command.context as GameObject;
            if (parent == null && Selection.activeGameObject != null &&
                Selection.activeGameObject.GetComponentInParent<Canvas>() != null) {
                parent = Selection.activeGameObject;
            }

            if (parent == null) {
                var canvas = Object.FindFirstObjectByType<Canvas>();
                parent = canvas != null ? canvas.gameObject : CreateCanvas(null);
            } else if (parent.GetComponentInParent<Canvas>() == null) {
                parent = CreateCanvas(parent);
            }

            EnsureEventSystem();
            return parent;
        }

        private static GameObject CreateCanvas(GameObject parent)
        {
            var canvasObject = ObjectFactory.CreateGameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.layer = LayerMask.NameToLayer(UiLayerName);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            if (parent != null) {
                GameObjectUtility.SetParentAndAlign(canvasObject, parent);
            }

            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Canvas");
            return canvasObject;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) {
                return;
            }

            var eventSystemObject = ObjectFactory.CreateGameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystemObject, "Create EventSystem");
        }
    }
}
