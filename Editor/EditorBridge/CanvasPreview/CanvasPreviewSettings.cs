using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Tripledot.CanvasKit.Editor.CanvasPreview
{
    [FilePath("ProjectSettings/CanvasKitCanvasPreviewSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class CanvasPreviewSettings : ScriptableSingleton<CanvasPreviewSettings>
    {
        [SerializeField]
        private string[] screenKeywords = { "screen", "page", "view" };
        [SerializeField]
        private string[] popupKeywords = { "popup", "modal", "dialog" };
        [SerializeField]
        private string[] elementKeywords = { "button", "btn", "control", "toggle", "slider", "cell", "item", "content", "icon", "image" };
        [SerializeField, HideInInspector]
        private string[] controlKeywords;
        [SerializeField, HideInInspector]
        private string[] contentKeywords;
        [SerializeField, HideInInspector]
        private bool legacyKeywordRulesMigrated;
        [SerializeField]
        private CanvasScaler.ScaleMode scalerUiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        [SerializeField]
        private Vector2 scalerReferenceResolution = new Vector2(1080f, 1920f);
        [SerializeField]
        private float scalerReferencePixelsPerUnit = 100f;
        [SerializeField]
        private int selectedReferenceSizeIndex = CanvasPreviewSize.DefaultIndex;

        internal static int Revision { get; private set; }
        internal static int SelectedReferenceSizeIndex => NormalizeReferenceSizeIndex(instance.selectedReferenceSizeIndex);

        internal string[] ScreenKeywords => screenKeywords;
        internal string[] PopupKeywords => popupKeywords;

        internal string[] ElementKeywords {
            get {
                MigrateLegacyKeywordRules();
                return elementKeywords;
            }
        }

        internal CanvasScaler.ScaleMode ScalerUiScaleMode => scalerUiScaleMode;
        internal Vector2 ScalerReferenceResolution => scalerReferenceResolution;
        internal float ScalerReferencePixelsPerUnit => scalerReferencePixelsPerUnit;

        internal static string[] GetKeywords(CanvasPreviewRole role)
        {
            var settings = instance;
            return role switch {
                CanvasPreviewRole.Screen => settings.screenKeywords,
                CanvasPreviewRole.Popup => settings.popupKeywords,
                CanvasPreviewRole.Element => settings.ElementKeywords,
                _ => Array.Empty<string>()
            };
        }

        internal static void SaveKeywordRules(string screen, string popup, string element)
        {
            SetKeywordRules(SplitKeywords(screen), SplitKeywords(popup), SplitKeywords(element), true);
        }

        internal static void SaveScalerDefaults(CanvasScaler.ScaleMode uiScaleMode, Vector2 referenceResolution, float referencePixelsPerUnit)
        {
            SetScalerDefaults(uiScaleMode, referenceResolution, referencePixelsPerUnit, true);
        }

        internal static void SaveSelectedReferenceSizeIndex(int selectedSizeIndex)
        {
            SetSelectedReferenceSizeIndex(selectedSizeIndex, true);
        }

        internal static void ConfigureScaler(CanvasScaler scaler)
        {
            if (scaler == null) {
                return;
            }

            var settings = instance;
            scaler.uiScaleMode = settings.scalerUiScaleMode;
            scaler.referenceResolution = SanitizeReferenceResolution(settings.scalerReferenceResolution);
            scaler.referencePixelsPerUnit = SanitizeReferencePixelsPerUnit(settings.scalerReferencePixelsPerUnit);
        }

        internal static string JoinKeywords(string[] keywords)
        {
            return string.Join(", ", SanitizeKeywords(keywords));
        }

        private void OnEnable()
        {
            MigrateLegacyKeywordRules();
        }

        private static void SetKeywordRules(string[] screen, string[] popup, string[] element, bool save)
        {
            var settings = instance;
            settings.screenKeywords = SanitizeKeywords(screen);
            settings.popupKeywords = SanitizeKeywords(popup);
            settings.elementKeywords = SanitizeKeywords(element);
            settings.controlKeywords = Array.Empty<string>();
            settings.contentKeywords = Array.Empty<string>();
            settings.legacyKeywordRulesMigrated = true;

            Revision++;
            CanvasPreviewWindow.ClearPreviewCache();

            if (save) {
                settings.Save(true);
            }
        }

        private static void SetScalerDefaults(CanvasScaler.ScaleMode uiScaleMode, Vector2 referenceResolution, float referencePixelsPerUnit, bool save)
        {
            var settings = instance;
            settings.scalerUiScaleMode = uiScaleMode;
            settings.scalerReferenceResolution = SanitizeReferenceResolution(referenceResolution);
            settings.scalerReferencePixelsPerUnit = SanitizeReferencePixelsPerUnit(referencePixelsPerUnit);

            Revision++;
            CanvasPreviewWindow.ClearPreviewCache();

            if (save) {
                settings.Save(true);
            }
        }

        private static void SetSelectedReferenceSizeIndex(int selectedSizeIndex, bool save)
        {
            var settings = instance;
            settings.selectedReferenceSizeIndex = NormalizeReferenceSizeIndex(selectedSizeIndex);

            Revision++;
            CanvasPreviewWindow.ClearPreviewCache();

            if (save) {
                settings.Save(true);
            }
        }

        private static Vector2 SanitizeReferenceResolution(Vector2 referenceResolution)
        {
            referenceResolution.x = Mathf.Max(1f, referenceResolution.x);
            referenceResolution.y = Mathf.Max(1f, referenceResolution.y);
            return referenceResolution;
        }

        private static int NormalizeReferenceSizeIndex(int selectedSizeIndex)
        {
            return selectedSizeIndex >= 0 && selectedSizeIndex < CanvasPreviewSize.StandardSizes.Length
                ? selectedSizeIndex
                : CanvasPreviewSize.DefaultIndex;
        }

        private static float SanitizeReferencePixelsPerUnit(float referencePixelsPerUnit)
        {
            return Mathf.Max(1f, referencePixelsPerUnit);
        }

        private static string[] SplitKeywords(string keywords)
        {
            return string.IsNullOrWhiteSpace(keywords)
                ? Array.Empty<string>()
                : keywords.Split(new[] { ',', '\n', '\r', ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string[] SanitizeKeywords(string[] keywords)
        {
            if (keywords == null || keywords.Length == 0) {
                return Array.Empty<string>();
            }

            var sanitized = new string[keywords.Length];
            var count = 0;
            foreach (var rawKeyword in keywords) {
                var keyword = rawKeyword?.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(keyword)) {
                    continue;
                }

                var duplicate = false;
                for (var existing = 0; existing < count; existing++) {
                    if (sanitized[existing] == keyword) {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate) {
                    sanitized[count++] = keyword;
                }
            }

            if (count == sanitized.Length) {
                return sanitized;
            }

            var result = new string[count];
            Array.Copy(sanitized, result, count);
            return result;
        }

        private void MigrateLegacyKeywordRules()
        {
            if (legacyKeywordRulesMigrated) {
                elementKeywords = SanitizeKeywords(elementKeywords);
                return;
            }

            if (HasKeywords(controlKeywords) || HasKeywords(contentKeywords)) {
                elementKeywords = MergeKeywords(controlKeywords, contentKeywords);
            } else {
                elementKeywords = SanitizeKeywords(elementKeywords);
            }

            controlKeywords = Array.Empty<string>();
            contentKeywords = Array.Empty<string>();
            legacyKeywordRulesMigrated = true;
        }

        private static bool HasKeywords(string[] keywords)
        {
            return SanitizeKeywords(keywords).Length > 0;
        }

        private static string[] MergeKeywords(string[] first, string[] second)
        {
            var firstKeywords = SanitizeKeywords(first);
            var secondKeywords = SanitizeKeywords(second);
            var merged = new string[firstKeywords.Length + secondKeywords.Length];
            Array.Copy(firstKeywords, merged, firstKeywords.Length);
            Array.Copy(secondKeywords, 0, merged, firstKeywords.Length, secondKeywords.Length);
            return SanitizeKeywords(merged);
        }
    }
}