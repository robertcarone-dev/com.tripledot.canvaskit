using UnityEditor;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    internal sealed class SerializedCanvasPaint
    {
        public readonly SerializedProperty Root;
        public readonly SerializedProperty Type;
        public readonly SerializedProperty GradientMode;
        public readonly SerializedProperty Color;
        public readonly SerializedProperty SecondaryColor;
        public readonly SerializedProperty Opacity;
        public readonly SerializedProperty Gradient;
        public readonly SerializedProperty Texture;
        public readonly SerializedProperty ColorUsesHdrPicker;
        public readonly SerializedProperty SecondaryColorUsesHdrPicker;
        public readonly TransformProperties Transform;

        public SerializedCanvasPaint(SerializedProperty root)
        {
            Root = root;
            Type = root.FindPropertyRelative("Type");
            GradientMode = root.FindPropertyRelative("GradientMode");
            Color = root.FindPropertyRelative("Color");
            SecondaryColor = root.FindPropertyRelative("SecondaryColor");
            Opacity = root.FindPropertyRelative("Opacity");
            Gradient = root.FindPropertyRelative("Gradient");
            Texture = root.FindPropertyRelative("Texture");
            ColorUsesHdrPicker = root.FindPropertyRelative("ColorUsesHdrPicker");
            SecondaryColorUsesHdrPicker = root.FindPropertyRelative("SecondaryColorUsesHdrPicker");
            Transform = new TransformProperties(root.FindPropertyRelative("Transform"));
        }

        public void ResetTransform()
        {
            Transform.Center.vector2Value = new Vector2(0.5f, 0.5f);
            Transform.Offset.vector2Value = Vector2.zero;
            Transform.Scale.vector2Value = Vector2.one;
            Transform.Rotation.floatValue = 0f;
        }

        internal sealed class TransformProperties
        {
            public readonly SerializedProperty Root;
            public readonly SerializedProperty Center;
            public readonly SerializedProperty Offset;
            public readonly SerializedProperty Scale;
            public readonly SerializedProperty Rotation;
            public readonly SerializedProperty WrapMode;

            public TransformProperties(SerializedProperty root)
            {
                Root = root;
                Center = root.FindPropertyRelative("Center");
                Offset = root.FindPropertyRelative("Offset");
                Scale = root.FindPropertyRelative("Scale");
                Rotation = root.FindPropertyRelative("Rotation");
                WrapMode = root.FindPropertyRelative("WrapMode");
            }
        }
    }
}