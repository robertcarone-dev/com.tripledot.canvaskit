using UnityEditor;

namespace Tripledot.CanvasKit.TextMeshPro.Editor
{
    internal readonly struct TextMeshProSerializedPaint
    {
        public readonly SerializedProperty Type;
        public readonly SerializedProperty GradientMode;
        public readonly SerializedProperty Color;
        public readonly SerializedProperty SecondaryColor;
        public readonly SerializedProperty Opacity;
        public readonly SerializedProperty Gradient;
        public readonly SerializedProperty Texture;

        public TextMeshProSerializedPaint(SerializedProperty root)
        {
            Type = root.FindPropertyRelative("Type");
            GradientMode = root.FindPropertyRelative("GradientMode");
            Color = root.FindPropertyRelative("Color");
            SecondaryColor = root.FindPropertyRelative("SecondaryColor");
            Opacity = root.FindPropertyRelative("Opacity");
            Gradient = root.FindPropertyRelative("Gradient");
            Texture = root.FindPropertyRelative("Texture");
        }
    }
}
