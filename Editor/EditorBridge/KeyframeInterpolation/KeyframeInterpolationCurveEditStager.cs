using System.Collections.Generic;
using Tripledot.CanvasKit.Editor;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor.KeyframeInterpolation
{
    internal static class KeyframeInterpolationCurveEditStager
    {
        public delegate int CurveMutation(AnimationCurve curve, IReadOnlyList<KeyframeInterpolationTangentUtility.KeyframeInterpolationSegmentSelection> selectedSegments);

        public readonly struct StagedCurveEdit
        {
            public readonly KeyframeInterpolationCurveSelection Selection;
            public readonly AnimationCurve Curve;

            public StagedCurveEdit(KeyframeInterpolationCurveSelection selection, AnimationCurve curve)
            {
                Selection = selection;
                Curve = curve;
            }
        }

        public static bool TryStageEdits(
            IReadOnlyList<KeyframeInterpolationCurveSelection> selections,
            CurveMutation mutateCurve,
            List<StagedCurveEdit> stagedEdits)
        {
            stagedEdits.Clear();
            if (selections == null || selections.Count == 0 || mutateCurve == null) {
                return false;
            }

            foreach (var selection in selections) {
                if (selection.IsObjectReferenceCurve || selection.IsDiscreteCurve || !selection.AnimationIsEditable) {
                    continue;
                }

                var sourceCurve = selection.LoadCurrentCurve();
                if (sourceCurve == null || sourceCurve.length < 2) {
                    continue;
                }

                var curve = KeyframeInterpolationCurveUtility.Clone(sourceCurve);
                var selectedSegments = KeyframeInterpolationTangentUtility.ResolveSelectedSegments(curve, selection.Keys);
                if (selectedSegments.Count == 0) {
                    continue;
                }

                if (KeyframeInterpolationTangentUtility.HasDuplicateKeyTimes(sourceCurve)
                    || mutateCurve(curve, selectedSegments) <= 0
                    || !KeyframeInterpolationCurveUtility.TryGetSanitizedCurveForSave(curve, out var sanitizedCurve)
                    || !selection.CanApplyEditedCurve(sanitizedCurve)) {
                    stagedEdits.Clear();
                    return false;
                }

                stagedEdits.Add(new StagedCurveEdit(selection, sanitizedCurve));
            }

            return stagedEdits.Count > 0;
        }
    }
}