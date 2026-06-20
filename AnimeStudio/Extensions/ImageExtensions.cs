using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SkiaSharp;

namespace AnimeStudio
{
    public static class ImageExtensions
    {
        public static SKBitmap CreateBitmapFromBgra(byte[] pixels, int width, int height)
        {
            var imageInfo = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            var bitmap = new SKBitmap(imageInfo);
            CopyBgraToBitmap(pixels, bitmap);
            return bitmap;
        }

        public static void WriteToStream(this SKBitmap image, Stream stream, ImageFormat imageFormat)
        {
            switch (imageFormat)
            {
                case ImageFormat.Jpeg:
                    image.EncodeToStream(stream, SKEncodedImageFormat.Jpeg, 90);
                    break;
                case ImageFormat.Png:
                    image.EncodeToStream(stream, SKEncodedImageFormat.Png, 100);
                    break;
                case ImageFormat.Bmp:
                    image.WriteBmpToStream(stream);
                    break;
                case ImageFormat.Tga:
                    image.WriteTgaToStream(stream);
                    break;
            }
        }

        public static MemoryStream ConvertToStream(this SKBitmap image, ImageFormat imageFormat)
        {
            var stream = new MemoryStream();
            image.WriteToStream(stream, imageFormat);
            return stream;
        }

        public static byte[] ConvertToBytes(this SKBitmap image)
        {
            var bytesPerRow = image.Width * 4;
            var bytes = new byte[bytesPerRow * image.Height];
            var source = image.GetPixels();
            if (source == IntPtr.Zero)
            {
                return null;
            }

            if (image.RowBytes == bytesPerRow)
            {
                Marshal.Copy(source, bytes, 0, bytes.Length);
                return bytes;
            }

            var sourceBytes = new byte[image.RowBytes * image.Height];
            Marshal.Copy(source, sourceBytes, 0, sourceBytes.Length);
            for (int y = 0; y < image.Height; y++)
            {
                Buffer.BlockCopy(sourceBytes, y * image.RowBytes, bytes, y * bytesPerRow, bytesPerRow);
            }
            return bytes;
        }

        public static SKBitmap Resize(this SKBitmap image, int width, int height)
        {
            var resized = CreateEmptyBitmap(width, height);
            using (var canvas = new SKCanvas(resized))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawBitmap(image, new SKRect(0, 0, width, height));
            }
            return resized;
        }

        public static SKBitmap Crop(this SKBitmap image, SKRectI rect)
        {
            var cropped = CreateEmptyBitmap(rect.Width, rect.Height);
            using (var canvas = new SKCanvas(cropped))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawBitmap(image, rect, new SKRect(0, 0, rect.Width, rect.Height));
            }
            return cropped;
        }

        public static SKBitmap FlipHorizontal(this SKBitmap image)
        {
            var flipped = CreateEmptyBitmap(image.Width, image.Height);
            using (var canvas = new SKCanvas(flipped))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.Scale(-1, 1);
                canvas.DrawBitmap(image, -image.Width, 0);
            }
            return flipped;
        }

        public static SKBitmap FlipVertical(this SKBitmap image)
        {
            var flipped = CreateEmptyBitmap(image.Width, image.Height);
            using (var canvas = new SKCanvas(flipped))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.Scale(1, -1);
                canvas.DrawBitmap(image, 0, -image.Height);
            }
            return flipped;
        }

        public static SKBitmap Rotate180(this SKBitmap image)
        {
            var rotated = CreateEmptyBitmap(image.Width, image.Height);
            using (var canvas = new SKCanvas(rotated))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.RotateDegrees(180, image.Width / 2f, image.Height / 2f);
                canvas.DrawBitmap(image, 0, 0);
            }
            return rotated;
        }

        public static SKBitmap Rotate270(this SKBitmap image)
        {
            var rotated = CreateEmptyBitmap(image.Height, image.Width);
            using (var canvas = new SKCanvas(rotated))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.Translate(0, image.Width);
                canvas.RotateDegrees(270);
                canvas.DrawBitmap(image, 0, 0);
            }
            return rotated;
        }

        public static void CopyBgraToBitmap(byte[] source, SKBitmap bitmap)
        {
            var destination = bitmap.GetPixels();
            if (destination == IntPtr.Zero)
            {
                return;
            }

            var bytesPerRow = bitmap.Width * 4;
            if (bitmap.RowBytes == bytesPerRow)
            {
                Marshal.Copy(source, 0, destination, bytesPerRow * bitmap.Height);
                return;
            }

            for (int y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(source, y * bytesPerRow, destination + y * bitmap.RowBytes, bytesPerRow);
            }
        }

        private static SKBitmap CreateEmptyBitmap(int width, int height)
        {
            return new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        }

        private static void EncodeToStream(this SKBitmap image, Stream stream, SKEncodedImageFormat format, int quality)
        {
            using (var skImage = SKImage.FromBitmap(image))
            using (var data = skImage.Encode(format, quality))
            {
                data?.SaveTo(stream);
            }
        }

        private static void WriteBmpToStream(this SKBitmap image, Stream stream)
        {
            var pixels = image.ConvertToBytes();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                var pixelDataSize = pixels.Length;
                const int fileHeaderSize = 14;
                const int bitmapV4HeaderSize = 108;
                var pixelOffset = fileHeaderSize + bitmapV4HeaderSize;

                writer.Write((byte)'B');
                writer.Write((byte)'M');
                writer.Write(pixelOffset + pixelDataSize);
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write(pixelOffset);

                writer.Write(bitmapV4HeaderSize);
                writer.Write(image.Width);
                writer.Write(-image.Height);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write(3);
                writer.Write(pixelDataSize);
                writer.Write(2835);
                writer.Write(2835);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0x00FF0000);
                writer.Write(0x0000FF00);
                writer.Write(0x000000FF);
                writer.Write(unchecked((int)0xFF000000));
                writer.Write(0x73524742);
                writer.Write(new byte[36]);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
                writer.Write(pixels);
            }
        }

        private static void WriteTgaToStream(this SKBitmap image, Stream stream)
        {
            var pixels = image.ConvertToBytes();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)2);
                writer.Write(new byte[5]);
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write((ushort)image.Width);
                writer.Write((ushort)image.Height);
                writer.Write((byte)32);
                writer.Write((byte)0x28);
                writer.Write(pixels);
            }
        }
    }
}
