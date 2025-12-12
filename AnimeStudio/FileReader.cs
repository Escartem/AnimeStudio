using System;
using System.IO;
using System.Linq;
using AnimeStudio.Crypto;
using static AnimeStudio.ImportHelper;

namespace AnimeStudio
{
    public class FileReader : EndianBinaryReader
    {
        public string FullPath;
        public string FileName;
        public FileType FileType;

        private static readonly byte[] gzipMagic = { 0x1f, 0x8b };
        private static readonly byte[] brotliMagic = { 0x62, 0x72, 0x6F, 0x74, 0x6C, 0x69 };
        private static readonly byte[] zipMagic = { 0x50, 0x4B, 0x03, 0x04 };
        private static readonly byte[] zipSpannedMagic = { 0x50, 0x4B, 0x07, 0x08 };
        private static readonly byte[] mhy0Magic = { 0x6D, 0x68, 0x79, 0x30 };
        private static readonly byte[] mhy1Magic = { 0x6D, 0x68, 0x79, 0x31 };
        private static readonly byte[] blb2Magic = { 0x42, 0x6C, 0x62, 0x02 };
        private static readonly byte[] blb3Magic = { 0x42, 0x6C, 0x62, 0x03 };
        private static readonly byte[] narakaMagic = { 0x15, 0x1E, 0x1C, 0x0D, 0x0D, 0x23, 0x21 };
        private static readonly byte[] gunfireMagic = { 0x7C, 0x6D, 0x79, 0x72, 0x27, 0x7A, 0x73, 0x78, 0x3F };
        private static readonly byte[] hygMagic = { 0xC3, 0x9C, 0xC3, 0xA3, 0xC3, 0x8A, 0x00 };


        public FileReader(string path) : this(path, File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { }

        public FileReader(string path, Stream stream, bool leaveOpen = false) : base(stream, EndianType.BigEndian, leaveOpen)
        {
            FullPath = Path.GetFullPath(path);
            FileName = Path.GetFileName(path);
            FileType = CheckFileType();
            Logger.Verbose($"File {path} type is {FileType}");
        }

        private FileType CheckFileType()
        {
            var signature = this.ReadStringToNull(20);
            Position = 0;
            Logger.Verbose($"Parsed signature is {signature}");
            switch (signature)
            {
                case "UnityWeb":
                case "UnityRaw":
                case "UnityArchive":
                case "UnityFS":
                    return FileType.BundleFile;
                case "UnityWebData1.0":
                    return FileType.WebFile;
                case "blk":
                    return FileType.BlkFile;
                case "ENCR":
                    return FileType.ENCRFile;
                default:
                    {
                        Logger.Verbose("signature does not match any of the supported string signatures, attempting to check bytes signatures");
                        if (VFSUtils.IsValidHeader(this))
                        {
                            Logger.Verbose("File is VFS !!");
                            Position = 0;
                            return FileType.VFSFile;
                        }
                        Position = 0;
                        byte[] magic = ReadBytes(2);
                        Position = 0;
                        Logger.Verbose($"Parsed signature is {Convert.ToHexString(magic)}");
                        if (gzipMagic.SequenceEqual(magic))
                        {
                            return FileType.GZipFile;
                        }
                        Logger.Verbose($"Parsed signature does not match with expected signature {Convert.ToHexString(gzipMagic)}");
                        Position = 0x20;
                        magic = ReadBytes(6);
                        Position = 0;
                        Logger.Verbose($"Parsed signature is {Convert.ToHexString(magic)}");
                        if (brotliMagic.SequenceEqual(magic))
                        {
                            return FileType.BrotliFile;
                        }
                        Logger.Verbose($"Parsed signature does not match with expected signature {Convert.ToHexString(brotliMagic)}");
                        if (IsSerializedFile())
                        {
                            return FileType.AssetsFile;
                        }
                        magic = ReadBytes(4);
                        Position = 0;
                        Logger.Verbose($"Parsed signature is {Convert.ToHexString(magic)}");
                        if (zipMagic.SequenceEqual(magic) || zipSpannedMagic.SequenceEqual(magic))
                        {
                            return FileType.ZipFile;
                        }
                        Logger.Verbose($"Parsed signature does not match with expected signature {Convert.ToHexString(zipMagic)} or {Convert.ToHexString(zipSpannedMagic)}");
                        if (mhy0Magic.SequenceEqual(magic) || mhy1Magic.SequenceEqual(magic))
                        {
                            return FileType.MhyFile;
                        }
                        Logger.Verbose($"Parsed signature does not match with expected signature {Convert.ToHexString(mhy0Magic)} or {Convert.ToHexString(mhy1Magic)}");
                        if (blb2Magic.SequenceEqual(magic))
                        {
                            return FileType.Blb2File;
                        }
                        Logger.Verbose($"Parsed signature does not match with expected signature {Convert.ToHexString(blb2Magic)}");
                        if (blb3Magic.SequenceEqual(magic))
                        {
                            return FileType.Blb3File;
                        }
                        Logger.Verbose($"Parsed signature does not match with expected signature {Convert.ToHexString(blb3Magic)}");
                        magic = ReadBytes(7);
                        Position = 0;
                        Logger.Verbose($"Parsed signature is {Convert.ToHexString(magic)}");
                        if (hygMagic.SequenceEqual(magic))
                        {
                            return FileType.HygFile;
                        }
                        Logger.Verbose($"Parsed signature does not match with expected signature {Convert.ToHexString(hygMagic)}");
                        if (narakaMagic.SequenceEqual(magic))
                        {
                            return FileType.BundleFile;
                        }
                        Logger.Verbose($"Parsed signature does not match with expected signature {Convert.ToHexString(narakaMagic)}");
                        magic = ReadBytes(9);
                        Position = 0;
                        Logger.Verbose($"Parsed signature is {Convert.ToHexString(magic)}");
                        if (gunfireMagic.SequenceEqual(magic))
                        {
                            Position = 0x32;
                            return FileType.BundleFile;
                        }
                        Logger.Verbose($"Parsed signature does not match with expected signature {Convert.ToHexString(gunfireMagic)}");
                        Logger.Verbose($"Parsed signature does not match any of the supported signatures, assuming resource file");
                        return FileType.ResourceFile;
                    }
            }
        }

        private bool IsSerializedFile()
        {
            Logger.Verbose($"Attempting to check if the file is serialized file...");

            var fileSize = BaseStream.Length;
            if (fileSize < 20)
            {
                Logger.Verbose($"File size 0x{fileSize:X8} is too small, minimal acceptable size is 0x14, aborting...");
                return false;
            }
            var m_MetadataSize = ReadUInt32();
            long m_FileSize = ReadUInt32();
            var m_Version = ReadUInt32();
            long m_DataOffset = ReadUInt32();
            var m_Endianess = ReadByte();
            var m_Reserved = ReadBytes(3);
            if (m_Version >= 22)
            {
                if (fileSize < 48)
                {
                    Logger.Verbose($"File size 0x{fileSize:X8} for version {m_Version} is too small, minimal acceptable size is 0x30, aborting...");
                    Position = 0;
                    return false;
                }
                m_MetadataSize = ReadUInt32();
                m_FileSize = ReadInt64();
                m_DataOffset = ReadInt64();
            }
            Position = 0;
            if (m_FileSize != fileSize)
            {
                Logger.Verbose($"Parsed file size 0x{m_FileSize:X8} does not match stream size {fileSize}, file might be corrupted, aborting...");
                return false;
            }
            if (m_DataOffset > fileSize)
            {
                Logger.Verbose($"Parsed data offset 0x{m_DataOffset:X8} is outside the stream of the size {fileSize}, file might be corrupted, aborting...");
                return false;
            }
            Logger.Verbose($"Valid serialized file !!");
            return true;
        }
    }

    public static class FileReaderExtensions
    {
        public static FileReader PreProcessing(this FileReader reader, Game game)
        {
            Logger.Verbose($"Applying preprocessing to file {reader.FileName}");
            if (reader.FileType == FileType.ResourceFile || !game.Type.IsNormal())
            {
                Logger.Verbose("File is encrypted !!");
                switch (game.Type)
                {
                    case GameType.GI_Pack:
                        reader = DecryptPack(reader, game);
                        break;
                    case GameType.GI_CB1:
                        reader = DecryptMark(reader);
                        break;
                    case GameType.EnsembleStars:
                        reader = DecryptEnsembleStar(reader);
                        break;
                    case GameType.OPFP:
                    case GameType.FakeHeader:
                    case GameType.ShiningNikki:
                        reader = ParseFakeHeader(reader);
                        break;
                    case GameType.FantasyOfWind:
                        reader = DecryptFantasyOfWind(reader);
                        break;
                    case GameType.HelixWaltz2:
                        reader = ParseHelixWaltz2(reader);
                        break;
                    case GameType.AnchorPanic:
                        reader = DecryptAnchorPanic(reader);
                        break;
                    case GameType.DreamscapeAlbireo:
                        reader = DecryptDreamscapeAlbireo(reader);
                        break;
                    case GameType.ImaginaryFest:
                        reader = DecryptImaginaryFest(reader);
                        break;
                    case GameType.AliceGearAegis:
                        reader = DecryptAliceGearAegis(reader);
                        break;
                    case GameType.ProjectSekai:
                        reader = DecryptProjectSekai(reader);
                        break;
                    case GameType.CodenameJump:
                        reader = DecryptCodenameJump(reader);
                        break;
                    case GameType.GirlsFrontline:
                        reader = DecryptGirlsFrontline(reader);
                        break; 
                    case GameType.Reverse1999:
                        reader = DecryptReverse1999(reader);
                        break;
                    case GameType.JJKPhantomParade:
                        reader = DecryptJJKPhantomParade(reader);
                        break;
                    case GameType.MuvLuvDimensions:
                        reader = DecryptMuvLuvDimensions(reader);
                        break;
                    case GameType.PartyAnimals:
                        reader = DecryptPartyAnimals(reader);
                        break;
                    case GameType.LoveAndDeepspace:
                        reader = DecryptLoveAndDeepspace(reader);
                        break;
                    case GameType.SchoolGirlStrikers:
                        reader = DecryptSchoolGirlStrikers(reader);
                        break;
                }
            }
            if (reader.FileType == FileType.BundleFile && game.Type.IsBlockFile() || reader.FileType == FileType.ENCRFile || reader.FileType == FileType.Blb2File || reader.FileType == FileType.Blb3File || reader.FileType == FileType.HygFile || reader.FileType == FileType.VFSFile)
            {
                Logger.Verbose("File might have multiple bundles !!");
                try
                {
                    var signature = reader.ReadStringToNull();
                    reader.ReadInt32();
                    reader.ReadStringToNull();
                    reader.ReadStringToNull();
                    var size = reader.ReadInt64();
                    if (size != reader.BaseStream.Length)
                    {
                        Logger.Verbose($"Found signature {signature}, expected bundle size is 0x{size:X8}, found 0x{reader.BaseStream.Length} instead !!");
                        Logger.Verbose("Loading as block file !!");
                        reader.FileType = FileType.BlockFile;
                    }
                }
                catch (Exception) { }
                reader.Position = 0;
            }

            if (reader.FileType == FileType.MhyFile && (game.Type.IsZZZCB2() || game.Type.IsZZZ()))
            {
                reader.FileType = FileType.BlockFile;
            }

            Logger.Verbose("No preprocessing is needed");
            return reader;
        }
    } 
}
