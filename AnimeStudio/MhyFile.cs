using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace AnimeStudio
{
    public class MhyFile
    {
        private string signature;
        private List<BundleFile.StorageBlock> m_BlocksInfo;
        private List<BundleFile.Node> m_DirectoryInfo;

        public BundleFile.Header m_Header;
        public List<StreamFile> fileList;
        public Mhy mhy;

        public long Offset;
        private bool isZZZ20; // zzz 2.0 uses oodle but the flag is still set to LZ4

        private static readonly byte[] Key = new byte[256]
        {
            41, 35, 190, 132, 225, 108, 214, 174, 82, 144,
            73, 241, 241, 187, 233, 235, 179, 166, 219, 60,
            135, 12, 62, 153, 36, 94, 13, 28, 6, 183,
            71, 222, 179, 18, 77, 200, 67, 187, 139, 166,
            31, 3, 90, 125, 9, 56, 37, 31, 93, 212,
            203, 252, 150, 245, 69, 59, 19, 13, 137, 10,
            28, 219, 174, 50, 32, 154, 80, 238, 64, 120,
            54, 253, 18, 73, 50, 246, 158, 125, 73, 220,
            173, 79, 20, 242, 68, 64, 102, 208, 107, 196,
            48, 183, 50, 59, 161, 34, 246, 34, 145, 157,
            225, 139, 31, 218, 176, 202, 153, 2, 185, 114,
            157, 73, 44, 128, 126, 197, 153, 213, 233, 128,
            178, 234, 201, 204, 83, 191, 103, 214, 191, 20,
            214, 126, 45, 220, 142, 102, 131, 239, 87, 73,
            97, 255, 105, 143, 97, 205, 209, 30, 157, 156,
            22, 114, 114, 230, 29, 240, 132, 79, 74, 119,
            2, 215, 232, 57, 44, 83, 203, 201, 18, 30,
            51, 116, 158, 12, 244, 213, 212, 159, 212, 164,
            89, 126, 53, 207, 50, 34, 244, 204, 207, 211,
            144, 45, 72, 211, 143, 117, 230, 217, 29, 42,
            229, 192, 247, 43, 120, 129, 135, 68, 14, 95,
            80, 0, 212, 97, 141, 190, 123, 5, 21, 7,
            59, 51, 130, 31, 24, 112, 146, 218, 100, 84,
            206, 177, 133, 62, 105, 21, 248, 70, 106, 4,
            150, 115, 14, 217, 22, 47, 103, 104, 212, 247,
            74, 74, 208, 87, 104, 118
        };

        public long TotalSize => 8 + m_Header.compressedBlocksInfoSize + m_BlocksInfo.Sum((BundleFile.StorageBlock x) => x.compressedSize);

        public MhyFile(FileReader reader, Mhy mhy)
        {
            this.mhy = mhy;
            Offset = reader.Position;
            reader.Endian = EndianType.LittleEndian;

            signature = reader.ReadStringToNull(4);
            Logger.Verbose($"Parsed signature {signature}");
            if (signature != "mhy0" && signature != "mhy1")
                throw new Exception("not a mhy file");

            m_Header = new BundleFile.Header
            {
                version = 6,
                unityVersion = "5.x.x",
                unityRevision = "2017.4.30f1",
                compressedBlocksInfoSize = reader.ReadUInt32(),
                flags = (ArchiveFlags)0x43
            };
            Logger.Verbose($"Header: {m_Header}");
            ReadBlocksInfoAndDirectory(reader);
            using var blocksStream = CreateBlocksStream(reader.FullPath);
            ReadBlocks(reader, blocksStream);
            ReadFiles(blocksStream, reader.FullPath);
            m_Header.size = 8 + m_Header.compressedBlocksInfoSize + m_BlocksInfo.Sum(x => x.compressedSize);
            while (reader.PeekChar() == '\0')
            {
                reader.Position++;
            }
        }

        private void ReadBlocksInfoAndDirectory(FileReader reader)
        {
            var blocksInfo = reader.ReadBytes((int)m_Header.compressedBlocksInfoSize);
            int offset;
            if (signature == "mhy0")
            {
                DescrambleHeader(blocksInfo);
                offset = 32;
            }
            else
            {
                DescrambleHeader2(blocksInfo);
                offset = 48;
            }

            Logger.Verbose($"Descrambled blocksInfo signature {Convert.ToHexString(blocksInfo, 0 , 4)}");
            using MemoryStream blocksInfoStream = new MemoryStream(blocksInfo, offset, (int)m_Header.compressedBlocksInfoSize - offset);
            using EndianBinaryReader blocksInfoReader = new EndianBinaryReader(blocksInfoStream);
            m_Header.uncompressedBlocksInfoSize = blocksInfoReader.ReadMhyUInt();
            Logger.Verbose($"uncompressed blocksInfo size: 0x{m_Header.uncompressedBlocksInfoSize:X8}");
            var compressedBlocksInfo = blocksInfoReader.ReadBytes((int)blocksInfoReader.Remaining);

            var uncompressedBlocksInfo = ArrayPool<byte>.Shared.Rent((int)m_Header.uncompressedBlocksInfoSize);
            var uncompressedBlocksInfoSpan = uncompressedBlocksInfo.AsSpan(0, (int)m_Header.uncompressedBlocksInfoSize);

            try
            {
                int numWrite;
                numWrite = Decompress(compressedBlocksInfo, uncompressedBlocksInfoSpan);

                Logger.Verbose($"Writing block and directory to blocks stream...");
                using var blocksInfoUncompressedStream = new MemoryStream(uncompressedBlocksInfo, 0, (int)m_Header.uncompressedBlocksInfoSize);
                using var blocksInfoUncompressedReader = new EndianBinaryReader(blocksInfoUncompressedStream);
                var nodesCount = blocksInfoUncompressedReader.ReadMhyInt();
                m_DirectoryInfo = new List<BundleFile.Node>();
                Logger.Verbose($"Directory count: {nodesCount}");
                for (int i = 0; i < nodesCount; i++)
                {
                    m_DirectoryInfo.Add(new BundleFile.Node
                    {
                        path = blocksInfoUncompressedReader.ReadMhyString(),
                        flags = blocksInfoUncompressedReader.ReadBoolean() ? 4u : 0,
                        offset = blocksInfoUncompressedReader.ReadMhyInt(),
                        size = blocksInfoUncompressedReader.ReadMhyUInt()
                    });

                    Logger.Verbose($"Directory {i} Info: {m_DirectoryInfo[i]}");
                }

                var blocksInfoCount = blocksInfoUncompressedReader.ReadMhyInt();
                m_BlocksInfo = new List<BundleFile.StorageBlock>();
                Logger.Verbose($"Blocks count: {blocksInfoCount}");
                for (int i = 0; i < blocksInfoCount; i++)
                {
                    m_BlocksInfo.Add(new BundleFile.StorageBlock
                    {
                        compressedSize = (uint)blocksInfoUncompressedReader.ReadMhyInt(),
                        uncompressedSize = blocksInfoUncompressedReader.ReadMhyUInt(),
                        flags = (StorageBlockFlags)0x43
                    });

                    Logger.Verbose($"Block {i} Info: {m_BlocksInfo[i]}");
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(uncompressedBlocksInfo, true);
            }    
        }

        private Stream CreateBlocksStream(string path)
        {
            Stream blocksStream;
            var uncompressedSizeSum = (int)m_BlocksInfo.Sum(x => x.uncompressedSize);
            Logger.Verbose($"Total size of decompressed blocks: 0x{uncompressedSizeSum:X8}");
            if (uncompressedSizeSum >= int.MaxValue)
                blocksStream = new FileStream(path + ".temp", FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
            else
                blocksStream = new MemoryStream(uncompressedSizeSum);
            return blocksStream;
        }

        private void ReadBlocks(EndianBinaryReader reader, Stream blocksStream)
        {
            foreach (var blockInfo in m_BlocksInfo)
            {
                var compressedSize = (int)blockInfo.compressedSize;
                var uncompressedSize = (int)blockInfo.uncompressedSize;
                if (compressedSize < 0x10)
                {
                    throw new Exception($"Wrong compressed length: {compressedSize}");
                }

                var compressedBytes = ArrayPool<byte>.Shared.Rent(compressedSize);
                var uncompressedBytes = ArrayPool<byte>.Shared.Rent(uncompressedSize);
                reader.Read(compressedBytes, 0, compressedSize);
                try
                {
                    var compressedBytesSpan = compressedBytes.AsSpan(0, compressedSize);
                    var uncompressedBytesSpan = uncompressedBytes.AsSpan(0, uncompressedSize);
                    
                    int offset = 0;
                    if (signature == "mhy0")
                    {
                        DescrambleEntry(compressedBytesSpan);
                        offset = 12;
                    }
                    else
                    {
                        DescrambleEntry2(compressedBytesSpan);
                        offset = 28;
                    }

                    Logger.Verbose($"Descrambled block signature {Convert.ToHexString(compressedBytes, 0, 4)}");
                    int num = offset;
                    int numWrite = Decompress(compressedBytesSpan.Slice(num, compressedBytesSpan.Length - num), uncompressedBytesSpan);
                    if (numWrite != uncompressedSize)
                    {
                        throw new IOException($"Lz4 decompression error, write {numWrite} bytes but expected {uncompressedSize} bytes");
                    }

                    blocksStream.Write(uncompressedBytesSpan);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(compressedBytes, true);
                    ArrayPool<byte>.Shared.Return(uncompressedBytes, true);
                }
            }
        }
        private int Decompress(Span<byte> compressed, Span<byte> decompressed)
        {
            if (isZZZ20) return OozHelper.Decompress(compressed, decompressed);

            try { return LZ4.Instance.Decompress(compressed, decompressed); }
            catch
            {
                try { 
                    int numWrite = OozHelper.Decompress(compressed, decompressed);
                    isZZZ20 = true;
                    return numWrite;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Decompression failed: {ex.Message}");
                    throw;
                }
            }
        }

        private void ReadFiles(Stream blocksStream, string path)
        {
            Logger.Verbose($"Writing files from blocks stream...");

            fileList = new List<StreamFile>();
            for (int i = 0; i < m_DirectoryInfo.Count; i++)
            {
                var node = m_DirectoryInfo[i];
                var file = new StreamFile();
                fileList.Add(file);
                file.path = node.path;
                file.fileName = Path.GetFileName(node.path);
                if (node.size >= int.MaxValue)
                {
                    var extractPath = path + "_unpacked" + Path.DirectorySeparatorChar;
                    Directory.CreateDirectory(extractPath);
                    file.stream = new FileStream(extractPath + file.fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                }
                else
                    file.stream = new MemoryStream((int)node.size);
                blocksStream.Position = node.offset;
                blocksStream.CopyTo(file.stream, node.size);
                file.stream.Position = 0;
            }
        }

        #region Scramble
        private static readonly byte[] GF256Exp = new byte[] { 0x01, 0x03, 0x05, 0x0F, 0x11, 0x33, 0x55, 0xFF, 0x1A, 0x2E, 0x72, 0x96, 0xA1, 0xF8, 0x13, 0x35, 0x5F, 0xE1, 0x38, 0x48, 0xD8, 0x73, 0x95, 0xA4, 0xF7, 0x02, 0x06, 0x0A, 0x1E, 0x22, 0x66, 0xAA, 0xE5, 0x34, 0x5C, 0xE4, 0x37, 0x59, 0xEB, 0x26, 0x6A, 0xBE, 0xD9, 0x70, 0x90, 0xAB, 0xE6, 0x31, 0x53, 0xF5, 0x04, 0x0C, 0x14, 0x3C, 0x44, 0xCC, 0x4F, 0xD1, 0x68, 0xB8, 0xD3, 0x6E, 0xB2, 0xCD, 0x4C, 0xD4, 0x67, 0xA9, 0xE0, 0x3B, 0x4D, 0xD7, 0x62, 0xA6, 0xF1, 0x08, 0x18, 0x28, 0x78, 0x88, 0x83, 0x9E, 0xB9, 0xD0, 0x6B, 0xBD, 0xDC, 0x7F, 0x81, 0x98, 0xB3, 0xCE, 0x49, 0xDB, 0x76, 0x9A, 0xB5, 0xC4, 0x57, 0xF9, 0x10, 0x30, 0x50, 0xF0, 0x0B, 0x1D, 0x27, 0x69, 0xBB, 0xD6, 0x61, 0xA3, 0xFE, 0x19, 0x2B, 0x7D, 0x87, 0x92, 0xAD, 0xEC, 0x2F, 0x71, 0x93, 0xAE, 0xE9, 0x20, 0x60, 0xA0, 0xFB, 0x16, 0x3A, 0x4E, 0xD2, 0x6D, 0xB7, 0xC2, 0x5D, 0xE7, 0x32, 0x56, 0xFA, 0x15, 0x3F, 0x41, 0xC3, 0x5E, 0xE2, 0x3D, 0x47, 0xC9, 0x40, 0xC0, 0x5B, 0xED, 0x2C, 0x74, 0x9C, 0xBF, 0xDA, 0x75, 0x9F, 0xBA, 0xD5, 0x64, 0xAC, 0xEF, 0x2A, 0x7E, 0x82, 0x9D, 0xBC, 0xDF, 0x7A, 0x8E, 0x89, 0x80, 0x9B, 0xB6, 0xC1, 0x58, 0xE8, 0x23, 0x65, 0xAF, 0xEA, 0x25, 0x6F, 0xB1, 0xC8, 0x43, 0xC5, 0x54, 0xFC, 0x1F, 0x21, 0x63, 0xA5, 0xF4, 0x07, 0x09, 0x1B, 0x2D, 0x77, 0x99, 0xB0, 0xCB, 0x46, 0xCA, 0x45, 0xCF, 0x4A, 0xDE, 0x79, 0x8B, 0x86, 0x91, 0xA8, 0xE3, 0x3E, 0x42, 0xC6, 0x51, 0xF3, 0x0E, 0x12, 0x36, 0x5A, 0xEE, 0x29, 0x7B, 0x8D, 0x8C, 0x8F, 0x8A, 0x85, 0x94, 0xA7, 0xF2, 0x0D, 0x17, 0x39, 0x4B, 0xDD, 0x7C, 0x84, 0x97, 0xA2, 0xFD, 0x1C, 0x24, 0x6C, 0xB4, 0xC7, 0x52, 0xF6, 0x01 };
        private static readonly byte[] GF256Log = new byte[] { 0x00, 0x00, 0x19, 0x01, 0x32, 0x02, 0x1A, 0xC6, 0x4B, 0xC7, 0x1B, 0x68, 0x33, 0xEE, 0xDF, 0x03, 0x64, 0x04, 0xE0, 0x0E, 0x34, 0x8D, 0x81, 0xEF, 0x4C, 0x71, 0x08, 0xC8, 0xF8, 0x69, 0x1C, 0xC1, 0x7D, 0xC2, 0x1D, 0xB5, 0xF9, 0xB9, 0x27, 0x6A, 0x4D, 0xE4, 0xA6, 0x72, 0x9A, 0xC9, 0x09, 0x78, 0x65, 0x2F, 0x8A, 0x05, 0x21, 0x0F, 0xE1, 0x24, 0x12, 0xF0, 0x82, 0x45, 0x35, 0x93, 0xDA, 0x8E, 0x96, 0x8F, 0xDB, 0xBD, 0x36, 0xD0, 0xCE, 0x94, 0x13, 0x5C, 0xD2, 0xF1, 0x40, 0x46, 0x83, 0x38, 0x66, 0xDD, 0xFD, 0x30, 0xBF, 0x06, 0x8B, 0x62, 0xB3, 0x25, 0xE2, 0x98, 0x22, 0x88, 0x91, 0x10, 0x7E, 0x6E, 0x48, 0xC3, 0xA3, 0xB6, 0x1E, 0x42, 0x3A, 0x6B, 0x28, 0x54, 0xFA, 0x85, 0x3D, 0xBA, 0x2B, 0x79, 0x0A, 0x15, 0x9B, 0x9F, 0x5E, 0xCA, 0x4E, 0xD4, 0xAC, 0xE5, 0xF3, 0x73, 0xA7, 0x57, 0xAF, 0x58, 0xA8, 0x50, 0xF4, 0xEA, 0xD6, 0x74, 0x4F, 0xAE, 0xE9, 0xD5, 0xE7, 0xE6, 0xAD, 0xE8, 0x2C, 0xD7, 0x75, 0x7A, 0xEB, 0x16, 0x0B, 0xF5, 0x59, 0xCB, 0x5F, 0xB0, 0x9C, 0xA9, 0x51, 0xA0, 0x7F, 0x0C, 0xF6, 0x6F, 0x17, 0xC4, 0x49, 0xEC, 0xD8, 0x43, 0x1F, 0x2D, 0xA4, 0x76, 0x7B, 0xB7, 0xCC, 0xBB, 0x3E, 0x5A, 0xFB, 0x60, 0xB1, 0x86, 0x3B, 0x52, 0xA1, 0x6C, 0xAA, 0x55, 0x29, 0x9D, 0x97, 0xB2, 0x87, 0x90, 0x61, 0xBE, 0xDC, 0xFC, 0xBC, 0x95, 0xCF, 0xCD, 0x37, 0x3F, 0x5B, 0xD1, 0x53, 0x39, 0x84, 0x3C, 0x41, 0xA2, 0x6D, 0x47, 0x14, 0x2A, 0x9E, 0x5D, 0x56, 0xF2, 0xD3, 0xAB, 0x44, 0x11, 0x92, 0xD9, 0x23, 0x20, 0x2E, 0x89, 0xB4, 0x7C, 0xB8, 0x26, 0x77, 0x99, 0xE3, 0xA5, 0x67, 0x4A, 0xED, 0xDE, 0xC5, 0x31, 0xFE, 0x18, 0x0D, 0x63, 0x8C, 0x80, 0xC0, 0xF7, 0x70, 0x07 };
        private static int GF256Mul(int a, int b) => (a == 0 || b == 0) ? 0 : GF256Exp[(GF256Log[a] + GF256Log[b]) % 0xFF];

        private void DescrambleChunk(Span<byte> input)
        {
            byte[] vector = new byte[input.Length];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < input.Length; j++)
                {
                    int k = mhy.MhyShiftRow[(2 - i) * 0x10 + j];
                    int idx = j % 8;
                    vector[j] = (byte)(mhy.MhyKey[idx] ^ mhy.SBox[(j % 4 * 0x100) | GF256Mul(mhy.MhyMul[idx], input[k % input.Length])]);
                }
                vector.AsSpan(0, input.Length).CopyTo(input);
            }
        }
        private void Descramble(Span<byte> input, int blockSize, int entrySize)
        {
            var roundedEntrySize = (entrySize + 0xF) / 0x10 * 0x10;
            if (mhy.Name == "ZZZ_CB2" || mhy.Name == "ZZZ")
            {
                DescrambleChunk(input.Slice(4, 16));
                string signature = Encoding.UTF8.GetString(input.Slice(4, 8));
                if (signature != "mhynewec")
                {
                    throw new Exception("Invalid signature, expected mhynewec got " + signature);
                }
                if (blockSize <= 35)
                {
                    return;
                }
                DescrambleChunk(input.Slice(20, 16));
                byte[] seed = input.Slice(0, 16).ToArray();
                byte[] data = input.Slice(20, 16).ToArray();
                using Aes aes = Aes.Create();
                aes.Key = seed;
                aes.EncryptEcb(data, data, PaddingMode.None);
                data.CopyTo(input.Slice(20));
                for (int i = 0; i < 4; i++)
                {
                    input[i] ^= data[i];
                }
                RC4(input.Slice(20 + roundedEntrySize, blockSize - (20 + roundedEntrySize)), input.Slice(20, 8), input.Slice(28, 8));
                return;
            }
            for (int i = 0; i < roundedEntrySize; i += 0x10)
                DescrambleChunk(input.Slice(i + 4, Math.Min(input.Length - 4, 0x10)));

            for (int i = 0; i < 4; i++)
                input[i] ^= input[i + 4];

            var finished = false;
            var currentEntry = roundedEntrySize + 4;
            while (currentEntry < blockSize && !finished)
            {
                for (int i = 0; i < entrySize; i++)
                {
                    input[i + currentEntry] ^= input[i + 4];
                    if (i + currentEntry >= blockSize - 1)
                    {
                        finished = true;
                        break;
                    }
                }
                currentEntry += entrySize;
            }
        }

        private void RC4(Span<byte> data, Span<byte> key, Span<byte> operation)
        {
            byte[] S = new byte[256];
            Key.CopyTo(S, 0);
            byte[] T = new byte[256];
            if (key.Length == 256)
            {
                key.CopyTo(T);
            }
            else
            {
                for (int _ = 0; _ < 256; _++)
                {
                    T[_] = key[_ % key.Length];
                }
            }
            int i = 0;
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
            for (int iteration = 0; iteration < data.Length; iteration++)
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
                switch (operation[i % operation.Length] % 3)
                {
                    case 0:
                        data[iteration] ^= Convert.ToByte(K);
                        break;
                    case 1:
                        data[iteration] -= Convert.ToByte(K);
                        break;
                    case 2:
                        data[iteration] += Convert.ToByte(K);
                        break;
                }
            }
        }
        public void DescrambleHeader2(Span<byte> input)
        {
            Descramble(input, Math.Min(input.Length, 128), 28);
        }

        public void DescrambleEntry2(Span<byte> input)
        {
            Descramble(input, Math.Min(input.Length, 128), 8);
        }
        public void DescrambleHeader(Span<byte> input) => Descramble(input, 0x39, 0x1C);
        public void DescrambleEntry(Span<byte> input) => Descramble(input, Math.Min(input.Length, 0x21), 8);
        #endregion
    }
}