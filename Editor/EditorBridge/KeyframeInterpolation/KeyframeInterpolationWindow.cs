using System;
using System.Collections.Generic;
using Tripledot.CanvasKit.InternalEditorBridge;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Toolbar = UnityEditor.UIElements.Toolbar;

namespace Tripledot.CanvasKit.Editor
{
    internal sealed class KeyframeInterpolationWindow : EditorWindow
    {
        private const string UndoLabel = "Set Keyframe Interpolation";
        private const string StyleSheetPath = "Packages/com.tripledot.canvaskit/Editor/EditorBridge/KeyframeInterpolation/KeyframeInterpolationWindow.uss";
        private const string IconRootPath = "Packages/com.tripledot.canvaskit/Editor Default Resources/Icons/KeyframeInterpolation/";
        private const float MinWindowWidth = 360f;
        private const float TimeCursorEpsilon = 0.0001f;
        private const int InvalidSelectionSignature = int.MinValue;

        private readonly struct ModeToolbarItem
        {
            public readonly AnimationUtility.TangentMode Mode;
            public readonly string IconFilename;

            public ModeToolbarItem(AnimationUtility.TangentMode mode, string iconFilename)
            {
                Mode = mode;
                IconFilename = iconFilename;
            }
        }

        private readonly struct PresetToolbarItem
        {
            public readonly KeyframeInterpolationPreset Preset;
            public readonly string IconFilename;

            public PresetToolbarItem(KeyframeInterpolationPreset preset, string iconFilename)
            {
                Preset = preset;
                IconFilename = iconFilename;
            }
        }

        private enum HandleSide
        {
            Out,
            In
        }

        private static readonly ModeToolbarItem[] ModeToolbarItems = {
            new ModeToolbarItem(AnimationUtility.TangentMode.Constant, "ModeConstantIcon.png"),
            new ModeToolbarItem(AnimationUtility.TangentMode.Linear, "ModeLinearIcon.png"),
            new ModeToolbarItem(AnimationUtility.TangentMode.Auto, "ModeAutoIcon.png"),
            new ModeToolbarItem(AnimationUtility.TangentMode.ClampedAuto, "ModeAutoClampedIcon.png"),
            new ModeToolbarItem(AnimationUtility.TangentMode.Free, "ModeFreeIcon.png")
        };

        private static readonly PresetToolbarItem[] PresetToolbarItems = {
            new PresetToolbarItem(KeyframeInterpolationPreset.EaseInOut, "PresetEaseInOutIcon.png"),
            new PresetToolbarItem(KeyframeInterpolationPreset.EaseIn, "PresetEaseInIcon.png"),
            new PresetToolbarItem(KeyframeInterpolationPreset.EaseOut, "PresetEaseOutIcon.png"),
            new PresetToolbarItem(KeyframeInterpolationPreset.Circular, "PresetCicularIcon.png"),
            new PresetToolbarItem(KeyframeInterpolationPreset.Exponential, "PresetExponentialIcon.png"),
            new PresetToolbarItem(KeyframeInterpolationPreset.Back, "PresetBackIcon.png"),
            new PresetToolbarItem(KeyframeInterpolationPreset.Bounce, "PresetBounceIcon.png"),
            new PresetToolbarItem(KeyframeInterpolationPreset.Elastic, "PresetElasticIcon.png")
        };

        private static Texture2D windowIcon;
        private static Texture2D[] modeToolbarIcons;
        private static Texture2D[] presetToolbarIcons;

        private readonly List<KeyframeInterpolationCurveSelection> selections = new List<KeyframeInterpolationCurveSelection>();
        private readonly List<KeyframeInterpolationCurveSelection> selectionReadBuffer = new List<KeyframeInterpolationCurveSelection>();
        private readonly List<KeyframeInterpolationCurveSelection> activeEditSelections = new List<KeyframeInterpolationCurveSelection>();
        private readonly List<KeyframeInterpolationCurveSelection> lastAppliedSelections = new List<KeyframeInterpolationCurveSelection>();
        private readonly Queue<Action> queuedAnimationWindowEdits = new Queue<Action>();
        private readonly List<ToolbarButton> modeButtons = new List<ToolbarButton>();
        private readonly List<ToolbarButton> presetButtons = new List<ToolbarButton>();

        private AnimationWindow currentAnimationWindow;
        private AnimationWindow activeEditAnimationWindow;
        private AnimationCurve currentCurve;
        private AnimationUtility.TangentMode currentMode = AnimationUtility.TangentMode.Free;
        private KeyframeInterpolationEditSession activeEditSession;
        private KeyframeInterpolationGraphElement graphElement;
        private Toolbar controlsToolbar;
        private VisualElement fieldsContainer;
        private Vector2Field outHandleField;
        private Vector2Field inHandleField;
        private IMGUIContainer guiEventPump;
        private Label readoutLabel;
        private AnimationCurve displayedCopyCurve;
        private AnimationWindow cachedSelectionAnimationWindow;
        private bool cachedSelectionShowsCurveEditor;
        private bool canCopyCurve;
        private int lastSelectionSignature = InvalidSelectionSignature;
        private bool isDraggingHandle;
        private bool activeEditApplied;
        private bool restoreSelectionAfterHandleDrag;
        private bool pendingSavedCurveRefresh;
        private bool queuedPostSaveRefresh;
        private bool queuedWindowStateRefresh;
        private bool processingQueuedGuiWork;
        private bool updatingControls;
        private bool hasPendingHandleFieldEdit;
        private bool pendingHandleFieldSaveQueued;
        private bool forcePendingHandleFieldSave;
        private bool handleFieldFocusCheckQueued;
        private bool undoRedoRefreshQueued;
        private int lastAppliedCurveDisplaySelectionSignature = InvalidSelectionSignature;
        private AnimationCurve pendingHandleFieldCurve;
        private ApplyContext pendingHandleFieldContext;

        private readonly struct ApplyContext
        {
            public readonly AnimationWindow AnimationWindow;
            public readonly IReadOnlyList<KeyframeInterpolationCurveSelection> Selections;
            public readonly KeyframeInterpolationSelectionAnalysis Analysis;
            public readonly bool CanEditSelection;
            public readonly bool CanEditCurve;

            public ApplyContext(
                AnimationWindow animationWindow,
                IReadOnlyList<KeyframeInterpolationCurveSelection> selections,
                KeyframeInterpolationSelectionAnalysis analysis,
                bool canEditSelection,
                bool canEditCurve)
            {
                AnimationWindow = animationWindow;
                Selections = selections;
                Analysis = analysis;
                CanEditSelection = canEditSelection;
                CanEditCurve = canEditCurve;
            }
        }

        private readonly struct SelectionView
        {
            public readonly IReadOnlyList<KeyframeInterpolationCurveSelection> Selections;
            public readonly int SelectedKeyCount;
            public readonly bool UsesActiveEditSelection;

            public SelectionView(
                IReadOnlyList<KeyframeInterpolationCurveSelection> selections,
                int selectedKeyCount,
                bool usesActiveEditSelection)
            {
                Selections = selections;
                SelectedKeyCount = selectedKeyCount;
                UsesActiveEditSelection = usesActiveEditSelection;
            }
        }

        private struct ViewState
        {
            public bool CanEditSelection;
            public bool CanApplyPreset;
            public int SelectedModeIndex;
            public AnimationUtility.TangentMode DisplayMode;
            public bool HasCurveDisplay;
            public bool CanEditHandles;
            public bool CanConvertGraphToFree;
            public bool HasMixedCurveValues;
            public bool LockActiveEditCurve;
            public AnimationCurve CurveForDrawing;
            public bool HasTimeCursor;
            public float TimeCursor;
        }

        [MenuItem("Window/Animation/Keyframe Interpolation")]
        private static void Open()
        {
            var window = GetWindow<KeyframeInterpolationWindow>();
            window.UpdateTitleContent();
            window.minSize = new Vector2(MinWindowWidth, 360f);
            window.Show();
        }

        [InitializeOnLoadMethod]
        private static void RefreshOpenWindowsAfterScriptLoad()
        {
            EditorApplication.delayCall -= RepaintOpenWindowsAfterScriptLoad;
            EditorApplication.delayCall += RepaintOpenWindowsAfterScriptLoad;
        }

        private static void RepaintOpenWindowsAfterScriptLoad()
        {
            var windows = Resources.FindObjectsOfTypeAll<KeyframeInterpolationWindow>();
            for (var i = 0; i < windows.Length; i++) {
                windows[i].UpdateTitleContent();
                windows[i].ResetSelectionTracking();
                windows[i].QueueWindowRefresh();
                windows[i].Repaint();
            }
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            UpdateTitleContent();
            minSize = new Vector2(MinWindowWidth, 360f);
            ResetSelectionTracking();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            EditorApplication.delayCall -= MarkPostSaveReadbackReady;
            EditorApplication.delayCall -= RunHandleFieldFocusCheck;
            EditorApplication.delayCall -= RunUndoRedoRefresh;
            
            if (TryTakePendingHandleFieldEdit(out var curve, out var context)) {
                ApplyManualCurve(curve, context, false);
            }

            pendingSavedCurveRefresh = false;
            queuedPostSaveRefresh = false;
            undoRedoRefreshQueued = false;
            handleFieldFocusCheckQueued = false;
            queuedAnimationWindowEdits.Clear();
            ClearHandleDrag();
            EndActiveEditSession();
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("ck-keyframe-window");

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            rootVisualElement.styleSheets.Add(styleSheet);

            CreateToolbar();
            CreateCurveArea();
            CreateGuiEventPump();
            QueueWindowRefresh();
        }

        private void Update()
        {
            if (!pendingSavedCurveRefresh
                && queuedAnimationWindowEdits.Count == 0
                && !queuedWindowStateRefresh
                && !hasPendingHandleFieldEdit
                && !IsEditingHandleField()) {
                QueueWindowRefresh();
            }
        }

        private void ResetSelectionTracking()
        {
            lastSelectionSignature = InvalidSelectionSignature;
        }

        private void UpdateTitleContent()
        {
            titleContent = new GUIContent("Keyframe Interpolation", GetWindowIcon());
        }

        private void CreateToolbar()
        {
            controlsToolbar = new Toolbar();
            controlsToolbar.AddToClassList("ck-keyframe-toolbar-strip");
            rootVisualElement.Add(controlsToolbar);

            modeButtons.Clear();
            presetButtons.Clear();

            var icons = GetModeToolbarIcons();
            for (var i = 0; i < ModeToolbarItems.Length; i++) {
                var mode = ModeToolbarItems[i].Mode;
                var button = CreateToolbarButton(icons[i], GetModeDisplayName(mode), () => OnModeClicked(mode), false);
                if (i == 0) {
                    button.AddToClassList("ck-keyframe-toolbar__button--first");
                }

                if (i == ModeToolbarItems.Length - 1) {
                    button.AddToClassList("ck-keyframe-toolbar__button--last");
                }

                modeButtons.Add(button);
                controlsToolbar.Add(button);
            }

            var separator = new VisualElement {
                pickingMode = PickingMode.Ignore
            };
            separator.AddToClassList("ck-keyframe-toolbar__gap");
            controlsToolbar.Add(separator);

            icons = GetPresetToolbarIcons();
            for (var i = 0; i < PresetToolbarItems.Length; i++) {
                var preset = PresetToolbarItems[i].Preset;
                var button = CreateToolbarButton(icons[i], KeyframeInterpolationCurveUtility.GetDisplayName(preset), () => OnPresetClicked(preset), true);
                if (i == 0) {
                    button.AddToClassList("ck-keyframe-toolbar__button--first");
                }

                if (i == PresetToolbarItems.Length - 1) {
                    button.AddToClassList("ck-keyframe-toolbar__button--last");
                }

                presetButtons.Add(button);
                controlsToolbar.Add(button);
            }
        }

        private void CreateCurveArea()
        {
            graphElement = new KeyframeInterpolationGraphElement();
            graphElement.AddToClassList("ck-keyframe-graph");
            graphElement.BeginDragRequested = OnGraphBeginDragRequested;
            graphElement.CurveChanged += OnGraphCurveChanged;
            graphElement.DragEnded += OnGraphDragEnded;
            graphElement.RegisterCallback<PointerDownEvent>(OnGraphPointerDown);
            rootVisualElement.Add(graphElement);

            fieldsContainer = new VisualElement();
            fieldsContainer.AddToClassList("ck-keyframe-fields");

            outHandleField = new Vector2Field("Out");
            outHandleField.AddToClassList("ck-keyframe-field");
            outHandleField.RegisterValueChangedCallback(OnOutHandleFieldChanged);
            outHandleField.RegisterCallback<FocusOutEvent>(OnHandleFieldFocusOut, TrickleDown.TrickleDown);
            fieldsContainer.Add(outHandleField);

            inHandleField = new Vector2Field("In");
            inHandleField.AddToClassList("ck-keyframe-field");
            inHandleField.RegisterValueChangedCallback(OnInHandleFieldChanged);
            inHandleField.RegisterCallback<FocusOutEvent>(OnHandleFieldFocusOut, TrickleDown.TrickleDown);
            fieldsContainer.Add(inHandleField);

            readoutLabel = new Label();
            readoutLabel.AddToClassList("ck-keyframe-readout");
            fieldsContainer.Add(readoutLabel);

            rootVisualElement.Add(fieldsContainer);
        }

        private void CreateGuiEventPump()
        {
            guiEventPump = new IMGUIContainer(RunQueuedGuiWork) {
                pickingMode = PickingMode.Ignore
            };
            guiEventPump.style.position = Position.Absolute;
            guiEventPump.style.left = 0f;
            guiEventPump.style.top = 0f;
            guiEventPump.style.width = 1f;
            guiEventPump.style.height = 1f;
            rootVisualElement.Add(guiEventPump);
        }

        private ToolbarButton CreateToolbarButton(Texture2D texture, string tooltip, Action command, bool tintIcon)
        {
            var button = new ToolbarButton(command) {
                text = string.Empty,
                tooltip = tooltip
            };
            button.AddToClassList("ck-keyframe-toolbar__button");
            button.Add(CreateToolbarImage(texture, tintIcon));
            return button;
        }

        private static Image CreateToolbarImage(Texture2D texture, bool tintIcon)
        {
            var image = new Image {
                image = texture,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            if (tintIcon) {
                image.tintColor = GetToolbarIconTint();
            }

            image.AddToClassList("ck-keyframe-toolbar__icon");
            return image;
        }

        private void RefreshWindowState()
        {
            if (graphElement == null) {
                return;
            }

            var animationWindow = GetTargetAnimationWindow();
            if (animationWindow == null) {
                ClearWindowState();
                return;
            }

            var selectionView = GetSelectionView(animationWindow);
            var analysis = KeyframeInterpolationSelectionAnalysis.Analyze(selectionView.Selections);
            var hasSelectedKeys = selectionView.UsesActiveEditSelection || selectionView.SelectedKeyCount > 0;
            var canEditSelection = hasSelectedKeys && analysis.EditablePairCount > 0;
            var lockActiveEditCurve = selectionView.UsesActiveEditSelection && currentCurve != null;
            var selectionChanged = UpdateSelectionTracking(selectionView.Selections);

            UpdateCurrentInterpolation(analysis, selectionChanged, lockActiveEditCurve, selectionView.Selections);
            ApplyViewState(CreateViewState(animationWindow, selectionView.Selections, analysis, canEditSelection, lockActiveEditCurve));
        }

        private void ClearWindowState()
        {
            selections.Clear();
            lastAppliedSelections.Clear();
            lastAppliedCurveDisplaySelectionSignature = InvalidSelectionSignature;
            cachedSelectionAnimationWindow = null;

            ResetSelectionTracking();
            ClearHandleDrag();
            EndActiveEditSession();

            ApplyViewState(new ViewState { SelectedModeIndex = -1 });
        }

        private SelectionView GetSelectionView(AnimationWindow animationWindow)
        {
            var usesActiveEditSelection = isDraggingHandle && activeEditSelections.Count > 0;
            return usesActiveEditSelection 
                ? new SelectionView(activeEditSelections, GetSelectedKeyCount(activeEditSelections), true)
                : new SelectionView(selections, ReadSelectedCurvesPreservingFocusCache(animationWindow), false);
        }

        private bool UpdateSelectionTracking(IReadOnlyList<KeyframeInterpolationCurveSelection> displayedSelections)
        {
            var selectionSignature = GetSelectionSignature(displayedSelections);
            var selectionChanged = selectionSignature != lastSelectionSignature;
            if (selectionChanged) {
                lastSelectionSignature = selectionSignature;
                if (lastAppliedCurveDisplaySelectionSignature != selectionSignature) {
                    lastAppliedCurveDisplaySelectionSignature = InvalidSelectionSignature;
                }

                if (!isDraggingHandle && !restoreSelectionAfterHandleDrag) {
                    lastAppliedSelections.Clear();
                }
            }

            return selectionChanged;
        }

        private void UpdateCurrentInterpolation(
            KeyframeInterpolationSelectionAnalysis analysis,
            bool selectionChanged,
            bool lockActiveEditCurve,
            IReadOnlyList<KeyframeInterpolationCurveSelection> displayedSelections)
        {
            var usesLastAppliedManualCurve = !lockActiveEditCurve && HasLastAppliedManualCurveForSelection(displayedSelections);
            if (usesLastAppliedManualCurve) {
                currentMode = AnimationUtility.TangentMode.Free;
                currentCurve = KeyframeInterpolationCurveUtility.NormalizeEditableCurve(currentCurve);
                return;
            }

            var selectionModeChanged = !lockActiveEditCurve && analysis.HasCommonMode && selectionChanged;
            if (selectionModeChanged) {
                currentMode = analysis.CommonMode;
            }

            var selectionHasCommonCurve = !lockActiveEditCurve && analysis.HasCommonCurve;
            var userIsEditingCurve = IsEditingHandleField() || isDraggingHandle;
            var commonCurveChanged = !KeyframeInterpolationCurveUtility.Approximately(currentCurve, analysis.CommonCurve);
            var selectionCurveChanged = selectionChanged || !userIsEditingCurve && commonCurveChanged;

            if (selectionHasCommonCurve && selectionCurveChanged) {
                currentCurve = KeyframeInterpolationCurveUtility.Clone(analysis.CommonCurve);
            } else if (!lockActiveEditCurve && analysis.HasOnlyIndeterminateCurve && selectionCurveChanged) {
                currentCurve = KeyframeInterpolationCurveUtility.CreateDefaultCurve();
            }
        }

        private bool HasLastAppliedManualCurveForSelection(IReadOnlyList<KeyframeInterpolationCurveSelection> displayedSelections)
        {
            if (currentCurve == null || lastAppliedCurveDisplaySelectionSignature == InvalidSelectionSignature) {
                return false;
            }

            var selectionSignature = GetSelectionSignature(displayedSelections);
            if (selectionSignature == lastAppliedCurveDisplaySelectionSignature) {
                return true;
            }

            lastAppliedCurveDisplaySelectionSignature = InvalidSelectionSignature;
            return false;
        }

        private ViewState CreateViewState(
            AnimationWindow animationWindow,
            IReadOnlyList<KeyframeInterpolationCurveSelection> displayedSelections,
            KeyframeInterpolationSelectionAnalysis analysis,
            bool canEditSelection,
            bool lockActiveEditCurve)
        {
            var usesLastAppliedManualCurve = !lockActiveEditCurve && HasLastAppliedManualCurveForSelection(displayedSelections);
            var commonMode = GetDisplayMode(analysis, lockActiveEditCurve, usesLastAppliedManualCurve);
            var hasMixedCurveValues = canEditSelection
                && !lockActiveEditCurve
                && !usesLastAppliedManualCurve
                && !analysis.HasCommonCurve
                && !analysis.HasOnlyIndeterminateCurve
                && (!analysis.HasCommonMode || commonMode == AnimationUtility.TangentMode.Free);
            
            var hasCommonManualCurve = lockActiveEditCurve
                || usesLastAppliedManualCurve
                || analysis.HasCommonCurve
                || analysis.HasOnlyIndeterminateCurve;
            var canEditHandles = canEditSelection
                && !hasMixedCurveValues
                && commonMode == AnimationUtility.TangentMode.Free
                && hasCommonManualCurve;
            var canConvertGraphToFree = canEditSelection
                && !hasMixedCurveValues
                && commonMode != AnimationUtility.TangentMode.Free;
            var selectedModeIndex = -1;
            if (canEditSelection
                && (analysis.HasCommonMode
                    || analysis.HasCommonCurve
                    || analysis.HasOnlyIndeterminateCurve
                    || usesLastAppliedManualCurve
                    || lockActiveEditCurve)) {
                selectedModeIndex = GetModeToolbarIndex(commonMode);
            }

            AnimationCurve curveForDrawing = null;
            if (canEditSelection) {
                curveForDrawing = lockActiveEditCurve || usesLastAppliedManualCurve || analysis.HasCommonCurve
                    ? currentCurve
                    : analysis.HasOnlyIndeterminateCurve
                        ? currentCurve ?? KeyframeInterpolationCurveUtility.CreateDefaultCurve()
                    : KeyframeInterpolationCurveUtility.CreateModePreviewCurve(commonMode);
            }

            var hasTimeCursor = TryGetCommonTimeCursor(animationWindow, displayedSelections, out var timeCursor);

            return new ViewState {
                CanEditSelection = canEditSelection,
                CanApplyPreset = canEditSelection,
                SelectedModeIndex = selectedModeIndex,
                DisplayMode = commonMode,
                HasCurveDisplay = canEditSelection,
                CanEditHandles = canEditHandles,
                CanConvertGraphToFree = canConvertGraphToFree,
                HasMixedCurveValues = hasMixedCurveValues,
                LockActiveEditCurve = lockActiveEditCurve,
                CurveForDrawing = curveForDrawing,
                HasTimeCursor = hasTimeCursor,
                TimeCursor = timeCursor
            };
        }

        private AnimationUtility.TangentMode GetDisplayMode(
            KeyframeInterpolationSelectionAnalysis analysis,
            bool lockActiveEditCurve,
            bool usesLastAppliedManualCurve)
        {
            var commonMode = currentMode;
            if (lockActiveEditCurve || usesLastAppliedManualCurve) {
                commonMode = AnimationUtility.TangentMode.Free;
            } else if (analysis.HasCommonMode) {
                commonMode = analysis.CommonMode;
            } else if (analysis.HasCommonCurve) {
                commonMode = AnimationUtility.TangentMode.Free;
            } else if (analysis.HasOnlyIndeterminateCurve) {
                commonMode = AnimationUtility.TangentMode.Free;
            }

            return commonMode;
        }
        private void ApplyViewState(ViewState state)
        {
            updatingControls = true;
            try {
                SetToolbarButtonsEnabled(modeButtons, state.CanEditSelection);
                SetToolbarButtonsEnabled(presetButtons, state.CanApplyPreset);
                SetModeToolbarSelection(state.SelectedModeIndex);

                graphElement.SetState(
                    state.CurveForDrawing,
                    state.DisplayMode,
                    state.HasCurveDisplay,
                    state.CanEditHandles,
                    state.CanConvertGraphToFree,
                    state.HasMixedCurveValues,
                    state.HasTimeCursor,
                    state.TimeCursor);

                var showHandleFields = state.HasCurveDisplay && state.CanEditHandles;
                fieldsContainer.style.display = showHandleFields || state.HasMixedCurveValues ? DisplayStyle.Flex : DisplayStyle.None;
                outHandleField.style.display = showHandleFields ? DisplayStyle.Flex : DisplayStyle.None;
                inHandleField.style.display = showHandleFields ? DisplayStyle.Flex : DisplayStyle.None;
                SetCopyCurveState(
                    state.CanEditHandles && !state.HasMixedCurveValues && state.CurveForDrawing != null,
                    state.CurveForDrawing);

                if (!state.HasCurveDisplay) {
                    readoutLabel.style.display = DisplayStyle.None;
                    readoutLabel.text = string.Empty;
                } else if (state.HasMixedCurveValues) {
                    SetHandleFields(KeyframeInterpolationCurveUtility.CreateDefaultCurve(), false, true);
                    readoutLabel.style.display = DisplayStyle.Flex;
                    readoutLabel.text = "Mixed";
                } else {
                    SetHandleFields(state.CurveForDrawing, state.CanEditHandles, false);
                    readoutLabel.style.display = DisplayStyle.None;
                    readoutLabel.text = string.Empty;
                }

                rootVisualElement.EnableInClassList("ck-keyframe-window--dragging", isDraggingHandle);
                rootVisualElement.EnableInClassList("ck-keyframe-window--mixed", state.HasMixedCurveValues);
                rootVisualElement.EnableInClassList("ck-keyframe-window--locked-curve", state.LockActiveEditCurve);
            } finally {
                updatingControls = false;
            }
        }

        private void SetCopyCurveState(bool canCopy, AnimationCurve copyCurve)
        {
            canCopyCurve = canCopy;
            displayedCopyCurve = canCopy && copyCurve != null ? KeyframeInterpolationCurveUtility.Clone(copyCurve) : null;
        }

        private static void SetToolbarButtonsEnabled(IReadOnlyList<ToolbarButton> buttons, bool enabled)
        {
            for (var i = 0; i < buttons.Count; i++) {
                buttons[i].SetEnabled(enabled);
                buttons[i].EnableInClassList("ck-keyframe-toolbar__button--disabled", !enabled);
            }
        }

        private void SetModeToolbarSelection(int selectedIndex)
        {
            for (var i = 0; i < modeButtons.Count; i++) {
                modeButtons[i].EnableInClassList("ck-keyframe-toolbar__button--selected", i == selectedIndex);
            }
        }

        private void SetHandleFields(AnimationCurve curve, bool enabled, bool mixed)
        {
            outHandleField.SetEnabled(enabled);
            inHandleField.SetEnabled(enabled);
            outHandleField.showMixedValue = mixed;
            inHandleField.showMixedValue = mixed;

            if (!mixed && curve != null && !IsEditingHandleField()) {
                KeyframeInterpolationGraphUtility.GetHandlePoints(curve, out var outHandle, out var inHandle);
                outHandleField.SetValueWithoutNotify(outHandle);
                inHandleField.SetValueWithoutNotify(inHandle);
            }
        }

        private bool IsEditingHandleField()
        {
            if (focusedWindow == this) {
                var focusedElement = rootVisualElement.focusController?.focusedElement as VisualElement;
                return focusedElement != null && (outHandleField.Contains(focusedElement) || inHandleField.Contains(focusedElement));
            }

            return false;
        }

        private bool TryResolveApplyContext(out ApplyContext context)
        {
            context = default;
            
            var animationWindow = GetTargetAnimationWindow();
            if (animationWindow == null) {
                return false;
            }

            if (isDraggingHandle && activeEditSelections.Count > 0) {
                return TryCreateApplyContext(animationWindow, activeEditSelections, out context);
            }

            if (!HasCapturedSelectionForAnimationWindow(animationWindow)) {
                return false;
            }

            return TryCreateApplyContext(animationWindow, selections, out context);
        }

        private AnimationWindow GetTargetAnimationWindow()
        {
            if (isDraggingHandle && activeEditAnimationWindow != null) {
                return activeEditAnimationWindow;
            }

            if ((focusedWindow == this || mouseOverWindow == this)
                && currentAnimationWindow != null
                && currentAnimationWindow.state != null) {
                return currentAnimationWindow;
            }

            var animationWindow = KeyframeInterpolationAnimationBridge.GetAnimationWindow();
            if (animationWindow != null) {
                currentAnimationWindow = animationWindow;
                return animationWindow;
            }

            if (currentAnimationWindow != null && currentAnimationWindow.state != null) {
                return currentAnimationWindow;
            }

            currentAnimationWindow = null;
            return null;
        }

        private int ReadSelectedCurvesPreservingFocusCache(AnimationWindow animationWindow)
        {
            if (CanUseCachedSelectionForFocusedWindow(animationWindow)) {
                return GetSelectedKeyCount(selections);
            }

            var selectedKeyCount = KeyframeInterpolationAnimationBridge.ReadSelectedCurves(animationWindow, selectionReadBuffer);
            if (selectedKeyCount > 0) {
                CopySelections(selections, selectionReadBuffer);
                cachedSelectionAnimationWindow = animationWindow;
                cachedSelectionShowsCurveEditor = animationWindow.state.showCurveEditor;
                return selectedKeyCount;
            }

            selections.Clear();
            cachedSelectionAnimationWindow = null;
            cachedSelectionShowsCurveEditor = false;
            return 0;
        }

        private bool HasCapturedSelectionForAnimationWindow(AnimationWindow animationWindow)
        {
            return animationWindow != null
                && ReferenceEquals(animationWindow, cachedSelectionAnimationWindow)
                && animationWindow.state != null
                && animationWindow.state.showCurveEditor == cachedSelectionShowsCurveEditor
                && selections.Count > 0;
        }

        private bool CanUseCachedSelectionForFocusedWindow(AnimationWindow animationWindow)
        {
            if (animationWindow == null
                || !ReferenceEquals(animationWindow, cachedSelectionAnimationWindow)
                || animationWindow.state == null
                || animationWindow.state.showCurveEditor != cachedSelectionShowsCurveEditor
                || selections.Count == 0) {
                return false;
            }

            return focusedWindow == this || mouseOverWindow == this;
        }

        private bool TryCreateApplyContext(
            AnimationWindow animationWindow,
            IReadOnlyList<KeyframeInterpolationCurveSelection> applySelections,
            out ApplyContext context)
        {
            context = default;
            var selectedKeyCount = GetSelectedKeyCount(applySelections);
            if (selectedKeyCount <= 0) {
                return false;
            }

            var analysis = KeyframeInterpolationSelectionAnalysis.Analyze(applySelections);
            var canEditSelectionValue = analysis.EditablePairCount > 0;
            var lockActiveEditCurve = isDraggingHandle && ReferenceEquals(applySelections, activeEditSelections) && currentCurve != null;
            var usesLastAppliedManualCurve = !lockActiveEditCurve && HasLastAppliedManualCurveForSelection(applySelections);
            var hasCommonManualCurve = lockActiveEditCurve
                || usesLastAppliedManualCurve
                || analysis.HasCommonCurve
                || analysis.HasOnlyIndeterminateCurve;
            var canEditCurveValue = canEditSelectionValue && hasCommonManualCurve;

            context = new ApplyContext(
                animationWindow,
                applySelections,
                analysis,
                canEditSelectionValue,
                canEditCurveValue);
            return true;
        }

        private void OnModeClicked(AnimationUtility.TangentMode mode)
        {
            if (updatingControls) {
                return;
            }

            if (!TryResolveApplyContext(out var context) || !context.CanEditSelection) {
                QueueWindowRefresh();
                return;
            }

            var queuedContext = SnapshotApplyContext(context);
            QueuePendingHandleFieldSave(true);
            QueueAnimationWindowEdit(() => ApplyQueuedMode(queuedContext, mode));
        }

        private void OnPresetClicked(KeyframeInterpolationPreset preset)
        {
            if (!TryResolveApplyContext(out var context) || !context.CanEditSelection) {
                QueueWindowRefresh();
                return;
            }

            var queuedContext = SnapshotApplyContext(context);
            QueuePendingHandleFieldSave(true);
            QueueAnimationWindowEdit(() => ApplyQueuedPreset(queuedContext, preset));
        }

        private void OnCopyCurveClicked()
        {
            if (updatingControls) {
                return;
            }

            if (!TryGetCopyCurve(out var curve)) {
                QueueWindowRefresh();
                return;
            }

            if (KeyframeInterpolationCurveClipboard.Copy(curve)) {
                QueueWindowRefresh();
            }
        }

        private void OnPasteCurveClicked()
        {
            if (updatingControls) {
                return;
            }

            if (!KeyframeInterpolationCurveClipboard.TryGetCurve(out var curve)
                || !TryResolveApplyContext(out var context)
                || !context.CanEditSelection) {
                QueueWindowRefresh();
                return;
            }

            var queuedCurve = KeyframeInterpolationCurveUtility.NormalizeEditableCurve(curve);
            var queuedContext = SnapshotApplyContext(context);
            QueuePendingHandleFieldSave(true);
            PreviewManualCurveEdit(queuedCurve);
            QueueAnimationWindowEdit(() => ApplyQueuedCurve(queuedContext, queuedCurve));
        }

        private bool TryGetCopyCurve(out AnimationCurve curve)
        {
            curve = null;
            if (!canCopyCurve) {
                return false;
            }

            var source = hasPendingHandleFieldEdit && pendingHandleFieldCurve != null
                ? pendingHandleFieldCurve
                : displayedCopyCurve;

            if (source == null) {
                return false;
            }

            curve = KeyframeInterpolationCurveUtility.NormalizeEditableCurve(source);
            return true;
        }

        private void OnGraphPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 1) {
                return;
            }

            ShowGraphContextMenu();
            evt.StopPropagation();
        }

        private void ShowGraphContextMenu()
        {
            var menu = new GenericMenu();
            if (TryGetCopyCurve(out _)) {
                menu.AddItem(new GUIContent("Copy Curve"), false, OnCopyCurveClicked);
            } else {
                menu.AddDisabledItem(new GUIContent("Copy Curve"));
            }

            if (KeyframeInterpolationCurveClipboard.HasCurve()
                && TryResolveApplyContext(out var context)
                && context.CanEditSelection) {
                menu.AddItem(new GUIContent("Paste Curve"), false, OnPasteCurveClicked);
            } else {
                menu.AddDisabledItem(new GUIContent("Paste Curve"));
            }

            menu.ShowAsContext();
        }

        private void OnOutHandleFieldChanged(ChangeEvent<Vector2> evt)
        {
            OnHandleFieldChanged(HandleSide.Out, evt.newValue);
        }

        private void OnInHandleFieldChanged(ChangeEvent<Vector2> evt)
        {
            OnHandleFieldChanged(HandleSide.In, evt.newValue);
        }

        private void OnHandleFieldChanged(HandleSide side, Vector2 handle)
        {
            if (updatingControls) {
                return;
            }

            if (!TryResolveApplyContext(out var context) || !context.CanEditCurve) {
                QueueWindowRefresh();
                return;
            }

            var sourceCurve = currentCurve ?? context.Analysis.CommonCurve;
            var queuedContext = SnapshotApplyContext(context);
            var queuedCurve = side == HandleSide.Out
                ? KeyframeInterpolationGraphUtility.SetOutHandle(sourceCurve, handle)
                : KeyframeInterpolationGraphUtility.SetInHandle(sourceCurve, handle);
            PreviewManualCurveEdit(queuedCurve);
            SetPendingHandleFieldEdit(queuedCurve, queuedContext);
        }

        private void OnHandleFieldFocusOut(FocusOutEvent evt)
        {
            QueueHandleFieldFocusCheck();
        }

        private bool OnGraphBeginDragRequested(Rect curveRange)
        {
            if (!TryResolveApplyContext(out var context) || !context.CanEditSelection) {
                QueueWindowRefresh();
                return false;
            }

            QueuePendingHandleFieldSave(true);
            isDraggingHandle = true;
            BeginHandleDrag(context.AnimationWindow, context.Selections);
            QueueWindowRefresh();
            return true;
        }

        private void OnGraphCurveChanged(AnimationCurve curve)
        {
            var queuedCurve = KeyframeInterpolationCurveUtility.Clone(curve);
            QueueAnimationWindowEdit(() => ApplyActiveDragCurve(queuedCurve));
        }

        private void OnGraphDragEnded()
        {
            QueueAnimationWindowEdit(EndGraphDrag);
        }

        private void SetPendingHandleFieldEdit(AnimationCurve curve, ApplyContext context)
        {
            pendingHandleFieldCurve = KeyframeInterpolationCurveUtility.Clone(curve);
            pendingHandleFieldContext = context;
            hasPendingHandleFieldEdit = true;
            
            if (!IsEditingHandleField()) {
                QueuePendingHandleFieldSave(false);
            }
        }

        private void ApplyManualCurve(AnimationCurve curve, ApplyContext context, bool queueReadback)
        {
            currentMode = AnimationUtility.TangentMode.Free;
            currentCurve = KeyframeInterpolationCurveUtility.NormalizeEditableCurve(curve);
            FinishApply(ApplyCurve(context.AnimationWindow, context.Selections), queueReadback);
        }

        private void PreviewManualCurveEdit(AnimationCurve curve)
        {
            currentMode = AnimationUtility.TangentMode.Free;
            currentCurve = KeyframeInterpolationCurveUtility.NormalizeEditableCurve(curve);

            updatingControls = true;
            try {
                SetModeToolbarSelection(GetModeToolbarIndex(AnimationUtility.TangentMode.Free));
                graphElement.SetState(currentCurve, AnimationUtility.TangentMode.Free, true, true, false, false, false, 0f);
                SetCopyCurveState(true, currentCurve);
                fieldsContainer.style.display = DisplayStyle.Flex;
                outHandleField.style.display = DisplayStyle.Flex;
                inHandleField.style.display = DisplayStyle.Flex;
                SetHandleFields(currentCurve, true, false);
                readoutLabel.style.display = DisplayStyle.None;
                readoutLabel.text = string.Empty;
                rootVisualElement.EnableInClassList("ck-keyframe-window--mixed", false);
            } finally {
                updatingControls = false;
            }

            Repaint();
        }

        private void QueuePendingHandleFieldSave(bool force)
        {
            if (!hasPendingHandleFieldEdit) {
                return;
            }

            if (pendingHandleFieldSaveQueued) {
                forcePendingHandleFieldSave |= force;
                return;
            }

            forcePendingHandleFieldSave = force;
            pendingHandleFieldSaveQueued = true;
            QueueAnimationWindowEdit(ApplyPendingHandleFieldCurve);
        }

        private void ApplyPendingHandleFieldCurve()
        {
            pendingHandleFieldSaveQueued = false;
            if (!hasPendingHandleFieldEdit || pendingHandleFieldCurve == null) {
                forcePendingHandleFieldSave = false;
                return;
            }

            if (!forcePendingHandleFieldSave && IsEditingHandleField()) {
                QueueHandleFieldFocusCheck();
                return;
            }

            forcePendingHandleFieldSave = false;
            if (TryTakePendingHandleFieldEdit(out var curve, out var context)) {
                ApplyManualCurve(curve, context, true);
            }
        }

        private bool TryTakePendingHandleFieldEdit(out AnimationCurve curve, out ApplyContext context)
        {
            curve = pendingHandleFieldCurve;
            context = pendingHandleFieldContext;
            var hasEdit = hasPendingHandleFieldEdit && curve != null;
            hasPendingHandleFieldEdit = false;
            pendingHandleFieldSaveQueued = false;
            forcePendingHandleFieldSave = false;
            pendingHandleFieldCurve = null;
            pendingHandleFieldContext = default;
            return hasEdit;
        }

        private void QueueHandleFieldFocusCheck()
        {
            if (handleFieldFocusCheckQueued) {
                return;
            }

            handleFieldFocusCheckQueued = true;
            EditorApplication.delayCall += RunHandleFieldFocusCheck;
        }

        private void RunHandleFieldFocusCheck()
        {
            EditorApplication.delayCall -= RunHandleFieldFocusCheck;
            handleFieldFocusCheckQueued = false;
            if (this == null || IsEditingHandleField()) {
                return;
            }

            QueuePendingHandleFieldSave(false);
        }

        private void OnUndoRedoPerformed()
        {
            EditorApplication.delayCall -= MarkPostSaveReadbackReady;
            EditorApplication.delayCall -= RunHandleFieldFocusCheck;
            pendingSavedCurveRefresh = false;
            queuedPostSaveRefresh = false;
            queuedWindowStateRefresh = false;
            handleFieldFocusCheckQueued = false;

            queuedAnimationWindowEdits.Clear();
            ClearPendingHandleFieldEdit();
            ClearHandleDrag();
            EndActiveEditSession();
            lastAppliedSelections.Clear();
            lastAppliedCurveDisplaySelectionSignature = InvalidSelectionSignature;
            restoreSelectionAfterHandleDrag = false;
            InvalidateCachedSelectionForReadback();
            ResetSelectionTracking();

            QueueUndoRedoRefresh();
        }

        private void QueueUndoRedoRefresh()
        {
            if (undoRedoRefreshQueued) {
                return;
            }

            undoRedoRefreshQueued = true;
            EditorApplication.delayCall += RunUndoRedoRefresh;
        }

        private void RunUndoRedoRefresh()
        {
            EditorApplication.delayCall -= RunUndoRedoRefresh;
            undoRedoRefreshQueued = false;
            if (this == null) {
                return;
            }

            var animationWindow = GetTargetAnimationWindow();
            if (animationWindow != null && selections.Count > 0) {
                KeyframeInterpolationAnimationBridge.RefreshChangedCurves(animationWindow, selections);
            }

            InvalidateCachedSelectionForReadback();
            ResetSelectionTracking();
            QueueWindowRefresh();
        }

        private void ClearPendingHandleFieldEdit()
        {
            hasPendingHandleFieldEdit = false;
            pendingHandleFieldSaveQueued = false;
            forcePendingHandleFieldSave = false;
            pendingHandleFieldCurve = null;
            pendingHandleFieldContext = default;
        }

        private void ApplyActiveDragCurve(AnimationCurve curve)
        {
            if (!isDraggingHandle || activeEditSelections.Count == 0) {
                return;
            }

            if (activeEditAnimationWindow == null || !EnsureActiveEditSession(activeEditAnimationWindow)) {
                return;
            }

            currentMode = AnimationUtility.TangentMode.Free;
            currentCurve = KeyframeInterpolationCurveUtility.NormalizeEditableCurve(curve);
            var applied = ApplyCurve(activeEditAnimationWindow, activeEditSelections);
            activeEditApplied |= applied;
            RefreshActiveDragControls();
        }

        private void RefreshActiveDragControls()
        {
            updatingControls = true;
            SetModeToolbarSelection(GetModeToolbarIndex(AnimationUtility.TangentMode.Free));
            SetCopyCurveState(currentCurve != null, currentCurve);
            fieldsContainer.style.display = DisplayStyle.Flex;
            outHandleField.style.display = DisplayStyle.Flex;
            inHandleField.style.display = DisplayStyle.Flex;
            SetHandleFields(currentCurve, true, false);
            rootVisualElement.EnableInClassList("ck-keyframe-window--dragging", true);
            rootVisualElement.EnableInClassList("ck-keyframe-window--mixed", false);
            rootVisualElement.EnableInClassList("ck-keyframe-window--locked-curve", true);
            updatingControls = false;
        }

        private void ApplyQueuedMode(ApplyContext context, AnimationUtility.TangentMode mode)
        {
            lastAppliedCurveDisplaySelectionSignature = InvalidSelectionSignature;
            currentMode = mode;
            if (currentMode != AnimationUtility.TangentMode.Free) {
                currentCurve = KeyframeInterpolationCurveUtility.CreateModePreviewCurve(currentMode);
            } else if (context.Analysis.HasCommonCurve) {
                currentCurve = KeyframeInterpolationCurveUtility.Clone(context.Analysis.CommonCurve);
            } else {
                currentCurve = KeyframeInterpolationCurveUtility.NormalizeEditableCurve(currentCurve);
            }

            FinishApply(ApplyMode(context.AnimationWindow, context.Selections, currentMode), true);
        }

        private void ApplyQueuedPreset(ApplyContext context, KeyframeInterpolationPreset preset)
        {
            currentMode = AnimationUtility.TangentMode.Free;
            currentCurve = KeyframeInterpolationCurveUtility.GetPresetCurve(preset);
            FinishApply(ApplyPreset(context.AnimationWindow, context.Selections, preset), true);
        }

        private void ApplyQueuedCurve(ApplyContext context, AnimationCurve curve)
        {
            currentMode = AnimationUtility.TangentMode.Free;
            currentCurve = KeyframeInterpolationCurveUtility.NormalizeEditableCurve(curve);
            FinishApply(ApplyCurve(context.AnimationWindow, context.Selections), true);
        }

        private void FinishApply(bool applied, bool queueReadback)
        {
            if (applied && queueReadback) {
                QueuePostSaveReadback();
            } else if (!applied) {
                lastAppliedCurveDisplaySelectionSignature = InvalidSelectionSignature;
                RefreshWindowState();
            }
        }

        private bool ApplyCurve(AnimationWindow animationWindow, IReadOnlyList<KeyframeInterpolationCurveSelection> applySelections)
        {
            var editSession = activeEditSession ?? KeyframeInterpolationAnimationBridge.BeginEditSession(animationWindow, UndoLabel, false);
            var ownsSession = activeEditSession == null;
            var applied = editSession.ApplyCurve(applySelections, currentCurve);
            return CompleteAppliedInterpolation(animationWindow, applySelections, editSession, !isDraggingHandle, ownsSession, applied, true);
        }

        private bool ApplyMode(AnimationWindow animationWindow, IReadOnlyList<KeyframeInterpolationCurveSelection> applySelections, AnimationUtility.TangentMode mode)
        {
            var editSession = KeyframeInterpolationAnimationBridge.BeginEditSession(animationWindow, UndoLabel, false);
            var applied = editSession.ApplyMode(applySelections, mode);
            return CompleteAppliedInterpolation(animationWindow, applySelections, editSession, true, true, applied, false);
        }

        private bool ApplyPreset(AnimationWindow animationWindow, IReadOnlyList<KeyframeInterpolationCurveSelection> applySelections, KeyframeInterpolationPreset preset)
        {
            var editSession = activeEditSession ?? KeyframeInterpolationAnimationBridge.BeginEditSession(animationWindow, UndoLabel, false);
            var ownsSession = activeEditSession == null;
            var applied = editSession.ApplyPreset(applySelections, preset);
            return CompleteAppliedInterpolation(animationWindow, applySelections, editSession, !isDraggingHandle, ownsSession, applied, true);
        }

        private bool CompleteAppliedInterpolation(
            AnimationWindow animationWindow,
            IReadOnlyList<KeyframeInterpolationCurveSelection> appliedSelections,
            KeyframeInterpolationEditSession editSession,
            bool restoreSelection,
            bool endSession,
            bool applied,
            bool cacheAppliedManualCurve)
        {
            if (!applied) {
                lastAppliedCurveDisplaySelectionSignature = InvalidSelectionSignature;
                if (endSession) {
                    editSession.End();
                }

                return false;
            }

            CacheAppliedSelections(appliedSelections);
            if (cacheAppliedManualCurve) {
                CacheAppliedCurveDisplaySelection(appliedSelections);
            } else {
                lastAppliedCurveDisplaySelectionSignature = InvalidSelectionSignature;
            }

            InvalidateCachedSelectionForReadback();

            if (endSession) {
                editSession.End();
            }

            if (restoreSelection) {
                restoreSelectionAfterHandleDrag = false;
                RestoreLastAppliedSelection(animationWindow);
            } else {
                restoreSelectionAfterHandleDrag = true;
                Repaint();
            }

            return true;
        }

        private void BeginHandleDrag(AnimationWindow animationWindow, IReadOnlyList<KeyframeInterpolationCurveSelection> applySelections)
        {
            EndActiveEditSession();

            restoreSelectionAfterHandleDrag = false;
            lastAppliedSelections.Clear();

            EditorGUIUtility.SetWantsMouseJumping(1);

            activeEditAnimationWindow = animationWindow;
            activeEditSelections.Clear();
            activeEditApplied = false;

            CopySelections(activeEditSelections, applySelections);
        }

        private bool EnsureActiveEditSession(AnimationWindow animationWindow)
        {
            if (activeEditSession != null) {
                return true;
            }

            if (animationWindow == null || activeEditSelections.Count == 0) {
                return false;
            }

            activeEditSession = KeyframeInterpolationAnimationBridge.BeginEditSession(animationWindow, UndoLabel, true);
            return activeEditSession != null;
        }

        private void EndGraphDrag()
        {
            ClearHandleDrag();
            
            activeEditSession?.End();
            activeEditSession = null;

            var shouldDeferRefresh = activeEditApplied;
            RestoreSelectionAfterHandleDrag(activeEditAnimationWindow);
            activeEditAnimationWindow = null;
            activeEditSelections.Clear();
            activeEditApplied = false;
            ResetSelectionTracking();
            
            if (shouldDeferRefresh) {
                QueuePostSaveReadback();
            } else {
                RefreshWindowState();
            }
        }

        private void ClearHandleDrag()
        {
            isDraggingHandle = false;
            EditorGUIUtility.SetWantsMouseJumping(0);
        }

        private void EndActiveEditSession()
        {
            activeEditSession?.End();
            activeEditSession = null;
            activeEditAnimationWindow = null;
            activeEditSelections.Clear();
            activeEditApplied = false;
        }

        private void RestoreSelectionAfterHandleDrag(AnimationWindow animationWindow)
        {
            if (restoreSelectionAfterHandleDrag && !isDraggingHandle) {
                restoreSelectionAfterHandleDrag = false;
                RestoreLastAppliedSelection(animationWindow);
            }
        }

        private void RestoreLastAppliedSelection(AnimationWindow animationWindow)
        {
            if (animationWindow != null && lastAppliedSelections.Count > 0) {
                KeyframeInterpolationAnimationBridge.RestoreSelection(animationWindow, lastAppliedSelections);
                lastAppliedSelections.Clear();
            }

            ResetSelectionTracking();
            Repaint();
        }

        private void CacheAppliedSelections(IReadOnlyList<KeyframeInterpolationCurveSelection> appliedSelections)
        {
            lastAppliedSelections.Clear();
            for (var i = 0; i < appliedSelections.Count; i++) {
                lastAppliedSelections.Add(appliedSelections[i]);
            }
        }

        private void CacheAppliedCurveDisplaySelection(IReadOnlyList<KeyframeInterpolationCurveSelection> appliedSelections)
        {
            lastAppliedCurveDisplaySelectionSignature = currentCurve != null
                ? GetSelectionSignature(appliedSelections)
                : InvalidSelectionSignature;
        }

        private void QueuePostSaveReadback()
        {
            if (pendingSavedCurveRefresh) {
                return;
            }

            pendingSavedCurveRefresh = true;
            EditorApplication.delayCall += MarkPostSaveReadbackReady;
        }

        private void MarkPostSaveReadbackReady()
        {
            EditorApplication.delayCall -= MarkPostSaveReadbackReady;
            if (this == null) {
                pendingSavedCurveRefresh = false;
                return;
            }

            queuedPostSaveRefresh = true;
            RequestGuiLoop();
        }

        private void RunQueuedGuiWork()
        {
            if (processingQueuedGuiWork) {
                return;
            }

            processingQueuedGuiWork = true;
            try {
                while (queuedAnimationWindowEdits.Count > 0) {
                    queuedAnimationWindowEdits.Dequeue().Invoke();
                }

                if (queuedPostSaveRefresh) {
                    RunPostSaveReadback();
                } else if (queuedWindowStateRefresh && !pendingSavedCurveRefresh) {
                    RunQueuedWindowRefresh();
                }
            } finally {
                processingQueuedGuiWork = false;
            }
        }

        private void RunQueuedWindowRefresh()
        {
            queuedWindowStateRefresh = false;
            RefreshWindowState();
            Repaint();
        }

        private void RunPostSaveReadback()
        {
            queuedPostSaveRefresh = false;
            queuedWindowStateRefresh = false;
            pendingSavedCurveRefresh = false;

            InvalidateCachedSelectionForReadback();
            RefreshWindowState();
            Repaint();
        }

        private void InvalidateCachedSelectionForReadback()
        {
            cachedSelectionAnimationWindow = null;
            cachedSelectionShowsCurveEditor = false;
        }

        private void QueueAnimationWindowEdit(Action action)
        {
            queuedAnimationWindowEdits.Enqueue(action);
            RequestGuiLoop();
        }

        private void QueueWindowRefresh()
        {
            queuedWindowStateRefresh = true;
            RequestGuiLoop();
        }

        private void RequestGuiLoop()
        {
            guiEventPump?.MarkDirtyRepaint();
            rootVisualElement?.MarkDirtyRepaint();
            Repaint();
        }

        private static ApplyContext SnapshotApplyContext(ApplyContext context)
        {
            var selectionsSnapshot = new List<KeyframeInterpolationCurveSelection>();
            CopySelections(selectionsSnapshot, context.Selections);
            
            return new ApplyContext(
                context.AnimationWindow,
                selectionsSnapshot,
                context.Analysis,
                context.CanEditSelection,
                context.CanEditCurve);
        }

        private static void CopySelections(List<KeyframeInterpolationCurveSelection> destination, IReadOnlyList<KeyframeInterpolationCurveSelection> source)
        {
            destination.Clear();
            for (var i = 0; i < source.Count; i++) {
                destination.Add(source[i]);
            }
        }

        private static int GetModeToolbarIndex(AnimationUtility.TangentMode mode)
        {
            for (var i = 0; i < ModeToolbarItems.Length; i++) {
                if (ModeToolbarItems[i].Mode == mode) {
                    return i;
                }
            }

            return -1;
        }

        private static Texture2D[] GetModeToolbarIcons()
        {
            modeToolbarIcons ??= LoadModeToolbarIcons();
            return modeToolbarIcons;
        }

        private static Texture2D[] GetPresetToolbarIcons()
        {
            presetToolbarIcons ??= LoadPresetToolbarIcons();
            return presetToolbarIcons;
        }

        private static Texture2D[] LoadModeToolbarIcons()
        {
            var icons = new Texture2D[ModeToolbarItems.Length];
            for (var i = 0; i < ModeToolbarItems.Length; i++) {
                icons[i] = LoadIcon(ModeToolbarItems[i].IconFilename);
            }

            return icons;
        }

        private static Texture2D[] LoadPresetToolbarIcons()
        {
            var icons = new Texture2D[PresetToolbarItems.Length];
            for (var i = 0; i < PresetToolbarItems.Length; i++) {
                icons[i] = LoadIcon(PresetToolbarItems[i].IconFilename);
            }

            return icons;
        }

        private static Texture2D GetWindowIcon()
        {
            if (windowIcon == null) {
                windowIcon = LoadIcon("WindowIcon.png");
            }

            return windowIcon;
        }

        private static Texture2D LoadIcon(string filename)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(IconRootPath + filename);
        }

        private static Color GetToolbarIconTint()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.74f, 0.74f, 0.74f, 1f)
                : new Color(0.28f, 0.28f, 0.28f, 1f);
        }

        private static string GetModeDisplayName(AnimationUtility.TangentMode mode)
        {
            return mode switch {
                AnimationUtility.TangentMode.Free => "Free",
                AnimationUtility.TangentMode.Auto => "Auto",
                AnimationUtility.TangentMode.Linear => "Linear",
                AnimationUtility.TangentMode.Constant => "Constant",
                AnimationUtility.TangentMode.ClampedAuto => "Clamped Auto",
                _ => mode.ToString()
            };
        }

        private static int GetSelectionSignature(IReadOnlyList<KeyframeInterpolationCurveSelection> curveSelections)
        {
            unchecked {
                var hash = 17;
                for (var i = 0; i < curveSelections.Count; i++) {
                    var selection = curveSelections[i];
                    hash = hash * 31 + (selection.ClipAsset != null ? selection.ClipAsset.GetInstanceID() : 0);
                    hash = hash * 31 + GetBindingHashCode(selection.Binding);
                    hash = hash * 31 + selection.Keys.Count;

                    for (var keyIndex = 0; keyIndex < selection.Keys.Count; keyIndex++) {
                        var key = selection.Keys[keyIndex];
                        hash = hash * 31 + key.Index;
                        hash = hash * 31 + key.Time.GetHashCode();
                        hash = hash * 31 + (int)key.Type;
                    }
                }

                return hash;
            }
        }

        private static int GetBindingHashCode(EditorCurveBinding binding)
        {
            unchecked {
                var hash = 17;
                hash = hash * 31 + (binding.path != null ? binding.path.GetHashCode() : 0);
                hash = hash * 31 + (binding.propertyName != null ? binding.propertyName.GetHashCode() : 0);
                hash = hash * 31 + (binding.type != null ? binding.type.GetHashCode() : 0);
                return hash;
            }
        }

        private static int GetSelectedKeyCount(IReadOnlyList<KeyframeInterpolationCurveSelection> curveSelections)
        {
            var count = 0;
            for (var i = 0; i < curveSelections.Count; i++) {
                count += curveSelections[i].Keys.Count;
            }

            return count;
        }

        private static bool TryGetCommonTimeCursor(AnimationWindow animationWindow, IReadOnlyList<KeyframeInterpolationCurveSelection> curveSelections, out float normalizedTime)
        {
            normalizedTime = 0f;
            if (animationWindow?.state == null || curveSelections.Count == 0) {
                return false;
            }

            var currentTime = animationWindow.state.currentTime;
            var hasAnySegment = false;
            
            for (var i = 0; i < curveSelections.Count; i++) {
                var selection = curveSelections[i];
                if (!selection.AnimationIsEditable || selection.IsObjectReferenceCurve || selection.IsDiscreteCurve) {
                    continue;
                }

                var curve = selection.LoadCurrentCurve();
                if (curve == null || curve.length < 2) {
                    continue;
                }

                var segments = KeyframeInterpolationTangentUtility.ResolveSelectedSegments(curve, selection.Keys);
                for (var segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++) {
                    var segment = segments[segmentIndex];
                    var left = curve[segment.LeftIndex];
                    var right = curve[segment.RightIndex];
                    var duration = right.time - left.time;
                    if (Mathf.Abs(duration) <= TimeCursorEpsilon) {
                        continue;
                    }

                    var segmentTime = (currentTime - left.time) / duration;
                    if (segmentTime is < -TimeCursorEpsilon or > 1f + TimeCursorEpsilon) {
                        return false;
                    }

                    segmentTime = Mathf.Clamp01(segmentTime);
                    if (!hasAnySegment) {
                        normalizedTime = segmentTime;
                        hasAnySegment = true;
                    } else if (Mathf.Abs(normalizedTime - segmentTime) > TimeCursorEpsilon) {
                        return false;
                    }
                }
            }

            return hasAnySegment;
        }
    }
}
