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
        private delegate int CurveMutation(
            AnimationCurve curve,
            IReadOnlyList<KeyframeInterpolationTangentUtility.KeyframeInterpolationSegmentSelection> selectedSegments);

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

        private bool ApplyToSelections(IReadOnlyList<KeyframeInterpolationCurveSelection> selections, CurveMutation mutateCurve)
        {
            if (ended || selections.Count == 0) {
                return false;
            }

            var changedSelections = new List<KeyframeInterpolationCurveSelection>();

            KeyframeInterpolationAnimationBridge.SynchronizeSelectionForEditing(window, undoLabel);

            for (var i = 0; i < selections.Count; i++) {
                var selection = selections[i];
                if (selection.IsObjectReferenceCurve) {
                    continue;
                }

                if (selection.IsDiscreteCurve) {
                    continue;
                }

                if (!selection.AnimationIsEditable) {
                    continue;
                }

                var curve = selection.LoadCurrentCurve();
                if (curve == null || curve.length < 2) {
                    continue;
                }

                if (KeyframeInterpolationTangentUtility.HasDuplicateKeyTimes(curve)) {
                    continue;
                }

                var selectedSegments = KeyframeInterpolationTangentUtility.ResolveSelectedSegments(curve, selection.Keys);
                if (selectedSegments.Count == 0) {
                    continue;
                }

                if (mutateCurve(curve, selectedSegments) <= 0) {
                    continue;
                }

                if (!selection.ApplyEditedCurve(curve)) {
                    continue;
                }

                changedSelections.Add(selection);
            }

            if (changedSelections.Count == 0) {
                return false;
            }

            return Commit(changedSelections);
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

    internal static class KeyframeInterpolationAnimationBridge
    {
        private const float TimeEpsilon = 0.00001f;

        private sealed class SavedCurveBucket
        {
            public readonly IAnimationWindowClip ClipObject;
            public readonly List<AnimationWindowCurve> WindowCurves = new List<AnimationWindowCurve>();
            public readonly List<CurveWrapper> CurveWrappers = new List<CurveWrapper>();

            public SavedCurveBucket(IAnimationWindowClip clipObject)
            {
                ClipObject = clipObject;
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

            var curveBuckets = new List<SavedCurveBucket>();
            var changedSelections = new List<KeyframeInterpolationCurveSelection>();
            
            for (var i = 0; i < selections.Count; i++) {
                var selection = selections[i];
                if (!selection.HasPendingSave || !selection.AnimationIsEditable) {
                    continue;
                }

                var clipObject = selection.ClipObject;
                if (clipObject == null || clipObject.isReadOnly) {
                    continue;
                }

                var bucket = GetOrCreateBucket(curveBuckets, clipObject);
                selection.AddSaveTarget(bucket.WindowCurves, bucket.CurveWrappers);
                changedSelections.Add(selection);
            }

            if (curveBuckets.Count == 0) {
                return false;
            }

            var didSave = false;
            for (var i = 0; i < curveBuckets.Count; i++) {
                var bucket = curveBuckets[i];
                if (bucket.WindowCurves.Count > 0) {
                    bucket.ClipObject.SaveCurves(bucket.WindowCurves, undoLabel);
                    didSave = true;
                }

                if (bucket.CurveWrappers.Count > 0) {
                    bucket.ClipObject.SaveCurves(bucket.CurveWrappers, undoLabel);
                    didSave = true;
                }
            }

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

                    var selection = KeyframeInterpolationCurveSelection.CreateForCurveWrapper(ResolveAnimationClip(state, clipObject), clipObject, curveWrapper, windowCurve.isPPtrCurve, windowCurve.isDiscreteCurve);
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

        private static SavedCurveBucket GetOrCreateBucket(List<SavedCurveBucket> buckets, IAnimationWindowClip clipObject)
        {
            for (var i = 0; i < buckets.Count; i++) {
                if (Equals(buckets[i].ClipObject, clipObject)) {
                    return buckets[i];
                }
            }

            var bucket = new SavedCurveBucket(clipObject);
            buckets.Add(bucket);
            return bucket;
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
                if (key.Index == keyIndex && Mathf.Approximately(key.Time, time)) {
                    var mergedType = MergeSelectionTypes(key.Type, type);
                    if (mergedType != key.Type) {
                        selection.Keys[i] = new KeyframeInterpolationKeySelection(keyIndex, time, mergedType);
                    }

                    return;
                }
            }

            selection.Keys.Add(new KeyframeInterpolationKeySelection(keyIndex, time, type));
        }

        private static CurveSelection.SelectionType MergeSelectionTypes(CurveSelection.SelectionType current, CurveSelection.SelectionType next)
        {
            if (current == next || next == CurveSelection.SelectionType.Key) {
                return current;
            }

            return current == CurveSelection.SelectionType.Key ? next : CurveSelection.SelectionType.Key;
        }
    }
}
