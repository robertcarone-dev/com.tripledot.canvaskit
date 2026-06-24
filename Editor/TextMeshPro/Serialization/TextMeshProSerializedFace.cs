using UnityEditor;
using Tripledot.CanvasKit.Editor;

namespace Tripledot.CanvasKit.TextMeshPro.Editor
{
    internal sealed class TextMeshProSerializedFace
    {
        public readonly SerializedProperty Root;
        public readonly SerializedProperty Enabled;
        public readonly SerializedProperty PaintRoot;
        public readonly SerializedCanvasPaint Paint;
        public readonly SerializedProperty Dilate;
        public readonly TextMeshProSerializedFaceLighting Lighting;

        public TextMeshProSerializedFace(SerializedProperty root)
        {
            Root = root;
            Enabled = root.FindPropertyRelative("Enabled");
            PaintRoot = root.FindPropertyRelative("Paint");
            Paint = new SerializedCanvasPaint(PaintRoot);
            Dilate = root.FindPropertyRelative("Dilate");
            Lighting = new TextMeshProSerializedFaceLighting(root.FindPropertyRelative("Lighting"));
        }
    }
}
