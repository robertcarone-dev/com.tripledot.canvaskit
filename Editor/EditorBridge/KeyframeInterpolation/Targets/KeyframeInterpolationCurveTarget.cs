using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor.KeyframeInterpolation
{
    internal abstract class KeyframeInterpolationCurveTarget
    {
        protected KeyframeInterpolationCurveTarget(
            AnimationClip clipAsset,
            EditorCurveBinding binding,
            bool animationIsEditable,
            bool isObjectReferenceCurve,
            bool isDiscreteCurve,
            IAnimationWindowClip clipObject,
            KeyframeInterpolationSelectionSource source)
        {
            ClipAsset = clipAsset;
            Binding = binding;
            AnimationIsEditable = animationIsEditable;
            IsObjectReferenceCurve = isObjectReferenceCurve;
            IsDiscreteCurve = isDiscreteCurve;
            ClipObject = clipObject;
            Source = source;
        }

        public AnimationClip ClipAsset { get; }
        public EditorCurveBinding Binding { get; }
        public bool AnimationIsEditable { get; }
        public bool IsObjectReferenceCurve { get; }
        public bool IsDiscreteCurve { get; }
        public IAnimationWindowClip ClipObject { get; }
        public KeyframeInterpolationSelectionSource Source { get; }

        public abstract AnimationCurve LoadCurrentCurve();

        public virtual bool CanApplyEditedCurve(AnimationCurve curve)
        {
            return KeyframeInterpolationCurveUtility.TryGetSanitizedCurveForSave(curve, out _);
        }

        public abstract bool ApplyEditedCurve(AnimationCurve curve, out AnimationCurve curveForSave);
        public abstract KeyframeInterpolationCurveTarget Copy();
        public abstract bool ReferencesAnimationWindowCurve(AnimationWindowCurve windowCurve);
        public abstract bool ReferencesCurveWrapper(CurveWrapper curveWrapper);
        public abstract bool MatchesAnimationWindowCurve(AnimationWindowCurve windowCurve, IAnimationWindowClip clipObject);
        public abstract bool MatchesCurveWrapper(CurveWrapper curveWrapper, IAnimationWindowClip clipObject);

        protected bool MatchesBinding(EditorCurveBinding binding)
        {
            return Binding.Equals(binding);
        }

        protected bool MatchesClipObject(IAnimationWindowClip clipObject)
        {
            return Equals(ClipObject, clipObject);
        }

        protected static void CopyCurve(AnimationCurve source, AnimationCurve destination)
        {
            destination.keys = source.keys;
            destination.preWrapMode = source.preWrapMode;
            destination.postWrapMode = source.postWrapMode;
            KeyframeInterpolationCurveUtility.CopyTangentModes(source, destination);
        }
    }
}