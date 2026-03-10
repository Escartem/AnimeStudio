using System;
using System.Collections.Generic;
using System.Diagnostics;
using static AnimeStudio.CryptoHelper;

namespace AnimeStudio
{
    public class BlbUtils
    {
        internal static byte[] BlbRC4Key;
        internal static byte[] BlbSBox;
        internal static byte[] BlbShiftRow;
        internal static byte[] BlbKey;
        internal static byte[] BlbMul;

        public static void InitKeys(byte[] RC4Key, byte[] SBox, byte[] ShiftRow, byte[] Key, byte[] Mul)
        {
            BlbRC4Key = RC4Key;
            BlbSBox = SBox;
            BlbShiftRow = ShiftRow;
            BlbKey = Key;
            BlbMul = Mul;
        }

        public static void Decrypt(byte[] header, Span<byte> buffer)
        {
            buffer = buffer[..Math.Min(128, buffer.Length)];
            Debug.Assert(header.Length == 0x10, $"Invalid header size: {header.Length} != 16");
            // Initial XOR step
            var count = Math.Min(buffer.Length, header.Length);
            for (int i = 0; i < count; i++)
            {
                buffer[i] ^= header[i];
            }

            if (buffer.Length >= 16)
            {
                // This is a modified AES implementation. Calling the Encrypt() method is intentional.
                BlbAES.Encrypt(buffer.Slice(0, 16).ToArray(), header).CopyTo(buffer);

                if (buffer.Length > 16)
                {
                    // The RC4 call only modified bytes after the first 16, though it uses those early bytes to seed its key schedule.
                    RC4(buffer);
                }

                Descramble(buffer.Slice(0, 16));
            }
        }

        private static int GF256Mul(int a, int b) => (a == 0 || b == 0) ? 0 : CryptoHelper.GF256Exp[(CryptoHelper.GF256Log[a] + CryptoHelper.GF256Log[b]) % 0xFF];

        // Same as in MhyFile.cs, but with other keys
        public static void Descramble(Span<byte> buf)
        {
            byte[] vector = new byte[buf.Length];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < buf.Length; j++)
                {
                    int k = BlbShiftRow[(2 - i) * 0x10 + j];
                    int idx = j % 8;
                    vector[j] = (byte)(BlbKey[idx] ^ BlbSBox[(j % 4 * 0x100) | GF256Mul(BlbMul[idx], buf[k % buf.Length])]);
                }
                vector.AsSpan(0, buf.Length).CopyTo(buf);
            }
        }

        public static void RC4(Span<byte> buf)
        {
            byte[] S = new byte[256];
            BlbRC4Key.CopyTo(S, 0);
            byte[] T = new byte[256];
            int i = 0;
            for (i = 0; i < 256; i += 2)
            {
                T[i] = buf[i & 6];
                T[i + 1] = buf[(i + 1) & 7];
            }

            int j = 0;
            for (i = 0; i < 256; i++)
            {
                j = (j + S[i] + T[i]) % 256;
                ref byte reference = ref S[j];
                ref byte reference2 = ref S[i];
                byte b = S[i];
                byte b2 = S[j];
                reference = b;
                reference2 = b2;
            }
            i = (j = 0);
            for (int iteration = 0; iteration < buf.Length - 0x10; iteration++)
            {
                i = (i + 1) % 256;
                j = (j + S[i]) % 256;
                ref byte reference = ref S[j];
                ref byte reference3 = ref S[i];
                byte b2 = S[i];
                byte b = S[j];
                reference = b2;
                reference3 = b;
                uint K = S[(S[j] + S[i]) % 256];
                switch (buf[(i % 8) + 8] % 3)
                {
                    case 0:
                        buf[iteration + 0x10] ^= Convert.ToByte(K);
                        break;
                    case 1:
                        buf[iteration + 0x10] -= Convert.ToByte(K);
                        break;
                    case 2:
                        buf[iteration + 0x10] += Convert.ToByte(K);
                        break;
                }
            }
        }
    }

    // Simple, thoroughly commented implementation of 128-bit AES / Rijndael using C#
    // Chris Hulbert - chris.hulbert@gmail.com - http://splinter.com.au/blog - http://github.com/chrishulbert/crypto
    // Note: This is not the same as implemented in AES.cs. This implementation uses a custom SBox, and has unique behavior for key expansion, xor round key, and sub bytes.
    internal static class BlbAES
    {
        #region AES Keys
        private static readonly byte[] shift_rows_table = { 0, 5, 10, 15, 4, 9, 14, 3, 8, 13, 2, 7, 12, 1, 6, 11 };
        private static readonly byte[] lookup_g2 = { 0x00, 0x02, 0x04, 0x06, 0x08, 0x0a, 0x0c, 0x0e, 0x10, 0x12, 0x14, 0x16, 0x18, 0x1a, 0x1c, 0x1e, 0x20, 0x22, 0x24, 0x26, 0x28, 0x2a, 0x2c, 0x2e, 0x30, 0x32, 0x34, 0x36, 0x38, 0x3a, 0x3c, 0x3e, 0x40, 0x42, 0x44, 0x46, 0x48, 0x4a, 0x4c, 0x4e, 0x50, 0x52, 0x54, 0x56, 0x58, 0x5a, 0x5c, 0x5e, 0x60, 0x62, 0x64, 0x66, 0x68, 0x6a, 0x6c, 0x6e, 0x70, 0x72, 0x74, 0x76, 0x78, 0x7a, 0x7c, 0x7e, 0x80, 0x82, 0x84, 0x86, 0x88, 0x8a, 0x8c, 0x8e, 0x90, 0x92, 0x94, 0x96, 0x98, 0x9a, 0x9c, 0x9e, 0xa0, 0xa2, 0xa4, 0xa6, 0xa8, 0xaa, 0xac, 0xae, 0xb0, 0xb2, 0xb4, 0xb6, 0xb8, 0xba, 0xbc, 0xbe, 0xc0, 0xc2, 0xc4, 0xc6, 0xc8, 0xca, 0xcc, 0xce, 0xd0, 0xd2, 0xd4, 0xd6, 0xd8, 0xda, 0xdc, 0xde, 0xe0, 0xe2, 0xe4, 0xe6, 0xe8, 0xea, 0xec, 0xee, 0xf0, 0xf2, 0xf4, 0xf6, 0xf8, 0xfa, 0xfc, 0xfe, 0x1b, 0x19, 0x1f, 0x1d, 0x13, 0x11, 0x17, 0x15, 0x0b, 0x09, 0x0f, 0x0d, 0x03, 0x01, 0x07, 0x05, 0x3b, 0x39, 0x3f, 0x3d, 0x33, 0x31, 0x37, 0x35, 0x2b, 0x29, 0x2f, 0x2d, 0x23, 0x21, 0x27, 0x25, 0x5b, 0x59, 0x5f, 0x5d, 0x53, 0x51, 0x57, 0x55, 0x4b, 0x49, 0x4f, 0x4d, 0x43, 0x41, 0x47, 0x45, 0x7b, 0x79, 0x7f, 0x7d, 0x73, 0x71, 0x77, 0x75, 0x6b, 0x69, 0x6f, 0x6d, 0x63, 0x61, 0x67, 0x65, 0x9b, 0x99, 0x9f, 0x9d, 0x93, 0x91, 0x97, 0x95, 0x8b, 0x89, 0x8f, 0x8d, 0x83, 0x81, 0x87, 0x85, 0xbb, 0xb9, 0xbf, 0xbd, 0xb3, 0xb1, 0xb7, 0xb5, 0xab, 0xa9, 0xaf, 0xad, 0xa3, 0xa1, 0xa7, 0xa5, 0xdb, 0xd9, 0xdf, 0xdd, 0xd3, 0xd1, 0xd7, 0xd5, 0xcb, 0xc9, 0xcf, 0xcd, 0xc3, 0xc1, 0xc7, 0xc5, 0xfb, 0xf9, 0xff, 0xfd, 0xf3, 0xf1, 0xf7, 0xf5, 0xeb, 0xe9, 0xef, 0xed, 0xe3, 0xe1, 0xe7, 0xe5 };
        private static readonly byte[] lookup_g3 = { 0x00, 0x03, 0x06, 0x05, 0x0c, 0x0f, 0x0a, 0x09, 0x18, 0x1b, 0x1e, 0x1d, 0x14, 0x17, 0x12, 0x11, 0x30, 0x33, 0x36, 0x35, 0x3c, 0x3f, 0x3a, 0x39, 0x28, 0x2b, 0x2e, 0x2d, 0x24, 0x27, 0x22, 0x21, 0x60, 0x63, 0x66, 0x65, 0x6c, 0x6f, 0x6a, 0x69, 0x78, 0x7b, 0x7e, 0x7d, 0x74, 0x77, 0x72, 0x71, 0x50, 0x53, 0x56, 0x55, 0x5c, 0x5f, 0x5a, 0x59, 0x48, 0x4b, 0x4e, 0x4d, 0x44, 0x47, 0x42, 0x41, 0xc0, 0xc3, 0xc6, 0xc5, 0xcc, 0xcf, 0xca, 0xc9, 0xd8, 0xdb, 0xde, 0xdd, 0xd4, 0xd7, 0xd2, 0xd1, 0xf0, 0xf3, 0xf6, 0xf5, 0xfc, 0xff, 0xfa, 0xf9, 0xe8, 0xeb, 0xee, 0xed, 0xe4, 0xe7, 0xe2, 0xe1, 0xa0, 0xa3, 0xa6, 0xa5, 0xac, 0xaf, 0xaa, 0xa9, 0xb8, 0xbb, 0xbe, 0xbd, 0xb4, 0xb7, 0xb2, 0xb1, 0x90, 0x93, 0x96, 0x95, 0x9c, 0x9f, 0x9a, 0x99, 0x88, 0x8b, 0x8e, 0x8d, 0x84, 0x87, 0x82, 0x81, 0x9b, 0x98, 0x9d, 0x9e, 0x97, 0x94, 0x91, 0x92, 0x83, 0x80, 0x85, 0x86, 0x8f, 0x8c, 0x89, 0x8a, 0xab, 0xa8, 0xad, 0xae, 0xa7, 0xa4, 0xa1, 0xa2, 0xb3, 0xb0, 0xb5, 0xb6, 0xbf, 0xbc, 0xb9, 0xba, 0xfb, 0xf8, 0xfd, 0xfe, 0xf7, 0xf4, 0xf1, 0xf2, 0xe3, 0xe0, 0xe5, 0xe6, 0xef, 0xec, 0xe9, 0xea, 0xcb, 0xc8, 0xcd, 0xce, 0xc7, 0xc4, 0xc1, 0xc2, 0xd3, 0xd0, 0xd5, 0xd6, 0xdf, 0xdc, 0xd9, 0xda, 0x5b, 0x58, 0x5d, 0x5e, 0x57, 0x54, 0x51, 0x52, 0x43, 0x40, 0x45, 0x46, 0x4f, 0x4c, 0x49, 0x4a, 0x6b, 0x68, 0x6d, 0x6e, 0x67, 0x64, 0x61, 0x62, 0x73, 0x70, 0x75, 0x76, 0x7f, 0x7c, 0x79, 0x7a, 0x3b, 0x38, 0x3d, 0x3e, 0x37, 0x34, 0x31, 0x32, 0x23, 0x20, 0x25, 0x26, 0x2f, 0x2c, 0x29, 0x2a, 0x0b, 0x08, 0x0d, 0x0e, 0x07, 0x04, 0x01, 0x02, 0x13, 0x10, 0x15, 0x16, 0x1f, 0x1c, 0x19, 0x1a };
        private static readonly byte[] power_schedule = { 0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80, 0x1b, 0x36 };
        #endregion

        private static readonly byte[] BlbAESSBox = CryptoHelper.Blb3AESSBox;
        private static readonly byte[] BlbAESShift = CryptoHelper.Blb3AESShift;

        // Note: completely bespoke implementation
        public static byte[] Expand(byte[] key)
        {
            byte[] keys = new byte[176];

            for (int i = 0; i < 16; i++)
            {
                keys[i] = key[BlbAESShift[i]];
            }

            int offset = 0x1f;
            for (int round = 0; round < 10; round++)
            {
                byte a = BlbAESSBox[keys[offset - 0x14]];
                byte b = BlbAESSBox[keys[offset - 0x10]];
                byte c = (byte)(BlbAESSBox[keys[offset - 0x18]] ^ keys[offset - 0x18] ^ power_schedule[round] ^ keys[offset - 0x1f]);
                byte d = BlbAESSBox[keys[offset - 0x1c]];
                byte temp = 0;

                keys[offset - 0xf] = c;
                temp = (byte)(a ^ keys[offset - 0x14] ^ keys[offset - 0x1b]);
                keys[offset - 0xb] = temp;
                a = (byte)(b ^ keys[offset - 0x10] ^ keys[offset - 0x17]);
                keys[offset - 7] = a;
                b = (byte)(d ^ keys[offset - 0x1c] ^ keys[offset - 0x13]);
                keys[offset - 3] = b;
                c = (byte)(c ^ keys[offset - 0x1e]);
                keys[offset - 0xe] = c;
                temp = (byte)(temp ^ keys[offset - 0x1a]);
                keys[offset - 10] = temp;
                a = (byte)(a ^ keys[offset - 0x16]);
                keys[offset - 6] = a;
                b = (byte)(b ^ keys[offset - 0x12]);
                keys[offset - 2] = b;
                c = (byte)(c ^ keys[offset - 0x1d]);
                keys[offset - 0xd] = c;
                temp = (byte)(temp ^ keys[offset - 0x19]);
                keys[offset - 9] = temp;
                a = (byte)(a ^ keys[offset - 0x15]);
                keys[offset - 5] = a;
                b = (byte)(b ^ keys[offset - 0x11]);
                keys[offset - 1] = b;
                keys[offset - 0xc] = (byte)(c ^ keys[offset - 0x1c]);
                keys[offset - 8] = (byte)(temp ^ keys[offset - 0x18]);
                keys[offset - 4] = (byte)(a ^ keys[offset - 0x14]);
                keys[offset] = (byte)(b ^ keys[offset - 0x10]);

                offset += 0x10;
            }

            return keys;
        }

        public static byte[] Encrypt(byte[] m, byte[] k)
        {
            // Key expansion
            byte[] keys = Expand(k);

            // First Round
            byte[] c = new byte[16];
            Array.Copy(m, c, 16);
            XorRoundKey(c, keys, 0);

            // Middle rounds
            for (int i = 0; i < 9; i++)
            {
                SubBytes(c);
                ShiftRows(c);
                MixCols(c);
                XorRoundKey(c, keys, i + 1);
            }

            // Final Round
            SubBytes(c);
            ShiftRows(c);
            XorRoundKey(c, keys, 10);

            return c;
        }

        private static void SubBytes(byte[] a)
        {
            for (int i = 0; i < a.Length; i++)
                // Note: we xor the data with the substitution. This is different from the ref impl
                a[i] ^= BlbAESSBox[a[i]];
        }

        private static void XorRoundKey(byte[] state, byte[] keys, int round)
        {
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    state[i * 4 + j] ^= keys[i + j * 4 + round * 16];
                }
            }
        }

        private static void ShiftRows(byte[] state)
        {
            byte[] temp = new byte[16];
            Array.Copy(state, temp, 16);
            for (int i = 0; i < 16; i++)
                state[i] = temp[shift_rows_table[i]];
        }

        private static void MixCol(byte[] state, int off)
        {
            byte a0 = state[off + 0];
            byte a1 = state[off + 1];
            byte a2 = state[off + 2];
            byte a3 = state[off + 3];
            state[off + 0] = (byte)(lookup_g2[a0] ^ lookup_g3[a1] ^ a2 ^ a3);
            state[off + 1] = (byte)(lookup_g2[a1] ^ lookup_g3[a2] ^ a3 ^ a0);
            state[off + 2] = (byte)(lookup_g2[a2] ^ lookup_g3[a3] ^ a0 ^ a1);
            state[off + 3] = (byte)(lookup_g2[a3] ^ lookup_g3[a0] ^ a1 ^ a2);
        }

        private static void MixCols(byte[] state)
        {
            MixCol(state, 0);
            MixCol(state, 4);
            MixCol(state, 8);
            MixCol(state, 12);
        }
    }
}