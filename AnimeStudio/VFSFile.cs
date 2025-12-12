using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnimeStudio.Crypto;

namespace AnimeStudio
{
    public class VFSFile
    {
        private List<BundleFile.StorageBlock> m_BlocksInfo;
        private List<BundleFile.Node> m_DirectoryInfo;

        public BundleFile.Header m_Header;
        public List<StreamFile> fileList;
        public long Offset;

        public VFSFile(FileReader reader, string path)
        {
            Offset = reader.Position;
            reader.Endian = EndianType.BigEndian;

            if (!VFSUtils.IsValidHeader(reader))
            {
                throw new Exception("Not a VFS file");
            }

            // read header
            m_Header = VFSUtils.ReadHeader(reader);
            Logger.Verbose($"Header : {m_Header.ToString()}");

            // go to blocks info
            uint blockInfosOffset;

            if (((int)m_Header.flags & 0x80) != 0)
                blockInfosOffset = (uint)(m_Header.size) - m_Header.compressedBlocksInfoSize;
            else
            {
                if (m_Header.encFlags >= 7)
                    blockInfosOffset = 48;
                else
                    blockInfosOffset = 40;
            }

            reader.Position = Offset + blockInfosOffset;
            ReadBlocksInfoAndDirectory(reader);

            // go to data
            uint dataOffset;

            if (m_Header.encFlags >= 7)
                dataOffset = 48;
            else
                dataOffset = 40;
            if (((int)(m_Header.flags) & 0x80) == 0)
            {
                var temp = m_Header.compressedBlocksInfoSize;
                if (((int)(m_Header.flags) & 0x200) != 0)
                    temp = (temp + 15) & 0xFFFFFFF0;
                dataOffset += temp;
            }

            reader.Position = Offset + dataOffset;

            //
            using var blocksStream = CreateBlocksStream(path);
            ReadBlocks(reader, blocksStream);
            ReadFiles(blocksStream, path);
        }

        private void ReadBlocksInfoAndDirectory(FileReader reader)
        {
            byte[] blocksInfoBytes = reader.ReadBytes((int)m_Header.compressedBlocksInfoSize);

            MemoryStream blocksInfoUncompressedStream = new MemoryStream();
            if (((int)m_Header.flags & 0x3F) != 0)
            {
                // compressed + encrypted
                VFSUtils.DecryptBlock(blocksInfoBytes);

                var uncompressedSize = m_Header.uncompressedBlocksInfoSize;
                var blocksInfoBytesSpan = blocksInfoBytes.AsSpan(0, blocksInfoBytes.Length);
                var uncompressedBytes = ArrayPool<byte>.Shared.Rent((int)uncompressedSize);

                try
                {
                    var uncompressedBytesSpan = uncompressedBytes.AsSpan(0, (int)uncompressedSize);
                    // normal LZ4
                    var numWrite = LZ4.Instance.Decompress(blocksInfoBytesSpan, uncompressedBytesSpan);

                    if (numWrite != uncompressedSize)
                    {
                        throw new IOException($"Lz4 decompression error, write {numWrite} bytes but expected {uncompressedSize} bytes");
                    }
                    blocksInfoUncompressedStream = new MemoryStream(uncompressedBytesSpan.ToArray());
                } catch (Exception e)
                {
                    throw new IOException($"Lz4 decompression error {e.Message}");
                } finally
                {
                    ArrayPool<byte>.Shared.Return(uncompressedBytes);
                }
            } else
            {
                blocksInfoUncompressedStream = new MemoryStream(blocksInfoBytes);
            }

            // read
            using (var blocksInfoReader = new EndianBinaryReader(blocksInfoUncompressedStream))
            {
                reader.Endian = EndianType.BigEndian;
                m_BlocksInfo = VFSUtils.ReadBlocksInfos(blocksInfoReader);
                m_DirectoryInfo = VFSUtils.ReadDirectoryInfos(blocksInfoReader);
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
                var compressionType = (int)blockInfo.flags; // no mask
                Logger.Verbose($"Block compression type {compressionType}");

                switch (compressionType)
                {
                    case 0:
                        var size = (int)blockInfo.uncompressedSize;
                        var buffer = reader.ReadBytes(size);
                        blocksStream.Write(buffer);
                        break;
                    case 5:
                        var compressedSize = (int)blockInfo.compressedSize;
                        var uncompressedSize = (int)blockInfo.uncompressedSize;

                        var compressedBytes = ArrayPool<byte>.Shared.Rent(compressedSize);
                        var uncompressedBytes = ArrayPool<byte>.Shared.Rent(uncompressedSize);

                        var compressedBytesSpan = compressedBytes.AsSpan(0, compressedSize);
                        var uncompressedBytesSpan = uncompressedBytes.AsSpan(0, uncompressedSize);

                        try
                        {
                            reader.Read(compressedBytesSpan);

                            VFSUtils.DecryptBlock(compressedBytesSpan);

                            // LZ4Inv this time
                            var numWrite = LZ4Inv.Instance.Decompress(compressedBytesSpan, uncompressedBytesSpan);
                            if (numWrite != uncompressedSize)
                            {
                                Logger.Warning($"Lz4 decompression error, write {numWrite} bytes but expected {uncompressedSize} bytes");
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Error($"Lz4 decompression error : {e.Message}");
                        }
                        finally
                        {
                            blocksStream.Write(uncompressedBytesSpan);
                            ArrayPool<byte>.Shared.Return(compressedBytes, true);
                            ArrayPool<byte>.Shared.Return(uncompressedBytes, true);
                        }

                        break;
                    default:
                        throw new Exception($"Unsupported block compression type {compressionType}");
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
