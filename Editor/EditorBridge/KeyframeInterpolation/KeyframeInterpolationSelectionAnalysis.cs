using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    internal readonly struct KeyframeInterpolationSelectionAnalysis
    {
        public readonly int EditablePairCount;
        public readonly bool HasCommonMode;
        public readonly AnimationUtility.TangentMode CommonMode;
        public readonly bool HasCommonCurve;
        public readonly AnimationCurve CommonCurve;

        public KeyframeInterpolationSelectionAnalysis(
            int editablePairCount,
            bool hasCommonMode,
            AnimationUtility.TangentMode commonMode,
            bool hasCommonCurve,
            AnimationCurve commonCurve)
        {
            EditablePairCount = editablePairCount;
            HasCommonMode = hasCommonMode;
            CommonMode = commonMode;
            HasCommonCurve = hasCommonCurve;
            CommonCurve = commonCurve;
        }

        public static KeyframeInterpolationSelectionAnalysis Analyze(IReadOnlyList<KeyframeInterpolationCurveSelection> selections)
        {
            var editablePairCount = 0;
            var hasAnyMode = false;
            var hasCommonMode = false;
            var commonMode = AnimationUtility.TangentMode.Free;
            var hasAnyCurve = false;
            var hasCommonCurve = false;
            AnimationCurve commonCurve = null;

            for (var i = 0; i < selections.Count; i++) {
                var selection = selections[i];
                if (!selection.AnimationIsEditable || selection.IsObjectReferenceCurve || selection.IsDiscreteCurve) {
                    continue;
                }

                var curve = selection.LoadCurrentCurve();
                if (curve == null || curve.length < 2) {
                    continue;
                }

                var selectedKeys = KeyframeInterpolationTangentUtility.ResolveSelectedKeyEdits(curve, selection.Keys);
                if (selectedKeys.Count == 0) {
                    continue;
                }

                for (var keyIndex = 0; keyIndex < selectedKeys.Count; keyIndex++) {
                    var selectedKey = selectedKeys[keyIndex];
                    editablePairCount++;
                    if (!KeyframeInterpolationTangentUtility.TryGetKeyInterpolation(curve, selectedKey, out var segmentInterpolation)) {
                        hasCommonMode = false;
                        hasCommonCurve = false;
                        continue;
                    }

                    if (!hasAnyMode) {
                        commonMode = segmentInterpolation.Mode;
                        hasAnyMode = true;
                        hasCommonMode = true;
                    } else if (commonMode != segmentInterpolation.Mode) {
                        hasCommonMode = false;
                    }

                    if (!segmentInterpolation.HasCurve || segmentInterpolation.Curve == null) {
                        hasCommonCurve = false;
                        continue;
                    }

                    if (!hasAnyCurve) {
                        commonCurve = KeyframeInterpolationCurveUtility.Clone(segmentInterpolation.Curve);
                        hasAnyCurve = true;
                        hasCommonCurve = true;
                    } else if (!KeyframeInterpolationCurveUtility.ApproximatelyEditableShape(commonCurve, segmentInterpolation.Curve)) {
                        hasCommonCurve = false;
                    }
                }
            }

            return new KeyframeInterpolationSelectionAnalysis(
                editablePairCount: editablePairCount,
                hasCommonMode: hasAnyMode && hasCommonMode,
                commonMode: commonMode,
                hasCommonCurve: hasAnyCurve && hasCommonCurve,
                commonCurve: commonCurve);
        }
    }
}
