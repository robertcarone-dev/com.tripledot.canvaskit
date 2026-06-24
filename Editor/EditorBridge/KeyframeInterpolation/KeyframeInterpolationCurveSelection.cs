using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor.KeyframeInterpolation
{
    internal enum KeyframeInterpolationSelectionSource
    {
        AnimationWindowCurve,
        CurveWrapper
    }
    
    internal readonly struct KeyframeInterpolationKeySelection
    {
        public readonly int Index;
        public readonly float Time;
        public readonly CurveSelection.SelectionType Type;

        public KeyframeInterpolationKeySelection(int index, float time, CurveSelection.SelectionType type = CurveSelection.SelectionType.Key)
        {
            Index = index;
            Time = time;
            Type = type;
        }
    }
    
    internal sealed class KeyframeInterpolationCurveSelection
    {
        public readonly List<KeyframeInterpolationKeySelection> Keys = new List<KeyframeInterpolationKeySelection>();
        private readonly KeyframeInterpolationCurveTarget target;
        private bool hasPendingSave;
        private AnimationCurve pendingSaveCurve;

        private KeyframeInterpolationCurveSelection(KeyframeInterpolationCurveTarget target)
        {
            this.target = target;
        }

        public static KeyframeInterpolationCurveSelection CreateForAnimationWindowCurve(AnimationClip clipAsset, IAnimationWindowClip clipObject, AnimationWindowCurve windowCurve)
        {
            return new KeyframeInterpolationCurveSelection(new AnimationWindowCurveTarget(
                clipAsset,
                windowCurve.binding,
                clipObject is { isReadOnly: false },
                windowCurve.isPPtrCurve,
                windowCurve.isDiscreteCurve,
                clipObject,
                windowCurve));
        }

        public static KeyframeInterpolationCurveSelection CreateForCurveWrapper(
            AnimationClip clipAsset,
            IAnimationWindowClip clipObject,
            CurveWrapper curveWrapper,
            bool isObjectReferenceCurve,
            bool isDiscreteCurve)
        {
            var binding = curveWrapper.binding;
            return new KeyframeInterpolationCurveSelection(new CurveWrapperTarget(
                clipAsset,
                binding,
                clipObject is { isReadOnly: false } && curveWrapper.animationIsEditable,
                isObjectReferenceCurve,
                isDiscreteCurve,
                clipObject,
                curveWrapper));
        }

        public AnimationClip ClipAsset => target.ClipAsset;
        public EditorCurveBinding Binding => target.Binding;
        public bool AnimationIsEditable => target.AnimationIsEditable;
        public bool IsObjectReferenceCurve => target.IsObjectReferenceCurve;
        public bool IsDiscreteCurve => target.IsDiscreteCurve;
        public IAnimationWindowClip ClipObject => target.ClipObject;
        public KeyframeInterpolationSelectionSource Source => target.Source;
        public bool HasPendingSave => hasPendingSave;

        public AnimationCurve LoadCurrentCurve()
        {
            return target.LoadCurrentCurve();
        }

        public bool ApplyEditedCurve(AnimationCurve curve)
        {
            if (!target.ApplyEditedCurve(curve, out var curveForSave)) {
                return false;
            }

            pendingSaveCurve = KeyframeInterpolationCurveUtility.Clone(curveForSave);
            hasPendingSave = true;
            return true;
        }

        public bool CanApplyEditedCurve(AnimationCurve curve)
        {
            return target.CanApplyEditedCurve(curve);
        }

        public bool TryGetPendingSaveCurve(out AnimationCurve curve)
        {
            curve = hasPendingSave && pendingSaveCurve != null
                ? KeyframeInterpolationCurveUtility.Clone(pendingSaveCurve)
                : null;
            return curve != null;
        }

        public bool ReferencesAnimationWindowCurve(AnimationWindowCurve windowCurve)
        {
            return target.ReferencesAnimationWindowCurve(windowCurve);
        }

        public bool ReferencesCurveWrapper(CurveWrapper curveWrapper)
        {
            return target.ReferencesCurveWrapper(curveWrapper);
        }

        public bool MatchesAnimationWindowCurve(AnimationWindowCurve windowCurve, IAnimationWindowClip clipObject)
        {
            return target.MatchesAnimationWindowCurve(windowCurve, clipObject);
        }

        public bool MatchesCurveWrapper(CurveWrapper curveWrapper, IAnimationWindowClip clipObject)
        {
            return target.MatchesCurveWrapper(curveWrapper, clipObject);
        }

        public void ClearPendingSave()
        {
            hasPendingSave = false;
            pendingSaveCurve = null;
        }

        public KeyframeInterpolationCurveSelection Copy()
        {
            var copy = new KeyframeInterpolationCurveSelection(target.Copy());
            for (var i = 0; i < Keys.Count; i++) {
                copy.Keys.Add(Keys[i]);
            }

            copy.hasPendingSave = hasPendingSave;
            copy.pendingSaveCurve = pendingSaveCurve != null
                ? KeyframeInterpolationCurveUtility.Clone(pendingSaveCurve)
                : null;
            return copy;
        }
    }
}