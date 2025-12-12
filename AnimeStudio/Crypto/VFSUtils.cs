using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp.PixelFormats;

namespace AnimeStudio.Crypto
{
    internal class VFSUtils
    {
        public static bool IsValidHeader(FileReader reader)
        {
            EndianType originalEndian = reader.Endian;
            reader.Endian = EndianType.BigEndian;

            var a = reader.ReadUInt32();
            var b = reader.ReadUInt32();

            var c1 = ((a ^ 0x91A64750) >> 3) ^ ((a ^ 0x91A64750) << 29);
            var c2 = (c1 << 16) ^ 0xD5F9BECC;
            var c3 = (c1 ^ c2) & 0xFFFFFFFF;

            reader.Endian = originalEndian;

            return b == c3;
        }

        public static BundleFile.Header ReadHeader(FileReader reader)
        {
            var Header = new BundleFile.Header
            {
                signature = "UnityFS",
                version = 6,
                unityVersion = "5.x.x",
                unityRevision = "2021.3.3f5",
            };

            // values
            var flags1 = reader.ReadUInt32();
            uint uncompressedBlocksInfoSize1 = reader.ReadUInt16();
            uint compressedBlocksInfoSize1 = reader.ReadUInt16();
            reader.ReadUInt32(); // unknown
            uint uncompressedBlocksInfoSize2 = reader.ReadUInt16();
            var encFlags = reader.ReadUInt32();
            ulong size1 = reader.ReadUInt32();
            uint compressedBlocksInfoSize2 = reader.ReadUInt16();
            var flags2 = reader.ReadUInt32();
            ulong size2 = reader.ReadUInt32();

            // descramble
            uint compressedBlocksInfoSize = BitConcat(16, compressedBlocksInfoSize1 ^ compressedBlocksInfoSize2 ^ 0xE114, compressedBlocksInfoSize2);
            compressedBlocksInfoSize = BitOperations.RotateLeft(compressedBlocksInfoSize, 3) ^ 0x5ADA4ABA;

            uint uncompressedBlocksInfoSize = BitConcat(16, uncompressedBlocksInfoSize1 ^ uncompressedBlocksInfoSize2 ^ 0xE114, uncompressedBlocksInfoSize2);
            uncompressedBlocksInfoSize = BitOperations.RotateLeft(uncompressedBlocksInfoSize, 3) ^ 0x5ADA4ABA;

            ulong size = BitConcat64(32, size1 ^ size2 ^ 0x342D983F, size2);
            size = (BitOperations.RotateLeft(size, 3)) ^ 0x5B4FA98A430D0E62UL;

            encFlags ^= flags2;
            var flags = flags1 ^ flags2 ^ 0x98B806A4;

            //
            Header.compressedBlocksInfoSize = (uint)compressedBlocksInfoSize;
            Header.uncompressedBlocksInfoSize = (uint)uncompressedBlocksInfoSize;
            Header.size = (uint)size;
            Header.flags = (ArchiveFlags)flags;
            Header.encFlags = encFlags;

            return Header;
        }

        public static List<BundleFile.StorageBlock> ReadBlocksInfos(EndianBinaryReader reader)
        {
            var encCount = reader.ReadUInt32() ^ 0xF6825038;

            var low = encCount & 0xFFFF;
            var high = (encCount >> 16) & 0xFFFF;

            var blocksCount = BitConcat(16, low ^ high, low);
            blocksCount = BitOperations.RotateLeft(blocksCount, 3) ^ 0x5F23A227;

            Logger.Verbose($"Blocks Count: {blocksCount}");

            List<BundleFile.StorageBlock> blocks = new();

            for (int i = 0; i < blocksCount; i++)
            {
                ushort encFlags = (ushort)(reader.ReadUInt16() ^ 0xAFEBU);
                var a = reader.ReadUInt16();
                var b = reader.ReadUInt16();
                var c = reader.ReadUInt16();
                var d = reader.ReadUInt16();

                uint a0 = (ushort)(encFlags & 0xFF);
                uint a1 = (ushort)((encFlags >> 8) & 0xFF);

                ushort flags = (ushort)BitConcat(8, a0 ^ a1, a0);
                flags = (ushort)(b ^ RotateLeft16(flags, 3) ^ 0xB7AF);

                var uncompressedSize = BitConcat(16, (uint)(c ^ b ^ 0xE114), b);
                uncompressedSize = BitOperations.RotateLeft(uncompressedSize, 3) ^ 0x5ADA4ABA;

                var compressedSize = BitConcat(16, (uint)(d ^ a ^ 0xE114), a);
                compressedSize = BitOperations.RotateLeft(compressedSize, 3) ^ 0x5ADA4ABA;

                blocks.Add(new BundleFile.StorageBlock
                {
                    flags = (StorageBlockFlags)flags,
                    uncompressedSize = uncompressedSize,
                    compressedSize = compressedSize,
                });

                Logger.Verbose($"Block {i} Info: {blocks[i]}");
            }

            return blocks;
        }

        public static List<BundleFile.Node> ReadDirectoryInfos(EndianBinaryReader reader)
        {
            var encCount = reader.ReadUInt32() ^ 0xA9535111;

            var low = encCount & 0xFFFF;
            var high = (encCount >> 16) & 0xFFFF;

            var nodesCount = BitConcat(16, low ^ high, low);
            nodesCount = BitOperations.RotateLeft(nodesCount, 3) ^ 0xAF807AFC;

            Logger.Verbose($"Nodes Count: {nodesCount}");

            List<BundleFile.Node> nodes = new();

            for (int i = 0; i < nodesCount; i++)
            {
                var a = reader.ReadUInt32();
                var b = reader.ReadUInt32();
                var c = reader.ReadUInt32();

                // read name
                var bytes = new List<byte>();
                int count = 0;
                while (reader.Remaining > 0 && count < 64)
                {
                    var bt = reader.ReadByte();
                    if (bt == 0)
                    {
                        break;
                    }
                    bytes.Add(bt);
                    count++;
                }
                string name = new string(bytes.Select(b => (char)(b ^ 0xAC)).ToArray());
                //

                var d = reader.ReadUInt32() ^ 0xE4A15748;
                var e = reader.ReadUInt32();
                
                var d0 = d & 0xFFFF;
                var d1 = (d >> 16) & 0xFFFF;

                var flags = (uint)BitConcat(16, d1 ^ d0, d0);
                flags = BitOperations.RotateLeft(flags, 3) ^ 0x54D7A374 ^ b;

                ulong offset = BitConcat64(32, c ^ a ^ 0x342D983F, a);
                offset = RotateLeft64(offset, 3) ^ 0x5B4FA98A430D0E62UL;

                ulong size = BitConcat64(32, e ^ b ^ 0x342D983F, b);
                size = RotateLeft64(size, 3) ^ 0x5B4FA98A430D0E62UL;

                nodes.Add(new BundleFile.Node
                {
                    path = name,
                    flags = flags,
                    offset = (long)offset,
                    size = (long)size,
                });

                Logger.Verbose($"Node {i} Info: {nodes[i]}");
            }

            return nodes;
        }

        public static void DecryptBlock(Span<byte> buffer)
        {
            if (buffer.Length <= 256)
            {
                var dec = VFSAES.Decrypt(buffer.ToArray());
                dec.CopyTo(buffer);
            } else
            {
                var encBuf = new List<byte>();
                
                var nRounds = Math.Floor((double)buffer.Length / 16);
                for (int i = 0; i < Math.Min(256, nRounds); i += 1)
                    encBuf.Add(buffer[i * 16]);

                var decBuf = VFSAES.Decrypt(encBuf.ToArray());

                for (int i = 0; i < decBuf.Length; i++)
                    buffer[i * 16] = decBuf[i];
            }
        }

        // managing both 32 and 64 in one function was too annoying lol
        private static uint BitConcat(int bits, params uint[] ns)
        {
            uint mask = (bits == 32) ? 0xFFFFFFFFu : ((1u << bits) - 1u);
            uint res = 0;
            int count = ns.Length;

            for (int i = 0; i < count; i++)
                res |= (ns[i] & mask) << (bits * (count - i - 1));

            return res;
        }

        private static ulong BitConcat64(int bits, params ulong[] ns)
        {
            ulong mask = (bits == 64) ? ulong.MaxValue : ((1UL << bits) - 1UL);
            ulong res = 0;
            int count = ns.Length;

            for (int i = 0; i < count; i++)
                res |= (ns[i] & mask) << (bits * (count - i - 1));

            return res;
        }

        private static ushort RotateLeft16(ushort value, int count) => (ushort)((value << count) | (value >> (16 - count)));

        private static ulong RotateLeft64(ulong value, int count) => (value << count) | (value >> (64 - count));
    }

    public static class VFSAES
    {
        // funky decrypt with cbc encrypt
        public static byte[] Decrypt(byte[] ciphertext)
        {
            byte[] iv = CryptoHelper.VFSAESIV;
            List<byte[]> blocks = new();
            byte[] previous = iv.ToArray();

            foreach (var ct in SplitBlocks(ciphertext))
            {
                // encrypt IV
                byte[] block = EncryptBlock(previous);
                // xor keystream with ciphertext
                byte[] pt = new byte[ct.Length];
                for (int i = 0; i < ct.Length; i++) pt[i] = (byte)(ct[i] ^ block[i]);
                blocks.Add(pt);
                // derive next IV
                byte[] nextIv = new byte[16];
                int count = 0;

                for (int i = 0; i < 16; i++)
                {
                    ulong shiftSrc = 0xDEA1BEEF2AF3BA0EUL >> (count & 0x38);
                    byte temp = (byte)(block[i] ^ (31 * i) ^ (byte)shiftSrc);
                    count += 8;
                    temp = (byte)((((temp >> 5) | (8 * temp)) & 0xFF));
                    temp = CryptoHelper.VFSAESSBox[temp];
                    nextIv[i] = temp;
                }
                // next IV
                previous = nextIv;
            }

            return blocks.SelectMany(b => b).ToArray();
        }

        private static byte[] EncryptBlock(byte[] plaintext)
        {
            List<byte[,]> keyMats = ExpandKey();
            int nRounds = 10;
            var state = BytesToMatrix(plaintext);

            AddRoundKey(state, keyMats[0]);

            for (int r = 1; r < nRounds; r++)
            {
                SubBytes(state);
                ShiftRows(state);
                MixColumns(state);
                AddRoundKey(state, keyMats[r]);
            }

            SubBytes(state);
            ShiftRows(state);
            AddRoundKey(state, keyMats[^1]);

            return MatrixToBytes(state);
        }

        // helpers
        private static IEnumerable<byte[]> SplitBlocks(byte[] msg, int blockSize = 16)
        {
            for (int i = 0; i < msg.Length; i += blockSize)
                yield return msg.Skip(i).Take(blockSize).ToArray();
        }

        private static void SubBytes(List<byte[]> s)
        {
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    s[i][j] = CryptoHelper.VFSAESSBox[s[i][j]];
        }

        private static void ShiftRows(List<byte[]> s)
        {
            (s[0][1], s[1][1], s[2][1], s[3][1]) = (s[1][1], s[2][1], s[3][1], s[0][1]);
            (s[0][2], s[1][2], s[2][2], s[3][2]) = (s[2][2], s[3][2], s[0][2], s[1][2]);
            (s[0][3], s[1][3], s[2][3], s[3][3]) = (s[3][3], s[0][3], s[1][3], s[2][3]);
        }

        private static void AddRoundKey(List<byte[]> s, byte[,] k)
        {
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    s[i][j] ^= k[i, j];
        }

        private static byte XTime(byte a) => (byte)(((a & 0x80) != 0) ? ((a << 1) ^ 0x1B) & 0xFF : (a << 1));

        private static void MixSingleColumn(byte[] a)
        {
            byte t = (byte)(a[0] ^ a[1] ^ a[2] ^ a[3]);
            byte u = a[0];
            a[0] ^= (byte)(t ^ XTime((byte)(a[0] ^ a[1])));
            a[1] ^= (byte)(t ^ XTime((byte)(a[1] ^ a[2])));
            a[2] ^= (byte)(t ^ XTime((byte)(a[2] ^ a[3])));
            a[3] ^= (byte)(t ^ XTime((byte)(a[3] ^ u)));
        }

        private static void MixColumns(List<byte[]> s)
        {
            for (int i = 0; i < 4; i++)
                MixSingleColumn(s[i]);
        }


        // key expansion
        private static List<byte[,]> ExpandKey()
        {
            int nRounds = 10; // 16 bytes keys
            byte[] masterKey = CryptoHelper.VFSAESKey;
            List<byte[]> keyCols = BytesToMatrix(masterKey);
            int iterationSize = masterKey.Length / 4;
            int i = 1;
            byte[] rCon = new byte[] { 0x00, 0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80, 0x1B, 0x36, 0x6C, 0xD8, 0xAB, 0x4D, 0x9A, 0x2F, 0x5E, 0xBC, 0x63, 0xC6, 0x97, 0x35, 0x6A, 0xD4, 0xB3, 0x7D, 0xFA, 0xEF, 0xC5, 0x91, 0x39 };

            while (keyCols.Count < (nRounds + 1) * 4)
            {
                byte[] word = keyCols[^1].ToArray();

                if (keyCols.Count % iterationSize == 0)
                {
                    // rotate
                    var first = word[0];
                    Array.Copy(word, 1, word, 0, word.Length - 1);
                    word[^1] = first;

                    // sbox
                    for (int k = 0; k < 4; k++) word[k] = CryptoHelper.VFSAESSBox[word[k]];

                    word[0] ^= rCon[i];
                    i++;
                }
                else if (masterKey.Length == 32 && keyCols.Count % iterationSize == 4)
                {
                    for (int k = 0; k < 4; k++) word[k] = CryptoHelper.VFSAESSBox[word[k]];
                }

                byte[] prev = keyCols[keyCols.Count - iterationSize];
                for (int k = 0; k < 4; k++) word[k] ^= prev[k];

                keyCols.Add(word);
            }

            var res = new List<byte[,]>();

            for (int x = 0; x < keyCols.Count / 4; x++)
            {
                byte[,] m = new byte[4, 4];

                for (int c = 0; c < 4; c++)
                    for (int r = 0; r < 4; r++)
                        m[c, r] = keyCols[x * 4 + c][r];   // swap indices

                res.Add(m);
            }
            return res;
        }

        private static List<byte[]> BytesToMatrix(byte[] text)
        {
            var rows = new List<byte[]>();
            for (int i = 0; i < text.Length; i += 4)
                rows.Add(new byte[] { text[i], text[i + 1], text[i + 2], text[i + 3] });
            return rows;
        }

        private static byte[] MatrixToBytes(List<byte[]> m)
        {
            byte[] r = new byte[16];
            int idx = 0;
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    r[idx++] = m[i][j];
            return r;
        }
    }
}
