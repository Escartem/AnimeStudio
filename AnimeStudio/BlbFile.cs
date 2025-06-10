using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace AnimeStudio
{
    public class Blb3File
    {
        private List<BundleFile.StorageBlock> m_BlocksInfo;
        private List<BundleFile.Node> m_DirectoryInfo;
        private byte[] Header;

        private static string ModuleName = "hk4e.dll";

        public BundleFile.Header m_Header;
        public List<StreamFile> fileList;
        public long Offset;


        [DllImport("kernel32.dll")]
        static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll")]
        static extern IntPtr LoadLibrary(string lpFileName);
        
        private delegate int DecryptBlb(ref byte buffer, ulong size, ref byte header, ulong headerSize, uint idk);
        
        private static DecryptBlb _decrypt;

        public static void Decrypt(byte[] header, Span<byte> buffer)
        {
            if (_decrypt == null)
            {
                var module = LoadLibrary(ModuleName);

                byte[] key1 = new byte[256] { 0x63, 0x7d, 0x75, 0x78, 0xf6, 0x6e, 0x69, 0xc2, 0x38, 0x08, 0x6d, 0x20, 0xf2, 0xda, 0xa5, 0x79, 0xda, 0x93, 0xdb, 0x6e, 0xee, 0x4c, 0x51, 0xe7, 0xb5, 0xcd, 0xb8, 0xb4, 0x80, 0xb9, 0x6c, 0xdf, 0x97, 0xdc, 0xb1, 0x05, 0x12, 0x1a, 0xd1, 0xeb, 0x1c, 0x8c, 0xcf, 0xda, 0x5d, 0xf5, 0x1f, 0x3a, 0x34, 0xf6, 0x11, 0xf0, 0x2c, 0xa3, 0x33, 0xad, 0x3f, 0x2b, 0xba, 0xd9, 0xd7, 0x1a, 0x8c, 0x4a, 0x49, 0xc2, 0x6e, 0x59, 0x5f, 0x2b, 0x1c, 0xe7, 0x1a, 0x72, 0x9c, 0xf8, 0x65, 0xae, 0x61, 0xcb, 0x03, 0x80, 0x52, 0xbe, 0x74, 0xa9, 0xe7, 0x0c, 0x32, 0x92, 0xe4, 0x62, 0x16, 0x11, 0x06, 0x90, 0xb0, 0x8e, 0xc8, 0x98, 0x27, 0x28, 0x55, 0xe2, 0x2d, 0x90, 0x68, 0x14, 0x3c, 0x51, 0xf1, 0xc7, 0x21, 0xd2, 0x32, 0xfc, 0xe6, 0xe8, 0x4e, 0x82, 0xc4, 0xcf, 0xa0, 0x5a, 0x6c, 0x82, 0x8d, 0xad, 0x4d, 0x8d, 0x91, 0x6f, 0xdb, 0x12, 0xc2, 0x90, 0x4c, 0x2e, 0xf4, 0xb6, 0xe8, 0xd0, 0x97, 0xfc, 0xf0, 0x10, 0xdd, 0x4f, 0xb6, 0xbf, 0x06, 0x1f, 0xde, 0x77, 0x22, 0x8f, 0x42, 0xc3, 0x95, 0x44, 0x40, 0x93, 0x98, 0xa9, 0xed, 0xa3, 0x82, 0xfb, 0x6a, 0x7a, 0x06, 0xc9, 0x3d, 0x38, 0x4a, 0xd6, 0x57, 0x79, 0x85, 0xde, 0x39, 0x60, 0xf8, 0x1e, 0xd4, 0xef, 0x4e, 0x51, 0xd9, 0xc7, 0x10, 0xb7, 0x7a, 0xb9, 0xe7, 0xed, 0xd8, 0x63, 0x72, 0x01, 0x20, 0x14, 0xbe, 0xd4, 0x87, 0x70, 0x45, 0x45, 0xa0, 0xef, 0x67, 0xb5, 0x9c, 0xd6, 0x20, 0xd9, 0xb9, 0xec, 0x8d, 0x62, 0x5a, 0x1c, 0xc3, 0x41, 0x01, 0x19, 0x7a, 0xf2, 0x8d, 0x3c, 0x68, 0x73, 0x73, 0xf7, 0x6d, 0x02, 0x22, 0xb8, 0xc6, 0x30, 0x7c, 0x50, 0x7b, 0xfe, 0x4b, 0x13, 0xb4, 0x9f, 0xb9, 0x60, 0xd7, 0xf4, 0x4c, 0xa9, 0x45, 0xe9 };
                byte[] key2 = new byte[256] { 0x29, 0x23, 0xbe, 0x84, 0xe1, 0x6c, 0xd6, 0xae, 0x52, 0x90, 0x49, 0xf1, 0xf1, 0xbb, 0xe9, 0xeb, 0xb3, 0xa6, 0xdb, 0x3c, 0x87, 0x0c, 0x3e, 0x99, 0x24, 0x5e, 0x0d, 0x1c, 0x06, 0xb7, 0x47, 0xde, 0xb3, 0x12, 0x4d, 0xc8, 0x43, 0xbb, 0x8b, 0xa6, 0x1f, 0x03, 0x5a, 0x7d, 0x09, 0x38, 0x25, 0x1f, 0x5d, 0xd4, 0xcb, 0xfc, 0x96, 0xf5, 0x45, 0x3b, 0x13, 0x0d, 0x89, 0x0a, 0x1c, 0xdb, 0xae, 0x32, 0x20, 0x9a, 0x50, 0xee, 0x40, 0x78, 0x36, 0xfd, 0x12, 0x49, 0x32, 0xf6, 0x9e, 0x7d, 0x49, 0xdc, 0xad, 0x4f, 0x14, 0xf2, 0x44, 0x40, 0x66, 0xd0, 0x6b, 0xc4, 0x30, 0xb7, 0x32, 0x3b, 0xa1, 0x22, 0xf6, 0x22, 0x91, 0x9d, 0xe1, 0x8b, 0x1f, 0xda, 0xb0, 0xca, 0x99, 0x02, 0xb9, 0x72, 0x9d, 0x49, 0x2c, 0x80, 0x7e, 0xc5, 0x99, 0xd5, 0xe9, 0x80, 0xb2, 0xea, 0xc9, 0xcc, 0x53, 0xbf, 0x67, 0xd6, 0xbf, 0x14, 0xd6, 0x7e, 0x2d, 0xdc, 0x8e, 0x66, 0x83, 0xef, 0x57, 0x49, 0x61, 0xff, 0x69, 0x8f, 0x61, 0xcd, 0xd1, 0x1e, 0x9d, 0x9c, 0x16, 0x72, 0x72, 0xe6, 0x1d, 0xf0, 0x84, 0x4f, 0x4a, 0x77, 0x02, 0xd7, 0xe8, 0x39, 0x2c, 0x53, 0xcb, 0xc9, 0x12, 0x1e, 0x33, 0x74, 0x9e, 0x0c, 0xf4, 0xd5, 0xd4, 0x9f, 0xd4, 0xa4, 0x59, 0x7e, 0x35, 0xcf, 0x32, 0x22, 0xf4, 0xcc, 0xcf, 0xd3, 0x90, 0x2d, 0x48, 0xd3, 0x8f, 0x75, 0xe6, 0xd9, 0x1d, 0x2a, 0xe5, 0xc0, 0xf7, 0x2b, 0x78, 0x81, 0x87, 0x44, 0x0e, 0x5f, 0x50, 0x00, 0xd4, 0x61, 0x8d, 0xbe, 0x7b, 0x05, 0x15, 0x07, 0x3b, 0x33, 0x82, 0x1f, 0x18, 0x70, 0x92, 0xda, 0x64, 0x54, 0xce, 0xb1, 0x85, 0x3e, 0x69, 0x15, 0xf8, 0x46, 0x6a, 0x04, 0x96, 0x73, 0x0e, 0xd9, 0x16, 0x2f, 0x67, 0x68, 0xd4, 0xf7, 0x4a, 0x4a, 0xd0, 0x57, 0x68, 0x76 };

                IntPtr key1Ptr = module + 0x1FF7090;
                IntPtr key2Ptr = module + 0x1FF6F90;

                OverrideBytes(key1Ptr, key1, 256);
                OverrideBytes(key2Ptr, key2, 256);
                
                IntPtr function_raw = module + 0xFC730;
                
                _decrypt = Marshal.GetDelegateForFunctionPointer<DecryptBlb>(function_raw);
            }

            ulong header_size = (ulong)header.Length;
            ulong buffer_size = (ulong)buffer.Length;


            _decrypt(ref buffer[0], buffer_size > 128 ? 128 : buffer_size, ref header[0], header_size, 0);
        }

        public Blb3File(FileReader reader, string path)
        {
            Offset = reader.Position;
            reader.Endian = EndianType.LittleEndian;

            var signature = reader.ReadStringToNull(4);
            Logger.Verbose($"Parsed signature {signature}");
            if (signature != "Blb\x03")
                throw new Exception("not a Blb3 file");

            var size = reader.ReadUInt32();
            m_Header = new BundleFile.Header
            {
                version = 6,
                unityVersion = "5.x.x",
                unityRevision = "2017.4.30f1",
                flags = 0
            };
            m_Header.compressedBlocksInfoSize = size;
            m_Header.uncompressedBlocksInfoSize = size;

            Logger.Verbose($"Header: {m_Header}");
            reader.ReadUInt32();
            Header = reader.ReadBytes(16);

            var header = reader.ReadBytes((int)m_Header.compressedBlocksInfoSize);

            Decrypt(Header, header);

            ReadBlocksInfoAndDirectory(header);
            using var blocksStream = CreateBlocksStream(path);
            ReadBlocks(reader, blocksStream);
            ReadFiles(blocksStream, path);
        }
        private static void OverrideBytes(IntPtr address, byte[] bytes, int length)
        {
            uint oldprotect;
            VirtualProtect(address, (uint)length, 0x40, out oldprotect);
            Marshal.Copy(bytes, 0, address, length);
            VirtualProtect(address, (uint)length, oldprotect, out oldprotect);
        }

        private void ReadBlocksInfoAndDirectory(byte[] header)
        {
            using var stream = new MemoryStream(header);
            using var reader = new EndianBinaryReader(stream, EndianType.LittleEndian);

            m_Header.size = reader.ReadUInt32();
            var lastUncompressedSize = reader.ReadUInt32();

            reader.Position += 4;
            var blobOffset = reader.ReadInt32();
            var blobSize = reader.ReadUInt32();
            var compressionType = (CompressionType)reader.ReadByte();
            var uncompressedSize = (uint)1 << reader.ReadByte();
            reader.AlignStream();

            var blocksInfoCount = reader.ReadInt32();
            var nodesCount = reader.ReadInt32();

            var blocksInfoOffset = reader.Position + reader.ReadInt64();
            var nodesInfoOffset = reader.Position + reader.ReadInt64();
            var flagInfoOffset = reader.Position + reader.ReadInt64();

            reader.Position = blocksInfoOffset;
            m_BlocksInfo = new List<BundleFile.StorageBlock>();
            Logger.Verbose($"Blocks count: {blocksInfoCount}");
            for (int i = 0; i < blocksInfoCount; i++)
            {
                m_BlocksInfo.Add(new BundleFile.StorageBlock
                {
                    compressedSize = reader.ReadUInt32(),
                    uncompressedSize = i == blocksInfoCount - 1 ? lastUncompressedSize : uncompressedSize,
                    flags = (StorageBlockFlags)compressionType
                });

                Logger.Verbose($"Block {i} Info: {m_BlocksInfo[i]}");
            }
            for (int i = m_BlocksInfo.Count - 1; i > 0; i--)
            {
                m_BlocksInfo[i].compressedSize -= m_BlocksInfo[i - 1].compressedSize;
                m_BlocksInfo[i].flags = (StorageBlockFlags)(m_BlocksInfo[i].compressedSize == m_BlocksInfo[i].uncompressedSize ? CompressionType.None : compressionType);
            }

            reader.Position = nodesInfoOffset;
            m_DirectoryInfo = new List<BundleFile.Node>();
            Logger.Verbose($"Directory count: {nodesCount}");
            for (int i = 0; i < nodesCount; i++)
            {
                m_DirectoryInfo.Add(new BundleFile.Node
                {
                    offset = reader.ReadInt32(),
                    size = reader.ReadInt32()
                });

                var pos = reader.Position;
                reader.Position = flagInfoOffset;
                var flag = reader.ReadUInt32();
                if (i >= 0x20)
                {
                    flag = reader.ReadUInt32();
                }
                m_DirectoryInfo[i].flags = (uint)(flag & (1 << i)) * 4;
                reader.Position = pos;

                var pathOffset = reader.Position + reader.ReadInt64();

                pos = reader.Position;
                reader.Position = pathOffset;
                m_DirectoryInfo[i].path = reader.ReadStringToNull();
                reader.Position = pos;

                Logger.Verbose($"Directory {i} Info: {m_DirectoryInfo[i]}");
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

        private void ReadBlocks(FileReader reader, Stream blocksStream)
        {
            foreach (var blockInfo in m_BlocksInfo)
            {
                var compressionType = (CompressionType)(blockInfo.flags & StorageBlockFlags.CompressionTypeMask);
                Logger.Verbose($"Block compression type {compressionType}");
                switch (compressionType) //kStorageBlockCompressionTypeMask
                {
                    case CompressionType.None: //None
                        {
                            var size = (int)blockInfo.uncompressedSize;
                            var buffer = reader.ReadBytes(size);
                            Decrypt(Header, buffer);
                            blocksStream.Write(buffer);
                            break;
                        }
                    case CompressionType.Oodle: //Oodle
                        {
                            var compressedSize = (int)blockInfo.compressedSize;
                            var uncompressedSize = (int)blockInfo.uncompressedSize;

                            var compressedBytes = ArrayPool<byte>.Shared.Rent(compressedSize);
                            var uncompressedBytes = ArrayPool<byte>.Shared.Rent(uncompressedSize);

                            var compressedBytesSpan = compressedBytes.AsSpan(0, compressedSize);
                            var uncompressedBytesSpan = uncompressedBytes.AsSpan(0, uncompressedSize);

                            try
                            {

                                reader.Read(compressedBytesSpan);
                                
                                if (compressedSize > 6)
                                    Decrypt(Header, compressedBytesSpan);

                                var numWrite = OozHelper.Decompress(compressedBytesSpan, uncompressedBytesSpan);
                                if (numWrite != uncompressedSize)
                                {
                                    Logger.Warning($"Oodle decompression error, write {numWrite} bytes but expected {uncompressedSize} bytes");
                                }
                            }
                            finally
                            {
                                blocksStream.Write(uncompressedBytesSpan);
                                ArrayPool<byte>.Shared.Return(compressedBytes, true);
                                ArrayPool<byte>.Shared.Return(uncompressedBytes, true);
                            }
                            break;
                        }
                    case CompressionType.Lzma: //LZMA
                        {
                            SevenZipHelper.StreamDecompress(reader.BaseStream, blocksStream, blockInfo.compressedSize, blockInfo.uncompressedSize);
                            break;
                        }
                    case CompressionType.Lz4: //LZ4
                    case CompressionType.Lz4HC: //LZ4HC
                        {
                            var compressedSize = (int)blockInfo.compressedSize;
                            var uncompressedSize = (int)blockInfo.uncompressedSize;

                            var compressedBytes = ArrayPool<byte>.Shared.Rent(compressedSize);
                            var uncompressedBytes = ArrayPool<byte>.Shared.Rent(uncompressedSize);

                            var compressedBytesSpan = compressedBytes.AsSpan(0, compressedSize);
                            var uncompressedBytesSpan = uncompressedBytes.AsSpan(0, uncompressedSize);

                            try
                            {

                                reader.Read(compressedBytesSpan);
                                Decrypt(Header, compressedBytesSpan);
                                var numWrite = LZ4.Instance.Decompress(compressedBytesSpan, uncompressedBytesSpan);
                                if (numWrite != uncompressedSize)
                                {
                                    Logger.Warning($"Lz4 decompression error, write {numWrite} bytes but expected {uncompressedSize} bytes");
                                }
                            }
                            catch (Exception e)
                            {
                                Logger.Error($"Lz4 decompression error {e.Message}");
                            }
                            finally
                            {
                                blocksStream.Write(uncompressedBytesSpan);
                                ArrayPool<byte>.Shared.Return(compressedBytes, true);
                                ArrayPool<byte>.Shared.Return(uncompressedBytes, true);
                            }
                            break;
                        }
                    default:
                        throw new IOException($"Unsupported compression type {compressionType}");
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
    }
}