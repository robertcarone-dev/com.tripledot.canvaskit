using System;
using UnityEditor;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    [Flags]
    internal enum CanvasPaintVisibleFields
    {
        None = 0,
        PrimaryColor = 1 << 0,
        SecondaryColor = 1 << 1,
        Gradient = 1 << 2,
        Texture = 1 << 3,
        Transform = 1 << 4,
        Output = 1 << 5,
        GradientMode = 1 << 6
    }

    internal static class CanvasPaintDrawer
    {
        public static CanvasPaintVisibleFields GetVisibleFields(SerializedCanvasPaint paint)
        {
            var fields = CanvasPaintVisibleFields.Output;
            var type = paint.Type;
            if (type.hasMultipleDifferentValues) {
                return fields | CanvasPaintVisibleFields.PrimaryColor | CanvasPaintVisibleFields.SecondaryColor |
                    CanvasPaintVisibleFields.Gradient | CanvasPaintVisibleFields.Texture |
                    CanvasPaintVisibleFields.Transform | CanvasPaintVisibleFields.GradientMode;
            }

            var paintType = (CanvasPaintType)type.enumValueIndex;
            switch (paintType) {
                case CanvasPaintType.LinearGradient:
                case CanvasPaintType.RadialGradient:
                    fields |= CanvasPaintVisibleFields.GradientMode | CanvasPaintVisibleFields.Transform;
                    var gradientMode = paint.GradientMode;
                    if (gradientMode.hasMultipleDifferentValues) {
                        fields |= CanvasPaintVisibleFields.PrimaryColor | CanvasPaintVisibleFields.SecondaryColor | CanvasPaintVisibleFields.Gradient;
                    } else if ((CanvasGradientMode)gradientMode.enumValueIndex == CanvasGradientMode.Texture) {
                        fields |= CanvasPaintVisibleFields.Gradient;
                    } else {
                        fields |= CanvasPaintVisibleFields.PrimaryColor | CanvasPaintVisibleFields.SecondaryColor;
                    }
                    break;
                case CanvasPaintType.Texture:
                    fields |= CanvasPaintVisibleFields.Texture | CanvasPaintVisibleFields.Transform;
                    break;
                default:
                    fields |= CanvasPaintVisibleFields.PrimaryColor;
                    break;
            }

            return fields;
        }

        public static void DrawFillMode(SerializedCanvasPaint paint)
        {
            var type = paint.Type;
            var previousType = type is { hasMultipleDifferentValues: false }
                ? (CanvasPaintType)type.enumValueIndex
                : CanvasPaintType.Solid;

            EditorGUI.BeginChangeCheck();
            CanvasEditorGUI.PropertyField(type, Styles.Mode);
            if (!EditorGUI.EndChangeCheck() || type.hasMultipleDifferentValues) {
                return;
            }

            var nextType = (CanvasPaintType)type.enumValueIndex;
            if (previousType != CanvasPaintType.LinearGradient &&
                nextType == CanvasPaintType.LinearGradient &&
                CanvasPaintEditorUtility.HasDefaultSpatialTransform(paint)) {
                CanvasPaintEditorUtility.ResetSpatialTransform(paint);
            }
        }

        public static void DrawAppearance(SerializedCanvasPaint paint)
        {
            var visible = GetVisibleFields(paint);
            if (HasField(visible, CanvasPaintVisibleFields.GradientMode)) {
                CanvasEditorGUI.PropertyField(paint.GradientMode, Styles.GradientMode);
            }

            if (HasField(visible, CanvasPaintVisibleFields.Gradient)) {
                CanvasEditorGUI.PropertyField(paint.Gradient, Styles.Gradient);
            }

            if (HasField(visible, CanvasPaintVisibleFields.Texture)) {
                CanvasEditorGUI.PropertyField(paint.Texture, Styles.Image);
            }

            if (HasField(visible, CanvasPaintVisibleFields.PrimaryColor)) {
                DrawColor(paint.Color, GetPrimaryColorLabel(paint), paint.ColorUsesHdrPicker);
            }

            if (HasField(visible, CanvasPaintVisibleFields.SecondaryColor)) {
                DrawColor(paint.SecondaryColor, GetSecondaryColorLabel(paint), paint.SecondaryColorUsesHdrPicker);
            }

            CanvasEditorGUI.Slider(paint.Opacity, Styles.Opacity, 0f, 1f, 1f, true, true);
        }

        public static bool HasMapping(SerializedCanvasPaint paint)
        {
            var visible = GetVisibleFields(paint);
            return HasField(visible, CanvasPaintVisibleFields.Transform);
        }

        public static void DrawMappingHeader(SerializedCanvasPaint paint, UnityEngine.Object sceneTarget = null, bool boxed = false, int layerIndex = -1)
        {
            if (boxed) {
                CanvasEditorGUI.DrawRoundedInspectorSubsection(Styles.Mapping, true, rect => DrawGradientHeaderButtons(paint, rect, sceneTarget, layerIndex));
                return;
            }

            CanvasEditorGUI.DrawSubsection(Styles.Mapping, true, rect => DrawGradientHeaderButtons(paint, rect, sceneTarget, layerIndex));
        }

        public static void DrawMapping(SerializedCanvasPaint paint)
        {
            var visible = GetVisibleFields(paint);
            if (HasField(visible, CanvasPaintVisibleFields.Transform)) {
                using (new EditorGUI.IndentLevelScope()) {
                    DrawPaintTransform(paint);
                }
            }
        }

        private static void DrawGradientHeaderButtons(SerializedCanvasPaint paint, Rect rect, UnityEngine.Object sceneTarget, int layerIndex)
        {
            if (!CanvasPaintEditorUtility.IsEditableGradientPaint(paint, out var paintType)) {
                return;
            }

            var buttonY = rect.y + Mathf.Floor((rect.height - Styles.HeaderButtonHeight) * 0.5f) - 3f;
            var resetRect = new Rect(rect.xMax - Styles.HeaderButtonWidth, buttonY, Styles.HeaderButtonWidth, Styles.HeaderButtonHeight);

            if (sceneTarget != null) {
                var editRect = new Rect(resetRect.xMin - Styles.HeaderButtonGap - Styles.HeaderButtonWidth, buttonY, Styles.HeaderButtonWidth, Styles.HeaderButtonHeight);
                var active = CanvasGradientSceneView.IsEditingPaint(paint.Root, sceneTarget);
                EditorGUI.BeginChangeCheck();
                var previousColor = GUI.backgroundColor;
                if (active) {
                    GUI.backgroundColor = EditorGUIUtility.isProSkin ? Styles.ActiveButtonColorDark : Styles.ActiveButtonColorLight;
                }

                var nextActive = GUI.Toggle(editRect, active, Styles.EditGradient, Styles.MappingButtonStyle);
                GUI.backgroundColor = previousColor;
                if (EditorGUI.EndChangeCheck()) {
                    if (nextActive) {
                        CanvasGradientSceneView.SetEditingPaint(paint.Root, sceneTarget, layerIndex);
                    } else {
                        CanvasGradientSceneView.ClearEditingPaint();
                    }

                    SceneView.RepaintAll();
                }
            }

            if (GUI.Button(resetRect, Styles.ResetGradient, Styles.MappingButtonStyle)) {
                CanvasPaintEditorUtility.ResetSpatialTransform(paint);
                GUI.changed = true;
                SceneView.RepaintAll();
            }
        }

        internal static GUIContent GetPrimaryColorLabel(SerializedCanvasPaint paint)
        {
            if (paint.Type is { hasMultipleDifferentValues: false } &&
                (CanvasPaintType)paint.Type.enumValueIndex == CanvasPaintType.Solid) {
                return Styles.Color;
            }

            return Styles.ColorA;
        }

        internal static GUIContent GetSecondaryColorLabel(SerializedCanvasPaint paint)
        {
            return Styles.ColorB;
        }

        private static void DrawColor(SerializedProperty property, GUIContent label, SerializedProperty hdr)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            var useHdr = hdr != null && !hdr.hasMultipleDifferentValues && hdr.boolValue;
            property.colorValue = EditorGUILayout.ColorField(label, property.colorValue, true, true, useHdr);
            EditorGUI.showMixedValue = false;

            if (hdr != null) {
                EditorGUI.showMixedValue = hdr.hasMultipleDifferentValues;
                hdr.boolValue = GUILayout.Toggle(useHdr, Styles.Hdr, EditorStyles.miniButton, GUILayout.Width(42f));
                EditorGUI.showMixedValue = false;
            }

            EditorGUILayout.EndHorizontal();
        }

        private static void DrawPaintTransform(SerializedCanvasPaint paint)
        {
            var transform = paint.Transform;
            var type = paint.Type;
            var paintType = type is { hasMultipleDifferentValues: false } ? (CanvasPaintType)type.enumValueIndex : CanvasPaintType.LinearGradient;
            var center = transform.Center;
            var offset = transform.Offset;
            var scale = transform.Scale;
            var rotation = transform.Rotation;
            var wrapMode = transform.WrapMode;

            if (paintType == CanvasPaintType.LinearGradient) {
                CanvasEditorGUI.Slider(rotation, Styles.Direction, 0f, 360f);
                CanvasEditorGUI.Vector2PercentSliders(center, Styles.CenterPercent);
                DrawScaleComponent(scale, Styles.LengthPercent, true, true);
                CanvasEditorGUI.PropertyField(wrapMode, Styles.Wrap);
                return;
            }

            CanvasEditorGUI.Slider(rotation, Styles.Rotation, 0f, 360f);
            CanvasEditorGUI.Vector2PercentSliders(center, Styles.CenterPercent);

            if (paintType == CanvasPaintType.Texture) {
                CanvasEditorGUI.Vector2PercentSliders(offset, Styles.OffsetPercent, -100f);
            }

            DrawScale(scale, Styles.ScalePercent);
            CanvasEditorGUI.PropertyField(wrapMode, Styles.Wrap);
        }

        private static void DrawScale(SerializedProperty property, GUIContent label)
        {
            EditorGUILayout.LabelField(label, GUIContent.none);

            using (new EditorGUI.IndentLevelScope()) {
                DrawScaleComponent(property: property, label: Styles.X, xAxis: true, xOnly: false);
                DrawScaleComponent(property: property, label: Styles.Y, xAxis: false, xOnly: false);
            }
        }

        private static void DrawScaleComponent(SerializedProperty property, GUIContent label, bool xAxis, bool xOnly)
        {
            var value = property.vector2Value;
            var displayValue = CanvasEditorGUI.UnitToPercentDisplay(xOnly || xAxis ? value.x : value.y);
            if (CanvasEditorGUI.SliderValue(label, ref displayValue, 1f, 400f, property.hasMultipleDifferentValues)) {
                if (xOnly || xAxis) {
                    value.x = CanvasEditorGUI.PercentDisplayToUnit(displayValue);
                } else {
                    value.y = CanvasEditorGUI.PercentDisplayToUnit(displayValue);
                }

                property.vector2Value = value;
            }
        }

        private static bool HasField(CanvasPaintVisibleFields fields, CanvasPaintVisibleFields flag)
        {
            return (fields & flag) != 0;
        }

        private static class Styles
        {
            public const float HeaderButtonWidth = 28f;
            public const float HeaderButtonHeight = 20f;
            public const float HeaderButtonGap = 2f;

            public static readonly GUIContent CenterPercent = L10n.TextContent("Center (%)", "Position the center of the gradient or image within the rendered shape.");
            public static readonly GUIContent Color = L10n.TextContent("Color", "Set the color used by solid paints.");
            public static readonly GUIContent ColorA = L10n.TextContent("Color A", "Set the primary color used by solid and two-color gradient paints.");
            public static readonly GUIContent ColorB = L10n.TextContent("Color B", "Set the secondary color used by two-color gradient paints.");
            public static readonly GUIContent Direction = L10n.TextContent("Direction", "Rotate the linear gradient direction in degrees.");
            public static readonly GUIContent EditGradient = L10n.TextContent(string.Empty, "Edit the gradient transform in the Scene view.");
            public static readonly GUIContent Gradient = L10n.TextContent("Gradient", "Choose the gradient asset used by texture gradient mode.");
            public static readonly GUIContent GradientMode = L10n.TextContent("Gradient Mode", "Choose whether gradients use two colors or a Gradient asset.");
            public static readonly GUIContent Hdr = L10n.TextContent("HDR", "Use an HDR color picker for this color.");
            public static readonly GUIContent Image = L10n.TextContent("Image", "Choose the texture sampled by this paint.");
            public static readonly GUIContent LengthPercent = L10n.TextContent("Length (%)", "Set the length of the linear gradient as a percentage of the shape.");
            public static readonly GUIContent Mapping = L10n.TextContent("Mapping", "Adjust how the gradient or image is positioned, scaled, rotated, and wrapped.");
            public static readonly GUIContent Mode = L10n.TextContent("Mode", "Choose the paint source: solid color, gradient, or image.");
            public static readonly GUIContent OffsetPercent = L10n.TextContent("Offset (%)", "Shift the image sampling position as a percentage of the shape.");
            public static readonly GUIContent Opacity = L10n.TextContent("Opacity", "Adjust the paint opacity before it is composited with the layer.");
            public static readonly GUIContent ResetGradient = L10n.TextContent(string.Empty, "Reset the gradient transform.");
            public static readonly GUIContent Rotation = L10n.TextContent("Rotation", "Rotate the gradient or image mapping in degrees.");
            public static readonly GUIContent ScalePercent = L10n.TextContent("Scale (%)", "Scale the gradient or image mapping as a percentage of the shape.");
            public static readonly GUIContent Wrap = L10n.TextContent("Wrap", "Choose how sampling behaves outside the mapped gradient or image area.");
            public static readonly GUIContent X = L10n.TextContent("X", "Adjust the horizontal component.");
            public static readonly GUIContent Y = L10n.TextContent("Y", "Adjust the vertical component.");

            public static readonly Color ActiveButtonColorDark = new Color(0.36f, 0.58f, 0.78f, 1f);
            public static readonly Color ActiveButtonColorLight = new Color(0.66f, 0.82f, 1f, 1f);

            public static readonly GUIStyle MappingButtonStyle;

            static Styles()
            {
                EditGradient.image = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.tripledot.canvaskit/Editor Default Resources/Icons/EditGradientIcon.png");
                ResetGradient.image = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.tripledot.canvaskit/Editor Default Resources/Icons/ResetIcon.png");
                
                MappingButtonStyle = new GUIStyle(EditorStyles.miniButton) {
                    alignment = TextAnchor.MiddleCenter,
                    fixedWidth = HeaderButtonWidth,
                    fixedHeight = HeaderButtonHeight,
                    imagePosition = ImagePosition.ImageOnly,
                    margin = new RectOffset(0, 0, 0, 0),
                    padding = new RectOffset(5, 5, 2, 2)
                };
            }
        }
    }
}
