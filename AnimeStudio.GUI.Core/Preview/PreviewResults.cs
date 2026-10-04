namespace AnimeStudio.GUI.Core.Preview
{
    public abstract record PreviewResult
    {
        public string InfoText { get; init; }
        public string Status { get; init; }

        // Approximate managed memory held, used by the preview cache.
        public virtual long Cost => 256;
    }

    public sealed record NoPreview : PreviewResult;

    public sealed record TextPreview(string Text) : PreviewResult
    {
        public override long Cost => 64 + (Text?.Length ?? 0) * 2L;
    }

    // Pixels are top-down BGRA32, unmasked. Channel toggles are applied on a copy.
    public sealed record TexturePreview(byte[] Pixels, int Width, int Height, bool ShowChannels) : PreviewResult
    {
        public override long Cost => 64 + Pixels.Length;
    }

    public sealed record FontPreview(byte[] Data) : PreviewResult
    {
        public override long Cost => 64 + Data.Length;
    }

    public sealed record AudioPreview(byte[] Data, uint Length) : PreviewResult
    {
        public override long Cost => 64 + Data.Length;
    }

    public sealed record MeshPreview(MeshData Mesh) : PreviewResult
    {
        public override long Cost => Mesh.Cost;
    }
}
