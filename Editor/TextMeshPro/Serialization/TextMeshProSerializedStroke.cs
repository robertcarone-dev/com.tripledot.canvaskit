using UnityEditor;
using Tripledot.CanvasKit.Editor;

namespace Tripledot.CanvasKit.TextMeshPro.Editor
{
    internal sealed class TextMeshProSerializedStroke
    {
        public readonly SerializedProperty Root;
        public readonly SerializedProperty Enabled;
        public readonly SerializedProperty PaintRoot;
        public readonly SerializedCanvasPaint Paint;
        public readonly SerializedProperty Position;
        public readonly SerializedProperty Width;
        public readonly SerializedProperty Feather;
        public readonly SerializedProperty Offset;

        public TextMeshProSerializedStroke(SerializedProperty root)
        {
            Root = root;
            Enabled = root.FindPropertyRelative("Enabled");
            PaintRoot = root.FindPropertyRelative("Paint");
            Paint = new SerializedCanvasPaint(PaintRoot);
            Position = root.FindPropertyRelative("Position");
            Width = root.FindPropertyRelative("Width");
            Feather = root.FindPropertyRelative("Feather");
            Offset = root.FindPropertyRelative("Offset");
        }
    }
}
