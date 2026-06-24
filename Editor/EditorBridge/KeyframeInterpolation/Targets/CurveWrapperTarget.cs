using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor.KeyframeInterpolation
{
    internal sealed class CurveWrapperTarget : KeyframeInterpolationCurveTarget
    {
        private readonly CurveWrapper curveWrapper;

        public CurveWrapperTarget(
            AnimationClip clipAsset,
            EditorCurveBinding binding,
            bool animationIsEditable,
            bool isObjectReferenceCurve,
            bool isDiscreteCurve,
            IAnimationWindowClip clipObject,
            CurveWrapper curveWrapper)
            : base(
                clipAsset,
                binding,
                animationIsEditable,
                isObjectReferenceCurve,
                isDiscreteCurve,
                clipObject,
                KeyframeInterpolationSelectionSource.CurveWrapper)
        {
            this.curveWrapper = curveWrapper;
        }

        public override AnimationCurve LoadCurrentCurve()
        {
            return curveWrapper.curve;
        }

        public override bool CanApplyEditedCurve(AnimationCurve curve)
        {
            return curveWrapper.curve != null && base.CanApplyEditedCurve(curve);
        }

        public override bool ApplyEditedCurve(AnimationCurve curve, out AnimationCurve curveForSave)
        {
            curveForSave = null;
            if (curveWrapper.curve == null) {
                return false;
            }

            if (!KeyframeInterpolationCurveUtility.TryGetSanitizedCurveForSave(curve, out var sanitizedCurve)) {
                return false;
            }

            if (!ReferenceEquals(sanitizedCurve, curveWrapper.curve)) {
                CopyCurve(sanitizedCurve, curveWrapper.curve);
            }

            curveWrapper.changed = true;
            curveForSave = sanitizedCurve;
            return true;
        }

        public override KeyframeInterpolationCurveTarget Copy()
        {
            return new CurveWrapperTarget(
                ClipAsset,
                Binding,
                AnimationIsEditable,
                IsObjectReferenceCurve,
                IsDiscreteCurve,
                ClipObject,
                curveWrapper);
        }

        public override bool ReferencesAnimationWindowCurve(AnimationWindowCurve windowCurve)
        {
            return false;
        }

        public override bool ReferencesCurveWrapper(CurveWrapper candidate)
        {
            return ReferenceEquals(candidate, curveWrapper);
        }

        public override bool MatchesAnimationWindowCurve(AnimationWindowCurve windowCurve, IAnimationWindowClip clipObject)
        {
            return windowCurve != null
                   && MatchesBinding(windowCurve.binding)
                   && MatchesClipObject(clipObject);
        }

        public override bool MatchesCurveWrapper(CurveWrapper candidate, IAnimationWindowClip clipObject)
        {
            return ReferenceEquals(candidate, curveWrapper)
                   || candidate?.curve != null
                   && MatchesBinding(candidate.binding)
                   && MatchesClipObject(clipObject);
        }
    }
}