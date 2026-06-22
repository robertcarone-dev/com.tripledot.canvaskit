using System;
using UnityEditor;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    internal static class CanvasPreviewRoleResolver
    {
        internal static CanvasPreviewRole Resolve(GameObject prefabAsset, CanvasPreviewTarget target)
        {
            return ResolveDetailed(prefabAsset, target).Role;
        }

        internal static CanvasPreviewRoleResult ResolveDetailed(GameObject prefabAsset, CanvasPreviewTarget target)
        {
            if (prefabAsset != null) {
                var assetName = GetAssetName(prefabAsset);
                if (TryResolveFromName(assetName, out var nameResult)) {
                    return nameResult;
                }
            }

            return ResolveFromStructure(target);
        }

        internal static bool UsesPreset(CanvasPreviewRole role)
        {
            return role is CanvasPreviewRole.Screen or CanvasPreviewRole.Popup;
        }

        internal static string GetDisplayName(CanvasPreviewRole role)
        {
            return role switch {
                CanvasPreviewRole.Screen => "Screen",
                CanvasPreviewRole.Popup => "Popup",
                _ => "Element"
            };
        }

        private static CanvasPreviewRoleResult ResolveFromStructure(CanvasPreviewTarget target)
        {
            if (target.Kind == CanvasPreviewTargetKind.Canvas && target.Canvas != null) {
                var role = target.Canvas.renderMode is RenderMode.ScreenSpaceOverlay or RenderMode.ScreenSpaceCamera ? CanvasPreviewRole.Screen : CanvasPreviewRole.Element;
                return new CanvasPreviewRoleResult(role, CanvasPreviewRoleSource.Structure, string.Empty);
            }

            if (target.Kind == CanvasPreviewTargetKind.RectTransform && target.RectTransform != null) {
                var role = HasStretchAnchors(target.RectTransform) ? CanvasPreviewRole.Popup : CanvasPreviewRole.Element;
                return new CanvasPreviewRoleResult(role, CanvasPreviewRoleSource.Structure, string.Empty);
            }

            return new CanvasPreviewRoleResult(CanvasPreviewRole.Element, CanvasPreviewRoleSource.Structure, string.Empty);
        }

        private static bool TryResolveFromName(string assetName, out CanvasPreviewRoleResult result)
        {
            var tokens = Tokenize(assetName);
            if (TryMatchRole(tokens, CanvasPreviewRole.Screen, out var keyword)) {
                result = new CanvasPreviewRoleResult(CanvasPreviewRole.Screen, CanvasPreviewRoleSource.Name, keyword);
                return true;
            }

            if (TryMatchRole(tokens, CanvasPreviewRole.Popup, out keyword)) {
                result = new CanvasPreviewRoleResult(CanvasPreviewRole.Popup, CanvasPreviewRoleSource.Name, keyword);
                return true;
            }

            if (TryMatchRole(tokens, CanvasPreviewRole.Element, out keyword)) {
                result = new CanvasPreviewRoleResult(CanvasPreviewRole.Element, CanvasPreviewRoleSource.Name, keyword);
                return true;
            }

            result = default;
            return false;
        }

        private static bool TryMatchRole(string[] assetTokens, CanvasPreviewRole role, out string matchedKeyword)
        {
            var keywords = CanvasPreviewSettings.GetKeywords(role);
            for (var i = 0; i < keywords.Length; i++) {
                var keyword = keywords[i];
                if (KeywordMatches(assetTokens, Tokenize(keyword))) {
                    matchedKeyword = keyword;
                    return true;
                }
            }

            matchedKeyword = string.Empty;
            return false;
        }

        private static bool KeywordMatches(string[] assetTokens, string[] keywordTokens)
        {
            if (assetTokens.Length == 0 || keywordTokens.Length == 0 || keywordTokens.Length > assetTokens.Length) {
                return false;
            }

            for (var start = 0; start <= assetTokens.Length - keywordTokens.Length; start++) {
                var matches = true;
                for (var offset = 0; offset < keywordTokens.Length; offset++) {
                    if (assetTokens[start + offset] != keywordTokens[offset]) {
                        matches = false;
                        break;
                    }
                }

                if (matches) {
                    return true;
                }
            }

            return false;
        }

        private static string[] Tokenize(string value)
        {
            if (string.IsNullOrEmpty(value)) {
                return Array.Empty<string>();
            }

            var buffer = new char[value.Length * 2];
            var count = 0;
            var previousWasLowerOrDigit = false;

            for (var i = 0; i < value.Length; i++) {
                var c = value[i];
                if (char.IsUpper(c) && previousWasLowerOrDigit) {
                    buffer[count++] = ' ';
                }

                if (char.IsLetterOrDigit(c)) {
                    buffer[count++] = char.ToLowerInvariant(c);
                    previousWasLowerOrDigit = char.IsLower(c) || char.IsDigit(c);
                } else {
                    buffer[count++] = ' ';
                    previousWasLowerOrDigit = false;
                }
            }

            return new string(buffer, 0, count)
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string GetAssetName(GameObject prefabAsset)
        {
            var assetPath = AssetDatabase.GetAssetPath(prefabAsset);
            return string.IsNullOrEmpty(assetPath)
                ? prefabAsset.name
                : System.IO.Path.GetFileNameWithoutExtension(assetPath);
        }

        private static bool HasStretchAnchors(RectTransform rectTransform)
        {
            return !Mathf.Approximately(rectTransform.anchorMin.x, rectTransform.anchorMax.x)
                   || !Mathf.Approximately(rectTransform.anchorMin.y, rectTransform.anchorMax.y);
        }
    }
}