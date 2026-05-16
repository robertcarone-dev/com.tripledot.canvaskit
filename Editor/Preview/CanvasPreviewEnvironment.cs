using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tripledot.CanvasKit.Editor
{
    internal readonly struct CanvasPreviewEnvironmentKey : IEquatable<CanvasPreviewEnvironmentKey>
    {
        public readonly string ScenePath;
        public readonly Hash128 SceneHash;

        public CanvasPreviewEnvironmentKey(string scenePath, Hash128 sceneHash)
        {
            ScenePath = scenePath ?? string.Empty;
            SceneHash = sceneHash;
        }

        public bool Equals(CanvasPreviewEnvironmentKey other)
        {
            return ScenePath == other.ScenePath && SceneHash == other.SceneHash;
        }

        public override bool Equals(object obj)
        {
            return obj is CanvasPreviewEnvironmentKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked {
                return ((ScenePath != null ? ScenePath.GetHashCode() : 0) * 397) ^ SceneHash.GetHashCode();
            }
        }

        public static bool operator ==(CanvasPreviewEnvironmentKey left, CanvasPreviewEnvironmentKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CanvasPreviewEnvironmentKey left, CanvasPreviewEnvironmentKey right)
        {
            return !left.Equals(right);
        }
    }

    internal static class CanvasPreviewEnvironment
    {
        private static readonly List<Canvas> CanvasBuffer = new List<Canvas>(8);

        public static CanvasPreviewEnvironmentKey CreateCacheKey()
        {
            return CreateCacheKey(EditorSettings.prefabUIEnvironment);
        }

        public static CanvasPreviewEnvironmentKey CreateCacheKey(SceneAsset sceneAsset)
        {
            var scenePath = sceneAsset != null ? AssetDatabase.GetAssetPath(sceneAsset) : string.Empty;
            if (string.IsNullOrEmpty(scenePath)) {
                return default;
            }

            return new CanvasPreviewEnvironmentKey(scenePath, AssetDatabase.GetAssetDependencyHash(scenePath));
        }

        public static Scene OpenPreviewScene(out bool usesEnvironmentScene)
        {
            usesEnvironmentScene = false;
            
            var sceneAsset = EditorSettings.prefabUIEnvironment;
            var scenePath = sceneAsset != null ? AssetDatabase.GetAssetPath(sceneAsset) : string.Empty;
            
            if (IsValidSceneAssetPath(scenePath)) {
                Scene environmentScene = default;
                try {
                    environmentScene = EditorSceneManager.OpenPreviewScene(scenePath);
                    if (HasUsableEnvironmentCanvas(environmentScene)) {
                        usesEnvironmentScene = true;
                        return environmentScene;
                    }
                } catch {
                    usesEnvironmentScene = false;
                }

                if (environmentScene.IsValid()) {
                    try {
                        EditorSceneManager.ClosePreviewScene(environmentScene);
                    } catch {
                        // Fall back to the generated preview scene even if cleanup of the unusable environment fails.
                    }
                }
            }

            return EditorSceneManager.NewPreviewScene();
        }

        public static Canvas SelectEnvironmentCanvas(GameObject[] sceneRoots, Transform previewRoot)
        {
            if (sceneRoots.Length == 0) {
                return null;
            }

            for (int i = 0; i < sceneRoots.Length; i++) {
                var root = sceneRoots[i];
                if (root == null || (previewRoot != null && root.transform == previewRoot)) {
                    continue;
                }

                root.GetComponentsInChildren(true, CanvasBuffer);
                try {
                    for (int canvasIndex = 0; canvasIndex < CanvasBuffer.Count; canvasIndex++) {
                        var canvas = CanvasBuffer[canvasIndex];
                        if (!IsUsableEnvironmentCanvas(canvas)) {
                            continue;
                        }

                        if (previewRoot != null && canvas.transform.IsChildOf(previewRoot)) {
                            continue;
                        }

                        return canvas;
                    }
                } finally {
                    CanvasBuffer.Clear();
                }
            }

            return null;
        }

        private static bool HasUsableEnvironmentCanvas(Scene scene)
        {
            return scene.IsValid() && SelectEnvironmentCanvas(scene.GetRootGameObjects(), null) != null;
        }

        private static bool IsValidSceneAssetPath(string scenePath)
        {
            return !string.IsNullOrEmpty(scenePath)
                && scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                && AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null;
        }

        private static bool IsUsableEnvironmentCanvas(Canvas canvas)
        {
            return canvas != null
                && canvas.isActiveAndEnabled
                && canvas.isRootCanvas
                && (canvas.renderMode == RenderMode.ScreenSpaceOverlay || canvas.renderMode == RenderMode.ScreenSpaceCamera)
                && canvas.TryGetComponent(out RectTransform _);
        }
    }
}
