using System;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    internal static class CanvasEditorGUI
    {
        internal static class Styles
        {
            public const float SliderNumericFieldWidth = 50f;
            public const float SliderRowGap = 4f;
            public const float SliderUnitWidth = 52f;
            public const float DefaultSdfSliderPadding = 64f;
            public const int RoundedInspectorContentPadding = 8;
            public const float SliderValueGroupWidth = SliderNumericFieldWidth + SliderRowGap + SliderUnitWidth;
            public const float CheckerSize = 4f;
            
            private const int RoundedInspectorTextureSize = 16;
            private const int RoundedInspectorRadius = 5;
            private const int RoundedInspectorBorderSegments = 4;

            public static readonly GUIContent X = L10n.TextContent("X", "Adjust the horizontal component.");
            public static readonly GUIContent Y = L10n.TextContent("Y", "Adjust the vertical component.");

            public static readonly GUIContent[] SdfLengthUnitLabels = {
                L10n.TextContent("PX", "Edit this SDF length in pixels."),
                L10n.TextContent("%", "Edit this SDF length as a percentage of available SDF padding.")
            };

            public static readonly Color CheckerLight = new Color(0.72f, 0.72f, 0.72f, 1f);
            public static readonly Color CheckerDark = new Color(0.47f, 0.47f, 0.47f, 1f);

            private static GUIStyle _subsectionStyle;
            private static GUIStyle _swatchLabelStyle;
            private static GUIStyle _roundedInspectorPanelStyleDark;
            private static GUIStyle _roundedInspectorPanelStyleLight;
            private static GUIStyle _roundedInspectorPanelHeaderStyleDark;
            private static GUIStyle _roundedInspectorPanelHeaderStyleLight;
            private static GUIStyle _roundedInspectorPanelCollapsedHeaderStyleDark;
            private static GUIStyle _roundedInspectorPanelCollapsedHeaderStyleLight;
            private static GUIStyle _roundedInspectorPanelContentStyle;
        
            public static Color SwatchBorder => 
                EditorGUIUtility.isProSkin ? new Color(0.06f, 0.06f, 0.06f, 1f) : new Color(0.42f, 0.42f, 0.42f, 1f);

            public static Color RoundedInspectorPanelBorder =>
                EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.12f, 1f) : new Color(0.52f, 0.53f, 0.55f, 1f);

            public static GUIStyle SubsectionStyle => _subsectionStyle ??= new GUIStyle(EditorStyles.boldLabel) {
                padding = new RectOffset(0, 0, 0, 0)
            };

            public static GUIStyle SwatchLabelStyle => _swatchLabelStyle ??= new GUIStyle(EditorStyles.label) {
                alignment = TextAnchor.MiddleLeft
            };

            public static GUIStyle RoundedInspectorPanelStyle => EditorGUIUtility.isProSkin
                ? _roundedInspectorPanelStyleDark ??= CreateRoundedInspectorPanelStyle(true)
                : _roundedInspectorPanelStyleLight ??= CreateRoundedInspectorPanelStyle(false);

            public static GUIStyle GetRoundedInspectorPanelHeaderStyle(bool expanded)
            {
                if (expanded) {
                    return EditorGUIUtility.isProSkin
                        ? _roundedInspectorPanelHeaderStyleDark ??= CreateRoundedInspectorPanelHeaderStyle(true, true)
                        : _roundedInspectorPanelHeaderStyleLight ??= CreateRoundedInspectorPanelHeaderStyle(false, true);
                }

                return EditorGUIUtility.isProSkin
                    ? _roundedInspectorPanelCollapsedHeaderStyleDark ??= CreateRoundedInspectorPanelHeaderStyle(true, false)
                    : _roundedInspectorPanelCollapsedHeaderStyleLight ??= CreateRoundedInspectorPanelHeaderStyle(false, false);
            }

            public static GUIStyle RoundedInspectorPanelContentStyle => _roundedInspectorPanelContentStyle ??= new GUIStyle {
                padding = new RectOffset(RoundedInspectorContentPadding, RoundedInspectorContentPadding, 6, 8)
            };

            private static GUIStyle CreateRoundedInspectorPanelStyle(bool darkSkin)
            {
                var fill = darkSkin ? new Color(0.20f, 0.20f, 0.20f, 1f) : new Color(0.76f, 0.76f, 0.76f, 1f);
                return new GUIStyle {
                    normal = { background = CreateRoundedRectTexture(fill, true, true) },
                    border = new RectOffset(RoundedInspectorRadius, RoundedInspectorRadius, RoundedInspectorRadius, RoundedInspectorRadius),
                    margin = new RectOffset(14, 8, 0, 6),
                    padding = new RectOffset(0, 0, 0, 0)
                };
            }

            private static GUIStyle CreateRoundedInspectorPanelHeaderStyle(bool darkSkin, bool expanded)
            {
                var fill = darkSkin ? new Color(0.22f, 0.225f, 0.23f, 1f) : new Color(0.70f, 0.71f, 0.72f, 1f);
                return new GUIStyle {
                    normal = { background = CreateRoundedRectTexture(fill, true, !expanded) },
                    border = new RectOffset(RoundedInspectorRadius, RoundedInspectorRadius, RoundedInspectorRadius, expanded ? 1 : RoundedInspectorRadius),
                    margin = new RectOffset(0, 0, 0, 0),
                    padding = new RectOffset(0, 0, 0, 0)
                };
            }

            internal static Vector3[] GetRoundedRectBorderPoints(Rect rect)
            {
                var radius = Mathf.Min(RoundedInspectorRadius, rect.width * 0.5f, rect.height * 0.5f);
                var points = new Vector3[(RoundedInspectorBorderSegments + 1) * 4 + 1];
                var index = 0;
                AddRoundedRectCorner(points, ref index, rect.xMax - radius, rect.yMin + radius, radius, -90f, 0f);
                AddRoundedRectCorner(points, ref index, rect.xMax - radius, rect.yMax - radius, radius, 0f, 90f);
                AddRoundedRectCorner(points, ref index, rect.xMin + radius, rect.yMax - radius, radius, 90f, 180f);
                AddRoundedRectCorner(points, ref index, rect.xMin + radius, rect.yMin + radius, radius, 180f, 270f);
                points[index] = points[0];
                return points;
            }

            private static Texture2D CreateRoundedRectTexture(Color fill, bool roundTop, bool roundBottom)
            {
                var texture = new Texture2D(RoundedInspectorTextureSize, RoundedInspectorTextureSize, TextureFormat.RGBA32, false) {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };

                var clear = new Color(0f, 0f, 0f, 0f);
                for (int y = 0; y < texture.height; y++) {
                    for (int x = 0; x < texture.width; x++) {
                        var inside = IsInsideRoundedRect(x + 0.5f, y + 0.5f, texture.width, texture.height, RoundedInspectorRadius, roundTop, roundBottom);
                        if (!inside) {
                            texture.SetPixel(x, y, clear);
                            continue;
                        }

                        texture.SetPixel(x, y, fill);
                    }
                }

                texture.Apply();
                return texture;
            }

            private static void AddRoundedRectCorner(Vector3[] points, ref int index, float centerX, float centerY, float radius, float startDegrees, float endDegrees)
            {
                for (int i = 0; i <= RoundedInspectorBorderSegments; i++) {
                    var angle = Mathf.Lerp(startDegrees, endDegrees, i / (float)RoundedInspectorBorderSegments) * Mathf.Deg2Rad;
                    points[index++] = new Vector3(
                        centerX + Mathf.Cos(angle) * radius,
                        centerY + Mathf.Sin(angle) * radius,
                        0f);
                }
            }

            private static bool IsInsideRoundedRect(float x, float y, float width, float height, float radius, bool roundTop, bool roundBottom)
            {
                if (radius <= 0f) {
                    return true;
                }

                if (roundBottom && x < radius && y < radius) {
                    return IsInsideCorner(x, y, radius, radius, radius);
                }

                if (roundBottom && x > width - radius && y < radius) {
                    return IsInsideCorner(x, y, width - radius, radius, radius);
                }

                if (roundTop && x < radius && y > height - radius) {
                    return IsInsideCorner(x, y, radius, height - radius, radius);
                }

                if (roundTop && x > width - radius && y > height - radius) {
                    return IsInsideCorner(x, y, width - radius, height - radius, radius);
                }

                return true;
            }

            private static bool IsInsideCorner(float x, float y, float centerX, float centerY, float radius)
            {
                var dx = x - centerX;
                var dy = y - centerY;
                return dx * dx + dy * dy <= radius * radius;
            }
        }

        internal readonly struct SdfLengthPresentation
        {
            public readonly float AuthoredPixels;
            public readonly float EffectivePixels;
            public readonly float FieldPixels;

            public SdfLengthPresentation(float authoredPixels, float effectivePixels, bool showEffectiveInField)
            {
                AuthoredPixels = authoredPixels;
                EffectivePixels = effectivePixels;
                FieldPixels = showEffectiveInField ? effectivePixels : authoredPixels;
            }
        }

        public static void DrawSubsection(GUIContent title, bool addSeparator = true, Action<Rect> drawTrailingControls = null)
        {
            if (addSeparator) {
                GUILayout.Space(5f);
                CoreEditorUtils.DrawSplitter();
                GUILayout.Space(5f);
            }

            var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(rect, title, Styles.SubsectionStyle);
            drawTrailingControls?.Invoke(rect);
        }

        public static void DrawRoundedInspectorSubsection(GUIContent title, bool addSeparator = true, Action<Rect> drawTrailingControls = null)
        {
            if (addSeparator) {
                GUILayout.Space(5f);
                var separatorRect = EditorGUILayout.GetControlRect(false, 1f);
                separatorRect.xMin -= Styles.RoundedInspectorContentPadding;
                separatorRect.xMax += Styles.RoundedInspectorContentPadding;
                EditorGUI.DrawRect(separatorRect, Styles.RoundedInspectorPanelBorder);
                GUILayout.Space(5f);
            }

            var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(rect, title, Styles.SubsectionStyle);
            drawTrailingControls?.Invoke(rect);
        }

        public static void PropertyField(SerializedProperty property, GUIContent label, bool includeChildren = true)
        {
            EditorGUILayout.PropertyField(property, label, includeChildren);
        }

        public static void Slider(SerializedProperty property, GUIContent label, float min, float max, float displayScale = 1f, bool clampToMin = false, bool clampToMax = false)
        {
            var value = property.floatValue * displayScale;
            if (SliderValue(label, ref value, min, max, property.hasMultipleDifferentValues)) {
                if (clampToMin) {
                    value = Mathf.Max(min, value);
                }

                if (clampToMax) {
                    value = Mathf.Min(max, value);
                }

                property.floatValue = value / displayScale;
            }
        }

        public static void SdfLengthSlider(SerializedProperty pixelProperty, SerializedProperty unitProperty, GUIContent label, float availablePadding, float minPixels, float maxPixels)
        {
            SdfLengthSlider(pixelProperty, unitProperty, label, availablePadding, minPixels, maxPixels, false);
        }

        public static void ConstrainedSdfLengthSlider(SerializedProperty pixelProperty, SerializedProperty unitProperty, GUIContent label, float availablePadding, float minPixels, float maxPixels)
        {
            SdfLengthSlider(pixelProperty, unitProperty, label, availablePadding, minPixels, maxPixels, true);
        }

        private static void SdfLengthSlider(SerializedProperty pixelProperty, SerializedProperty unitProperty, GUIContent label, float availablePadding, float minPixels, float maxPixels, bool showEffectiveValue)
        {
            var sliderPadding = float.IsPositiveInfinity(availablePadding) ? 0f : availablePadding;
            var fallbackMax = GetSdfSliderMax(sliderPadding, pixelProperty.floatValue);
            if (float.IsNegativeInfinity(minPixels)) {
                minPixels = -fallbackMax;
            }

            if (float.IsPositiveInfinity(maxPixels)) {
                maxPixels = fallbackMax;
            }

            NormalizeSdfLengthRange(ref minPixels, ref maxPixels);

            var currentPixels = pixelProperty.floatValue;
            var presentation = showEffectiveValue
                ? GetConstrainedSdfLengthPresentation(currentPixels, minPixels, maxPixels)
                : new SdfLengthPresentation(currentPixels, Mathf.Clamp(currentPixels, minPixels, maxPixels), false);

            var unit = (TextMeshProSdfLengthUnit)Mathf.Clamp(unitProperty.enumValueIndex, 0, 1);
            var percentBasis = availablePadding > 0f && !float.IsPositiveInfinity(availablePadding)
                ? availablePadding
                : Mathf.Max(Styles.DefaultSdfSliderPadding, Mathf.Abs(currentPixels), Mathf.Abs(minPixels), Mathf.Abs(maxPixels));
            
            var fieldValue = GetSdfLengthDisplayValue(presentation.FieldPixels, unit, percentBasis);
            var sliderValue = GetSdfLengthDisplayValue(presentation.EffectivePixels, unit, percentBasis);
            var min = GetSdfLengthDisplayValue(minPixels, unit, percentBasis);
            var max = GetSdfLengthDisplayValue(maxPixels, unit, percentBasis);

            if (DrawSdfLengthSliderValue(label, ref sliderValue, ref fieldValue, min, max, unit, pixelProperty.hasMultipleDifferentValues, unitProperty is { hasMultipleDifferentValues: true }, out var nextUnit, out var valueChange)) {
                var nextValue = valueChange == SdfLengthValueChange.Slider ? sliderValue : fieldValue;
                pixelProperty.floatValue = GetConstrainedSdfLengthEditedPixels(nextValue, nextUnit, percentBasis, minPixels, maxPixels);
            }

            if (nextUnit != unit) {
                unitProperty.enumValueIndex = (int)nextUnit;
            }
        }

        private static float GetSdfSliderMax(float availablePadding, float currentPixels)
        {
            var currentMagnitude = Mathf.Abs(currentPixels);

            if (availablePadding > 0f) {
                return Mathf.Max(availablePadding, currentMagnitude);
            }

            return Mathf.Max(Styles.DefaultSdfSliderPadding, currentMagnitude);
        }

        public static void NormalizeSdfLengthRange(ref float minPixels, ref float maxPixels)
        {
            if (float.IsNaN(minPixels)) {
                minPixels = 0f;
            }

            if (float.IsNaN(maxPixels)) {
                maxPixels = minPixels;
            }

            if (maxPixels < minPixels) {
                maxPixels = minPixels;
            }
        }

        public static SdfLengthPresentation GetConstrainedSdfLengthPresentation(float authoredPixels, float minPixels, float maxPixels)
        {
            NormalizeSdfLengthRange(ref minPixels, ref maxPixels);
            return new SdfLengthPresentation(authoredPixels, Mathf.Clamp(authoredPixels, minPixels, maxPixels), true);
        }

        public static float GetConstrainedSdfLengthEditedPixels(float displayValue, TextMeshProSdfLengthUnit unit, float percentBasis, float minPixels, float maxPixels)
        {
            NormalizeSdfLengthRange(ref minPixels, ref maxPixels);
            var pixels = unit == TextMeshProSdfLengthUnit.Percent ? TextMeshProUtility.PercentToPixels(displayValue, percentBasis) : displayValue;
            return Mathf.Clamp(pixels, minPixels, maxPixels);
        }

        public static void Vector2PercentSliders(SerializedProperty property, GUIContent label, float min = 0f, float max = 100f)
        {
            EditorGUILayout.LabelField(label, GUIContent.none);

            using (new EditorGUI.IndentLevelScope()) {
                var value = property.vector2Value;
                var x = UnitToPercentDisplay(value.x);
                var y = UnitToPercentDisplay(value.y);
                var changed = false;
                changed |= ChildSliderValue(Styles.X, ref x, min, max, property.hasMultipleDifferentValues);
                changed |= ChildSliderValue(Styles.Y, ref y, min, max, property.hasMultipleDifferentValues);
                if (changed) {
                    property.vector2Value = new Vector2(PercentDisplayToUnit(x), PercentDisplayToUnit(y));
                }
            }
        }

        public static float UnitToPercentDisplay(float value)
        {
            return value * 100f;
        }

        public static float PercentDisplayToUnit(float value)
        {
            return value / 100f;
        }

        public static float GetSdfLengthDisplayValue(float pixels, TextMeshProSdfLengthUnit unit, float percentBasis)
        {
            return unit == TextMeshProSdfLengthUnit.Percent ? TextMeshProUtility.PixelsToPercent(pixels, percentBasis) : pixels;
        }

        public static void CalculateSliderValueRects(Rect controlRect, out Rect sliderRect, out Rect fieldRect)
        {
            CalculateSliderRowRects(controlRect, false, out sliderRect, out fieldRect, out _);
        }

        public static void CalculateSdfSliderValueRects(Rect controlRect, out Rect sliderRect, out Rect fieldRect, out Rect unitRect)
        {
            CalculateSliderRowRects(controlRect, true, out sliderRect, out fieldRect, out unitRect);
        }

        public static bool SliderValue(GUIContent label, ref float value, float min, float max, bool mixed)
        {
            return DrawSliderValue(label, ref value, min, max, mixed);
        }

        public static bool ChildSliderValue(GUIContent label, ref float value, float min, float max, bool mixed)
        {
            return DrawSliderValue(label, ref value, min, max, mixed);
        }

        public static void Vector2Field(SerializedProperty property, GUIContent label)
        {
            EditorGUILayout.PropertyField(property, label);
        }

        public static void DrawPaintSwatch(Rect rect, CanvasPaint paint, Color fallback)
        {
            DrawPaintSwatchFill(rect, paint, fallback);
            DrawSwatchBorder(rect);
        }

        public static void DrawPaintOutlineSwatch(Rect rect, CanvasPaint paint, Color fallback)
        {
            if (Event.current.type != EventType.Repaint) {
                return;
            }

            var contentRect = new Rect(rect.x + 1f, rect.y + 1f, Mathf.Max(0f, rect.width - 2f), Mathf.Max(0f, rect.height - 2f));
            const float thickness = 1f;
            if (contentRect.width <= 0f || contentRect.height <= 0f) {
                DrawSwatchBorder(rect);
                return;
            }

            var topRect = new Rect(contentRect.x, contentRect.y, contentRect.width, thickness);
            var bottomRect = new Rect(contentRect.x, contentRect.yMax - thickness, contentRect.width, thickness);
            var sideY = topRect.yMax;
            var sideHeight = Mathf.Max(0f, bottomRect.yMin - sideY);
            var leftRect = new Rect(contentRect.x, sideY, thickness, sideHeight);
            var rightRect = new Rect(contentRect.xMax - thickness, sideY, thickness, sideHeight);

            DrawPaintSwatchFill(topRect, paint, fallback);
            DrawPaintSwatchFill(bottomRect, paint, fallback);
            if (sideHeight > 0f) {
                DrawPaintSwatchFill(leftRect, paint, fallback);
                DrawPaintSwatchFill(rightRect, paint, fallback);
            }

            DrawSwatchBorder(rect);
        }

        private static void DrawPaintSwatchFill(Rect rect, CanvasPaint paint, Color fallback)
        {
            switch (paint.Type) {
                case CanvasPaintType.LinearGradient:
                case CanvasPaintType.RadialGradient:
                    if (paint.GradientMode == CanvasGradientMode.Texture && paint.Gradient != null) {
                        DrawGradientSwatch(rect, paint.Gradient, paint.Opacity);
                    } else {
                        DrawGradientColorSwatch(rect, paint.Color, paint.SecondaryColor, paint.Opacity);
                    }

                    break;
                case CanvasPaintType.Texture:
                    if (paint.Texture != null) {
                        DrawCheckerboard(rect);
                        GUI.DrawTexture(rect, paint.Texture, ScaleMode.ScaleAndCrop);
                    } else {
                        DrawColorSwatchFill(rect, paint.Color, paint.Opacity);
                    }

                    break;
                default:
                    DrawColorSwatchFill(rect, paint.Color, paint.Opacity);
                    break;
            }
        }

        public static void DrawColorSwatch(Rect rect, Color color, float alphaScale = 1f)
        {
            DrawColorSwatchFill(rect, color, alphaScale);
            DrawSwatchBorder(rect);
        }

        public static void DrawTransparentSwatch(Rect rect)
        {
            DrawCheckerboard(rect);
            DrawSwatchBorder(rect);
        }

        private static void DrawColorSwatchFill(Rect rect, Color color, float alphaScale = 1f)
        {
            var leftRect = LeftHalf(rect);
            var rightRect = RightHalf(rect, leftRect);
            EditorGUI.DrawRect(leftRect, Opaque(color));
            DrawCheckerboard(rightRect);
            EditorGUI.DrawRect(rightRect, WithAlphaScale(color, alphaScale));
        }

        public static void DrawSwatchLabel(Rect rect, GUIContent content)
        {
            GUI.Label(rect, content, Styles.SwatchLabelStyle);
        }

        public static void DrawRoundedInspectorPanelBorder(Rect rect)
        {
            if (Event.current.type != EventType.Repaint) {
                return;
            }

            rect.xMin = Mathf.Floor(rect.xMin) + 0.5f;
            rect.yMin = Mathf.Floor(rect.yMin) + 0.5f;
            rect.xMax = Mathf.Ceil(rect.xMax) - 0.5f;
            rect.yMax = Mathf.Ceil(rect.yMax) - 0.5f;

            Handles.BeginGUI();
            var previousColor = Handles.color;
            Handles.color = Styles.RoundedInspectorPanelBorder;
            Handles.DrawAAPolyLine(2f, Styles.GetRoundedRectBorderPoints(rect));
            Handles.color = previousColor;
            Handles.EndGUI();
        }

        public static void DrawRoundedInspectorHeaderSeparator(Rect rect)
        {
            if (Event.current.type != EventType.Repaint) {
                return;
            }

            var separatorRect = new Rect(
                Mathf.Ceil(rect.xMin) + 1f,
                Mathf.Floor(rect.yMax) - 1f,
                Mathf.Max(0f, Mathf.Floor(rect.width) - 2f),
                1f);
            EditorGUI.DrawRect(separatorRect, Styles.RoundedInspectorPanelBorder);
        }

        private static bool DrawSliderValue(GUIContent label, ref float value, float min, float max, bool mixed)
        {
            var rect = EditorGUILayout.GetControlRect();
            var controlRect = EditorGUI.PrefixLabel(rect, label);
            CalculateSliderValueRects(controlRect, out var sliderRect, out var fieldRect);

            var previousIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            try {
                EditorGUI.showMixedValue = mixed;
                EditorGUI.BeginChangeCheck();
                if (!mixed) {
                    value = GUI.HorizontalSlider(sliderRect, value, min, max);
                }

                value = EditorGUI.FloatField(fieldRect, value);
                var changed = EditorGUI.EndChangeCheck();
                EditorGUI.showMixedValue = false;
                return changed;
            } finally {
                EditorGUI.showMixedValue = false;
                EditorGUI.indentLevel = previousIndent;
            }
        }

        private enum SdfLengthValueChange
        {
            None,
            Slider,
            Field
        }

        private static bool DrawSdfLengthSliderValue(GUIContent label, ref float sliderValue, ref float fieldValue, float min, float max, TextMeshProSdfLengthUnit unit, bool valueMixed, bool unitMixed, out TextMeshProSdfLengthUnit nextUnit, out SdfLengthValueChange valueChange)
        {
            nextUnit = unit;
            valueChange = SdfLengthValueChange.None;
            var rect = EditorGUILayout.GetControlRect();
            var controlRect = EditorGUI.PrefixLabel(rect, label);
            CalculateSliderRowRects(controlRect, true, out var sliderRect, out var fieldRect, out var unitRect);

            var previousIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            try {
                EditorGUI.showMixedValue = valueMixed;
                EditorGUI.BeginChangeCheck();
                if (!valueMixed) {
                    sliderValue = GUI.HorizontalSlider(sliderRect, sliderValue, min, max);
                }

                var sliderChanged = EditorGUI.EndChangeCheck();
                if (sliderChanged) {
                    valueChange = SdfLengthValueChange.Slider;
                }

                EditorGUI.BeginChangeCheck();
                fieldValue = EditorGUI.FloatField(fieldRect, fieldValue);
                var fieldChanged = EditorGUI.EndChangeCheck();
                if (fieldChanged) {
                    valueChange = SdfLengthValueChange.Field;
                }

                EditorGUI.showMixedValue = false;

                EditorGUI.showMixedValue = unitMixed;
                EditorGUI.BeginChangeCheck();
                var unitIndex = GUI.Toolbar(unitRect, (int)unit, Styles.SdfLengthUnitLabels, EditorStyles.miniButton);
                var unitChanged = EditorGUI.EndChangeCheck();
                EditorGUI.showMixedValue = false;

                if (unitChanged) {
                    nextUnit = (TextMeshProSdfLengthUnit)Mathf.Clamp(unitIndex, 0, 1);
                }

                return valueChange != SdfLengthValueChange.None;
            } finally {
                EditorGUI.showMixedValue = false;
                EditorGUI.indentLevel = previousIndent;
            }
        }

        private static void CalculateSliderRowRects(Rect controlRect, bool includeUnit, out Rect sliderRect, out Rect fieldRect, out Rect unitRect)
        {
            var valueGroupWidth = includeUnit ? Styles.SliderValueGroupWidth : Styles.SliderNumericFieldWidth;
            var valueGroupRect = new Rect(controlRect.xMax - valueGroupWidth, controlRect.y, valueGroupWidth, controlRect.height);
            if (includeUnit) {
                var fieldWidth = Mathf.Min(Styles.SliderNumericFieldWidth, valueGroupWidth);
                var remainingWidth = Mathf.Max(0f, valueGroupWidth - fieldWidth);
                var unitWidth = remainingWidth > Styles.SliderRowGap ? Mathf.Min(Styles.SliderUnitWidth, remainingWidth - Styles.SliderRowGap) : 0f;
                var unitGap = unitWidth > 0f ? Styles.SliderRowGap : 0f;
                unitRect = new Rect(valueGroupRect.xMax - unitWidth, controlRect.y, unitWidth, controlRect.height);
                fieldRect = new Rect(unitRect.xMin - unitGap - fieldWidth, controlRect.y, fieldWidth, controlRect.height);
            } else {
                unitRect = Rect.zero;
                fieldRect = valueGroupRect;
            }

            var sliderWidth = Mathf.Max(0f, valueGroupRect.xMin - Styles.SliderRowGap - controlRect.x);
            sliderRect = new Rect(controlRect.x, controlRect.y + 3f, sliderWidth, Mathf.Max(0f, controlRect.height - 6f));
        }

        private static Color Opaque(Color color)
        {
            color.a = 1f;
            return color;
        }

        private static Color WithAlphaScale(Color color, float alphaScale)
        {
            color.a *= Mathf.Clamp01(alphaScale);
            return color;
        }

        private static void DrawGradientSwatch(Rect rect, Gradient gradient, float alphaScale)
        {
            if (Event.current.type == EventType.Repaint) {
                DrawCheckerboard(rect);
                DrawGradientRange(rect, gradient, alphaScale, false);
            }
        }

        private static void DrawGradientColorSwatch(Rect rect, Color left, Color right, float alphaScale)
        {
            if (Event.current.type == EventType.Repaint) {
                DrawCheckerboard(rect);
                DrawColorRange(rect, left, right, alphaScale, false);
            }
        }

        private static void DrawGradientRange(Rect rect, Gradient gradient, float alphaScale, bool opaque)
        {
            var steps = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            for (int i = 0; i < steps; i++) {
                var t = steps == 1 ? 0f : i / (steps - 1f);
                var slice = new Rect(rect.x + i, rect.y, 1f, rect.height);
                var color = gradient.Evaluate(t);
                EditorGUI.DrawRect(slice, opaque ? Opaque(color) : WithAlphaScale(color, alphaScale));
            }
        }

        private static void DrawColorRange(Rect rect, Color left, Color right, float alphaScale, bool opaque)
        {
            var steps = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            for (int i = 0; i < steps; i++) {
                var t = steps == 1 ? 0f : i / (steps - 1f);
                var slice = new Rect(rect.x + i, rect.y, 1f, rect.height);
                var color = Color.Lerp(left, right, t);
                EditorGUI.DrawRect(slice, opaque ? Opaque(color) : WithAlphaScale(color, alphaScale));
            }
        }

        private static void DrawCheckerboard(Rect rect)
        {
            var columns = Mathf.Max(1, Mathf.CeilToInt(rect.width / Styles.CheckerSize));
            var rows = Mathf.Max(1, Mathf.CeilToInt(rect.height / Styles.CheckerSize));
            for (int y = 0; y < rows; y++) {
                for (int x = 0; x < columns; x++) {
                    var tile = new Rect(
                        rect.x + x * Styles.CheckerSize,
                        rect.y + y * Styles.CheckerSize,
                        Mathf.Min(Styles.CheckerSize, rect.xMax - rect.x - x * Styles.CheckerSize),
                        Mathf.Min(Styles.CheckerSize, rect.yMax - rect.y - y * Styles.CheckerSize));
                    EditorGUI.DrawRect(tile, ((x + y) & 1) == 0 ? Styles.CheckerLight : Styles.CheckerDark);
                }
            }
        }

        private static Rect LeftHalf(Rect rect)
        {
            return new Rect(rect.x, rect.y, Mathf.Ceil(rect.width * 0.5f), rect.height);
        }

        private static Rect RightHalf(Rect rect, Rect leftRect)
        {
            return new Rect(leftRect.xMax, rect.y, rect.xMax - leftRect.xMax, rect.height);
        }

        private static void DrawSwatchBorder(Rect rect)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), Styles.SwatchBorder);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Styles.SwatchBorder);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), Styles.SwatchBorder);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), Styles.SwatchBorder);
        }
    }
}
