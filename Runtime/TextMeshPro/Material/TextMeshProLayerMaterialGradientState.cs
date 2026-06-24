namespace Tripledot.CanvasKit.TextMeshPro
{
    internal sealed class TextMeshProLayerMaterialGradientState
    {
        public readonly CanvasGradientAtlas.Lease Face = new CanvasGradientAtlas.Lease();
        public readonly CanvasGradientAtlas.Lease Stroke = new CanvasGradientAtlas.Lease();
        public readonly CanvasGradientAtlas.Lease Shadow = new CanvasGradientAtlas.Lease();

        public void Release()
        {
            CanvasGradientAtlas.Release(Face);
            CanvasGradientAtlas.Release(Stroke);
            CanvasGradientAtlas.Release(Shadow);
        }
    }
}