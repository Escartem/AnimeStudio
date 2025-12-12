using ZstdSharp;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Buffers;

namespace AnimeStudio
{
    [Flags]
    public enum ArchiveFlags
    {
        CompressionTypeMask = 0x3f,
        BlocksAndDirectoryInfoCombined = 0x40,
        BlocksInfoAtTheEnd = 0x80,
        OldWebPluginCompatibility = 0x100,
        BlockInfoNeedPaddingAtStart = 0x200,
        UnityCNEncryption = 0x400,
        UnityCNEncryption2 = 0x1000
    }

    [Flags]
    public enum StorageBlockFlags
    {
        CompressionTypeMask = 0x3f,
        Streamed = 0x40,
    }

    public enum CompressionType
    {
        None,
        Lzma,
        Lz4,
        Lz4HC,
        Lzham,
        Lz4Mr0k,
        Lz4Inv = 5,
        Zstd = 5,
        Lz4Lit4 = 4,
        Lz4Lit5 = 5,
        OodleHSR = 6,
        OodleMr0k = 7,
        Oodle = 9,
    }

    public class BundleFile
    {
        public class Header
        {
            public string signature;
            public uint version;
            public string unityVersion;
            public string unityRevision;
            public long size;
            public uint compressedBlocksInfoSize;
            public uint uncompressedBlocksInfoSize;
            public ArchiveFlags flags;
            public uint encFlags;

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.Append($"signature: {signature} | ");
                sb.Append($"version: {version} | ");
                sb.Append($"unityVersion: {unityVersion} | ");
                sb.Append($"unityRevision: {unityRevision} | ");
                sb.Append($"size: 0x{size:X8} | ");
                sb.Append($"compressedBlocksInfoSize: 0x{compressedBlocksInfoSize:X8} | ");
                sb.Append($"uncompressedBlocksInfoSize: 0x{uncompressedBlocksInfoSize:X8} | ");
                sb.Append($"flags: 0x{(int)flags:X8}");
                return sb.ToString();
            }
        }

        public class StorageBlock
        {
            public uint compressedSize;
            public uint uncompressedSize;
            public StorageBlockFlags flags;

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.Append($"compressedSize: 0x{compressedSize:X8} | ");
                sb.Append($"uncompressedSize: 0x{uncompressedSize:X8} | ");
                sb.Append($"flags: 0x{(int)flags:X8}");
                return sb.ToString();
            }
        }

        public class Node
        {
            public long offset;
            public long size;
            public uint flags;
            public string path;

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.Append($"offset: 0x{offset:X8} | ");
                sb.Append($"size: 0x{size:X8} | ");
                sb.Append($"flags: {flags} | ");
                sb.Append($"path: {path}");
                return sb.ToString();
            }
        }

        private Game Game;
        private UnityCN UnityCN;

        public Header m_Header;
        private List<Node> m_DirectoryInfo;
        private List<StorageBlock> m_BlocksInfo;

        public List<StreamFile> fileList;
        
        private bool HasUncompressedDataHash = true;
        private bool HasBlockInfoNeedPaddingAtStart = true;

        public BundleFile(FileReader reader, Game game)
        {
            Game = game;
            m_Header = ReadBundleHeader(reader);
            switch (m_Header.signature)
            {
                case "UnityArchive":
                    break; //TODO
                case "UnityWeb":
                case "UnityRaw":
                    if (m_Header.version == 6)
                    {
                        goto case "UnityFS";
                    }
                    ReadHeaderAndBlocksInfo(reader);
                    using (var blocksStream = CreateBlocksStream(reader.FullPath))
                    {
                        ReadBlocksAndDirectory(reader, blocksStream);
                        ReadFiles(blocksStream, reader.FullPath);
                    }
                    break;
                case "UnityFS":
                case "ENCR":
                    ReadHeader(reader);
                    if (game.IsUnityCN())
                    {
                        ReadUnityCN(reader);
                    }
                    ReadBlocksInfoAndDirectory(reader);
                    using (var blocksStream = CreateBlocksStream(reader.FullPath))
                    {
                        ReadBlocks(reader, blocksStream);
                        ReadFiles(blocksStream, reader.FullPath);
                    }
                    break;
            }
        }

        private Header ReadBundleHeader(FileReader reader)
        {
            Header header = new Header();
            header.signature = reader.ReadStringToNull(20);
            Logger.Verbose($"Parsed signature {header.signature}");
            switch (header.signature)
            {
                case "UnityFS":
                    if (Game.Type.IsBH3Group() || Game.Type.IsBH3PrePre())
                    {
                        if (Game.Type.IsBH3Group())
                        {
                            var key = reader.ReadUInt32();
                            if (key <= 11)
                            {
                                reader.Position -= 4;
                                goto default;
                            }
                            Logger.Verbose($"Encrypted bundle header with key {key}");
                            XORShift128.InitSeed(key);
                        }
                        else if (Game.Type.IsBH3PrePre())
                        {
                            Logger.Verbose($"Encrypted bundle header with key {reader.Length}");
                            XORShift128.InitSeed((uint)reader.Length);
                        }

                        header.version = 6;
                        header.unityVersion = "5.x.x";
                        header.unityRevision = "2017.4.18f1";
                    }
                    else
                    {
                        header.version = reader.ReadUInt32();
                        header.unityVersion = reader.ReadStringToNull();
                        header.unityRevision = reader.ReadStringToNull();
                    }
                    break;
                case "ENCR":
                    header.version = 6; // is 7 but does not have uncompressedDataHash
                    header.unityVersion = "5.x.x";
                    header.unityRevision = "2019.4.32f1";
                    HasUncompressedDataHash = false;
                    break;
                default:
                    if (Game.Type.IsNaraka())
                    {
                        header.signature = "UnityFS";
                        goto case "UnityFS";
                    }
                    header.version = reader.ReadUInt32();
                    header.unityVersion = reader.ReadStringToNull();
                    header.unityRevision = reader.ReadStringToNull();
                    break;

            }

            return header;
        }

        private void ReadHeaderAndBlocksInfo(FileReader reader)
        {
            if (m_Header.version >= 4)
            {
                var hash = reader.ReadBytes(16);
                var crc = reader.ReadUInt32();
            }
            var minimumStreamedBytes = reader.ReadUInt32();
            m_Header.size = reader.ReadUInt32();
            var numberOfLevelsToDownloadBeforeStreaming = reader.ReadUInt32();
            var levelCount = reader.ReadInt32();
            m_BlocksInfo = new List<StorageBlock>();
            for (int i = 0; i < levelCount; i++)
            {
                var storageBlock = new StorageBlock()
                {
                    compressedSize = reader.ReadUInt32(),
                    uncompressedSize = reader.ReadUInt32(),
                };
                if (i == levelCount - 1)
                {
                    m_BlocksInfo.Add(storageBlock);
                }
            }
            if (m_Header.version >= 2)
            {
                var completeFileSize = reader.ReadUInt32();
            }
            if (m_Header.version >= 3)
            {
                var fileInfoHeaderSize = reader.ReadUInt32();
            }
            reader.Position = m_Header.size;
        }

        private Stream CreateBlocksStream(string path)
        {
            Stream blocksStream;
            var uncompressedSizeSum = m_BlocksInfo.Sum(x => x.uncompressedSize);
            Logger.Verbose($"Total size of decompressed blocks: {uncompressedSizeSum}");
            if (uncompressedSizeSum >= int.MaxValue)
            {
                /*var memoryMappedFile = MemoryMappedFile.CreateNew(null, uncompressedSizeSum);
                assetsDataStream = memoryMappedFile.CreateViewStream();*/
                blocksStream = new FileStream(path + ".temp", FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
            }
            else
            {
                blocksStream = new MemoryStream((int)uncompressedSizeSum);
            }
            return blocksStream;
        }

        private void ReadBlocksAndDirectory(FileReader reader, Stream blocksStream)
        {
            Logger.Verbose($"Writing block and directory to blocks stream...");

            var isCompressed = m_Header.signature == "UnityWeb";
            foreach (var blockInfo in m_BlocksInfo)
            {
                var uncompressedBytes = reader.ReadBytes((int)blockInfo.compressedSize);
                if (isCompressed)
                {
                    using var memoryStream = new MemoryStream(uncompressedBytes);
                    using var decompressStream = SevenZipHelper.StreamDecompress(memoryStream);
                    uncompressedBytes = decompressStream.ToArray();
                }
                blocksStream.Write(uncompressedBytes, 0, uncompressedBytes.Length);
            }
            blocksStream.Position = 0;
            var blocksReader = new EndianBinaryReader(blocksStream);
            var nodesCount = blocksReader.ReadInt32();
            m_DirectoryInfo = new List<Node>();
            Logger.Verbose($"Directory count: {nodesCount}");
            for (int i = 0; i < nodesCount; i++)
            {
                m_DirectoryInfo.Add(new Node
                {
                    path = blocksReader.ReadStringToNull(),
                    offset = blocksReader.ReadUInt32(),
                    size = blocksReader.ReadUInt32()
                });
            }
        }

        public void ReadFiles(Stream blocksStream, string path)
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
                    /*var memoryMappedFile = MemoryMappedFile.CreateNew(null, entryinfo_size);
                    file.stream = memoryMappedFile.CreateViewStream();*/
                    var extractPath = path + "_unpacked" + Path.DirectorySeparatorChar;
                    Directory.CreateDirectory(extractPath);
                    file.stream = new FileStream(extractPath + file.fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                }
                else
                {
                    file.stream = new MemoryStream((int)node.size);
                }
                blocksStream.Position = node.offset;
                blocksStream.CopyTo(file.stream, node.size);
                file.stream.Position = 0;
            }
        }

        private void ReadHeader(FileReader reader)
        {
            if (XORShift128.Init)
            {
                if (Game.Type.IsBH3PrePre())
                {
                    m_Header.uncompressedBlocksInfoSize = reader.ReadUInt32() ^ XORShift128.NextDecryptUInt();
                    m_Header.compressedBlocksInfoSize = reader.ReadUInt32() ^ XORShift128.NextDecryptUInt();
                    m_Header.flags = (ArchiveFlags)(reader.ReadUInt32() ^ XORShift128.NextDecryptInt());
                    m_Header.size = reader.ReadInt64() ^ XORShift128.NextDecryptLong();
                    reader.ReadUInt32(); // version
                }
                else
                {
                    m_Header.flags = (ArchiveFlags)(reader.ReadUInt32() ^ XORShift128.NextDecryptInt());
                    m_Header.size = reader.ReadInt64() ^ XORShift128.NextDecryptLong();
                    m_Header.uncompressedBlocksInfoSize = reader.ReadUInt32() ^ XORShift128.NextDecryptUInt();
                    m_Header.compressedBlocksInfoSize = reader.ReadUInt32() ^ XORShift128.NextDecryptUInt();
                }

                XORShift128.Init = false;
                Logger.Verbose($"Bundle header decrypted");
               
                var encUnityVersion = reader.ReadStringToNull();
                var encUnityRevision = reader.ReadStringToNull();
                return;
            }

            m_Header.size = reader.ReadInt64();
            m_Header.compressedBlocksInfoSize = reader.ReadUInt32();
            m_Header.uncompressedBlocksInfoSize = reader.ReadUInt32();
            m_Header.flags = (ArchiveFlags)reader.ReadUInt32();
            if (m_Header.signature != "UnityFS" && !Game.Type.IsSRGroup())
            {
                reader.ReadByte();
            }

            if (Game.Type.IsNaraka())
            {
                m_Header.compressedBlocksInfoSize -= 0xCA;
                m_Header.uncompressedBlocksInfoSize -= 0xCA;
            }

            Logger.Verbose($"Bundle header Info: {m_Header}");
        }

        private void ReadUnityCN(FileReader reader)
        {
            Logger.Verbose($"Attempting to decrypt file {reader.FileName} with UnityCN encryption");
            ArchiveFlags mask;

            var version = ParseVersion();
            //Flag changed it in these versions
            if (version[0] < 2020 || //2020 and earlier
                (version[0] == 2020 && version[1] == 3 && version[2] <= 34) || //2020.3.34 and earlier
                (version[0] == 2021 && version[1] == 3 && version[2] <= 2) || //2021.3.2 and earlier
                (version[0] == 2022 && version[1] == 3 && version[2] <= 1)) //2022.3.1 and earlier
            {
                mask = ArchiveFlags.BlockInfoNeedPaddingAtStart;
                HasBlockInfoNeedPaddingAtStart = false;
            }
            else
            {
                mask = ArchiveFlags.UnityCNEncryption;
                HasBlockInfoNeedPaddingAtStart = true;
            }

            Logger.Verbose($"Mask set to {mask}");

            if ((m_Header.flags & mask) != 0 || (m_Header.flags & ArchiveFlags.UnityCNEncryption2) != 0)
            {
                Logger.Verbose($"Encryption flag exist, file is encrypted, attempting to decrypt");
                UnityCN = new UnityCN(reader);
            }
        }

        private void ReadBlocksInfoAndDirectory(FileReader reader)
        {
            byte[] blocksInfoBytes;
            if (m_Header.version >= 7 && !Game.Type.IsSRGroup())
            {
                reader.AlignStream(16);
            }
            if ((m_Header.flags & ArchiveFlags.BlocksInfoAtTheEnd) != 0) //kArchiveBlocksInfoAtTheEnd
            {
                var position = reader.Position;
                reader.Position = reader.BaseStream.Length - m_Header.compressedBlocksInfoSize;
                blocksInfoBytes = reader.ReadBytes((int)m_Header.compressedBlocksInfoSize);
                reader.Position = position;
            }
            else //0x40 BlocksAndDirectoryInfoCombined
            {
                blocksInfoBytes = reader.ReadBytes((int)m_Header.compressedBlocksInfoSize);
            }
            MemoryStream blocksInfoUncompresseddStream;
            var blocksInfoBytesSpan = blocksInfoBytes.AsSpan(0, (int)m_Header.compressedBlocksInfoSize);
            var uncompressedSize = m_Header.uncompressedBlocksInfoSize;
            var compressionType = (CompressionType)(m_Header.flags & ArchiveFlags.CompressionTypeMask);
            Logger.Verbose($"BlockInfo compression type: {compressionType}");
            switch (compressionType) //kArchiveCompressionTypeMask
            {
                case CompressionType.None: //None
                    {
                        blocksInfoUncompresseddStream = new MemoryStream(blocksInfoBytes);
                        break;
                    }
                case CompressionType.Lzma: //LZMA
                    {
                        blocksInfoUncompresseddStream = new MemoryStream((int)(uncompressedSize));
                        using (var blocksInfoCompressedStream = new MemoryStream(blocksInfoBytes))
                        {
                            SevenZipHelper.StreamDecompress(blocksInfoCompressedStream, blocksInfoUncompresseddStream, m_Header.compressedBlocksInfoSize, m_Header.uncompressedBlocksInfoSize);
                        }
                        blocksInfoUncompresseddStream.Position = 0;
                        break;
                    }
                case CompressionType.Lz4: //LZ4
                case CompressionType.Lz4HC: //LZ4HC
                    {
                        var uncompressedBytes = ArrayPool<byte>.Shared.Rent((int)uncompressedSize);
                        try
                        {
                            var uncompressedBytesSpan = uncompressedBytes.AsSpan(0, (int)uncompressedSize);
                            if (Game.Type.IsPerpetualNovelty())
                            {
                                var key = blocksInfoBytesSpan[1];
                                for (int j = 0; j < Math.Min(0x32, blocksInfoBytesSpan.Length); j++)
                                {
                                    blocksInfoBytesSpan[j] ^= key;
                                }
                            }
                            var numWrite = LZ4.Instance.Decompress(blocksInfoBytesSpan, uncompressedBytesSpan);
                            if (numWrite != uncompressedSize)
                            {
                                throw new IOException($"Lz4 decompression error, write {numWrite} bytes but expected {uncompressedSize} bytes");
                            }
                            blocksInfoUncompresseddStream = new MemoryStream(uncompressedBytesSpan.ToArray());
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(uncompressedBytes, true);
                        }
                        break;
                    }
                case CompressionType.Lz4Mr0k: //Lz4Mr0k
                    if (Mr0kUtils.IsMr0k(blocksInfoBytesSpan))
                    {
                        Logger.Verbose($"Header encrypted with mr0k, decrypting...");
                        blocksInfoBytesSpan = Mr0kUtils.Decrypt(blocksInfoBytesSpan, (Mr0k)Game).ToArray();
                    }
                    goto case CompressionType.Lz4HC;
                default:
                    throw new IOException($"Unsupported compression type {compressionType}");
            }
            using (var blocksInfoReader = new EndianBinaryReader(blocksInfoUncompresseddStream))
            {
                if (HasUncompressedDataHash)
                {
                    var uncompressedDataHash = blocksInfoReader.ReadBytes(16);
                }
                var blocksInfoCount = blocksInfoReader.ReadInt32();
                m_BlocksInfo = new List<StorageBlock>();
                Logger.Verbose($"Blocks count: {blocksInfoCount}");
                for (int i = 0; i < blocksInfoCount; i++)
                {
                    m_BlocksInfo.Add(new StorageBlock
                    {
                        uncompressedSize = blocksInfoReader.ReadUInt32(),
                        compressedSize = blocksInfoReader.ReadUInt32(),
                        flags = (StorageBlockFlags)blocksInfoReader.ReadUInt16()
                    });

                    Logger.Verbose($"Block {i} Info: {m_BlocksInfo[i]}");
                }

                var nodesCount = blocksInfoReader.ReadInt32();
                m_DirectoryInfo = new List<Node>();
                Logger.Verbose($"Directory count: {nodesCount}");
                for (int i = 0; i < nodesCount; i++)
                {
                    m_DirectoryInfo.Add(new Node
                    {
                        offset = blocksInfoReader.ReadInt64(),
                        size = blocksInfoReader.ReadInt64(),
                        flags = blocksInfoReader.ReadUInt32(),
                        path = blocksInfoReader.ReadStringToNull(),
                    });

                    Logger.Verbose($"Directory {i} Info: {m_DirectoryInfo[i]}");
                }
            }
            if (HasBlockInfoNeedPaddingAtStart && (m_Header.flags & ArchiveFlags.BlockInfoNeedPaddingAtStart) != 0)
            {
                reader.AlignStream(16);
            }
        }

        private void ReadBlocks(FileReader reader, Stream blocksStream)
        {
            Logger.Verbose($"Writing block to blocks stream...");

            byte[] firstBlockBytes = new byte[0];
            Span<byte> firstBlockSpan = Span<byte>.Empty;

            for (int i = 0; i < m_BlocksInfo.Count; i++)
            {
                Logger.Verbose($"Reading block {i}...");
                var blockInfo = m_BlocksInfo[i];
                var compressionType = (CompressionType)(blockInfo.flags & StorageBlockFlags.CompressionTypeMask);
                Logger.Verbose($"Block compression type {compressionType}");
                switch (compressionType) //kStorageBlockCompressionTypeMask
                {
                    case CompressionType.None: //None
                        {
                            reader.BaseStream.CopyTo(blocksStream, blockInfo.compressedSize);
                            break;
                        }
                    case CompressionType.Lzma: //LZMA
                        {
                            var compressedStream = reader.BaseStream;
                            if (Game.Type.IsNetEase() && i == 0)
                            {
                                var compressedBytesSpan = reader.ReadBytes((int)blockInfo.compressedSize).AsSpan();
                                NetEaseUtils.DecryptWithoutHeader(compressedBytesSpan);
                                var ms = new MemoryStream(compressedBytesSpan.ToArray());
                                compressedStream = ms;
                            }
                            SevenZipHelper.StreamDecompress(compressedStream, blocksStream, blockInfo.compressedSize, blockInfo.uncompressedSize);
                            break;
                        }
                    case CompressionType.OodleHSR:
                    case CompressionType.OodleMr0k:
                        {
                            // Star Rail v2.7 fix, thanks to Yarik
                            var compressedSize = (int)blockInfo.compressedSize;
                            var uncompressedSize = (int)blockInfo.uncompressedSize;

                            var compressedBytes = ArrayPool<byte>.Shared.Rent(compressedSize);
                            var uncompressedBytes = ArrayPool<byte>.Shared.Rent(uncompressedSize);

                            var compressedBytesSpan = compressedBytes.AsSpan(0, compressedSize);
                            var uncompressedBytesSpan = uncompressedBytes.AsSpan(0, uncompressedSize);

                            try
                            {
                                reader.Read(compressedBytesSpan);
                                if (compressionType == CompressionType.OodleMr0k && Mr0kUtils.IsMr0k(compressedBytes))
                                {
                                    Logger.Verbose($"Block encrypted with mr0k, decrypting...");
                                    compressedBytesSpan = Mr0kUtils.Decrypt(compressedBytesSpan, (Mr0k)Game);
                                }

                                var numWrite = OodleHelper.Decompress(compressedBytesSpan, uncompressedBytesSpan);
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
                    case CompressionType.Lz4: //LZ4
                    case CompressionType.Lz4HC: //LZ4HC
                    case CompressionType.Lz4Mr0k when Game.Type.IsMhyGroup(): //Lz4Mr0k
                        {
                            var compressedSize = (int)blockInfo.compressedSize;
                            var uncompressedSize = (int)blockInfo.uncompressedSize;

                            var compressedBytes = ArrayPool<byte>.Shared.Rent(compressedSize);
                            var uncompressedBytes = ArrayPool<byte>.Shared.Rent(uncompressedSize);

                            try
                            {
                                var compressedBytesSpan = compressedBytes.AsSpan(0, compressedSize);
                                var uncompressedBytesSpan = uncompressedBytes.AsSpan(0, uncompressedSize);

                                reader.Read(compressedBytesSpan);

                                if (Game.Type.IsHNACB1())
                                {
                                    if (i == 0)
                                    {
                                        // if first block, decrypt
                                        WmvUtils.Decrypt(compressedBytesSpan);
                                    }
                                    else
                                    {
                                        // else, xor with first one
                                        for (int j = 0; j < compressedBytesSpan.Length; j++)
                                        {
                                            compressedBytesSpan[j] ^= firstBlockSpan[j % firstBlockSpan.Length];
                                        }
                                    }
                                }
                                
                                if (compressionType == CompressionType.Lz4Mr0k && Mr0kUtils.IsMr0k(compressedBytes))
                                {
                                    Logger.Verbose($"Block encrypted with mr0k, decrypting...");
                                    compressedBytesSpan = Mr0kUtils.Decrypt(compressedBytesSpan, (Mr0k)Game);
                                }
                                if (Game.IsUnityCN() && ((int)blockInfo.flags & 0x100) != 0)
                                {
                                    Logger.Verbose($"Decrypting block with UnityCN...");
                                    UnityCN.DecryptBlock(compressedBytes, compressedSize, i);
                                }
                                if (Game.Type.IsNetEase() && i == 0)
                                {
                                    NetEaseUtils.DecryptWithHeader(compressedBytesSpan);
                                }
                                if (Game.Type.IsArknightsEndfield() && i == 0 && compressedBytesSpan[..32].Count((byte)0xa6) > 5)
                                {
                                    FairGuardUtils.Decrypt(compressedBytesSpan);
                                }
                                if (Game.Type.IsOPFP())
                                {
                                    OPFPUtils.Decrypt(compressedBytesSpan, reader.FullPath);
                                }
                                var numWrite = LZ4.Instance.Decompress(compressedBytesSpan, uncompressedBytesSpan);
                                if (numWrite != uncompressedSize)
                                {
                                    throw new IOException($"Lz4 decompression error, write {numWrite} bytes but expected {uncompressedSize} bytes");
                                }

                                if (Game.Type.IsHNACB1() && i == 0)
                                {
                                    firstBlockBytes = ArrayPool<byte>.Shared.Rent(uncompressedSize);
                                    firstBlockSpan = firstBlockBytes.AsSpan(0, uncompressedSize);
                                    uncompressedBytesSpan.CopyTo(firstBlockSpan);
                                }

                                blocksStream.Write(uncompressedBytesSpan);
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(compressedBytes, true);
                                ArrayPool<byte>.Shared.Return(uncompressedBytes, true);
                            }
                            break;
                        }
                    case CompressionType.Lz4Inv when Game.Type.IsArknightsEndfield():
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
                                if (i == 0 && compressedBytesSpan[..32].Count((byte)0xa6) > 5)
                                {
                                    FairGuardUtils.Decrypt(compressedBytesSpan);
                                }

                                var numWrite = LZ4Inv.Instance.Decompress(compressedBytesSpan, uncompressedBytesSpan);
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
                            break;
                        }
                    case CompressionType.Lz4Lit4 or CompressionType.Lz4Lit5 when Game.Type.IsExAstris():
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
                                var numWrite = LZ4Lit.Instance.Decompress(compressedBytesSpan, uncompressedBytesSpan);
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
                            break;
                        }
                    case CompressionType.Zstd when !Game.Type.IsMhyGroup(): //Zstd
                        {
                            var compressedSize = (int)blockInfo.compressedSize;
                            var uncompressedSize = (int)blockInfo.uncompressedSize;

                            var compressedBytes = ArrayPool<byte>.Shared.Rent(compressedSize);
                            var uncompressedBytes = ArrayPool<byte>.Shared.Rent(uncompressedSize);

                            try
                            {
                                reader.Read(compressedBytes, 0, compressedSize);
                                using var decompressor = new Decompressor();
                                var numWrite = decompressor.Unwrap(compressedBytes, 0, compressedSize, uncompressedBytes, 0, uncompressedSize);
                                if (numWrite != uncompressedSize)
                                {
                                    throw new IOException($"Zstd decompression error, write {numWrite} bytes but expected {uncompressedSize} bytes");
                                }
                                blocksStream.Write(uncompressedBytes.ToArray(), 0, uncompressedSize);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Zstd decompression error:\n{ex}");
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(compressedBytes, true);
                                ArrayPool<byte>.Shared.Return(uncompressedBytes, true);
                            }
                            break;
                        }
                    case CompressionType.Lz4Lit4 or CompressionType.Lz4Lit5 when Game.Type.IsArknights():
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
                                var numWrite = LZ4Ak.Instance.Decompress(compressedBytesSpan, uncompressedBytesSpan);
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
                            break;
                        }
                    default:
                        throw new IOException($"Unsupported compression type {compressionType}");
                }
            }
            ArrayPool<byte>.Shared.Return(firstBlockBytes, true);
            blocksStream.Position = 0;
        }

        public int[] ParseVersion()
        {
            var versionSplit = Regex.Replace(m_Header.unityRevision, @"\D", ".").Split(new[] { "." }, StringSplitOptions.RemoveEmptyEntries);
            return versionSplit.Select(int.Parse).ToArray();
        }
    }
}
