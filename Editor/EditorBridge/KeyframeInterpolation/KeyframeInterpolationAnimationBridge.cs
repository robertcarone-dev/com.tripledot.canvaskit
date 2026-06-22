using System.Collections.Generic;
using Tripledot.CanvasKit.Editor;
using UnityEditor;
using UnityEditor.AnimationWindowBuiltin;
using UnityEditorInternal;
using UnityEngine;

namespace Tripledot.CanvasKit.InternalEditorBridge
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
            for (var i = 0; i < stagedEdits.Count; i++) {
                var stagedEdit = stagedEdits[i];
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

    internal static class KeyframeInterpolationCurveEditStager
    {
        internal delegate int CurveMutation(AnimationCurve curve, IReadOnlyList<KeyframeInterpolationTangentUtility.KeyframeInterpolationSegmentSelection> selectedSegments);

        internal readonly struct StagedCurveEdit
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

            for (var i = 0; i < selections.Count; i++) {
                var selection = selections[i];
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

    internal static class KeyframeInterpolationAnimationBridge
    {
        private const float TimeEpsilon = 0.00001f;

        private sealed class SavedCurveBucket
        {
            public readonly IAnimationWindowClip ClipObject;
            public readonly AnimationClip ClipAsset;
            public readonly List<EditorCurveBinding> Bindings = new List<EditorCurveBinding>();
            public readonly List<AnimationCurve> Curves = new List<AnimationCurve>();

            public SavedCurveBucket(IAnimationWindowClip clipObject, AnimationClip clipAsset)
            {
                ClipObject = clipObject;
                ClipAsset = clipAsset;
            }
        }

        public static AnimationWindow GetAnimationWindow()
        {
            var focusedAnimationWindow = EditorWindow.focusedWindow as AnimationWindow;
            if (focusedAnimationWindow != null && focusedAnimationWindow.state != null) {
                return focusedAnimationWindow;
            }

            var mouseOverAnimationWindow = EditorWindow.mouseOverWindow as AnimationWindow;
            if (mouseOverAnimationWindow != null && mouseOverAnimationWindow.state != null) {
                return mouseOverAnimationWindow;
            }

            var animationWindows = AnimationWindow.GetAllAnimationWindows();
            if (animationWindows == null || animationWindows.Count == 0) {
                return null;
            }

            for (var i = 0; i < animationWindows.Count; i++) {
                var animationWindow = animationWindows[i];
                if (animationWindow != null && animationWindow.state != null) {
                    return animationWindow;
                }
            }

            return null;
        }

        public static int ReadSelectedCurves(AnimationWindow window, List<KeyframeInterpolationCurveSelection> selections)
        {
            selections.Clear();

            var windowState = window.state;
            return windowState.showCurveEditor
                ? AddCurveEditorKeysToSelections(window, windowState, selections)
                : AddAnimationWindowKeysToSelections(windowState, windowState.selectedKeys, selections);
        }

        public static bool TryResolveCurrentSelections(
            AnimationWindow window,
            IReadOnlyList<KeyframeInterpolationCurveSelection> selections,
            List<KeyframeInterpolationCurveSelection> resolvedSelections)
        {
            resolvedSelections.Clear();
            if (window?.state == null || selections == null || selections.Count == 0) {
                return false;
            }

            for (var i = 0; i < selections.Count; i++) {
                var selection = selections[i];
                if (!selection.AnimationIsEditable || selection.IsObjectReferenceCurve || selection.IsDiscreteCurve) {
                    resolvedSelections.Add(selection.Copy());
                    continue;
                }

                if (!TryResolveCurrentSelection(window, window.state, selection, out var resolvedSelection)) {
                    resolvedSelections.Clear();
                    return false;
                }

                resolvedSelections.Add(resolvedSelection);
            }

            return resolvedSelections.Count > 0;
        }

        public static void RefreshChangedCurves(AnimationWindow window, IReadOnlyList<KeyframeInterpolationCurveSelection> selections)
        {
            if (selections.Count == 0) {
                return;
            }

            var windowState = window.state;
            windowState.refresh = AnimationWindowState.RefreshType.CurvesOnly;

            var editorCurveBindings = new HashSet<EditorCurveBinding>();
            for (var i = 0; i < selections.Count; i++) {
                var selection = selections[i];
                if (!editorCurveBindings.Add(selection.Binding)) {
                    continue;
                }

                windowState.RefreshCurve(selection.Binding);
                window.RefreshCurve(selection.Binding);
            }

            InvalidateCurveEditor(window);
            window.Repaint();
        }

        public static KeyframeInterpolationEditSession BeginEditSession(AnimationWindow window, string undoLabel, bool continuous)
        {
            return new KeyframeInterpolationEditSession(window, undoLabel, continuous);
        }

        public static bool CommitSavedCurves(AnimationWindow window, IReadOnlyList<KeyframeInterpolationCurveSelection> selections, string undoLabel)
        {
            if (selections.Count == 0) {
                return false;
            }

            var changedSelections = new List<KeyframeInterpolationCurveSelection>();
            var didSave = SavePendingCurves(window.state, selections, undoLabel, changedSelections);

            if (didSave) {
                window.state.ResampleAnimation();

                for (var i = 0; i < changedSelections.Count; i++) {
                    changedSelections[i].ClearPendingSave();
                    if (changedSelections[i].ClipAsset != null) {
                        EditorUtility.SetDirty(changedSelections[i].ClipAsset);
                    }
                }

                RefreshChangedCurves(window, changedSelections);
                return true;
            }

            return false;
        }

        internal static bool SavePendingCurves(
            AnimationWindowState windowState,
            IReadOnlyList<KeyframeInterpolationCurveSelection> selections,
            string undoLabel,
            List<KeyframeInterpolationCurveSelection> changedSelections)
        {
            if (selections.Count == 0) {
                return false;
            }

            var curveBuckets = new List<SavedCurveBucket>();

            for (var i = 0; i < selections.Count; i++) {
                var selection = selections[i];
                if (!selection.HasPendingSave || !selection.AnimationIsEditable) {
                    continue;
                }

                var clipObject = selection.ClipObject;
                if (clipObject == null || clipObject.isReadOnly) {
                    continue;
                }

                var clipAsset = selection.ClipAsset ?? (windowState != null ? ResolveAnimationClip(windowState, clipObject) : null);
                if (clipAsset == null || !selection.TryGetPendingSaveCurve(out var curveForSave)) {
                    continue;
                }

                var bucket = GetOrCreateBucket(curveBuckets, clipObject, clipAsset);
                AddOrReplaceSavedCurve(bucket, selection.Binding, curveForSave);
                changedSelections?.Add(selection);
            }

            if (curveBuckets.Count == 0) {
                return false;
            }

            var didSave = false;
            for (var i = 0; i < curveBuckets.Count; i++) {
                var bucket = curveBuckets[i];
                if (bucket.Curves.Count == 0) {
                    continue;
                }

                Undo.RegisterCompleteObjectUndo(bucket.ClipAsset, undoLabel);
                AnimationUtility.SetEditorCurves(bucket.ClipAsset, bucket.Bindings.ToArray(), bucket.Curves.ToArray());
                didSave = true;
            }

            return didSave;
        }

        public static bool RestoreSelection(AnimationWindow window, IReadOnlyList<KeyframeInterpolationCurveSelection> selections)
        {
            if (selections.Count == 0) {
                return false;
            }

            return window.state.showCurveEditor
                ? RestoreCurveEditorSelection(window, selections)
                : RestoreAnimationWindowSelection(window, selections);
        }

        internal static void SynchronizeSelectionForEditing(AnimationWindow window, string undoLabel)
        {
            var windowState = window.state;
            var curveEditor = GetCurveEditor(window);

            if (windowState.showCurveEditor && curveEditor?.selectedCurves != null) {
                SyncSelectedKeysFromCurveEditor(windowState, curveEditor);
                curveEditor.SaveKeySelection(undoLabel);
                return;
            }

            windowState.SaveKeySelection(undoLabel);
        }

        private static int AddAnimationWindowKeysToSelections(AnimationWindowState state, IEnumerable<AnimationWindowKeyframe> selectedKeys, List<KeyframeInterpolationCurveSelection> selections)
        {
            if (selectedKeys == null) {
                return 0;
            }

            var selectedKeyCount = 0;
            foreach (var selectedKey in selectedKeys) {
                if (selectedKey?.curve == null) {
                    continue;
                }

                var windowCurve = selectedKey.curve;
                var selectionIndex = IndexOfWindowCurveSelection(selections, windowCurve);
                if (selectionIndex < 0) {
                    var clipObject = ResolveClipObject(state, windowCurve);
                    if (clipObject == null) {
                        continue;
                    }

                    selections.Add(KeyframeInterpolationCurveSelection.CreateForAnimationWindowCurve(ResolveAnimationClip(state, clipObject), clipObject, windowCurve));
                    selectionIndex = selections.Count - 1;
                }

                selectedKeyCount++;
                AddKeySelection(selections[selectionIndex], selectedKey.GetIndex(), selectedKey.time);
            }

            return selectedKeyCount;
        }

        private static int AddCurveEditorKeysToSelections(AnimationWindow window, AnimationWindowState state, List<KeyframeInterpolationCurveSelection> selections)
        {
            var curveEditor = GetCurveEditor(window);
            if (curveEditor?.selectedCurves == null || curveEditor.selectedCurves.Count == 0) {
                return 0;
            }

            var filteredCurves = state.filteredCurves;
            if (filteredCurves == null || filteredCurves.Count == 0) {
                return 0;
            }

            var selectedKeyCount = 0;
            for (var i = 0; i < curveEditor.selectedCurves.Count; i++) {
                var curveSelection = curveEditor.selectedCurves[i];
                if (curveSelection == null) {
                    continue;
                }

                var windowKey = AnimationWindowUtility.CurveSelectionToAnimationWindowKeyframe(curveSelection, filteredCurves);
                var windowCurve = windowKey?.curve;
                if (windowCurve == null) {
                    continue;
                }

                var curveWrapper = curveEditor.GetCurveWrapperFromSelection(curveSelection);
                if (curveWrapper?.curve == null || curveSelection.key < 0 || curveSelection.key >= curveWrapper.curve.length) {
                    continue;
                }

                var selectionIndex = IndexOfCurveWrapperSelection(selections, curveWrapper);
                if (selectionIndex < 0) {
                    var clipObject = ResolveClipObject(state, windowCurve);
                    if (clipObject == null) {
                        continue;
                    }

                    var selection = KeyframeInterpolationCurveSelection.CreateForCurveWrapper(ResolveAnimationClip(state, clipObject), clipObject, curveWrapper, windowCurve.isPPtrCurve,
                        windowCurve.isDiscreteCurve);
                    selections.Add(selection);
                    selectionIndex = selections.Count - 1;
                }

                var keyframe = curveWrapper.curve[curveSelection.key];
                selectedKeyCount++;
                AddKeySelection(selections[selectionIndex], curveSelection.key, keyframe.time, curveSelection.type);
            }

            return selectedKeyCount;
        }

        private static bool RestoreAnimationWindowSelection(AnimationWindow window, IReadOnlyList<KeyframeInterpolationCurveSelection> selections)
        {
            var windowState = window.state;
            var allCurves = windowState.allCurves;
            if (allCurves == null || allCurves.Count == 0) {
                return false;
            }

            var restoredKeys = new List<AnimationWindowKeyframe>();
            for (var i = 0; i < selections.Count; i++) {
                var selection = selections[i];
                var curve = FindCurveBySelection(windowState, allCurves, selection);
                if (curve == null) {
                    continue;
                }

                AddRestoredWindowKeys(curve, selection, restoredKeys);
            }

            if (restoredKeys.Count == 0) {
                return false;
            }

            windowState.ClearKeySelections();
            for (var i = 0; i < restoredKeys.Count; i++) {
                windowState.SelectKey(restoredKeys[i]);
            }

            window.Repaint();
            return true;
        }

        private static bool RestoreCurveEditorSelection(AnimationWindow window, IReadOnlyList<KeyframeInterpolationCurveSelection> selections)
        {
            var windowState = window.state;
            var curveEditor = GetCurveEditor(window);
            var curveWrappers = curveEditor?.animationCurves;

            if (curveEditor == null || curveWrappers == null || curveWrappers.Length == 0) {
                return false;
            }

            var restoredSelections = new List<CurveSelection>();
            var clipObject = windowState.selection?.clip ?? windowState.activeClip;

            for (var i = 0; i < selections.Count; i++) {
                var selection = selections[i];
                var curveWrapper = FindCurveWrapperBySelection(curveWrappers, selection, clipObject);
                if (curveWrapper?.curve == null) {
                    continue;
                }

                for (var keyIndex = 0; keyIndex < selection.Keys.Count; keyIndex++) {
                    var selectedKey = selection.Keys[keyIndex];
                    var resolvedKeyIndex = ResolveCurveKeyIndex(curveWrapper.curve, selectedKey);
                    if (resolvedKeyIndex < 0) {
                        continue;
                    }

                    var restoredSelection = new CurveSelection(curveWrapper.id, resolvedKeyIndex, selectedKey.Type);
                    restoredSelection.type = GetRestorableSelectionType(curveWrapper.curve, resolvedKeyIndex, restoredSelection.type);
                    if (!restoredSelections.Contains(restoredSelection)) {
                        restoredSelections.Add(restoredSelection);
                    }
                }
            }

            if (restoredSelections.Count == 0) {
                return false;
            }

            curveEditor.ClearSelection();
            for (var i = 0; i < restoredSelections.Count; i++) {
                curveEditor.AddSelection(restoredSelections[i]);
            }

            SyncSelectedKeysFromCurveEditor(windowState, curveEditor);
            window.Repaint();

            return true;
        }

        private static bool TryResolveCurrentSelection(
            AnimationWindow window,
            AnimationWindowState state,
            KeyframeInterpolationCurveSelection selection,
            out KeyframeInterpolationCurveSelection resolvedSelection)
        {
            resolvedSelection = null;
            return selection.Source == KeyframeInterpolationSelectionSource.CurveWrapper
                ? TryResolveCurveWrapperSelection(window, state, selection, out resolvedSelection)
                : TryResolveAnimationWindowCurveSelection(state, selection, out resolvedSelection);
        }

        private static bool TryResolveAnimationWindowCurveSelection(
            AnimationWindowState state,
            KeyframeInterpolationCurveSelection selection,
            out KeyframeInterpolationCurveSelection resolvedSelection)
        {
            resolvedSelection = null;
            var allCurves = state.allCurves;
            var curves = allCurves != null && allCurves.Count > 0 ? allCurves : state.filteredCurves;
            if (curves == null || curves.Count == 0) {
                return false;
            }

            var windowCurve = FindCurveBySelection(state, curves, selection);
            if (windowCurve == null) {
                return false;
            }

            var clipObject = ResolveClipObject(state, windowCurve);
            if (clipObject == null) {
                return false;
            }

            resolvedSelection = KeyframeInterpolationCurveSelection.CreateForAnimationWindowCurve(
                selection.ClipAsset ?? ResolveAnimationClip(state, clipObject),
                clipObject,
                windowCurve);
            CopySelectedKeys(selection, resolvedSelection);
            return true;
        }

        private static bool TryResolveCurveWrapperSelection(
            AnimationWindow window,
            AnimationWindowState state,
            KeyframeInterpolationCurveSelection selection,
            out KeyframeInterpolationCurveSelection resolvedSelection)
        {
            resolvedSelection = null;
            var curveEditor = GetCurveEditor(window);
            var curveWrappers = curveEditor?.animationCurves;
            if (curveWrappers == null || curveWrappers.Length == 0) {
                return false;
            }

            var clipObject = state.selection?.clip;
            if (clipObject == null) {
                clipObject = state.activeClip;
            }

            if (clipObject == null) {
                clipObject = selection.ClipObject;
            }

            if (clipObject == null || !Equals(clipObject, selection.ClipObject)) {
                return false;
            }

            var curveWrapper = FindCurveWrapperBySelection(curveWrappers, selection, clipObject);
            if (curveWrapper?.curve == null) {
                return false;
            }

            resolvedSelection = KeyframeInterpolationCurveSelection.CreateForCurveWrapper(
                selection.ClipAsset ?? ResolveAnimationClip(state, clipObject),
                clipObject,
                curveWrapper,
                selection.IsObjectReferenceCurve,
                selection.IsDiscreteCurve);
            CopySelectedKeys(selection, resolvedSelection);
            return true;
        }

        private static void CopySelectedKeys(
            KeyframeInterpolationCurveSelection source,
            KeyframeInterpolationCurveSelection destination)
        {
            destination.Keys.Clear();
            for (var i = 0; i < source.Keys.Count; i++) {
                destination.Keys.Add(source.Keys[i]);
            }
        }

        private static void AddRestoredWindowKeys(
            AnimationWindowCurve curve,
            KeyframeInterpolationCurveSelection selection,
            List<AnimationWindowKeyframe> restoredKeys)
        {
            var keyframes = curve.keyframes;
            if (keyframes == null || keyframes.Count == 0) {
                return;
            }

            for (var keyIndex = 0; keyIndex < selection.Keys.Count; keyIndex++) {
                var keyframe = FindKeyframe(keyframes, selection.Keys[keyIndex]);
                if (keyframe != null && !restoredKeys.Contains(keyframe)) {
                    restoredKeys.Add(keyframe);
                }
            }
        }

        private static void SyncSelectedKeysFromCurveEditor(AnimationWindowState state, CurveEditor curveEditor)
        {
            var filteredCurves = state.filteredCurves;
            state.ClearKeySelections();

            for (var i = 0; i < curveEditor.selectedCurves.Count; i++) {
                var windowKeyframe = AnimationWindowUtility.CurveSelectionToAnimationWindowKeyframe(curveEditor.selectedCurves[i], filteredCurves);
                if (windowKeyframe != null) {
                    state.SelectKey(windowKeyframe);
                }
            }
        }

        private static void InvalidateCurveEditor(AnimationWindow window)
        {
            var curveEditor = GetCurveEditor(window);
            if (curveEditor != null) {
                curveEditor.InvalidateBounds();
                curveEditor.InvalidateSelectionBounds();
            }
        }

        private static CurveEditor GetCurveEditor(AnimationWindow window)
        {
            return window.animEditor?.curveEditor;
        }

        private static SavedCurveBucket GetOrCreateBucket(List<SavedCurveBucket> buckets, IAnimationWindowClip clipObject, AnimationClip clipAsset)
        {
            for (var i = 0; i < buckets.Count; i++) {
                if (Equals(buckets[i].ClipObject, clipObject) && Equals(buckets[i].ClipAsset, clipAsset)) {
                    return buckets[i];
                }
            }

            var bucket = new SavedCurveBucket(clipObject, clipAsset);
            buckets.Add(bucket);
            return bucket;
        }

        private static void AddOrReplaceSavedCurve(SavedCurveBucket bucket, EditorCurveBinding binding, AnimationCurve curve)
        {
            for (var i = 0; i < bucket.Bindings.Count; i++) {
                if (bucket.Bindings[i].Equals(binding)) {
                    bucket.Curves[i] = curve;
                    return;
                }
            }

            bucket.Bindings.Add(binding);
            bucket.Curves.Add(curve);
        }

        private static IAnimationWindowClip ResolveClipObject(AnimationWindowState state, AnimationWindowCurve windowCurve)
        {
            if (windowCurve.clip != null) {
                return windowCurve.clip;
            }

            if (state.selection.clip != null) {
                return state.selection.clip;
            }

            return state.activeClip;
        }

        private static AnimationClip ResolveAnimationClip(AnimationWindowState state, IAnimationWindowClip clipObject)
        {
            if (clipObject is AnimationWindowClip builtInClip) {
                return builtInClip.animationClip;
            }

            return Equals(state.activeClip, clipObject) ? state.activeAnimationClip : null;
        }

        private static AnimationWindowCurve FindCurveBySelection(AnimationWindowState state, IReadOnlyList<AnimationWindowCurve> curves, KeyframeInterpolationCurveSelection selection)
        {
            AnimationWindowCurve fallback = null;

            for (var i = 0; i < curves.Count; i++) {
                var curve = curves[i];
                if (!selection.MatchesAnimationWindowCurve(curve, ResolveClipObject(state, curve))) {
                    continue;
                }

                if (!curve.isPhantom) {
                    return curve;
                }

                fallback ??= curve;
            }

            return fallback;
        }

        private static CurveWrapper FindCurveWrapperBySelection(IReadOnlyList<CurveWrapper> curveWrappers, KeyframeInterpolationCurveSelection selection, IAnimationWindowClip clipObject)
        {
            CurveWrapper fallback = null;

            for (var i = 0; i < curveWrappers.Count; i++) {
                var curveWrapper = curveWrappers[i];
                if (!selection.MatchesCurveWrapper(curveWrapper, clipObject)) {
                    continue;
                }

                if (!curveWrapper.isPhantom) {
                    return curveWrapper;
                }

                fallback ??= curveWrapper;
            }

            return fallback;
        }

        private static AnimationWindowKeyframe FindKeyframe(IReadOnlyList<AnimationWindowKeyframe> keyframes, KeyframeInterpolationKeySelection selectedKey)
        {
            if (keyframes == null || keyframes.Count == 0) {
                return null;
            }

            if (selectedKey.Index >= 0 && selectedKey.Index < keyframes.Count) {
                var keyframe = keyframes[selectedKey.Index];
                if (keyframe != null && Mathf.Abs(keyframe.time - selectedKey.Time) <= TimeEpsilon) {
                    return keyframe;
                }
            }

            for (var i = 0; i < keyframes.Count; i++) {
                var keyframe = keyframes[i];
                if (keyframe != null && Mathf.Abs(keyframe.time - selectedKey.Time) <= TimeEpsilon) {
                    return keyframe;
                }
            }

            return null;
        }

        private static int ResolveCurveKeyIndex(AnimationCurve curve, KeyframeInterpolationKeySelection selectedKey)
        {
            if (selectedKey.Index >= 0 && selectedKey.Index < curve.length) {
                var keyframe = curve[selectedKey.Index];
                if (Mathf.Abs(keyframe.time - selectedKey.Time) <= TimeEpsilon) {
                    return selectedKey.Index;
                }
            }

            for (var i = 0; i < curve.length; i++) {
                if (Mathf.Abs(curve[i].time - selectedKey.Time) <= TimeEpsilon) {
                    return i;
                }
            }

            return -1;
        }

        private static int IndexOfWindowCurveSelection(IReadOnlyList<KeyframeInterpolationCurveSelection> selections, AnimationWindowCurve windowCurve)
        {
            for (var i = 0; i < selections.Count; i++) {
                if (selections[i].ReferencesAnimationWindowCurve(windowCurve)) {
                    return i;
                }
            }

            return -1;
        }

        private static int IndexOfCurveWrapperSelection(IReadOnlyList<KeyframeInterpolationCurveSelection> selections, CurveWrapper curveWrapper)
        {
            for (var i = 0; i < selections.Count; i++) {
                if (selections[i].ReferencesCurveWrapper(curveWrapper)) {
                    return i;
                }
            }

            return -1;
        }

        private static void AddKeySelection(KeyframeInterpolationCurveSelection selection, int keyIndex, float time, CurveSelection.SelectionType type = CurveSelection.SelectionType.Key)
        {
            for (var i = 0; i < selection.Keys.Count; i++) {
                var key = selection.Keys[i];
                if (key.Index == keyIndex && Mathf.Approximately(key.Time, time) && key.Type == type) {
                    return;
                }
            }

            selection.Keys.Add(new KeyframeInterpolationKeySelection(keyIndex, time, type));
        }

        private static CurveSelection.SelectionType GetRestorableSelectionType(AnimationCurve curve, int keyIndex, CurveSelection.SelectionType type)
        {
            if (curve == null || keyIndex < 0 || keyIndex >= curve.length) {
                return type;
            }

            if (type == CurveSelection.SelectionType.InTangent && keyIndex == 0) {
                return CurveSelection.SelectionType.Key;
            }

            if (type == CurveSelection.SelectionType.OutTangent && keyIndex == curve.length - 1) {
                return CurveSelection.SelectionType.Key;
            }

            return type;
        }
    }
}