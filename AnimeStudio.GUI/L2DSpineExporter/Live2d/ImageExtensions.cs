using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Tga;
using System.IO;

namespace UnityLive2DExtractor
{
    public static class ImageExtensions
    {
        public static void WriteToStream(this Image image, Stream stream, ImageFormat imageFormat)
        {
            switch (imageFormat)
            {
                case ImageFormat.Jpeg:
                    image.SaveAsJpeg(stream);
                    break;
                case ImageFormat.Png:
                    image.SaveAsPng(stream);
                    break;
                case ImageFormat.Bmp:
                    image.Save(stream, new BmpEncoder
                    {
                        BitsPerPixel = BmpBitsPerPixel.Pixel32,
                        SupportTransparency = true
                    });
                    break;
                case ImageFormat.Tga:
                    image.Save(stream, new TgaEncoder
                    {
                        BitsPerPixel = TgaBitsPerPixel.Pixel32,
                        Compression = TgaCompression.None
                    });
                    break;
            }
        }
    }
}
