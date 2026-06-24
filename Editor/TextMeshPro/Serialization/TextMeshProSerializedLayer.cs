using UnityEditor;

namespace Tripledot.CanvasKit.TextMeshPro.Editor
{
    internal sealed class TextMeshProSerializedLayer
    {
        public readonly SerializedProperty Root;
        public readonly SerializedProperty Enabled;
        public readonly SerializedProperty Label;
        public readonly SerializedProperty BlendMode;
        public readonly SerializedProperty Opacity;
        public readonly TextMeshProSerializedFace Face;
        public readonly TextMeshProSerializedStroke Stroke;
        public readonly TextMeshProSerializedShadow Shadow;

        public TextMeshProSerializedLayer(SerializedProperty root)
        {
            Root = root;
            Enabled = root.FindPropertyRelative("enabled");
            Label = root.FindPropertyRelative("label");
            BlendMode = root.FindPropertyRelative("blendMode");
            Opacity = root.FindPropertyRelative("opacity");
            Face = new TextMeshProSerializedFace(root.FindPropertyRelative("face"));
            Stroke = new TextMeshProSerializedStroke(root.FindPropertyRelative("stroke"));
            Shadow = new TextMeshProSerializedShadow(root.FindPropertyRelative("shadow"));
        }

        public bool IsDisabled => Enabled is { hasMultipleDifferentValues: false, boolValue: false };

        public string DisplayLabel {
            get {
                if (!string.IsNullOrWhiteSpace(Label.stringValue)) {
                    return Label.stringValue.Trim();
                }

                return TextMeshProLayerInspectorStyles.Layer.text;
            }
        }

        public TextMeshProLayerFeatureFlags FeatureFlags {
            get {
                var flags = TextMeshProLayerFeatureFlags.None;
                if (Face.Enabled.boolValue) {
                    flags |= TextMeshProLayerFeatureFlags.Face;
                }

                if (Stroke.Enabled.boolValue) {
                    flags |= TextMeshProLayerFeatureFlags.Stroke;
                }

                if (Shadow.Enabled.boolValue) {
                    flags |= TextMeshProLayerFeatureFlags.Shadow;
                }

                return flags;
            }
        }
    }
}
