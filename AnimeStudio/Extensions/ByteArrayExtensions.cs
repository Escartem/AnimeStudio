using System;
using System.Text;

namespace AnimeStudio
{
    public static class ByteArrayExtensions
    {
        public static bool IsNullOrEmpty<T>(this T[] array) => array == null || array.Length == 0;
        public static byte[] ToUInt4Array(this byte[] source) => ToUInt4Array(source, 0, source.Length);
        public static byte[] ToUInt4Array(this byte[] source, int offset, int size)
        {
            var buffer = new byte[size * 2];
            for (var i = 0; i < size; i++)
            {
                var idx = i * 2;
                buffer[idx] = (byte)(source[offset + i] >> 4);
                buffer[idx + 1] = (byte)(source[offset + i] & 0xF);
            }
            return buffer;
        }
        public static byte[] ToUInt8Array(this byte[] source, int offset, int size)
        {
            var buffer = new byte[(size + 1) / 2];
            for (var i = 0; i < size; i++)
            {
                var idx = i / 2;
                if (i % 2 == 0)
                {
                    buffer[idx] = (byte)(source[offset + i] << 4);
                }
                else
                {
                    buffer[idx] |= source[offset + i];
                }
            }
            return buffer;
        }
        public static int Search(this byte[] src, string value, int offset = 0) => Search(src.AsSpan(), Encoding.UTF8.GetBytes(value), offset);
        public static int Search(this Span<byte> src, byte[] pattern, int offset = 0)
        {
            int maxFirstCharSlot = src.Length - pattern.Length + 1;
            for (int i = offset; i < maxFirstCharSlot; i++)
            {
                if (src[i] != pattern[0])
                    continue;

                for (int j = pattern.Length - 1; j >= 1; j--)
                {
                    if (src[i + j] != pattern[j]) break;
                    if (j == 1) return i;
                }
            }
            return -1;
        }
    }
}
