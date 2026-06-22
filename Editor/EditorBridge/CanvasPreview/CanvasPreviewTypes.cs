using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    internal enum CanvasPreviewTargetKind
    {
        None,
        Canvas,
        RectTransform
    }

    internal enum CanvasPreviewRole
    {
        Screen,
        Popup,
        Element
    }

    internal enum CanvasPreviewRoleSource
    {
        None,
        Name,
        Structure
    }

    internal readonly struct CanvasPreviewRoleResult
    {
        internal readonly CanvasPreviewRole Role;
        internal readonly CanvasPreviewRoleSource Source;
        internal readonly string MatchedKeyword;

        internal CanvasPreviewRoleResult(CanvasPreviewRole role, CanvasPreviewRoleSource source, string matchedKeyword)
        {
            Role = role;
            Source = source;
            MatchedKeyword = matchedKeyword;
        }
    }

    internal readonly struct CanvasPreviewSize
    {
        internal const int DefaultIndex = 1;

        internal static readonly CanvasPreviewSize[] StandardSizes = {
            new CanvasPreviewSize("iPhone SE | 8 - 375 x 667 (9:16)", 375, 667),
            new CanvasPreviewSize("iPhone 12 | 13 - 390 x 844 (19.5:9)", 390, 844),
            new CanvasPreviewSize("iPhone 14 Pro Max - 430 x 932 (19.5:9)", 430, 932),
            new CanvasPreviewSize("iPad - 810 x 1080 (3:4)", 810, 1080),
            new CanvasPreviewSize("Landscape 16:9 - 1920 x 1080", 1920, 1080)
        };

        internal static readonly CanvasPreviewSize Default = StandardSizes[DefaultIndex];

        internal readonly string Label;
        internal readonly int Width;
        internal readonly int Height;

        private CanvasPreviewSize(string label, int width, int height)
        {
            Label = label;
            Width = width;
            Height = height;
        }

        internal Vector2 Vector => new Vector2(Width, Height);
    }

    internal readonly struct CanvasPreviewTarget
    {
        internal static readonly CanvasPreviewTarget Empty = new CanvasPreviewTarget(
            CanvasPreviewTargetKind.None,
            null,
            null,
            null);

        internal readonly CanvasPreviewTargetKind Kind;
        internal readonly GameObject PrefabRoot;
        internal readonly Canvas Canvas;
        internal readonly RectTransform RectTransform;

        internal CanvasPreviewTarget(
            CanvasPreviewTargetKind kind,
            GameObject prefabRoot,
            Canvas canvas,
            RectTransform rectTransform)
        {
            Kind = kind;
            PrefabRoot = prefabRoot;
            Canvas = canvas;
            RectTransform = rectTransform;
        }
    }
}