using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor.KeyframeInterpolation
{
    internal sealed class AnimationWindowCurveTarget : KeyframeInterpolationCurveTarget
    {
        private readonly AnimationWindowCurve windowCurve;

        public AnimationWindowCurveTarget(
            AnimationClip clipAsset,
            EditorCurveBinding binding,
            bool animationIsEditable,
            bool isObjectReferenceCurve,
            bool isDiscreteCurve,
            IAnimationWindowClip clipObject,
            AnimationWindowCurve windowCurve)
            : base(
                clipAsset,
                binding,
                animationIsEditable,
                isObjectReferenceCurve,
                isDiscreteCurve,
                clipObject,
                KeyframeInterpolationSelectionSource.AnimationWindowCurve)
        {
            this.windowCurve = windowCurve;
        }

        public override AnimationCurve LoadCurrentCurve()
        {
            return windowCurve.ToAnimationCurve();
        }

        public override bool ApplyEditedCurve(AnimationCurve curve, out AnimationCurve curveForSave)
        {
            curveForSave = null;
            if (!KeyframeInterpolationCurveUtility.TryGetSanitizedCurveForSave(curve, out var sanitizedCurve)) {
                return false;
            }

            windowCurve.Clear();
            windowCurve.FromAnimationCurve(sanitizedCurve);
            curveForSave = sanitizedCurve;
            return true;
        }

        public override KeyframeInterpolationCurveTarget Copy()
        {
            return new AnimationWindowCurveTarget(
                ClipAsset,
                Binding,
                AnimationIsEditable,
                IsObjectReferenceCurve,
                IsDiscreteCurve,
                ClipObject,
                windowCurve);
        }

        public override bool ReferencesAnimationWindowCurve(AnimationWindowCurve candidate)
        {
            return ReferenceEquals(candidate, windowCurve);
        }

        public override bool ReferencesCurveWrapper(CurveWrapper curveWrapper)
        {
            return false;
        }

        public override bool MatchesAnimationWindowCurve(AnimationWindowCurve candidate, IAnimationWindowClip clipObject)
        {
            return ReferenceEquals(candidate, windowCurve)
                   || candidate != null
                   && MatchesBinding(candidate.binding)
                   && MatchesClipObject(clipObject);
        }

        public override bool MatchesCurveWrapper(CurveWrapper curveWrapper, IAnimationWindowClip clipObject)
        {
            return curveWrapper?.curve != null
                   && MatchesBinding(curveWrapper.binding)
                   && MatchesClipObject(clipObject);
        }
    }
}