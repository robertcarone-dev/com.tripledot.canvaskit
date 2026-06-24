using System;
using UnityEditor;
using UnityEngine;
using Tripledot.CanvasKit.Editor;

namespace Tripledot.CanvasKit.TextMeshPro.Editor
{
    [Flags]
    internal enum TextMeshProLayerFeatureFlags
    {
        None = 0,
        Face = 1 << 0,
        Stroke = 1 << 1,
        Shadow = 1 << 2
    }

    internal readonly struct TextMeshProLayerSwatchDescriptor
    {
        public readonly bool HasFill;
        public readonly CanvasPaint Fill;
        public readonly bool HasInsetOutline;
        public readonly CanvasPaint InsetOutline;

        public TextMeshProLayerSwatchDescriptor(
            bool hasFill,
            CanvasPaint fill,
            bool hasInsetOutline,
            CanvasPaint insetOutline)
        {
            HasFill = hasFill;
            Fill = fill;
            HasInsetOutline = hasInsetOutline;
            InsetOutline = insetOutline;
        }
    }

    internal static class TextMeshProLayerSwatches
    {
        public static GUIContent GetLayerDisplayContent(TextMeshProSerializedLayer layer)
        {
            return TextMeshProLayerInspectorStyles.GetLayerDisplayContent(layer.DisplayLabel);
        }

        public static Rect GetLayerTitleRect(Rect rect, int iconCount)
        {
            var iconWidth = GetLayerFeatureIconBadgesWidth(iconCount);
            if (iconWidth <= 0f) {
                return rect;
            }

            rect.width = Mathf.Max(0f, rect.width - iconWidth - TextMeshProLayerInspectorStyles.HeaderControlGap);
            return rect;
        }

        public static int GetLayerFeatureIconCount(TextMeshProLayerFeatureFlags flags)
        {
            var count = 0;
            if ((flags & TextMeshProLayerFeatureFlags.Face) != 0) {
                count++;
            }

            if ((flags & TextMeshProLayerFeatureFlags.Stroke) != 0) {
                count++;
            }

            if ((flags & TextMeshProLayerFeatureFlags.Shadow) != 0) {
                count++;
            }

            return count;
        }

        public static void DrawLayerSwatch(Rect rect, TextMeshProSerializedLayer layer)
        {
            var descriptor = GetLayerSwatchDescriptor(layer);
            if (descriptor.HasFill) {
                CanvasEditorGUI.DrawPaintSwatch(rect, descriptor.Fill);
            } else {
                CanvasEditorGUI.DrawTransparentSwatch(rect);
            }

            if (descriptor.HasInsetOutline) {
                CanvasEditorGUI.DrawPaintOutlineSwatch(rect, descriptor.InsetOutline);
            }
        }

        public static void DrawFeatureIconBadges(Rect rect, TextMeshProLayerFeatureFlags flags)
        {
            var iconCount = GetLayerFeatureIconCount(flags);
            if (iconCount == 0) {
                return;
            }

            var totalWidth = GetLayerFeatureIconBadgesWidth(iconCount);
            var iconRect = new Rect(
                rect.xMax - totalWidth,
                rect.y + Mathf.Floor((rect.height - TextMeshProLayerInspectorStyles.FeatureIconBadgeSize) * 0.5f),
                TextMeshProLayerInspectorStyles.FeatureIconBadgeSize,
                TextMeshProLayerInspectorStyles.FeatureIconBadgeSize);

            DrawFeatureIconBadge(ref iconRect, flags, TextMeshProLayerFeatureFlags.Face, TextMeshProLayerInspectorStyles.FillLayerIcon);
            DrawFeatureIconBadge(ref iconRect, flags, TextMeshProLayerFeatureFlags.Stroke, TextMeshProLayerInspectorStyles.StrokeLayerIcon);
            DrawFeatureIconBadge(ref iconRect, flags, TextMeshProLayerFeatureFlags.Shadow, TextMeshProLayerInspectorStyles.ShadowLayerIcon);
        }

        public static void DrawPresetModeMarker(Rect rect, GUIContent content)
        {
            var isProSkin = EditorGUIUtility.isProSkin;
            var borderColor = isProSkin ? TextMeshProLayerInspectorStyles.InstanceMarkerBorderColorDark : TextMeshProLayerInspectorStyles.InstanceMarkerBorderColorLight;
            EditorGUI.DrawRect(rect, isProSkin ? TextMeshProLayerInspectorStyles.InstanceMarkerBackgroundColorDark : TextMeshProLayerInspectorStyles.InstanceMarkerBackgroundColorLight);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), borderColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), borderColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), borderColor);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), borderColor);
            GUI.Label(rect, content, TextMeshProLayerInspectorStyles.InstanceMarkerStyle);
        }

        public static CanvasPaint ReadPaintForSwatch(SerializedProperty paint)
        {
            return ReadPaintForSwatch(new TextMeshProSerializedPaint(paint));
        }

        private static float GetLayerFeatureIconBadgesWidth(int iconCount)
        {
            return iconCount <= 0
                ? 0f
                : iconCount * TextMeshProLayerInspectorStyles.FeatureIconBadgeSize + (iconCount - 1) * TextMeshProLayerInspectorStyles.FeatureIconBadgeGap;
        }

        private static void DrawFeatureIconBadge(ref Rect iconRect, TextMeshProLayerFeatureFlags flags, TextMeshProLayerFeatureFlags flag, Texture2D icon)
        {
            if ((flags & flag) == 0) {
                return;
            }

            if (icon != null) {
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            }

            iconRect.x += TextMeshProLayerInspectorStyles.FeatureIconBadgeSize + TextMeshProLayerInspectorStyles.FeatureIconBadgeGap;
        }

        private static TextMeshProLayerSwatchDescriptor GetLayerSwatchDescriptor(TextMeshProSerializedLayer layer)
        {
            var strokeEnabled = layer.Stroke.Enabled.boolValue;
            var strokePaint = strokeEnabled ? ReadPaintForSwatch(layer.Stroke.PaintRoot) : default;
            if (layer.Face.Enabled.boolValue) {
                return new TextMeshProLayerSwatchDescriptor(true, ReadPaintForSwatch(layer.Face.PaintRoot), strokeEnabled, strokePaint);
            }

            if (strokeEnabled) {
                return new TextMeshProLayerSwatchDescriptor(false, default, true, strokePaint);
            }

            if (layer.Shadow.Enabled.boolValue) {
                return new TextMeshProLayerSwatchDescriptor(true, ReadPaintForSwatch(layer.Shadow.PaintRoot), false, default);
            }

            return new TextMeshProLayerSwatchDescriptor(true, CanvasPaint.Solid(Color.clear), false, default);
        }

        private static CanvasPaint ReadPaintForSwatch(TextMeshProSerializedPaint paint)
        {
            return new CanvasPaint {
                Type = (CanvasPaintType)paint.Type.enumValueIndex,
                GradientMode = (CanvasGradientMode)paint.GradientMode.enumValueIndex,
                Color = paint.Color.colorValue,
                SecondaryColor = paint.SecondaryColor.colorValue,
                Opacity = paint.Opacity.floatValue,
                Gradient = paint.Gradient.gradientValue,
                Texture = paint.Texture.objectReferenceValue as Texture2D
            };
        }
    }
}
