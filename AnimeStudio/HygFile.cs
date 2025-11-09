using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp.PixelFormats;

namespace AnimeStudio
{
    public class HygFile
    {
        private List<BundleFile.StorageBlock> m_BlocksInfo;
        private List<BundleFile.Node> m_DirectoryInfo;

        public BundleFile.Header m_Header;
        public List<StreamFile> fileList;
        public long Offset;

        public HygFile(FileReader reader, string path) {
            Offset = reader.Position;
            reader.Endian = EndianType.BigEndian; // uses big endian

            var signature = reader.ReadBytes(7);
            Logger.Verbose($"Parsed signature {Convert.ToHexString(signature)}");
            if (!signature.SequenceEqual(new byte[] { 0xC3, 0x9C, 0xC3, 0xA3, 0xC3, 0x8A, 0x00 }))
                throw new Exception("not a Hyg file");

            ulong headerKey1 = reader.ReadUInt32();
            ulong headerKey2 = reader.ReadUInt64();
            var header = reader.ReadBytes(32);

            HygUtils.Decrypt(header, headerKey1, headerKey2, false); // descramble keys here

            m_Header = new BundleFile.Header
            {
                version = 6,
                unityVersion = "5.x.x",
                unityRevision = "2022.3.43f1",
            };

            using (var headerReader = new EndianBinaryReader(new MemoryStream(header)))
            {
                headerReader.Endian = EndianType.LittleEndian;
                long fileSize = headerReader.ReadInt64();
                m_Header.compressedBlocksInfoSize = headerReader.ReadUInt32();
                m_Header.uncompressedBlocksInfoSize = headerReader.ReadUInt32();
                m_Header.flags = (ArchiveFlags)headerReader.ReadUInt32();
            }

            Logger.Verbose($"Header : {m_Header.ToString()}");
            reader.AlignStream(16);

            ReadBlocksInfoAndDirectory(reader);
            reader.AlignStream(16);
            using var blocksStream = CreateBlocksStream(path);
            ReadBlocks(reader, blocksStream);
            ReadFiles(blocksStream, path);
        }

        private void ReadBlocksInfoAndDirectory(FileReader reader)
        {
            byte [] blocksInfoBytes = reader.ReadBytes((int)m_Header.compressedBlocksInfoSize);
            
            // decrypt
            HygUtils.Decrypt(blocksInfoBytes, m_Header.compressedBlocksInfoSize, m_Header.uncompressedBlocksInfoSize);

            MemoryStream blocksInfoUncompressedStream = new MemoryStream();
            var blocksInfoBytesSpan = blocksInfoBytes.AsSpan(0, blocksInfoBytes.Length);
            var uncompressedSize = m_Header.uncompressedBlocksInfoSize;
            var compressionType = CompressionType.Lz4; // flags are lying
            Logger.Verbose($"BlockInfo compression type: {compressionType}");

            switch (compressionType)
            {
                case CompressionType.None:
                {
                    blocksInfoUncompressedStream = new MemoryStream(blocksInfoBytes);
                    break;
                }
                case CompressionType.Lz4:
                case CompressionType.Lz4HC:
                {
                    var uncompressedBytes = ArrayPool<byte>.Shared.Rent((int)uncompressedSize);
                    try
                    {
                        var uncompressedBytesSpan = uncompressedBytes.AsSpan(0, (int)uncompressedSize);
                        var numWrite = LZ4.Instance.Decompress(blocksInfoBytesSpan, uncompressedBytesSpan);
                        if (numWrite != uncompressedSize)
                        {
                            throw new IOException($"Lz4 decompression error, write {numWrite} bytes but expected {uncompressedSize} bytes");
                        }
                        blocksInfoUncompressedStream = new MemoryStream(uncompressedBytesSpan.ToArray());
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(uncompressedBytes);
                    }
                    break;
                }
            }

            using (var blocksInfoReader = new EndianBinaryReader(blocksInfoUncompressedStream))
            {
                blocksInfoReader.ReadBytes(16); // skip 16
                blocksInfoReader.Endian = EndianType.BigEndian; // back to big

                var blocksInfoCount = blocksInfoReader.ReadInt32();
                m_BlocksInfo = new List<BundleFile.StorageBlock>();
                Logger.Verbose($"Blocks count: {blocksInfoCount}");
                for (int i = 0; i < blocksInfoCount; i++)
                {
                    m_BlocksInfo.Add(new BundleFile.StorageBlock
                    {
                        uncompressedSize = blocksInfoReader.ReadUInt32(),
                        compressedSize = blocksInfoReader.ReadUInt32(),
                        flags = (StorageBlockFlags)blocksInfoReader.ReadUInt16()
                    });

                    Logger.Verbose($"Block {i} Info: {m_BlocksInfo[i]}");
                }

                var nodesCount = blocksInfoReader.ReadInt32();
                m_DirectoryInfo = new List<BundleFile.Node>();
                Logger.Verbose($"Directory count: {nodesCount}");
                for (int i = 0; i < nodesCount; i++)
                {
                    m_DirectoryInfo.Add(new BundleFile.Node
                    {
                        offset = blocksInfoReader.ReadInt64(),
                        size = blocksInfoReader.ReadInt64(),
                        flags = blocksInfoReader.ReadUInt32(),
                        path = blocksInfoReader.ReadStringToNull(),
                    });

                    Logger.Verbose($"Directory {i} Info: {m_DirectoryInfo[i]}");
                }
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
                var compressionType = (CompressionType)(blockInfo.flags); // no mask
                Logger.Verbose($"Block compression type {compressionType}");

                switch (compressionType)
                {
                    case CompressionType.None: //None
                        {
                            var size = (int)blockInfo.uncompressedSize;
                            var buffer = reader.ReadBytes(size);
                            blocksStream.Write(buffer);
                            break;
                        }
                    case CompressionType.Lz4Mr0k: // uses this flag despite not being that
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
                                
                                // only flag=5 blocks are encrypted
                                if ((int)blockInfo.flags == 5)
                                {
                                    HygUtils.Decrypt(compressedBytesSpan, (ulong)compressedSize, (ulong)uncompressedSize);
                                }

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
