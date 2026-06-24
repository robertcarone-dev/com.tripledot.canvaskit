using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor.KeyframeInterpolation
{
    internal sealed class KeyframeInterpolationEditSession
    {
        private readonly AnimationWindow window;
        private readonly string undoLabel;
        private readonly bool continuous;
        private readonly int undoGroup;
        private bool committed;
        private bool ended;

        public KeyframeInterpolationEditSession(AnimationWindow window, string undoLabel, bool continuous)
        {
            this.window = window;
            this.undoLabel = undoLabel;
            this.continuous = continuous;
            undoGroup = Undo.GetCurrentGroup();
            if (continuous) {
                Undo.SetCurrentGroupName(undoLabel);
            }
        }

        public bool ApplyCurve(IReadOnlyList<KeyframeInterpolationCurveSelection> selections, AnimationCurve interpolationCurve)
        {
            return ApplyToSelections(selections,
                (curve, selectedSegments) => KeyframeInterpolationTangentUtility.ApplyCurve(curve, selectedSegments, interpolationCurve));
        }

        public bool ApplyPreset(IReadOnlyList<KeyframeInterpolationCurveSelection> selections, KeyframeInterpolationPreset preset)
        {
            return ApplyToSelections(selections,
                (curve, selectedSegments) => KeyframeInterpolationTangentUtility.ApplyPreset(curve, selectedSegments, preset));
        }

        public bool ApplyMode(IReadOnlyList<KeyframeInterpolationCurveSelection> selections, AnimationUtility.TangentMode mode)
        {
            return ApplyToSelections(selections,
                (curve, selectedSegments) => KeyframeInterpolationTangentUtility.ApplyMode(curve, selectedSegments, mode));
        }

        public bool Commit(IReadOnlyList<KeyframeInterpolationCurveSelection> selections)
        {
            if (!ended) {
                var saved = KeyframeInterpolationAnimationBridge.CommitSavedCurves(window, selections, undoLabel);
                committed |= saved;
                return saved;
            }

            return false;
        }

        private bool ApplyToSelections(IReadOnlyList<KeyframeInterpolationCurveSelection> selections, KeyframeInterpolationCurveEditStager.CurveMutation mutateCurve)
        {
            if (ended || selections.Count == 0) {
                return false;
            }

            KeyframeInterpolationAnimationBridge.SynchronizeSelectionForEditing(window, undoLabel);

            var resolvedSelections = new List<KeyframeInterpolationCurveSelection>();
            if (!KeyframeInterpolationAnimationBridge.TryResolveCurrentSelections(window, selections, resolvedSelections)) {
                return false;
            }

            var stagedEdits = new List<KeyframeInterpolationCurveEditStager.StagedCurveEdit>();
            if (!KeyframeInterpolationCurveEditStager.TryStageEdits(resolvedSelections, mutateCurve, stagedEdits)) {
                return false;
            }

            var changedSelections = new List<KeyframeInterpolationCurveSelection>();
            foreach (var stagedEdit in stagedEdits) {
                if (!stagedEdit.Selection.ApplyEditedCurve(stagedEdit.Curve)) {
                    return false;
                }

                changedSelections.Add(stagedEdit.Selection);
            }

            return changedSelections.Count != 0 && Commit(changedSelections);
        }

        public void End()
        {
            if (!ended) {
                ended = true;
                if (continuous && committed) {
                    Undo.CollapseUndoOperations(undoGroup);
                }
            }
        }
    }
}