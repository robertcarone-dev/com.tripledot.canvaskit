using System.Runtime.CompilerServices;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    internal static class CanvasGradientSceneView
    {
        public readonly struct DrawResult
        {
            public readonly bool Changed;
            public readonly int LayerIndex;

            public DrawResult(bool changed, int layerIndex)
            {
                Changed = changed;
                LayerIndex = layerIndex;
            }
        }

        private const float HandleSize = 0.07f;
        private const float CenterHandleSize = 0.085f;
        private const int EllipseSegments = 72;

        private static readonly Color LineColor = new Color(0.13f, 0.68f, 1f, 0.95f);
        private static readonly Color FillColor = new Color(0.13f, 0.68f, 1f, 0.12f);
        private static readonly Color CenterColor = new Color(1f, 1f, 1f, 0.95f);

        private static readonly Vector3[] EllipsePoints = new Vector3[EllipseSegments + 1];

        private static Object _cachedTarget;
        private static string _cachedPropertyPath;
        private static SerializedObject _cachedSerializedObject;
        private static bool _cachedForcedMeshUpdate;
        private static Object _activeSerializedTarget;
        private static Object _activeSceneTarget;
        private static string _activePropertyPath;
        private static int _activeLayerIndex = -1;

        public static bool IsEditingPaint(SerializedProperty paint, Object sceneTarget)
        {
            return paint != null
                   && sceneTarget != null
                   && _activeSerializedTarget == paint.serializedObject.targetObject
                   && _activeSceneTarget == sceneTarget
                   && _activePropertyPath == paint.propertyPath;
        }

        public static void SetEditingPaint(SerializedProperty paint, Object sceneTarget, int layerIndex = -1)
        {
            _activeSerializedTarget = paint.serializedObject.targetObject;
            _activeSceneTarget = sceneTarget;
            _activePropertyPath = paint.propertyPath;
            _activeLayerIndex = layerIndex;
        }

        public static void ClearEditingPaint()
        {
            _activeSerializedTarget = null;
            _activeSceneTarget = null;
            _activePropertyPath = null;
            _activeLayerIndex = -1;
            ClearCache();
        }

        public static DrawResult Draw(Object sceneTarget)
        {
            if (!TryGetEditingPaint(sceneTarget, out var serializedTarget, out var propertyPath, out var layerIndex)) {
                return default;
            }

            var serializedObject = GetSerializedObject(serializedTarget, propertyPath);
            serializedObject.UpdateIfRequiredOrScript();

            var paintProperty = serializedObject.FindProperty(propertyPath);
            if (paintProperty == null) {
                ClearEditingPaint();
                SceneView.RepaintAll();
                return default;
            }

            var paint = new SerializedCanvasPaint(paintProperty);
            if (!CanvasPaintEditorUtility.IsEditableGradientPaint(paint, out var paintType)) {
                ClearEditingPaint();
                SceneView.RepaintAll();
                return default;
            }

            if (sceneTarget is not Component component) {
                return default;
            }

            var text = component.GetComponent<TextMeshProUGUI>();
            if (text == null || text.rectTransform == null) {
                return default;
            }

            var rectTransform = text.rectTransform;
            var paintBounds = GetPaintBounds(text);
            if (paintBounds.z <= 0f || paintBounds.w <= 0f) {
                return default;
            }

            var transform = paint.Transform;
            var center = transform.Center;
            var offset = transform.Offset;
            var scale = transform.Scale;
            var rotation = transform.Rotation;

            var changed = false;
            if (paintType == CanvasPaintType.LinearGradient) {
                changed = DrawLinear(rectTransform, paintBounds, center, offset, scale, rotation);
            } else {
                changed = DrawRadial(rectTransform, paintBounds, center, offset, scale, rotation);
            }

            if (!changed) {
                return default;
            }

            Undo.RecordObject(serializedTarget, "Edit Gradient Transform");
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(serializedTarget);
            if (serializedTarget is TextMeshProLayerPreset preset) {
                preset.NotifyChanged();
            }

            SceneView.RepaintAll();
            return new DrawResult(true, layerIndex);
        }

        private static bool DrawLinear(RectTransform rectTransform, Vector4 paintBounds, SerializedProperty center, SerializedProperty offset,
            SerializedProperty scale, SerializedProperty rotation)
        {
            var changed = false;
            var origin = center.vector2Value + offset.vector2Value;
            var scaleValue = scale.vector2Value;
            var direction = CanvasPaintEditorUtility.DirectionFromRotation(rotation.floatValue);
            var startUv = origin - direction * (scaleValue.x * 0.5f);
            var endUv = origin + direction * (scaleValue.x * 0.5f);

            var startWorld = UvToWorld(rectTransform, paintBounds, startUv);
            var endWorld = UvToWorld(rectTransform, paintBounds, endUv);
            var centerWorld = UvToWorld(rectTransform, paintBounds, origin);

            Handles.color = LineColor;
            Handles.DrawAAPolyLine(4f, startWorld, endWorld);

            var nextCenterWorld = DrawPlaneHandle(rectTransform, centerWorld, CenterColor, CenterHandleSize);
            if (!Approximately(nextCenterWorld, centerWorld)) {
                center.vector2Value = WorldToUv(rectTransform, paintBounds, nextCenterWorld);
                offset.vector2Value = Vector2.zero;
                changed = true;
            }

            var nextStartWorld = DrawPlaneHandle(rectTransform, startWorld, LineColor, HandleSize);
            if (!Approximately(nextStartWorld, startWorld)) {
                CanvasPaintEditorUtility.CalculateLinearTransformFromEndpoints(WorldToUv(rectTransform, paintBounds, nextStartWorld), endUv,
                    out var nextCenter, out var nextScale, out var nextRotation);
                center.vector2Value = nextCenter;
                offset.vector2Value = Vector2.zero;
                scale.vector2Value = new Vector2(nextScale.x, scale.vector2Value.y);
                rotation.floatValue = nextRotation;
                changed = true;
            }

            var nextEndWorld = DrawPlaneHandle(rectTransform, endWorld, LineColor, HandleSize);
            if (!Approximately(nextEndWorld, endWorld)) {
                CanvasPaintEditorUtility.CalculateLinearTransformFromEndpoints(startUv, WorldToUv(rectTransform, paintBounds, nextEndWorld),
                    out var nextCenter, out var nextScale, out var nextRotation);
                center.vector2Value = nextCenter;
                offset.vector2Value = Vector2.zero;
                scale.vector2Value = new Vector2(nextScale.x, scale.vector2Value.y);
                rotation.floatValue = nextRotation;
                changed = true;
            }

            return changed;
        }

        private static bool DrawRadial(RectTransform rectTransform, Vector4 paintBounds, SerializedProperty center, SerializedProperty offset,
            SerializedProperty scale, SerializedProperty rotation)
        {
            var changed = false;
            var origin = center.vector2Value + offset.vector2Value;
            var scaleValue = scale.vector2Value;
            var direction = CanvasPaintEditorUtility.DirectionFromRotation(rotation.floatValue);
            var perpendicular = CanvasPaintEditorUtility.PerpendicularFromRotation(rotation.floatValue);
            var primaryUv = origin + direction * (scaleValue.x * 0.5f);
            var secondaryUv = origin + perpendicular * (scaleValue.y * 0.5f);

            DrawEllipse(rectTransform, paintBounds, origin, direction, perpendicular, scaleValue);

            var centerWorld = UvToWorld(rectTransform, paintBounds, origin);
            var nextCenterWorld = DrawPlaneHandle(rectTransform, centerWorld, CenterColor, CenterHandleSize);
            if (!Approximately(nextCenterWorld, centerWorld)) {
                center.vector2Value = WorldToUv(rectTransform, paintBounds, nextCenterWorld);
                offset.vector2Value = Vector2.zero;
                changed = true;
            }

            var primaryWorld = UvToWorld(rectTransform, paintBounds, primaryUv);
            var nextPrimaryWorld = DrawPlaneHandle(rectTransform, primaryWorld, LineColor, HandleSize);
            if (!Approximately(nextPrimaryWorld, primaryWorld)) {
                var delta = WorldToUv(rectTransform, paintBounds, nextPrimaryWorld) - origin;
                var radius = Mathf.Max(CanvasPaintEditorUtility.MinScale * 0.5f, delta.magnitude);
                scale.vector2Value = new Vector2(radius * 2f, scale.vector2Value.y);
                rotation.floatValue = CanvasPaintEditorUtility.NormalizeDegrees(Mathf.Atan2(-delta.y, delta.x) * Mathf.Rad2Deg);
                offset.vector2Value = Vector2.zero;
                changed = true;
            }

            var secondaryWorld = UvToWorld(rectTransform, paintBounds, secondaryUv);
            var nextSecondaryWorld = DrawPlaneHandle(rectTransform, secondaryWorld, LineColor, HandleSize);
            if (!Approximately(nextSecondaryWorld, secondaryWorld)) {
                var delta = WorldToUv(rectTransform, paintBounds, nextSecondaryWorld) - origin;
                var projectedRadius = Mathf.Abs(Vector2.Dot(delta, perpendicular));
                scale.vector2Value = new Vector2(scale.vector2Value.x, Mathf.Max(CanvasPaintEditorUtility.MinScale, projectedRadius * 2f));
                offset.vector2Value = Vector2.zero;
                changed = true;
            }

            return changed;
        }

        private static Vector3 DrawPlaneHandle(RectTransform rectTransform, Vector3 position, Color color, float sizeFactor)
        {
            var right = rectTransform.TransformDirection(Vector3.right);
            var up = rectTransform.TransformDirection(Vector3.up);
            var normal = Vector3.Cross(right, up).normalized;
            var size = HandleUtility.GetHandleSize(position) * sizeFactor;
            Handles.color = color;
            return Handles.Slider2D(position, normal, right, up, size, Handles.CircleHandleCap, Vector2.zero);
        }

        private static void DrawEllipse(RectTransform rectTransform, Vector4 paintBounds, Vector2 origin, Vector2 direction, Vector2 perpendicular, Vector2 scale)
        {
            for (int i = 0; i <= EllipseSegments; i++) {
                var t = i / (float)EllipseSegments * Mathf.PI * 2f;
                var uv = origin + direction * (Mathf.Cos(t) * scale.x * 0.5f) + perpendicular * (Mathf.Sin(t) * scale.y * 0.5f);
                EllipsePoints[i] = UvToWorld(rectTransform, paintBounds, uv);
            }

            if (Event.current.type == EventType.Repaint && GUIUtility.hotControl == 0) {
                Handles.color = FillColor;
                Handles.DrawAAConvexPolygon(EllipsePoints);
            }

            Handles.color = LineColor;
            Handles.DrawAAPolyLine(3f, EllipsePoints);
        }

        private static Vector3 UvToWorld(RectTransform rectTransform, Vector4 paintBounds, Vector2 uv)
        {
            var local = new Vector3(paintBounds.x + uv.x * paintBounds.z, paintBounds.y + uv.y * paintBounds.w, 0f);
            return rectTransform.TransformPoint(local);
        }

        private static Vector2 WorldToUv(RectTransform rectTransform, Vector4 paintBounds, Vector3 world)
        {
            var local = rectTransform.InverseTransformPoint(world);
            return new Vector2(
                (local.x - paintBounds.x) / Mathf.Max(0.0001f, paintBounds.z),
                (local.y - paintBounds.y) / Mathf.Max(0.0001f, paintBounds.w));
        }

        private static SerializedObject GetSerializedObject(Object target, string propertyPath)
        {
            if (_cachedSerializedObject == null || _cachedTarget != target || _cachedPropertyPath != propertyPath) {
                _cachedTarget = target;
                _cachedPropertyPath = propertyPath;
                _cachedSerializedObject = new SerializedObject(target);
                _cachedForcedMeshUpdate = false;
            }

            return _cachedSerializedObject;
        }

        private static Vector4 GetPaintBounds(TextMeshProUGUI text)
        {
            if (text.TryGetComponent<TextMeshProLayerStack>(out var layerStack) &&
                layerStack.TryGetCurrentPaintBounds(out var stackBounds)) {
                return stackBounds;
            }

            if (TextMeshProUtility.TryCalculateGlyphBounds(text.textInfo, out var bounds)) {
                return bounds;
            }

            if (!_cachedForcedMeshUpdate) {
                text.ForceMeshUpdate();
                _cachedForcedMeshUpdate = true;
                if (TextMeshProUtility.TryCalculateGlyphBounds(text.textInfo, out bounds)) {
                    return bounds;
                }
            }

            return TextMeshProUtility.CalculateFrameBounds(text);
        }

        private static void ClearCache()
        {
            _cachedTarget = null;
            _cachedPropertyPath = null;
            _cachedSerializedObject = null;
            _cachedForcedMeshUpdate = false;
        }

        private static bool TryGetEditingPaint(Object sceneTarget, out Object serializedTarget, out string propertyPath, out int layerIndex)
        {
            serializedTarget = null;
            propertyPath = null;
            layerIndex = -1;

            if (sceneTarget == null ||
                _activeSceneTarget != sceneTarget ||
                _activeSerializedTarget == null ||
                string.IsNullOrEmpty(_activePropertyPath)) {
                return false;
            }

            serializedTarget = _activeSerializedTarget;
            propertyPath = _activePropertyPath;
            layerIndex = _activeLayerIndex;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool Approximately(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude <= 0.0000001f;
        }
    }
}