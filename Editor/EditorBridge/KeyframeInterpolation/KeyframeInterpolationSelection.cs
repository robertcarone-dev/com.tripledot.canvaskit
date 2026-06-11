using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
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
        public bool HasPendingSave => hasPendingSave;

        public AnimationCurve LoadCurrentCurve()
        {
            return target.LoadCurrentCurve();
        }

        public bool ApplyEditedCurve(AnimationCurve curve)
        {
            if (!target.ApplyEditedCurve(curve)) {
                return false;
            }

            hasPendingSave = true;
            return true;
        }

        public bool CanApplyEditedCurve(AnimationCurve curve)
        {
            return target.CanApplyEditedCurve(curve);
        }

        public void AddSaveTarget(List<AnimationWindowCurve> windowCurves, List<CurveWrapper> curveWrappers)
        {
            target.AddSaveTarget(windowCurves, curveWrappers);
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
        }
    }

    internal abstract class KeyframeInterpolationCurveTarget
    {
        protected KeyframeInterpolationCurveTarget(
            AnimationClip clipAsset,
            EditorCurveBinding binding,
            bool animationIsEditable,
            bool isObjectReferenceCurve,
            bool isDiscreteCurve,
            IAnimationWindowClip clipObject)
        {
            ClipAsset = clipAsset;
            Binding = binding;
            AnimationIsEditable = animationIsEditable;
            IsObjectReferenceCurve = isObjectReferenceCurve;
            IsDiscreteCurve = isDiscreteCurve;
            ClipObject = clipObject;
        }

        public AnimationClip ClipAsset { get; }
        public EditorCurveBinding Binding { get; }
        public bool AnimationIsEditable { get; }
        public bool IsObjectReferenceCurve { get; }
        public bool IsDiscreteCurve { get; }
        public IAnimationWindowClip ClipObject { get; }

        public abstract AnimationCurve LoadCurrentCurve();

        public virtual bool CanApplyEditedCurve(AnimationCurve curve)
        {
            return KeyframeInterpolationCurveUtility.TryGetSanitizedCurveForSave(curve, out _);
        }

        public abstract bool ApplyEditedCurve(AnimationCurve curve);
        public abstract void AddSaveTarget(List<AnimationWindowCurve> windowCurves, List<CurveWrapper> curveWrappers);
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

        protected static void AddUniqueReference<T>(List<T> values, T value) where T : class
        {
            for (var i = 0; i < values.Count; i++) {
                if (ReferenceEquals(values[i], value)) {
                    return;
                }
            }

            values.Add(value);
        }
    }

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
            : base(clipAsset, binding, animationIsEditable, isObjectReferenceCurve, isDiscreteCurve, clipObject)
        {
            this.windowCurve = windowCurve;
        }

        public override AnimationCurve LoadCurrentCurve()
        {
            return windowCurve.ToAnimationCurve();
        }

        public override bool ApplyEditedCurve(AnimationCurve curve)
        {
            if (!KeyframeInterpolationCurveUtility.TryGetSanitizedCurveForSave(curve, out var sanitizedCurve)) {
                return false;
            }

            windowCurve.Clear();
            windowCurve.FromAnimationCurve(sanitizedCurve);
            return true;
        }

        public override void AddSaveTarget(List<AnimationWindowCurve> windowCurves, List<CurveWrapper> curveWrappers)
        {
            AddUniqueReference(windowCurves, windowCurve);
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
            : base(clipAsset, binding, animationIsEditable, isObjectReferenceCurve, isDiscreteCurve, clipObject)
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

        public override bool ApplyEditedCurve(AnimationCurve curve)
        {
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
            return true;
        }

        public override void AddSaveTarget(List<AnimationWindowCurve> windowCurves, List<CurveWrapper> curveWrappers)
        {
            AddUniqueReference(curveWrappers, curveWrapper);
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
