using UnityEditor;
using UnityEngine;

namespace Tripledot.CanvasKit.TextMeshPro.Editor
{
    internal static class TextMeshProLayerInspectorStyles
    {
        public static readonly GUIContent BlendMode = L10n.TextContent("Blend Mode", "Choose how this layer is composited with layers below it.");
        public static readonly GUIContent BevelSoftness = L10n.TextContent("Softness", "Soften the normalized fake bevel lighting band.");
        public static readonly GUIContent BevelWidth = L10n.TextContent("Width", "Set the normalized fake bevel lighting band width inside the fill edge.");
        public static readonly GUIContent Blur = L10n.TextContent("Blur", "Soften the shadow edge within the available SDF padding.");
        public static readonly GUIContent Dilate = L10n.TextContent("Dilate", "Expand or contract the face shape within the available SDF padding.");
        public static readonly GUIContent Effect = L10n.TextContent("Effect", "Controls for the shadow spread, blur, and offset.");
        public static readonly GUIContent EnableLighting = L10n.TextContent("Enabled", "Enable fake bevel lighting on the fill.");
        public static readonly GUIContent Face = L10n.TextContent("Fill", "Enable and edit the main text fill for this layer.");
        public static readonly GUIContent Glow = L10n.TextContent("Glow", "Add an additive glow layer.");
        public static readonly GUIContent Feather = L10n.TextContent("Feather", "Soften the stroke edge within the available SDF padding.");
        public static readonly GUIContent HighlightColor = L10n.TextContent("Highlight", "Tint and alpha used on fill edges facing the light.");
        public static readonly GUIContent Label = L10n.TextContent("Label", "Optional display name for this layer.");
        public static readonly GUIContent Layer = L10n.TextContent("Layer", "Add a text layer.");
        public static readonly GUIContent InstanceLayer = L10n.TextContent("I", "Instance layer: this row overrides the shared preset layer on this object.");
        public static readonly GUIContent InstanceMode = L10n.TextContent("Instance", "Use an object-specific copy of this preset layer.");
        public static readonly GUIContent SharedLayer = L10n.TextContent("S", "Shared layer: this row uses the assigned preset asset directly.");
        public static readonly GUIContent SharedMode = L10n.TextContent("Shared", "Use the shared preset layer. Editing this row changes the preset asset.");
        public static readonly GUIContent Layers = L10n.TextContent("Layers", "TextMeshPro rendering layers applied by this stack or preset.");
        public static readonly GUIContent LayerStackEmptyInfo = L10n.TextContent("TextMeshPro renders normally until at least one TextMeshPro layer is added.");
        public static readonly GUIContent LightAngle = L10n.TextContent("Angle", "Set the fake light direction in degrees.");
        public static readonly GUIContent Lighting = L10n.TextContent("Lighting", "Controls for fake bevel highlights and shadows on the fill.");
        public static readonly GUIContent Offset = L10n.TextContent("Offset", "Shift this effect relative to the text face.");
        public static readonly GUIContent Opacity = L10n.TextContent("Opacity", "Fade the entire layer before it is blended with layers below it.");
        public static readonly GUIContent Outline = L10n.TextContent("Stroke", "Enable and edit the stroke effect for this layer.");
        public static readonly GUIContent Position = L10n.TextContent("Position", "Choose where the stroke is placed relative to the glyph edge.");
        public static readonly GUIContent Shadow = L10n.TextContent("Shadow", "Add or edit a shadow layer.");
        public static readonly GUIContent ShadowClampWarning = L10n.TextContent("Shadow spread or blur is clamped by the available TMP font atlas padding. Increase the font asset padding or reduce other SDF effects that consume this layer's padding budget.");
        public static readonly GUIContent ShadowColor = L10n.TextContent("Shadow", "Tint and alpha used on fill edges facing away from the light.");
        public static readonly GUIContent Shape = L10n.TextContent("Shape", "Controls for SDF shape expansion and edge softness.");
        public static readonly GUIContent Spread = L10n.TextContent("Spread", "Expand or contract the shadow shape within the available SDF padding.");
        public static readonly GUIContent Stroke = L10n.TextContent("Stroke", "Add or edit a stroke layer.");
        public static readonly GUIContent Width = L10n.TextContent("Width", "Set the stroke thickness within the available SDF padding.");

        public static readonly Color LayerHeaderBackgroundColorDark = new Color(0.2f, 0.205f, 0.21f, 1f);
        public static readonly Color LayerHeaderBackgroundColorLight = new Color(0.76f, 0.77f, 0.79f, 1f);
        public static readonly Color LayerHeaderTopSeparatorColorDark = new Color(0.28f, 0.285f, 0.29f, 1f);
        public static readonly Color LayerHeaderTopSeparatorColorLight = new Color(0.86f, 0.87f, 0.89f, 1f);
        public static readonly Color LayerHeaderBottomSeparatorColorDark = new Color(0.08f, 0.08f, 0.08f, 1f);
        public static readonly Color LayerHeaderBottomSeparatorColorLight = new Color(0.52f, 0.53f, 0.55f, 1f);
        public static readonly Color InstanceMarkerBackgroundColorDark = new Color(0.22f, 0.28f, 0.34f, 1f);
        public static readonly Color InstanceMarkerBackgroundColorLight = new Color(0.72f, 0.82f, 0.93f, 1f);
        public static readonly Color InstanceMarkerBorderColorDark = new Color(0.34f, 0.42f, 0.5f, 1f);
        public static readonly Color InstanceMarkerBorderColorLight = new Color(0.48f, 0.6f, 0.74f, 1f);
        public static readonly Color InstanceMarkerTextColorDark = new Color(0.66f, 0.82f, 1f, 1f);
        public static readonly Color InstanceMarkerTextColorLight = new Color(0.12f, 0.28f, 0.55f, 1f);

        public static readonly Texture2D FillLayerIcon;
        public static readonly Texture2D StrokeLayerIcon;
        public static readonly Texture2D ShadowLayerIcon;
        public static readonly GUIContent ScratchContent = new GUIContent();
        public static readonly GUIStyle InstanceMarkerStyle;

        public const float LayerHeaderHeight = 26f;
        public const float FoldoutSize = 13f;
        public const float EnabledToggleSize = 13f;
        public const float LayerSwatchSize = 16f;
        public const float LayerIconSize = 16f;
        public const float InstanceMarkerSize = 16f;
        public const float FeatureIconBadgeSize = 16f;
        public const float FeatureIconBadgeGap = 3f;
        public const float HeaderControlGap = 6f;
        public const float TrailingControlWidth = 126f;
        public const float FillSectionHeaderHeight = 25f;

        static TextMeshProLayerInspectorStyles()
        {
            InstanceMarkerStyle = new GUIStyle(EditorStyles.miniLabel) {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = EditorGUIUtility.isProSkin ? InstanceMarkerTextColorDark : InstanceMarkerTextColorLight }
            };

            FillLayerIcon = LoadLayerIcon("TextIcon.png");
            StrokeLayerIcon = LoadLayerIcon("StrokeIcon.png");
            ShadowLayerIcon = LoadLayerIcon("ShadowIcon.png");

            Face.image = FillLayerIcon;
            Outline.image = StrokeLayerIcon;
            Shadow.image = ShadowLayerIcon;
        }

        public static GUIContent GetLayerDisplayContent(string text)
        {
            ScratchContent.text = text;
            ScratchContent.tooltip = Layer.tooltip;
            ScratchContent.image = null;
            return ScratchContent;
        }

        public static GUIContent GetTextOnlyContent(GUIContent content)
        {
            ScratchContent.text = content.text;
            ScratchContent.tooltip = content.tooltip;
            ScratchContent.image = null;
            return ScratchContent;
        }

        private static Texture2D LoadLayerIcon(string filename)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.tripledot.canvaskit/Editor Default Resources/Icons/TextMeshPro/" + filename);
        }
    }
}
