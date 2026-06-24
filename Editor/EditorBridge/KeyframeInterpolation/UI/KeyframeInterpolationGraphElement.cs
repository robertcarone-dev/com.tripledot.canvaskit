using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tripledot.CanvasKit.Editor.KeyframeInterpolation
{
    internal sealed class KeyframeInterpolationGraphElement : VisualElement
    {
        private enum DragHandle
        {
            None,
            Out,
            In
        }

        public event Action<AnimationCurve> CurveChanged;
        public event Action DragEnded;

        public Func<Rect, bool> BeginDragRequested { get; set; }

        private AnimationCurve curve;
        private readonly VisualElement handleReadout;
        private readonly Label outXReadoutLabel;
        private readonly Label outCommaReadoutLabel;
        private readonly Label outYReadoutLabel;
        private readonly Label middleCommaReadoutLabel;
        private readonly Label inXReadoutLabel;
        private readonly Label inCommaReadoutLabel;
        private readonly Label inYReadoutLabel;
        private DragHandle draggingHandle;
        private Rect draggingCurveRange;
        private Vector2 lastPointerPosition;
        private int activePointerId;
        private AnimationUtility.TangentMode displayMode = AnimationUtility.TangentMode.Free;
        private bool hasCurveDisplay;
        private bool canEditHandles;
        private bool canConvertGraphToFree;
        private bool hasMixedCurveValues;
        private bool hasTimeCursor;
        private bool releasingPointer;
        private float timeCursor;

        public KeyframeInterpolationGraphElement()
        {
            pickingMode = PickingMode.Position;
            focusable = true;
            style.minHeight = KeyframeInterpolationGraphUtility.GraphMinHeight;
            style.flexGrow = 1f;
            style.flexShrink = 1f;
            generateVisualContent += OnGenerateVisualContent;

            handleReadout = new VisualElement { pickingMode = PickingMode.Ignore };
            handleReadout.AddToClassList("ck-keyframe-graph__readout");

            outXReadoutLabel = CreateReadoutLabel("ck-keyframe-graph__readout-out");
            outCommaReadoutLabel = CreateReadoutLabel("ck-keyframe-graph__readout-separator");
            outYReadoutLabel = CreateReadoutLabel("ck-keyframe-graph__readout-out");
            middleCommaReadoutLabel = CreateReadoutLabel("ck-keyframe-graph__readout-separator");
            inXReadoutLabel = CreateReadoutLabel("ck-keyframe-graph__readout-in");
            inCommaReadoutLabel = CreateReadoutLabel("ck-keyframe-graph__readout-separator");
            inYReadoutLabel = CreateReadoutLabel("ck-keyframe-graph__readout-in");

            handleReadout.Add(outXReadoutLabel);
            handleReadout.Add(outCommaReadoutLabel);
            handleReadout.Add(outYReadoutLabel);
            handleReadout.Add(middleCommaReadoutLabel);
            handleReadout.Add(inXReadoutLabel);
            handleReadout.Add(inCommaReadoutLabel);
            handleReadout.Add(inYReadoutLabel);
            Add(handleReadout);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCancelEvent>(OnPointerCanceled);
            RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            RegisterCallback<DetachFromPanelEvent>(_ => EndDrag(true));
        }

        public void SetState(
            AnimationCurve nextCurve,
            AnimationUtility.TangentMode nextDisplayMode,
            bool nextHasCurveDisplay,
            bool nextCanEditHandles,
            bool nextCanConvertGraphToFree,
            bool nextHasMixedCurveValues,
            bool nextHasTimeCursor,
            float nextTimeCursor)
        {
            displayMode = nextDisplayMode;
            curve = CreateDisplayCurve(nextCurve, nextDisplayMode, nextHasCurveDisplay, nextHasMixedCurveValues);
            hasCurveDisplay = nextHasCurveDisplay;
            canEditHandles = nextCanEditHandles;
            canConvertGraphToFree = nextCanConvertGraphToFree;
            hasMixedCurveValues = nextHasMixedCurveValues;
            hasTimeCursor = nextHasTimeCursor;
            timeCursor = nextTimeCursor;

            EnableInClassList("ck-keyframe-graph--disabled", !hasCurveDisplay);
            EnableInClassList("ck-keyframe-graph--mixed", hasMixedCurveValues);

            UpdateHandleReadout();
            MarkDirtyRepaint();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if ((!canEditHandles && !canConvertGraphToFree) || curve == null || evt.button != 0 || hasMixedCurveValues) {
                return;
            }

            var graphRect = GetLocalGraphRect();
            var plotRect = KeyframeInterpolationGraphUtility.GetGraphPlotRect(graphRect);
            var editableCurve = KeyframeInterpolationCurveUtility.NormalizeEditableCurve(curve);
            var displayRange = KeyframeInterpolationCurveUtility.GetCurveRange(editableCurve);

            KeyframeInterpolationGraphUtility.GetHandlePoints(editableCurve, out var outHandle, out var inHandle);
            var outPosition = KeyframeInterpolationGraphUtility.CurveToGUI(plotRect, outHandle, displayRange.yMin, displayRange.yMax);
            var inPosition = KeyframeInterpolationGraphUtility.CurveToGUI(plotRect, inHandle, displayRange.yMin, displayRange.yMax);
            var pointerPosition = AsVector2(evt.localPosition);

            var outDistance = Vector2.Distance(pointerPosition, outPosition);
            var inDistance = Vector2.Distance(pointerPosition, inPosition);

            if (canEditHandles && outDistance <= KeyframeInterpolationGraphUtility.HandleHitSize) {
                if (BeginDrag(DragHandle.Out, evt, displayRange, editableCurve)) {
                    evt.StopImmediatePropagation();
                }
            } else if (canEditHandles && inDistance <= KeyframeInterpolationGraphUtility.HandleHitSize) {
                if (BeginDrag(DragHandle.In, evt, displayRange, editableCurve)) {
                    evt.StopImmediatePropagation();
                }
            } else if (canConvertGraphToFree) {
                var nearestHandle = outDistance <= inDistance ? DragHandle.Out : DragHandle.In;
                if (BeginDrag(nearestHandle, evt, displayRange, editableCurve)) {
                    evt.StopImmediatePropagation();
                }
            }
        }

        private bool BeginDrag(DragHandle handle, PointerDownEvent evt, Rect displayRange, AnimationCurve dragCurve)
        {
            if (BeginDragRequested == null || !BeginDragRequested.Invoke(displayRange)) {
                return false;
            }

            curve = dragCurve;
            displayMode = AnimationUtility.TangentMode.Free;
            canEditHandles = true;
            canConvertGraphToFree = false;
            draggingHandle = handle;
            draggingCurveRange = displayRange;
            activePointerId = evt.pointerId;
            lastPointerPosition = AsVector2(evt.localPosition);

            Focus();
            this.CapturePointer(activePointerId);

            UpdateHandleReadout();
            MarkDirtyRepaint();

            return true;
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (draggingHandle == DragHandle.None || evt.pointerId != activePointerId || !this.HasPointerCapture(activePointerId) || curve == null) {
                return;
            }

            var pointerPosition = AsVector2(evt.localPosition);
            var delta = pointerPosition - lastPointerPosition;
            lastPointerPosition = pointerPosition;
            if (delta.sqrMagnitude <= Mathf.Epsilon) {
                return;
            }

            var graphRect = GetLocalGraphRect();
            var plotRect = KeyframeInterpolationGraphUtility.GetGraphPlotRect(graphRect);

            KeyframeInterpolationGraphUtility.GetHandlePoints(curve, out var outHandle, out var inHandle);
            var handle = draggingHandle == DragHandle.Out ? outHandle : inHandle;
            var normalized = KeyframeInterpolationGraphUtility.ApplyHandleDragDelta(handle, delta, plotRect, draggingCurveRange);
            curve = draggingHandle == DragHandle.Out ? KeyframeInterpolationGraphUtility.SetOutHandle(curve, normalized) : KeyframeInterpolationGraphUtility.SetInHandle(curve, normalized);

            UpdateHandleReadout();
            MarkDirtyRepaint();

            CurveChanged?.Invoke(KeyframeInterpolationCurveUtility.Clone(curve));
            evt.StopImmediatePropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (draggingHandle != DragHandle.None && evt.pointerId == activePointerId) {
                EndDrag(true);
                evt.StopImmediatePropagation();
            }
        }

        private void OnPointerCanceled(PointerCancelEvent evt)
        {
            if (draggingHandle != DragHandle.None && evt.pointerId == activePointerId) {
                EndDrag(true);
                evt.StopImmediatePropagation();
            }
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (!releasingPointer && draggingHandle != DragHandle.None && evt.pointerId == activePointerId) {
                EndDrag(false);
            }
        }

        private void EndDrag(bool releasePointer)
        {
            if (draggingHandle == DragHandle.None) {
                return;
            }

            if (releasePointer && this.HasPointerCapture(activePointerId)) {
                releasingPointer = true;
                this.ReleasePointer(activePointerId);
                releasingPointer = false;
            }

            draggingHandle = DragHandle.None;
            activePointerId = 0;
            DragEnded?.Invoke();
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            var graphRect = GetLocalGraphRect();
            if (graphRect.width <= 0.01f || graphRect.height <= 0.01f) {
                return;
            }

            var painter = context.painter2D;
            var enabled = hasCurveDisplay && !hasMixedCurveValues;
            var plotRect = KeyframeInterpolationGraphUtility.GetGraphPlotRect(graphRect);
            var displayCurve = curve;
            var drawCurve = hasCurveDisplay && !hasMixedCurveValues && displayCurve != null;
            var range = drawCurve ? KeyframeInterpolationCurveUtility.GetCurveRange(displayCurve) : new Rect(0f, 0f, 1f, 1f);

            DrawGraphBackground(painter, graphRect, plotRect, range.yMin, range.yMax, enabled && drawCurve);
            DrawTimeCursor(painter, plotRect);

            if (drawCurve) {
                DrawModeCurve(painter, plotRect, displayCurve, range.yMin, range.yMax, enabled);
                if (ShouldShowHandles()) {
                    KeyframeInterpolationGraphUtility.GetHandlePoints(displayCurve, out var outHandle, out var inHandle);
                    var outPosition = KeyframeInterpolationGraphUtility.CurveToGUI(plotRect, outHandle, range.yMin, range.yMax);
                    var inPosition = KeyframeInterpolationGraphUtility.CurveToGUI(plotRect, inHandle, range.yMin, range.yMax);

                    DrawHandleLines(painter, plotRect, outPosition, inPosition, range.yMin, range.yMax, enabled);
                    DrawDisc(painter, outPosition, 3.5f, KeyframeInterpolationGraphUtility.GetColor(KeyframeInterpolationGraphUtility.OutHandleColor, enabled));
                    DrawDisc(painter, inPosition, 3.5f, KeyframeInterpolationGraphUtility.GetColor(KeyframeInterpolationGraphUtility.InHandleColor, enabled));
                }
            }
        }

        private static AnimationCurve CreateDisplayCurve(
            AnimationCurve nextCurve,
            AnimationUtility.TangentMode nextDisplayMode,
            bool nextHasCurveDisplay,
            bool nextHasMixedCurveValues)
        {
            if (nextCurve == null || !nextHasCurveDisplay || nextHasMixedCurveValues) {
                return null;
            }

            return nextDisplayMode == AnimationUtility.TangentMode.Free
                ? KeyframeInterpolationCurveUtility.NormalizeEditableCurve(nextCurve)
                : KeyframeInterpolationCurveUtility.Clone(nextCurve);
        }

        private bool ShouldShowHandles()
        {
            return canEditHandles
                   && displayMode == AnimationUtility.TangentMode.Free
                   && hasCurveDisplay
                   && !hasMixedCurveValues
                   && curve != null;
        }

        private Rect GetLocalGraphRect()
        {
            return new Rect(0f, 0f, Mathf.Max(1f, contentRect.width), Mathf.Max(1f, contentRect.height));
        }

        private static Vector2 AsVector2(Vector3 value)
        {
            return new Vector2(value.x, value.y);
        }

        private static Label CreateReadoutLabel(string className)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        private void UpdateHandleReadout()
        {
            var showReadout = ShouldShowHandles();
            handleReadout.style.display = showReadout ? DisplayStyle.Flex : DisplayStyle.None;
            if (!showReadout) {
                return;
            }

            KeyframeInterpolationGraphUtility.GetHandlePoints(curve, out var outHandle, out var inHandle);
            outXReadoutLabel.text = KeyframeInterpolationGraphUtility.FormatHandleValue(outHandle.x);
            outCommaReadoutLabel.text = ", ";
            outYReadoutLabel.text = KeyframeInterpolationGraphUtility.FormatHandleValue(outHandle.y);
            middleCommaReadoutLabel.text = ", ";
            inXReadoutLabel.text = KeyframeInterpolationGraphUtility.FormatHandleValue(inHandle.x);
            inCommaReadoutLabel.text = ", ";
            inYReadoutLabel.text = KeyframeInterpolationGraphUtility.FormatHandleValue(inHandle.y);

            SetReadoutColor(outXReadoutLabel, KeyframeInterpolationGraphUtility.OutHandleColor);
            SetReadoutColor(outYReadoutLabel, KeyframeInterpolationGraphUtility.OutHandleColor);
            SetReadoutColor(inXReadoutLabel, KeyframeInterpolationGraphUtility.InHandleColor);
            SetReadoutColor(inYReadoutLabel, KeyframeInterpolationGraphUtility.InHandleColor);
            SetSeparatorReadoutColor(outCommaReadoutLabel);
            SetSeparatorReadoutColor(middleCommaReadoutLabel);
            SetSeparatorReadoutColor(inCommaReadoutLabel);
        }

        private void SetReadoutColor(Label label, Color color)
        {
            label.style.color = KeyframeInterpolationGraphUtility.GetColor(new Color(color.r, color.g, color.b, 0.66f), canEditHandles);
        }

        private void SetSeparatorReadoutColor(Label label)
        {
            label.style.color = KeyframeInterpolationGraphUtility.GetColor(new Color(1f, 1f, 1f, 0.32f), canEditHandles);
        }

        private static void DrawGraphBackground(Painter2D painter, Rect graphRect, Rect plotRect, float minY, float maxY, bool enabled)
        {
            FillRect(painter, graphRect, KeyframeInterpolationGraphUtility.GetColor(KeyframeInterpolationGraphUtility.GraphBorderColor, enabled));
            FillRect(painter, new Rect(graphRect.x + 2f, graphRect.y + 2f, graphRect.width - 4f, graphRect.height - 4f),
                KeyframeInterpolationGraphUtility.GetColor(KeyframeInterpolationGraphUtility.GraphColor, enabled));
            DrawVerticalAxis(painter, plotRect, 0f, enabled);
            DrawVerticalAxis(painter, plotRect, 1f, enabled);
            DrawHorizontalAxis(painter, plotRect, 0f, minY, maxY, enabled);
            DrawHorizontalAxis(painter, plotRect, 1f, minY, maxY, enabled);
        }

        private void DrawTimeCursor(Painter2D painter, Rect rect)
        {
            if (!hasTimeCursor || timeCursor < 0f || timeCursor > 1f) {
                return;
            }

            var x = Mathf.Lerp(rect.x, rect.xMax, timeCursor);
            FillRect(painter, new Rect(x - 0.5f, rect.y, 1f, rect.height), KeyframeInterpolationGraphUtility.TimeCursorColor);
        }

        private static void DrawVerticalAxis(Painter2D painter, Rect rect, float normalizedX, bool enabled)
        {
            var x = Mathf.Lerp(rect.x, rect.xMax, normalizedX);
            FillRect(painter, new Rect(x, rect.y, 1f, rect.height), KeyframeInterpolationGraphUtility.GetColor(KeyframeInterpolationGraphUtility.AxisColor, enabled));
        }

        private static void DrawHorizontalAxis(Painter2D painter, Rect rect, float value, float minY, float maxY, bool enabled)
        {
            if (value < minY || value > maxY) {
                return;
            }

            var y = Mathf.Clamp(KeyframeInterpolationGraphUtility.CurveToGUI(rect, new Vector2(0f, value), minY, maxY).y, rect.y, rect.yMax);
            FillRect(painter, new Rect(rect.x, y, rect.width, 1f), KeyframeInterpolationGraphUtility.GetColor(KeyframeInterpolationGraphUtility.AxisColor, enabled));
        }

        private static void DrawCurve(Painter2D painter, Rect rect, AnimationCurve curve, float minY, float maxY, bool enabled)
        {
            var segmentCount = Mathf.Clamp(Mathf.CeilToInt(rect.width * 2f), KeyframeInterpolationGraphUtility.GraphCurveMinSegments, KeyframeInterpolationGraphUtility.GraphCurveMaxSegments);
            painter.lineWidth = 2f;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;

            var previousTime = 0f;
            var previousPoint = KeyframeInterpolationGraphUtility.CurveToGUI(rect, new Vector2(previousTime, curve.Evaluate(previousTime)), minY, maxY);

            for (var i = 1; i < segmentCount; i++) {
                var time = i / (float)(segmentCount - 1);
                var point = KeyframeInterpolationGraphUtility.CurveToGUI(rect, new Vector2(time, curve.Evaluate(time)), minY, maxY);
                var color = Color.Lerp(KeyframeInterpolationGraphUtility.OutHandleColor, KeyframeInterpolationGraphUtility.InHandleColor, (previousTime + time) * 0.5f);

                painter.strokeColor = KeyframeInterpolationGraphUtility.GetColor(color, enabled);
                painter.BeginPath();
                painter.MoveTo(previousPoint);
                painter.LineTo(point);
                painter.Stroke();

                previousTime = time;
                previousPoint = point;
            }
        }

        private void DrawModeCurve(Painter2D painter, Rect rect, AnimationCurve displayCurve, float minY, float maxY, bool enabled)
        {
            switch (displayMode) {
                case AnimationUtility.TangentMode.Constant:
                    DrawConstantCurve(painter, rect, minY, maxY, enabled);
                    break;
                case AnimationUtility.TangentMode.Linear:
                    DrawLinearCurve(painter, rect, minY, maxY, enabled);
                    break;
                default:
                    DrawCurve(painter, rect, displayCurve, minY, maxY, enabled);
                    break;
            }
        }

        private static void DrawLinearCurve(Painter2D painter, Rect rect, float minY, float maxY, bool enabled)
        {
            painter.lineWidth = 2f;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;
            painter.strokeColor = KeyframeInterpolationGraphUtility.GetColor(
                Color.Lerp(KeyframeInterpolationGraphUtility.OutHandleColor, KeyframeInterpolationGraphUtility.InHandleColor, 0.5f),
                enabled);

            painter.BeginPath();
            painter.MoveTo(KeyframeInterpolationGraphUtility.CurveToGUI(rect, Vector2.zero, minY, maxY));
            painter.LineTo(KeyframeInterpolationGraphUtility.CurveToGUI(rect, Vector2.one, minY, maxY));
            painter.Stroke();
        }

        private static void DrawConstantCurve(Painter2D painter, Rect rect, float minY, float maxY, bool enabled)
        {
            var start = KeyframeInterpolationGraphUtility.CurveToGUI(rect, Vector2.zero, minY, maxY);
            var end = KeyframeInterpolationGraphUtility.CurveToGUI(rect, Vector2.one, minY, maxY);
            var stepX = Mathf.Max(rect.xMin, rect.xMax - 0.5f);
            var lowStep = new Vector2(stepX, start.y);
            var highStep = new Vector2(stepX, end.y);

            painter.lineWidth = 2f;
            painter.lineJoin = LineJoin.Miter;
            painter.lineCap = LineCap.Butt;

            painter.strokeColor = KeyframeInterpolationGraphUtility.GetColor(KeyframeInterpolationGraphUtility.OutHandleColor, enabled);
            painter.BeginPath();
            painter.MoveTo(start);
            painter.LineTo(lowStep);
            painter.Stroke();

            painter.strokeColor = KeyframeInterpolationGraphUtility.GetColor(KeyframeInterpolationGraphUtility.InHandleColor, enabled);
            painter.BeginPath();
            painter.MoveTo(lowStep);
            painter.LineTo(highStep);
            painter.LineTo(end);
            painter.Stroke();
        }

        private static void DrawHandleLines(Painter2D painter, Rect rect, Vector2 outPosition, Vector2 inPosition, float minY, float maxY, bool enabled)
        {
            painter.lineWidth = 1f;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;

            painter.strokeColor = KeyframeInterpolationGraphUtility.GetColor(KeyframeInterpolationGraphUtility.OutHandleColor, enabled);
            painter.BeginPath();
            painter.MoveTo(KeyframeInterpolationGraphUtility.CurveToGUI(rect, Vector2.zero, minY, maxY));
            painter.LineTo(outPosition);
            painter.Stroke();

            painter.strokeColor = KeyframeInterpolationGraphUtility.GetColor(KeyframeInterpolationGraphUtility.InHandleColor, enabled);
            painter.BeginPath();
            painter.MoveTo(KeyframeInterpolationGraphUtility.CurveToGUI(rect, Vector2.one, minY, maxY));
            painter.LineTo(inPosition);
            painter.Stroke();
        }

        private static void FillRect(Painter2D painter, Rect rect, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }

        private static void DrawDisc(Painter2D painter, Vector2 center, float radius, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.Fill();
        }
    }
}