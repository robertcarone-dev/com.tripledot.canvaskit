using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Tripledot.CanvasKit.Editor.CanvasPreview
{
    internal static class CanvasPreviewSettingsProvider
    {
        [SettingsProvider]
        private static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider("Project/CanvasKit/Canvas Preview", SettingsScope.Project) {
                label = "Canvas Preview",
                guiHandler = _ => DrawSettings()
            };
        }

        private static void DrawSettings()
        {
            var settings = CanvasPreviewSettings.instance;
            EditorGUILayout.LabelField("Asset Name Role Keywords", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Asset names are checked using these case-insensitive keywords, then structural fallback is used.", MessageType.None);

            var screen = EditorGUILayout.TextField("Screen", CanvasPreviewSettings.JoinKeywords(settings.ScreenKeywords));
            var popup = EditorGUILayout.TextField("Popup", CanvasPreviewSettings.JoinKeywords(settings.PopupKeywords));
            var element = EditorGUILayout.TextField("Element", CanvasPreviewSettings.JoinKeywords(settings.ElementKeywords));

            if (GUILayout.Button("Save Canvas Preview Keywords")) {
                CanvasPreviewSettings.SaveKeywordRules(screen, popup, element);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Reference Canvas Scaler", EditorStyles.boldLabel);
            var scaleMode = (CanvasScaler.ScaleMode)EditorGUILayout.EnumPopup("UI Scale Mode", settings.ScalerUiScaleMode);
            var referenceResolution = EditorGUILayout.Vector2Field("Reference Resolution", settings.ScalerReferenceResolution);
            var referencePixelsPerUnit = EditorGUILayout.FloatField("Reference Pixels Per Unit", settings.ScalerReferencePixelsPerUnit);

            if (GUILayout.Button("Save Canvas Preview Scaler Defaults")) {
                CanvasPreviewSettings.SaveScalerDefaults(scaleMode, referenceResolution, referencePixelsPerUnit);
            }
        }
    }
}