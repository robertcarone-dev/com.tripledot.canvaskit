using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
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