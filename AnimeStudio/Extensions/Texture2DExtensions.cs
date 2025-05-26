﻿using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;

namespace AnimeStudio
{
    public static class Texture2DExtensions
    {
        private static Configuration _configuration;

        static Texture2DExtensions()
        {
            _configuration = Configuration.Default.Clone();
            _configuration.PreferContiguousImageBuffers = true;
        }

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
                    image.Save(ms, new PngEncoder());
                    ms.Position = 0;
                    using var sha256 = SHA256.Create();
                    var hash = sha256.ComputeHash(ms);
                    hashstring = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
                catch
                {
                    hashstring = "";
                }

                image.Dispose();
            }

            return hashstring;
        }

        public static Image<Bgra32> ConvertToImage(this Texture2D m_Texture2D, bool flip)
        {
            var converter = new Texture2DConverter(m_Texture2D);
            var buff = ArrayPool<byte>.Shared.Rent(m_Texture2D.m_Width * m_Texture2D.m_Height * 4);
            try
            {
                if (converter.DecodeTexture2D(buff))
                {
                    var image = Image.LoadPixelData<Bgra32>(_configuration, buff, m_Texture2D.m_Width, m_Texture2D.m_Height);
                    if (flip)
                    {
                        image.Mutate(x => x.Flip(FlipMode.Vertical));
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
