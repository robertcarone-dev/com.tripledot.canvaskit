using System;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor.CanvasPreview
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
}