using UnityEngine;

namespace Tripledot.CanvasKit.Editor.CanvasPreview
{
    internal readonly struct CanvasPreviewSize
    {
        public const int DefaultIndex = 1;

        public static readonly CanvasPreviewSize[] StandardSizes = {
            new CanvasPreviewSize("iPhone SE | 8 - 375 x 667 (9:16)", 375, 667),
            new CanvasPreviewSize("iPhone 12 | 13 - 390 x 844 (19.5:9)", 390, 844),
            new CanvasPreviewSize("iPhone 14 Pro Max - 430 x 932 (19.5:9)", 430, 932),
            new CanvasPreviewSize("iPad - 810 x 1080 (3:4)", 810, 1080),
            new CanvasPreviewSize("Landscape 16:9 - 1920 x 1080", 1920, 1080)
        };

        public static readonly CanvasPreviewSize Default = StandardSizes[DefaultIndex];

        public readonly string Label;
        public readonly int Width;
        public readonly int Height;

        public Vector2 Vector => new Vector2(Width, Height);

        private CanvasPreviewSize(string label, int width, int height)
        {
            Label = label;
            Width = width;
            Height = height;
        }
    }
}