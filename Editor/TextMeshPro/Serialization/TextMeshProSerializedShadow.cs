using UnityEditor;
using Tripledot.CanvasKit.Editor;

namespace Tripledot.CanvasKit.TextMeshPro.Editor
{
    internal sealed class TextMeshProSerializedShadow
    {
        public readonly SerializedProperty Root;
        public readonly SerializedProperty Enabled;
        public readonly SerializedProperty PaintRoot;
        public readonly SerializedCanvasPaint Paint;
        public readonly SerializedProperty Offset;
        public readonly SerializedProperty Blur;
        public readonly SerializedProperty Spread;

        public TextMeshProSerializedShadow(SerializedProperty root)
        {
            Root = root;
            Enabled = root.FindPropertyRelative("Enabled");
            PaintRoot = root.FindPropertyRelative("Paint");
            Paint = new SerializedCanvasPaint(PaintRoot);
            Offset = root.FindPropertyRelative("Offset");
            Blur = root.FindPropertyRelative("Blur");
            Spread = root.FindPropertyRelative("Spread");
        }
    }
}
