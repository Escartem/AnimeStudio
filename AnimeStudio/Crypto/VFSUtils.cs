using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AnimeStudio
{
    internal class VFSUtils
    {
        public static bool IsValidHeader(FileReader reader, GameType game)
        {
            int originalPosition = (int)reader.Position;
            EndianType originalEndian = reader.Endian;
            reader.Endian = EndianType.BigEndian;

            var a = reader.ReadUInt32();
            var b = reader.ReadUInt32();

            uint c1 = 0, c2 = 0, c3 = 0;

            switch (game)
            {
                case GameType.ArknightsEndfield:
                    return false;
                case GameType.ArknightsEndfieldCB3:
                    c1 = ((a ^ 0x91A64750) >> 3) ^ ((a ^ 0x91A64750) << 29);
                    c2 = (c1 << 16) ^ 0xD5F9BECC;
                    c3 = (c1 ^ c2) & 0xFFFFFFFF;
                    break;
            }

            reader.Endian = originalEndian;
            reader.Position = (uint)originalPosition;

            return b == c3;
        }

        public static BundleFile.Header ReadHeader(FileReader reader, GameType game)
        {
            var Header = new BundleFile.Header
            {
                signature = "UnityFS",
                version = 6,
                unityVersion = "5.x.x",
                unityRevision = "2021.3.3f5",
            };

            // values
            ushort compressedBlocksInfoSize1 = 0, compressedBlocksInfoSize2 = 0;
            ushort uncompressedBlocksInfoSize1 = 0, uncompressedBlocksInfoSize2 = 0;
            uint flags1 = 0, flags2 = 0;
            uint encFlags = 0;
            uint size1 = 0, size2 = 0;

            switch (game)
            {
                case GameType.ArknightsEndfield:
                    break;
                case GameType.ArknightsEndfieldCB3:
                    flags1 = reader.ReadUInt32();
                    uncompressedBlocksInfoSize1 = reader.ReadUInt16();
                    compressedBlocksInfoSize1 = reader.ReadUInt16();
                    reader.ReadUInt32(); // unknown
                    uncompressedBlocksInfoSize2 = reader.ReadUInt16();
                    encFlags = reader.ReadUInt32();
                    size1 = reader.ReadUInt32();
                    compressedBlocksInfoSize2 = reader.ReadUInt16();
                    flags2 = reader.ReadUInt32();
                    size2 = reader.ReadUInt32();
                    break;
            }

            // descramble
            uint compressedBlocksInfoSize = 0, uncompressedBlocksInfoSize = 0, flags = 0;
            ulong size = 0;

            switch (game)
            {
                case GameType.ArknightsEndfield:


                    break;
                case GameType.ArknightsEndfieldCB3:
                    compressedBlocksInfoSize = BitConcat((ushort)(compressedBlocksInfoSize1 ^ compressedBlocksInfoSize2 ^ 0xE114), compressedBlocksInfoSize2);
                    compressedBlocksInfoSize = BitOperations.RotateLeft(compressedBlocksInfoSize, 3) ^ 0x5ADA4ABA;

                    uncompressedBlocksInfoSize = BitConcat((ushort)(uncompressedBlocksInfoSize1 ^ uncompressedBlocksInfoSize2 ^ 0xE114), uncompressedBlocksInfoSize2);
                    uncompressedBlocksInfoSize = BitOperations.RotateLeft(uncompressedBlocksInfoSize, 3) ^ 0x5ADA4ABA;

                    size = BitConcat(size1 ^ size2 ^ 0x342D983F, size2);
                    size = (BitOperations.RotateLeft(size, 3)) ^ 0x5B4FA98A430D0E62UL;

                    encFlags ^= flags2;
                    flags = flags1 ^ flags2 ^ 0x98B806A4;
                    break;
            }

            //
            Header.compressedBlocksInfoSize = compressedBlocksInfoSize;
            Header.uncompressedBlocksInfoSize = uncompressedBlocksInfoSize;
            Header.size = (uint)size;
            Header.flags = (ArchiveFlags)flags;
            Header.encFlags = encFlags;

            return Header;
        }

        public static List<BundleFile.StorageBlock> ReadBlocksInfos(EndianBinaryReader reader, GameType game)
        {
            uint encCount = 0;

            switch (game)
            {
                case GameType.ArknightsEndfield:
                    break;
                case GameType.ArknightsEndfieldCB3:
                    encCount = reader.ReadUInt32() ^ 0xF6825038;
                    break;
            }

            var low = (ushort)encCount;
            var high = (ushort)(encCount >> 16);

            var blocksCount = BitConcat((ushort)(low ^ high), low);
            switch (game)
            {
                case GameType.ArknightsEndfield:
                    break;
                case GameType.ArknightsEndfieldCB3:
                    blocksCount = BitOperations.RotateLeft(blocksCount, 3) ^ 0x5F23A227;
                    break;
            }

            Logger.Verbose($"Blocks Count: {blocksCount}");

            List<BundleFile.StorageBlock> blocks = new();

            for (int i = 0; i < blocksCount; i++)
            {
                ushort a = 0, b = 0, c = 0, d = 0, encFlags = 0;

                switch (game)
                {
                    case GameType.ArknightsEndfield:
                        break;
                    case GameType.ArknightsEndfieldCB3:
                        encFlags = (ushort)(reader.ReadUInt16() ^ 0xAFEBU);
                        a = reader.ReadUInt16();
                        b = reader.ReadUInt16();
                        c = reader.ReadUInt16();
                        d = reader.ReadUInt16();
                        break;
                }

                var a0 = (byte)encFlags;
                var a1 = (byte)(encFlags >> 8);

                ushort flags = 0;
                uint uncompressedSize = 0, compressedSize = 0;

                switch (game)
                {
                    case GameType.ArknightsEndfield:
                        break;
                    case GameType.ArknightsEndfieldCB3:
                        flags = BitConcat((byte)(a0 ^ a1), a0);
                        flags = (ushort)(b ^ RotateLeft(flags, 3) ^ 0xB7AF);

                        uncompressedSize = BitConcat((ushort)(c ^ b ^ 0xE114), b);
                        uncompressedSize = BitOperations.RotateLeft(uncompressedSize, 3) ^ 0x5ADA4ABA;

                        compressedSize = BitConcat((ushort)(d ^ a ^ 0xE114), a);
                        compressedSize = BitOperations.RotateLeft(compressedSize, 3) ^ 0x5ADA4ABA;
                        break;
                }

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

        public static List<BundleFile.Node> ReadDirectoryInfos(EndianBinaryReader reader, GameType game)
        {
            uint encCount = 0;

            switch (game)
            {
                case GameType.ArknightsEndfield:
                    break;
                case GameType.ArknightsEndfieldCB3:
                    encCount = reader.ReadUInt32() ^ 0xA9535111;
                    break;
            }

            var low = (ushort)encCount;
            var high = (ushort)(encCount >> 16);

            var nodesCount = BitConcat((ushort)(low ^ high), low);
            switch (game)
            {
                case GameType.ArknightsEndfield:
                    break;
                case GameType.ArknightsEndfieldCB3:
                    nodesCount = BitOperations.RotateLeft(nodesCount, 3) ^ 0xAF807AFC;
                    break;
            }

            Logger.Verbose($"Nodes Count: {nodesCount}");

            List<BundleFile.Node> nodes = new();

            for (int i = 0; i < nodesCount; i++)
            {
                uint a = 0, b = 0, c = 0, d = 0, e = 0;

                switch (game)
                {
                    case GameType.ArknightsEndfield:
                        break;
                    case GameType.ArknightsEndfieldCB3:
                        a = reader.ReadUInt32();
                        b = reader.ReadUInt32();
                        c = reader.ReadUInt32();
                        break;
                }

                // read name
                var bytes = new List<byte>();
                while (reader.Remaining > 0 && bytes.Count < 64)
                {
                    var bt = reader.ReadByte();
                    if (bt == 0)
                        break;
                    bytes.Add(bt);
                }

                string name = "";

                switch (game)
                {
                    case GameType.ArknightsEndfield:
                        break;
                    case GameType.ArknightsEndfieldCB3:
                        name = new string(bytes.Select(b => (char)(b ^ 0xAC)).ToArray());
                        break;
                }
                //

                switch (game)
                {
                    case GameType.ArknightsEndfield:
                        break;
                    case GameType.ArknightsEndfieldCB3:
                        d = reader.ReadUInt32() ^ 0xE4A15748;
                        e = reader.ReadUInt32();
                        break;
                }

                uint flags = 0;
                ulong offset = 0, size = 0;

                switch (game)
                {
                    case GameType.ArknightsEndfield:
                        break;
                    case GameType.ArknightsEndfieldCB3:
                        var d0 = (ushort)d;
                        var d1 = (ushort)(d >> 16);

                        flags = BitConcat((ushort)(d1 ^ d0), d0);
                        flags = BitOperations.RotateLeft(flags, 3) ^ 0x54D7A374 ^ b;

                        offset = BitConcat(c ^ a ^ 0x342D983F, a);
                        offset = BitOperations.RotateLeft(offset, 3) ^ 0x5B4FA98A430D0E62UL;

                        size = BitConcat(e ^ b ^ 0x342D983F, b);
                        size = BitOperations.RotateLeft(size, 3) ^ 0x5B4FA98A430D0E62UL;
                        break;
                }

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

        public static void DecryptBlock(Span<byte> buffer, GameType game)
        {
            switch (game)
            {
                case GameType.ArknightsEndfield:
                    break;
                case GameType.ArknightsEndfieldCB3:
                    VFSAES.InitKeys(CryptoHelper.VFSCB3AESSBox, CryptoHelper.VFSCB3AESKey, CryptoHelper.VFSCB3AESIV, 0xDEA1BEEF2AF3BA0EUL);
                    break;
            }

            if (buffer.Length <= 256)
            {
                var dec = VFSAES.Decrypt(buffer.ToArray());
                dec.CopyTo(buffer);
            } else
            {
                var numBlocksFloor = buffer.Length / 16;
                var step = (int)(256 / numBlocksFloor);

                if (numBlocksFloor > 256)
                    step = 1;

                Span<byte> decBuffer = new byte[256];

                for (int i = 0; i < Math.Min(numBlocksFloor, 256); i++)
                    buffer.Slice(i * 16, step).CopyTo(decBuffer.Slice((int)(i * step), step));

                decBuffer = VFSAES.Decrypt(decBuffer.ToArray());

                for (int i = 0; i < Math.Min(numBlocksFloor, 256); i++)
                    decBuffer.Slice(i * step, step).CopyTo(buffer.Slice((int)(i * 16), step));
            }
        }

        private static ushort BitConcat(byte a, byte b) => (ushort)(((ushort)a << 8) | (ushort)b);
        private static uint BitConcat(ushort a, ushort b) => ((uint)a << 16) | (uint)b;
        private static ulong BitConcat(uint a, uint b) => ((ulong)a << 32) | (ulong)b;
        private static ushort RotateLeft(ushort value, int count) => (ushort)((value << count) | (value >> (16 - count)));
    }

    public static class VFSAES
    {
        // funky decrypt with cbc encrypt
        private static byte[] SBox;
        private static byte[] Key;
        private static byte[] IV;
        private static ulong XORKey;

        public static void InitKeys(byte[] VFSSBox, byte[] VFSKey, byte[] VFSIV, ulong VFSXORKey)
        {
            SBox = VFSSBox;
            Key = VFSKey;
            IV = VFSIV;
            XORKey = VFSXORKey;
        }

        public static byte[] Decrypt(byte[] ciphertext)
        {
            byte[] iv = IV;
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
                    ulong shiftSrc = XORKey >> (count & 0x38);
                    byte temp = (byte)(block[i] ^ (31 * i) ^ (byte)shiftSrc);
                    count += 8;
                    temp = (byte)((((temp >> 5) | (8 * temp)) & 0xFF));
                    temp = SBox[temp];
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
                    s[i][j] = SBox[s[i][j]];
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
            byte[] masterKey = Key;
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
                    for (int k = 0; k < 4; k++) word[k] = SBox[word[k]];

                    word[0] ^= rCon[i];
                    i++;
                }
                else if (masterKey.Length == 32 && keyCols.Count % iterationSize == 4)
                {
                    for (int k = 0; k < 4; k++) word[k] = SBox[word[k]];
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
