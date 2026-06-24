using UnityEditor;

namespace Tripledot.CanvasKit.TextMeshPro.Editor
{
    internal sealed class TextMeshProSerializedFaceLighting
    {
        public readonly SerializedProperty Root;
        public readonly SerializedProperty Enabled;
        public readonly SerializedProperty BevelWidth;
        public readonly SerializedProperty BevelSoftness;
        public readonly SerializedProperty LightAngle;
        public readonly SerializedProperty HighlightColor;
        public readonly SerializedProperty HighlightColorUsesHdrPicker;
        public readonly SerializedProperty ShadowColor;
        public readonly SerializedProperty ShadowColorUsesHdrPicker;

        public TextMeshProSerializedFaceLighting(SerializedProperty root)
        {
            Root = root;
            Enabled = root.FindPropertyRelative("Enabled");
            BevelWidth = root.FindPropertyRelative("BevelWidth");
            BevelSoftness = root.FindPropertyRelative("BevelSoftness");
            LightAngle = root.FindPropertyRelative("LightAngle");
            HighlightColor = root.FindPropertyRelative("HighlightColor");
            HighlightColorUsesHdrPicker = root.FindPropertyRelative("HighlightColorUsesHdrPicker");
            ShadowColor = root.FindPropertyRelative("ShadowColor");
            ShadowColorUsesHdrPicker = root.FindPropertyRelative("ShadowColorUsesHdrPicker");
        }
    }
}
