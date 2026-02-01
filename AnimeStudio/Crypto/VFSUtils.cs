using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace AnimeStudio;

internal class VFSUtils
{

    public static bool IsValidHeader(FileReader reader, GameType game)
    {
        var originalPosition = (int) reader.Position;
        var originalEndian   = reader.Endian;
        reader.Endian = EndianType.BigEndian;

        var isValid = false;

        switch (game)
        {
            case GameType.ArknightsEndfield:
            {
                var v1 = reader.ReadUInt32();
                var v2 = reader.ReadUInt32();

                var v6 = (v1 ^ 0x4A92F0CD) | (((ulong) v1 ^ 0x4A92F0CD) << 32);

                // 0xD8B1E637
                var magic = (uint) (v2 ^ ((v6 >> 14) << 16) ^ (v6 >> 14));

                isValid = magic == 0xD8B1E637;
            }

                break;

            case GameType.ArknightsEndfieldCB3:
            {
                var a = reader.ReadUInt32();
                var b = reader.ReadUInt32();

                uint c1 = 0, c2 = 0, c3 = 0;
                c1 = ((a ^ 0x91A64750) >> 3)  ^ ((a ^ 0x91A64750) << 29);
                c2 = (c1               << 16) ^ 0xD5F9BECC;
                c3 = (c1 ^ c2) & 0xFFFFFFFF;

                isValid = b == c3;
            }

                break;
        }

        reader.Endian   = originalEndian;
        reader.Position = (uint) originalPosition;

        return isValid;
    }

    public static BundleFile.Header ReadHeader(FileReader reader, GameType game)
    {
        var Header = new BundleFile.Header
        {
            signature     = "UnityFS",
            unityVersion  = "5.x.x",
            unityRevision = "2021.3.3f5"
        };

        // descramble
        uint  compressedBlocksInfoSize = 0, uncompressedBlocksInfoSize = 0, flags = 0;
        ulong size                     = 0;

        uint version = 0;

        switch (game)
        {
            case GameType.ArknightsEndfield:
            {
                Header.unityRevision = "2021.3.34f5";

                var v23 = reader.ReadUInt16();
                var v19 = reader.ReadUInt32();

                // I feel like this should be header.Version
                version = reader.ReadUInt32() ^ v19;


                var v21 = reader.ReadUInt32();

                flags = reader.ReadUInt32() ^ v19 ^ 0xA7F49310;


                var v26 = reader.ReadUInt16();
                var v20 = reader.ReadUInt32();
                var v25 = reader.ReadUInt16();
                var v22 = reader.ReadUInt32();

                var v24 = reader.ReadUInt16();

                var v18 = reader.ReadByte();

                compressedBlocksInfoSize   = BitOperations.RotateRight((uint) ((v23 ^ 0xD3E8 | ((v24 ^ v23) << 16)) ^ 0xA1210000), 18) ^ 0xC3B924EE;
                uncompressedBlocksInfoSize = BitOperations.RotateRight((uint) ((v25 ^ 0xD3E8 | ((v26 ^ v25) << 16)) ^ 0xA1210000), 18) ^ 0xC3B924EE;

                Debug.Assert(compressedBlocksInfoSize <= uncompressedBlocksInfoSize);

                size = BitOperations.RotateRight((v21 ^ 0xF4AB3198 | ((ulong) (v22 ^ v21) << 32)) ^ 0xDAD7684800000000L, 50) ^ 0xA4F19C3D8BE76520L;
            }

                break;

            case GameType.ArknightsEndfieldCB3:
            {
                // values
                ushort compressedBlocksInfoSize1   = 0, compressedBlocksInfoSize2   = 0;
                ushort uncompressedBlocksInfoSize1 = 0, uncompressedBlocksInfoSize2 = 0;
                uint   flags1                      = 0, flags2                      = 0;

                uint size1 = 0, size2 = 0;


                flags1                      = reader.ReadUInt32();
                uncompressedBlocksInfoSize1 = reader.ReadUInt16();
                compressedBlocksInfoSize1   = reader.ReadUInt16();
                reader.ReadUInt32(); // unknown
                uncompressedBlocksInfoSize2 = reader.ReadUInt16();
                version                     = reader.ReadUInt32();
                size1                       = reader.ReadUInt32();
                compressedBlocksInfoSize2   = reader.ReadUInt16();
                flags2                      = reader.ReadUInt32();
                size2                       = reader.ReadUInt32();


                compressedBlocksInfoSize = BitConcat((ushort) (compressedBlocksInfoSize1 ^ compressedBlocksInfoSize2 ^ 0xE114), compressedBlocksInfoSize2);
                compressedBlocksInfoSize = BitOperations.RotateLeft(compressedBlocksInfoSize, 3) ^ 0x5ADA4ABA;

                uncompressedBlocksInfoSize = BitConcat((ushort) (uncompressedBlocksInfoSize1 ^ uncompressedBlocksInfoSize2 ^ 0xE114), uncompressedBlocksInfoSize2);
                uncompressedBlocksInfoSize = BitOperations.RotateLeft(uncompressedBlocksInfoSize, 3) ^ 0x5ADA4ABA;

                size = BitConcat(size1 ^ size2 ^ 0x342D983F, size2);
                size = BitOperations.RotateLeft(size, 3) ^ 0x5B4FA98A430D0E62UL;

                version ^= flags2;
                flags   =  flags1 ^ flags2 ^ 0x98B806A4;
            }

                break;
        }

        //
        Header.compressedBlocksInfoSize   = compressedBlocksInfoSize;
        Header.uncompressedBlocksInfoSize = uncompressedBlocksInfoSize;
        Header.size                       = (uint) size;
        Header.flags                      = (ArchiveFlags) flags;
        Header.version                    = version;

        return Header;
    }

    public static List <BundleFile.StorageBlock> ReadBlocksInfos(EndianBinaryReader reader, GameType game)
    {
        var blocksCount = 0u;


        switch (game)
        {
            case GameType.ArknightsEndfield:
            {
                var v7 = reader.ReadUInt32() ^ 0x23F77B8A;
                blocksCount = BitOperations.RotateRight((v7 & 0xFFFF0000 ^ (v7 << 16)) & 0xFFFF0000 | (ushort) v7, 18) ^ 0x91CE0A4F;
            }

                break;

            case GameType.ArknightsEndfieldCB3:
            {
                var encCount = reader.ReadUInt32() ^ 0xF6825038;
                var low      = (ushort) encCount;
                var high     = (ushort) (encCount >> 16);

                blocksCount = BitConcat((ushort) (low ^ high), low);
                blocksCount = BitOperations.RotateLeft(blocksCount, 3) ^ 0x5F23A227;
            }

                break;
        }


        Logger.Verbose($"Blocks Count: {blocksCount}");

        List <BundleFile.StorageBlock> blocks = new();

        for (var i = 0; i < blocksCount; i++)
        {
            ushort flags            = 0;
            uint   uncompressedSize = 0, compressedSize = 0;
            switch (game)
            {
                case GameType.ArknightsEndfield:
                {
                    var v22 = reader.ReadUInt16();
                    var v20 = reader.ReadUInt16();
                    var v23 = reader.ReadUInt16();
                    var x   = reader.ReadUInt16();
                    var v21 = reader.ReadUInt16();

                    var v15 = (ushort) (((x ^ 0x9CD6) & 0xFF00 ^ ((x ^ 0x9CD6) << 8)) & 0xFF00 | (byte) x ^ 0xD6);

                    var v16 = (ushort) ((v15 >> 2) | (v15 << 14));

                    flags = (ushort) (v23 ^ 0x523F ^ v16);

                    compressedSize   = BitOperations.RotateRight(((uint) (v21 ^ 0xD3E8) | ((uint) (v20 ^ v21) << 16)) ^ 0xA1210000, 18) ^ 0xC3B924EE;
                    uncompressedSize = BitOperations.RotateRight(((uint) (v23 ^ 0xD3E8) | ((uint) (v22 ^ v23) << 16)) ^ 0xA1210000, 18) ^ 0xC3B924EE;
                }

                    break;

                case GameType.ArknightsEndfieldCB3:
                {
                    ushort a = 0, b = 0, c = 0, d = 0, encFlags = 0;

                    encFlags = (ushort) (reader.ReadUInt16() ^ 0xAFEBU);
                    a        = reader.ReadUInt16();
                    b        = reader.ReadUInt16();
                    c        = reader.ReadUInt16();
                    d        = reader.ReadUInt16();

                    var a0 = (byte) encFlags;
                    var a1 = (byte) (encFlags >> 8);

                    flags = BitConcat((byte) (a0 ^ a1), a0);
                    flags = (ushort) (b ^ RotateLeft(flags, 3) ^ 0xB7AF);

                    uncompressedSize = BitConcat((ushort) (c ^ b ^ 0xE114), b);
                    uncompressedSize = BitOperations.RotateLeft(uncompressedSize, 3) ^ 0x5ADA4ABA;

                    compressedSize = BitConcat((ushort) (d ^ a ^ 0xE114), a);
                    compressedSize = BitOperations.RotateLeft(compressedSize, 3) ^ 0x5ADA4ABA;
                }

                    break;
            }


            blocks.Add(new BundleFile.StorageBlock
            {
                flags            = (StorageBlockFlags) flags,
                uncompressedSize = uncompressedSize,
                compressedSize   = compressedSize
            });

            Logger.Verbose($"Block {i} Info: {blocks[i]}");
        }

        return blocks;
    }

    private static byte[] ReadNullTerminatedBytes(EndianBinaryReader reader, int chunkSize = 64)
    {
        var bytes = new List <byte>();

        while (true)
        {
            var chunk = reader.ReadBytes(chunkSize);

            if (chunk.Length == 0) break;

            var nullIndex = Array.IndexOf(chunk, (byte) 0);
            if (nullIndex >= 0)
            {
                for (var i = 0; i < nullIndex; i++)
                    bytes.Add(chunk[i]);

                var unusedBytes = chunk.Length - nullIndex - 1;
                if (unusedBytes > 0)
                    reader.BaseStream.Seek(-unusedBytes, SeekOrigin.Current);

                break;
            }

            bytes.AddRange(chunk);
        }

        return bytes.ToArray();
    }

    private static byte[] DecryptPath(byte[] encrypted)
    {
        var decrypted = new byte[encrypted.Length];

        for (var i = 0; i < encrypted.Length; i++)
        {
            var key = (byte) (0x97 ^ (byte) (i + i / 255));
            decrypted[i] = (byte) (encrypted[i] ^ key);
        }

        return decrypted;
    }


    public static List <BundleFile.Node> ReadDirectoryInfos(EndianBinaryReader reader, GameType game)
    {
        var nodesCount = 0u;
        switch (game)
        {
            case GameType.ArknightsEndfield:
            {
                var v67 = reader.ReadInt32() ^ 0x6B0AE55D;
                nodesCount = BitOperations.RotateRight((uint) ((v67 & 0xFFFF0000 ^ (v67 << 16)) & 0xFFFF0000 | (ushort) v67), 18) ^ 0xE4C1D9F2;
            }

                break;

            case GameType.ArknightsEndfieldCB3:
            {
                uint encCount = 0;

                encCount = reader.ReadUInt32() ^ 0xA9535111;

                var low  = (ushort) encCount;
                var high = (ushort) (encCount >> 16);

                nodesCount = BitConcat((ushort) (low ^ high), low);

                nodesCount = BitOperations.RotateLeft(nodesCount, 3) ^ 0xAF807AFC;
            }

                break;
        }


        Logger.Verbose($"Nodes Count: {nodesCount}");

        List <BundleFile.Node> nodes = new();

        for (var i = 0; i < nodesCount; i++)
        {
            var   name   = "";
            uint  flags  = 0;
            ulong offset = 0, size = 0;

            switch (game)
            {
                case GameType.ArknightsEndfield:
                {
                    var v62 = reader.ReadUInt32();
                    var v63 = reader.ReadUInt32();
                    var v66 = reader.ReadUInt32();
                    var v64 = reader.ReadUInt32();


                    name = Encoding.UTF8.GetString(DecryptPath(ReadNullTerminatedBytes(reader)));

                    var v65 = reader.ReadUInt32();

                    flags = v63 ^ BitOperations.RotateRight((uint) (((v62 ^ 0x8E06A9F8) & 0xFFFF0000 ^ ((v62 ^ 0x8E06A9F8) << 16)) & 0xFFFF0000 | (ushort) v62 ^ 0xA9F8), 18) ^ 0xF13927C4;

                    offset = BitOperations.RotateRight((v66 ^ 0xF4AB3198 | ((ulong) (v64 ^ v66) << 32)) ^ 0xDAD7684800000000L, 50) ^ 0xA4F19C3D8BE76520L;

                    size = BitOperations.RotateRight((v65 ^ 0xF4AB3198 | ((ulong) (v65 ^ v63) << 32)) ^ 0xDAD7684800000000L, 50) ^ 0xA4F19C3D8BE76520L;
                }

                    break;

                case GameType.ArknightsEndfieldCB3:
                {
                    uint a = 0, b = 0, c = 0, d = 0, e = 0;

                    a = reader.ReadUInt32();
                    b = reader.ReadUInt32();
                    c = reader.ReadUInt32();

                    // read name
                    var bytes = new List <byte>();
                    while (reader.Remaining > 0 && bytes.Count < 64)
                    {
                        var bt = reader.ReadByte();

                        if (bt == 0)
                            break;

                        bytes.Add(bt);
                    }

                    name = new string(bytes.Select(b => (char) (b ^ 0xAC)).ToArray());

                    d = reader.ReadUInt32() ^ 0xE4A15748;
                    e = reader.ReadUInt32();

                    var d0 = (ushort) d;
                    var d1 = (ushort) (d >> 16);

                    flags = BitConcat((ushort) (d1 ^ d0), d0);
                    flags = BitOperations.RotateLeft(flags, 3) ^ 0x54D7A374 ^ b;

                    offset = BitConcat(c ^ a ^ 0x342D983F, a);
                    offset = BitOperations.RotateLeft(offset, 3) ^ 0x5B4FA98A430D0E62UL;

                    size = BitConcat(e ^ b ^ 0x342D983F, b);
                    size = BitOperations.RotateLeft(size, 3) ^ 0x5B4FA98A430D0E62UL;
                }


                    break;
            }


            nodes.Add(new BundleFile.Node
            {
                path   = name,
                flags  = flags,
                offset = (long) offset,
                size   = (long) size
            });

            Logger.Verbose($"Node {i} Info: {nodes[i]}");
        }

        return nodes;
    }

    private static void DecryptWithAES(Span <byte> buffer, GameType game)
    {
        byte[] dec = null;
        switch (game)
        {
            case GameType.ArknightsEndfield:
                VFSAES_1_0_14.Decrypt(buffer.ToArray(), out dec);

                break;


            case GameType.ArknightsEndfieldCB3:
                dec = VFSAES.Decrypt(buffer.ToArray());

                break;
        }

        dec.CopyTo(buffer);
    }

    public static void DecryptBlock(Span <byte> buffer, GameType game)
    {
        switch (game)
        {
            case GameType.ArknightsEndfield:
                break;


            case GameType.ArknightsEndfieldCB3:
                VFSAES.InitKeys(CryptoHelper.VFSCB3AESSBox,
                                CryptoHelper.VFSCB3AESKey,
                                CryptoHelper.VFSCB3AESIV,
                                0xDEA1BEEF2AF3BA0EUL);

                break;
        }

        if (buffer.Length <= 256)
        {
            DecryptWithAES(buffer, game);
        }
        else
        {
            var numBlocksFloor = buffer.Length / 16;
            var step           = 256           / numBlocksFloor;

            if (numBlocksFloor > 256)
                step = 1;

            Span <byte> decBuffer = new byte[256];

            for (var i = 0; i < Math.Min(numBlocksFloor, 256); i++)
                buffer.Slice(i * 16, step).CopyTo(decBuffer.Slice(i * step, step));

            DecryptWithAES(decBuffer, game);

            for (var i = 0; i < Math.Min(numBlocksFloor, 256); i++)
                decBuffer.Slice(i * step, step).CopyTo(buffer.Slice(i * 16, step));
        }
    }


    private static ushort BitConcat(byte a, byte b) => (ushort) ((a << 8) | b);

    private static uint BitConcat(ushort a, ushort b) => ((uint) a << 16) | b;

    private static ulong BitConcat(uint a, uint b) => ((ulong) a << 32) | b;

    private static ushort RotateLeft(ushort value, int count) => (ushort) ((value << count) | (value >> (16 - count)));

}

public static class VFSAES
{

    // funky decrypt with cbc encrypt
    private static byte[] SBox;

    private static byte[] Key;

    private static byte[] IV;

    private static ulong XORKey;

    public static void InitKeys(byte[] VFSSBox,
                                byte[] VFSKey,
                                byte[] VFSIV,
                                ulong  VFSXORKey)
    {
        SBox   = VFSSBox;
        Key    = VFSKey;
        IV     = VFSIV;
        XORKey = VFSXORKey;
    }

    public static byte[] Decrypt(byte[] ciphertext)
    {
        var           iv       = IV;
        List <byte[]> blocks   = new();
        var           previous = iv.ToArray();

        foreach (var ct in SplitBlocks(ciphertext))
        {
            // encrypt IV
            var block = EncryptBlock(previous);

            // xor keystream with ciphertext
            var pt                                    = new byte[ct.Length];
            for (var i = 0; i < ct.Length; i++) pt[i] = (byte) (ct[i] ^ block[i]);
            blocks.Add(pt);

            // derive next IV
            var nextIv = new byte[16];
            var count  = 0;

            for (var i = 0; i < 16; i++)
            {
                var shiftSrc = XORKey >> (count & 0x38);
                var temp     = (byte) (block[i] ^ (31 * i) ^ (byte) shiftSrc);
                count     += 8;
                temp      =  (byte) (((temp >> 5) | (8 * temp)) & 0xFF);
                temp      =  SBox[temp];
                nextIv[i] =  temp;
            }

            // next IV
            previous = nextIv;
        }

        return blocks.SelectMany(b => b).ToArray();
    }

    private static byte[] EncryptBlock(byte[] plaintext)
    {
        var keyMats = ExpandKey();
        var nRounds = 10;
        var state   = BytesToMatrix(plaintext);

        AddRoundKey(state, keyMats[0]);

        for (var r = 1; r < nRounds; r++)
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
    private static IEnumerable <byte[]> SplitBlocks(byte[] msg, int blockSize = 16)
    {
        for (var i = 0; i < msg.Length; i += blockSize)
            yield return msg.Skip(i).Take(blockSize).ToArray();
    }

    private static void SubBytes(List <byte[]> s)
    {
        for (var i = 0; i < 4; i++)
        for (var j = 0; j < 4; j++)
            s[i][j] = SBox[s[i][j]];
    }

    private static void ShiftRows(List <byte[]> s)
    {
        (s[0][1], s[1][1], s[2][1], s[3][1]) = (s[1][1], s[2][1], s[3][1], s[0][1]);
        (s[0][2], s[1][2], s[2][2], s[3][2]) = (s[2][2], s[3][2], s[0][2], s[1][2]);
        (s[0][3], s[1][3], s[2][3], s[3][3]) = (s[3][3], s[0][3], s[1][3], s[2][3]);
    }

    private static void AddRoundKey(List <byte[]> s, byte[,] k)
    {
        for (var i = 0; i < 4; i++)
        for (var j = 0; j < 4; j++)
            s[i][j] ^= k[i, j];
    }

    private static byte XTime(byte a) => (byte) ((a & 0x80) != 0 ? ((a << 1) ^ 0x1B) & 0xFF : a << 1);

    private static void MixSingleColumn(byte[] a)
    {
        var t = (byte) (a[0] ^ a[1] ^ a[2] ^ a[3]);
        var u = a[0];
        a[0] ^= (byte) (t ^ XTime((byte) (a[0] ^ a[1])));
        a[1] ^= (byte) (t ^ XTime((byte) (a[1] ^ a[2])));
        a[2] ^= (byte) (t ^ XTime((byte) (a[2] ^ a[3])));
        a[3] ^= (byte) (t ^ XTime((byte) (a[3] ^ u)));
    }

    private static void MixColumns(List <byte[]> s)
    {
        for (var i = 0; i < 4; i++)
            MixSingleColumn(s[i]);
    }


    // key expansion
    private static List <byte[,]> ExpandKey()
    {
        var nRounds       = 10; // 16 bytes keys
        var masterKey     = Key;
        var keyCols       = BytesToMatrix(masterKey);
        var iterationSize = masterKey.Length / 4;
        var i             = 1;
        var rCon          = new byte[] { 0x00, 0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80, 0x1B, 0x36, 0x6C, 0xD8, 0xAB, 0x4D, 0x9A, 0x2F, 0x5E, 0xBC, 0x63, 0xC6, 0x97, 0x35, 0x6A, 0xD4, 0xB3, 0x7D, 0xFA, 0xEF, 0xC5, 0x91, 0x39 };

        while (keyCols.Count < (nRounds + 1) * 4)
        {
            var word = keyCols[^1].ToArray();

            if (keyCols.Count % iterationSize == 0)
            {
                // rotate
                var first = word[0];
                Array.Copy(word,
                           1,
                           word,
                           0,
                           word.Length - 1);
                word[^1] = first;

                // sbox
                for (var k = 0; k < 4; k++) word[k] = SBox[word[k]];

                word[0] ^= rCon[i];
                i++;
            }
            else if (masterKey.Length == 32 && keyCols.Count % iterationSize == 4)
            {
                for (var k = 0; k < 4; k++) word[k] = SBox[word[k]];
            }

            var prev                            = keyCols[keyCols.Count - iterationSize];
            for (var k = 0; k < 4; k++) word[k] ^= prev[k];

            keyCols.Add(word);
        }

        var res = new List <byte[,]>();

        for (var x = 0; x < keyCols.Count / 4; x++)
        {
            var m = new byte[4, 4];

            for (var c = 0; c < 4; c++)
            for (var r = 0; r < 4; r++)
                m[c, r] = keyCols[x * 4 + c][r]; // swap indices

            res.Add(m);
        }

        return res;
    }

    private static List <byte[]> BytesToMatrix(byte[] text)
    {
        var rows = new List <byte[]>();
        for (var i = 0; i < text.Length; i += 4)
            rows.Add(new[] { text[i], text[i + 1], text[i + 2], text[i + 3] });

        return rows;
    }

    private static byte[] MatrixToBytes(List <byte[]> m)
    {
        var r   = new byte[16];
        var idx = 0;
        for (var i = 0; i < 4; i++)
        for (var j = 0; j < 4; j++)
            r[idx++] = m[i][j];

        return r;
    }

}

public class VFSAES_1_0_14
{

    

    #region Constants

    private const int BLOCK_SIZE = 16;

    private const int NUM_ROUNDS = 10;

    private const int MAX_INPUT_LEN = 256;

    #endregion

    #region Static Tables

    
    private static readonly byte[] s_rcon = [ 0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80, 0x1B, 0x36 ];

    private static readonly byte[] s_feedbackXorValues =
    [
        0x96, 0x1E, 0xE3, 0x71, 0x09, 0x2C, 0x20, 0x28,
        0x6E, 0x16, 0xEB, 0x79, 0x01, 0x24, 0x28, 0x20
    ];

    private static readonly byte[] s_feedbackTypes = [ 0, 1, 0, 0, 1, 0, 2, 0, 0, 1, 0, 0, 1, 0, 0, 2 ];

    // 行优先 → 列优先 映射表
    private static readonly int[] s_bytesToStateMap = [ 0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15 ];

    // SubBytes + ShiftColumns 映射表
    // 列0: 不移位      [0,1,2,3]   ← [0,1,2,3]
    // 列1: 循环上移1   [4,5,6,7]   ← [5,6,7,4]
    // 列2: 循环上移2   [8,9,10,11] ← [10,11,8,9]
    // 列3: 循环上移3   [12,13,14,15] ← [15,12,13,14]
    private static readonly int[] s_shiftColumnsMap = [ 0, 1, 2, 3, 5, 6, 7, 4, 10, 11, 8, 9, 15, 12, 13, 14 ];

    #endregion

    #region Instance Fields

    private readonly byte[] _iv;

    private readonly byte[] _key;

    private readonly byte[] _sbox;

    private readonly byte[][] _roundKeys;

    #endregion

    #region Public API
    
    private static readonly VFSAES_1_0_14 s_instance = new();

    public static long Decrypt(byte[] input, out byte[] output) => s_instance.DecryptInternal(input, out output);

    public static VFSAES_1_0_14 GetContext() => s_instance;

    public byte[] GetSbox() => (byte[]) _sbox.Clone();

    public byte[][] GetRoundKeys()
    {
        var result = new byte[11][];
        for (var i = 0; i < 11; i++)
            result[i] = (byte[]) _roundKeys[i].Clone();

        return result;
    }

    #endregion

    #region Constructor

    private VFSAES_1_0_14()
    {
        _iv        = new byte[BLOCK_SIZE];
        _key       = new byte[BLOCK_SIZE];
        _sbox      = new byte[256];
        _roundKeys = new byte[11][];

        for (var i = 0; i < 11; i++)
            _roundKeys[i] = new byte[BLOCK_SIZE];

        Initialize();
        KeyExpansion();
    }

    #endregion

    #region ========== AES 核心变换 ==========

    /// <summary>
    ///     BytesToState + AddRoundKey (初始轮)
    ///     将输入从行优先转为列优先，同时与轮密钥异或
    /// </summary>
    private static void BytesToState_AddRoundKey(byte[] state, byte[] input, byte[] roundKey)
    {
        for (var i = 0; i < BLOCK_SIZE; i++)
        {
            var stateIdx = s_bytesToStateMap[i];
            state[stateIdx] = (byte) (input[i] ^ roundKey[stateIdx]);
        }
    }

    /// <summary>
    ///     SubBytes + ShiftColumns (白盒变体)
    ///     标准AES: ShiftRows - 每行循环左移
    ///     此变体: ShiftColumns - 每列循环上移
    ///     状态布局 (列优先):
    ///     Col0   Col1   Col2   Col3
    ///     [0]    [4]    [8]    [12]
    ///     [1]    [5]    [9]    [13]
    ///     [2]    [6]    [10]   [14]
    ///     [3]    [7]    [11]   [15]
    ///     移位规则:
    ///     Col0: 不移位
    ///     Col1: 上移1
    ///     Col2: 上移2
    ///     Col3: 上移3
    /// </summary>
    private void SubBytes_ShiftColumns(byte[] output, byte[] input)
    {
        for (var i = 0; i < BLOCK_SIZE; i++)
            output[i] = _sbox[input[s_shiftColumnsMap[i]]];
    }

    /// <summary>
    ///     MixRows (白盒变体)
    ///     标准AES: MixColumns - 对每列做 GF(2^8) 矩阵乘法
    ///     此变体: MixRows - 对每行做 GF(2^8) 矩阵乘法
    ///     状态布局:
    ///     [0]  [4]  [8]  [12]  ← Row 0: 作为一组处理
    ///     [1]  [5]  [9]  [13]  ← Row 1: 作为一组处理
    ///     [2]  [6]  [10] [14]  ← Row 2: 作为一组处理
    ///     [3]  [7]  [11] [15]  ← Row 3: 作为一组处理
    /// </summary>
    private static void MixRows(byte[] state)
    {
        for (var row = 0; row < 4; row++)
        {
            var a0 = (sbyte) state[row];
            var a1 = (sbyte) state[row + 4];
            var a2 = (sbyte) state[row + 8];
            var a3 = (sbyte) state[row + 12];

            var t0 = XTime(a0);
            var t1 = XTime(a1);
            var t2 = XTime(a2);
            var t3 = XTime(a3);

            state[row]      = (byte) (t0 ^ t1 ^ a1 ^ a2 ^ a3); // 2·a0 + 3·a1 + a2 + a3
            state[row + 4]  = (byte) (t1 ^ t2 ^ a0 ^ a2 ^ a3); // a0 + 2·a1 + 3·a2 + a3
            state[row + 8]  = (byte) (t2 ^ t3 ^ a0 ^ a1 ^ a3); // a0 + a1 + 2·a2 + 3·a3
            state[row + 12] = (byte) (t0 ^ t3 ^ a0 ^ a1 ^ a2); // 3·a0 + a1 + a2 + 2·a3
        }
    }

    /// <summary>
    ///     XTime: GF(2^8) 乘以 2
    /// </summary>
    private static byte XTime(sbyte a)
    {
        return (byte) (a < 0 ? (2 * a) ^ 0x1B : 2 * a);
    }

    /// <summary>
    ///     AddRoundKey: 状态与轮密钥异或
    /// </summary>
    private static void AddRoundKey(byte[] output, byte[] state, byte[] roundKey)
    {
        for (var i = 0; i < BLOCK_SIZE; i++)
            output[i] = (byte) (state[i] ^ roundKey[i]);
    }

    /// <summary>
    ///     StateToBytes: 列优先 → 行优先
    /// </summary>
    private static void StateToBytes(byte[] output, byte[] state)
    {
        for (var i = 0; i < BLOCK_SIZE; i++)
            output[i] = state[s_bytesToStateMap[i]];
    }

    #endregion

    #region ========== AES 加密流程 ==========

    /// <summary>
    ///     AES 单块加密 (白盒变体)
    /// </summary>
    private void EncryptBlock(byte[] output, byte[] input)
    {
        var s    = new byte[BLOCK_SIZE];
        var temp = new byte[BLOCK_SIZE];

        // 1. BytesToState + AddRoundKey[0]
        BytesToState_AddRoundKey(s, input, _roundKeys[0]);

        // 2. 10轮变换
        for (var round = 1; round <= NUM_ROUNDS; round++)
        {
            SubBytes_ShiftColumns(temp, s);

            if (round != NUM_ROUNDS)
                MixRows(temp);

            AddRoundKey(s, temp, _roundKeys[round]);
        }

        // 3. StateToBytes
        StateToBytes(output, s);
    }

    #endregion


    #region ========== 密钥扩展 ==========

    private void Initialize()
    {
        Array.Copy(CryptoHelper.VFSAESIV,   _iv,           BLOCK_SIZE);
        Array.Copy(CryptoHelper.VFSAESKey,  _key,          BLOCK_SIZE);
        Array.Copy(CryptoHelper.VFSAESSBox, _sbox,         256);
        Array.Copy(CryptoHelper.VFSAESRoundKey0,            _roundKeys[0], BLOCK_SIZE);
    }

    private void KeyExpansion()
    {
        const int bufferSize      = 16 + 16 + 256 + 11 * 16;
        const int roundKeysOffset = 288;

        var buffer = new byte[bufferSize];

        Array.Copy(_iv,
                   0,
                   buffer,
                   0,
                   16);
        Array.Copy(_key,
                   0,
                   buffer,
                   16,
                   16);
        Array.Copy(_sbox,
                   0,
                   buffer,
                   32,
                   256);
        for (var i = 0; i < 11; i++)
        {
            Array.Copy(_roundKeys[i],
                       0,
                       buffer,
                       roundKeysOffset + i * 16,
                       16);
        }

        int[] sboxOffsets = [ 6, 10, 14, 2 ];
        int[][] xorOffsets =
        [
            [ 3, 7, 11, -1 ],
            [ 0, 4, 8, 12 ],
            [ 1, 5, 9, 13 ],
            [ 2, 6, 10, 14 ]
        ];
        int[][] dstOffsets =
        [
            [ 15, 19, 23, 27 ],
            [ 16, 20, 24, 28 ],
            [ 17, 21, 25, 29 ],
            [ 18, 22, 26, 30 ]
        ];

        byte stateVal = 58;
        var  rconIdx  = 0;

        for (var pos = 289; pos != 449; pos += 16)
        {
            var sboxVals = new byte[4];
            var temp     = new byte[4, 4];

            for (var col = 0; col < 4; col++)
                sboxVals[col] = _sbox[buffer[pos + sboxOffsets[col]]];

            stateVal   ^= (byte) (sboxVals[0] ^ s_rcon[rconIdx++]);
            temp[0, 0] =  stateVal;
            temp[0, 1] =  (byte) (sboxVals[1] ^ buffer[pos + 3]);
            temp[0, 2] =  (byte) (sboxVals[2] ^ buffer[pos + 7]);
            temp[0, 3] =  (byte) (sboxVals[3] ^ buffer[pos + 11]);

            for (var row = 1; row < 4; row++)
            {
                for (var col = 0; col < 4; col++)
                    temp[row, col] = (byte) (temp[row - 1, col] ^ buffer[pos + xorOffsets[row][col]]);
            }

            for (var row = 0; row < 4; row++)
            {
                for (var col = 0; col < 4; col++)
                    buffer[pos + dstOffsets[row][col]] = temp[row, col];
            }
        }

        for (var i = 0; i < 11; i++)
            Array.Copy(buffer,
                       roundKeysOffset + i * 16,
                       _roundKeys[i],
                       0,
                       16);
    }

    #endregion

    #region ========== 反馈计算 (CFB变体) ==========

    private void ComputeFeedback(byte[] feedback, byte[] block)
    {
        for (var i = 0; i < BLOCK_SIZE; i++)
        {
            var val    = block[i];
            var xorVal = s_feedbackXorValues[i];

            var transformed = s_feedbackTypes[i] switch
            {
                0 => (byte) (((val ^ xorVal) >> 5) | (8 * (val ^ xorVal))),                 // ROL3(val ^ xor)
                1 => (byte) ((val            >> 5) | (8 * (val ^ xorVal))),                 // 混合
                _ => (byte) (((val                             ^ xorVal) >> 5) | (8 * val)) // 混合
            };

            feedback[i] = _sbox[transformed];
        }
    }

    #endregion

    #region ========== 解密主流程 ==========

    private long DecryptInternal(byte[]? input, out byte[] output)
    {
        output = [ ];

        if (input == null || input.Length == 0 || input.Length > MAX_INPUT_LEN)
            return 0;

        var inputLen  = input.Length;
        var numBlocks = (inputLen + BLOCK_SIZE - 1) / BLOCK_SIZE;

        var outputBuffer = new byte[numBlocks * BLOCK_SIZE];
        var feedback     = new byte[BLOCK_SIZE];
        var cipherBlock  = new byte[BLOCK_SIZE];
        var keystream    = new byte[BLOCK_SIZE];

        long totalOutput  = 0;
        var  isFirstBlock = true;

        for (var blockIdx = 0; blockIdx < numBlocks; blockIdx++)
        {
            var blockStart = BLOCK_SIZE * blockIdx;
            var blockSize  = Math.Min(BLOCK_SIZE, inputLen - blockStart);

            // 读取密文块
            Array.Clear(cipherBlock, 0, BLOCK_SIZE);
            Array.Copy(input,
                       blockStart,
                       cipherBlock,
                       0,
                       blockSize);

            // AES 加密生成密钥流
            var aesInput = isFirstBlock ? _key : feedback;
            EncryptBlock(keystream, aesInput);

            // XOR 解密
            for (var i = 0; i < blockSize; i++)
                cipherBlock[i] ^= keystream[i];

            // 保存输出
            Array.Copy(cipherBlock,
                       0,
                       outputBuffer,
                       totalOutput,
                       blockSize);
            totalOutput += blockSize;

            // 更新反馈
            ComputeFeedback(feedback, keystream);
            isFirstBlock = false;
        }

        output = new byte[totalOutput];
        Array.Copy(outputBuffer, output, totalOutput);

        return totalOutput;
    }

    #endregion

}
