using System;
using System.Buffers;
using System.IO;
using K4os.Hash.xxHash;
using SkiaSharp;

namespace AnimeStudio
{
    public static class Texture2DExtensions
    {
        public static string GetImageHash(this Texture2D m_Texture2D)
        {
            var image = m_Texture2D.ConvertToImage(true);
            var hashstring = "";
            if (image != null)
            {
                try
                {
                    // TODO: be not only for png's since people may not always use that format, but in the end the hash is still unique and different from the raw data one
                    using var ms = new MemoryStream();
                    image.WriteToStream(ms, ImageFormat.Png);
                    ms.Position = 0;
                    Span<byte> span = ms.GetBuffer().AsSpan(0, (int)ms.Length);
                    var hash = XXH64.DigestOf(span);
                    hashstring = hash.ToString("x");
                }
                catch
                {
                    hashstring = "";
                }

                image.Dispose();
            }

            return hashstring;
        }

        public static SKBitmap ConvertToImage(this Texture2D m_Texture2D, bool flip)
        {
            var converter = new Texture2DConverter(m_Texture2D);
            byte[] buff = ArrayPool<byte>.Shared.Rent(m_Texture2D.m_Width * m_Texture2D.m_Height * 4);
            try
            {
                if (converter.DecodeTexture2D(buff))
                {
                    var image = ImageExtensions.CreateBitmapFromBgra(buff, m_Texture2D.m_Width, m_Texture2D.m_Height);
                    if (flip)
                    {
                        var flippedImage = image.FlipVertical();
                        image.Dispose();
                        image = flippedImage;
                    }
                    return image;
                }
                return null;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buff, true);
            }
        }

        public static MemoryStream ConvertToStream(this Texture2D m_Texture2D, ImageFormat imageFormat, bool flip)
        {
            var image = ConvertToImage(m_Texture2D, flip);
            if (image != null)
            {
                using (image)
                {
                    return image.ConvertToStream(imageFormat);
                }
            }
            return null;
        }
    }
}
