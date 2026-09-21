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

        // Keep transforms in straight-alpha BGRA space. SKCanvas draws can destroy RGB hidden by alpha.
        public static SKBitmap Resize(this SKBitmap image, int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            var source = GetBgraBytes(image);
            var destination = new byte[checked(width * height * 4)];
            var scaleX = (float)image.Width / width;
            var scaleY = (float)image.Height / height;

            for (int destinationY = 0; destinationY < height; destinationY++)
            {
                var sourceY = (destinationY + 0.5f) * scaleY - 0.5f;
                var top = (int)Math.Floor(sourceY);
                var verticalWeight = sourceY - top;
                if (top < 0)
                {
                    top = 0;
                    verticalWeight = 0f;
                }
                else if (top >= image.Height - 1)
                {
                    top = image.Height - 1;
                    verticalWeight = 0f;
                }
                var bottom = Math.Min(top + 1, image.Height - 1);

                for (int destinationX = 0; destinationX < width; destinationX++)
                {
                    var sourceX = (destinationX + 0.5f) * scaleX - 0.5f;
                    var left = (int)Math.Floor(sourceX);
                    var horizontalWeight = sourceX - left;
                    if (left < 0)
                    {
                        left = 0;
                        horizontalWeight = 0f;
                    }
                    else if (left >= image.Width - 1)
                    {
                        left = image.Width - 1;
                        horizontalWeight = 0f;
                    }
                    var right = Math.Min(left + 1, image.Width - 1);

                    var topLeftOffset = (top * image.Width + left) * 4;
                    var topRightOffset = (top * image.Width + right) * 4;
                    var bottomLeftOffset = (bottom * image.Width + left) * 4;
                    var bottomRightOffset = (bottom * image.Width + right) * 4;
                    var destinationOffset = (destinationY * width + destinationX) * 4;

                    for (int channel = 0; channel < 4; channel++)
                    {
                        var topValue = Lerp(source[topLeftOffset + channel], source[topRightOffset + channel], horizontalWeight);
                        var bottomValue = Lerp(source[bottomLeftOffset + channel], source[bottomRightOffset + channel], horizontalWeight);
                        destination[destinationOffset + channel] = (byte)(Lerp(topValue, bottomValue, verticalWeight) + 0.5f);
                    }
                }
            }

            return CreateBitmapFromBgra(destination, width, height);
        }

        public static SKBitmap Crop(this SKBitmap image, SKRectI rect)
        {
            if (rect.Width <= 0 || rect.Height <= 0 || rect.Left < 0 || rect.Top < 0 || rect.Right > image.Width || rect.Bottom > image.Height)
            {
                throw new ArgumentOutOfRangeException(nameof(rect));
            }

            var source = GetBgraBytes(image);
            var destinationRowBytes = checked(rect.Width * 4);
            var destination = new byte[checked(destinationRowBytes * rect.Height)];
            for (int y = 0; y < rect.Height; y++)
            {
                var sourceOffset = ((rect.Top + y) * image.Width + rect.Left) * 4;
                Buffer.BlockCopy(source, sourceOffset, destination, y * destinationRowBytes, destinationRowBytes);
            }

            return CreateBitmapFromBgra(destination, rect.Width, rect.Height);
        }

        public static SKBitmap FlipHorizontal(this SKBitmap image)
        {
            var source = GetBgraBytes(image);
            var destination = new byte[source.Length];
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var sourceOffset = (y * image.Width + x) * 4;
                    var destinationOffset = (y * image.Width + image.Width - 1 - x) * 4;
                    CopyPixel(source, sourceOffset, destination, destinationOffset);
                }
            }

            return CreateBitmapFromBgra(destination, image.Width, image.Height);
        }

        public static SKBitmap FlipVertical(this SKBitmap image)
        {
            var source = GetBgraBytes(image);
            var destination = new byte[source.Length];
            var rowBytes = checked(image.Width * 4);
            for (int y = 0; y < image.Height; y++)
            {
                Buffer.BlockCopy(source, y * rowBytes, destination, (image.Height - 1 - y) * rowBytes, rowBytes);
            }

            return CreateBitmapFromBgra(destination, image.Width, image.Height);
        }

        public static SKBitmap Rotate180(this SKBitmap image)
        {
            var source = GetBgraBytes(image);
            var destination = new byte[source.Length];
            var pixelCount = checked(image.Width * image.Height);
            for (int sourcePixel = 0; sourcePixel < pixelCount; sourcePixel++)
            {
                CopyPixel(source, sourcePixel * 4, destination, (pixelCount - 1 - sourcePixel) * 4);
            }

            return CreateBitmapFromBgra(destination, image.Width, image.Height);
        }

        public static SKBitmap Rotate270(this SKBitmap image)
        {
            var source = GetBgraBytes(image);
            var destination = new byte[source.Length];
            var destinationWidth = image.Height;
            for (int sourceY = 0; sourceY < image.Height; sourceY++)
            {
                for (int sourceX = 0; sourceX < image.Width; sourceX++)
                {
                    var destinationX = sourceY;
                    var destinationY = image.Width - 1 - sourceX;
                    var sourceOffset = (sourceY * image.Width + sourceX) * 4;
                    var destinationOffset = (destinationY * destinationWidth + destinationX) * 4;
                    CopyPixel(source, sourceOffset, destination, destinationOffset);
                }
            }

            return CreateBitmapFromBgra(destination, image.Height, image.Width);
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

        private static byte[] GetBgraBytes(SKBitmap image)
        {
            if (image.ColorType != SKColorType.Bgra8888)
            {
                throw new NotSupportedException($"Expected {SKColorType.Bgra8888} pixels, but found {image.ColorType}.");
            }

            return image.ConvertToBytes() ?? throw new InvalidOperationException("The bitmap has no pixel buffer.");
        }

        private static void CopyPixel(byte[] source, int sourceOffset, byte[] destination, int destinationOffset)
        {
            destination[destinationOffset] = source[sourceOffset];
            destination[destinationOffset + 1] = source[sourceOffset + 1];
            destination[destinationOffset + 2] = source[sourceOffset + 2];
            destination[destinationOffset + 3] = source[sourceOffset + 3];
        }

        private static float Lerp(float first, float second, float amount)
        {
            return first + (second - first) * amount;
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
