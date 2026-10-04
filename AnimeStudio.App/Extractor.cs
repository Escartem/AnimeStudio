using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace AnimeStudio.App
{
    public sealed class Extractor
    {
        private readonly Game game;
        private readonly Action<string> status;

        public Extractor(Game game, Action<string> status = null)
        {
            this.game = game;
            this.status = status ?? Logger.Info;
        }

        public int ExtractFolder(string path, string savePath, CancellationToken token = default)
        {
            var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
            var extractedCount = 0;
            Progress.Reset();
            for (int i = 0; i < files.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                var fileSavePath = Path.GetDirectoryName(files[i]).Replace(path, savePath);
                extractedCount += ExtractFile(files[i], fileSavePath);
                Progress.Report(i + 1, files.Length);
            }
            return extractedCount;
        }

        public int ExtractFiles(string[] fileNames, string savePath, CancellationToken token = default)
        {
            var extractedCount = 0;
            Progress.Reset();
            for (var i = 0; i < fileNames.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                extractedCount += ExtractFile(fileNames[i], savePath);
                Progress.Report(i + 1, fileNames.Length);
            }
            return extractedCount;
        }

        public int ExtractFile(string fileName, string savePath)
        {
            if (game?.Type == GameType.AFKJourney && AFKJourneyUtils.IsSupportedSpecialFile(fileName))
            {
                return AFKJourneyUtils.TryExtractSpecialFile(fileName, savePath);
            }

            var reader = new FileReader(fileName).PreProcessing(game);
            switch (reader.FileType)
            {
                case FileType.BundleFile:
                    return ExtractBundleFile(reader, savePath);
                case FileType.WebFile:
                    return ExtractWebDataFile(reader, savePath);
                case FileType.BlkFile:
                    return ExtractBlkFile(reader, savePath);
                case FileType.BlockFile:
                    return ExtractBlockFile(reader, savePath);
                case FileType.Blb3File:
                    return ExtractBlb3File(reader, savePath);
                case FileType.VFSFile:
                    return ExtractVFSFile(reader, savePath);
                default:
                    reader.Dispose();
                    return 0;
            }
        }

        private int ExtractBlb3File(FileReader reader, string savePath)
        {
            status($"Decompressing {reader.FileName} ...");
            try
            {
                var file = new Blb3File(reader, reader.FullPath);
                reader.Dispose();
                return ExtractStreamFiles(Unpacked(savePath, reader), file.fileList);
            }
            catch (InvalidCastException)
            {
                LogMismatch(nameof(Mr0k));
            }
            return 0;
        }

        private int ExtractBundleFile(FileReader reader, string savePath)
        {
            status($"Decompressing {reader.FileName} ...");
            try
            {
                var file = new BundleFile(reader, game);
                reader.Dispose();
                return ExtractStreamFiles(Unpacked(savePath, reader), file.fileList);
            }
            catch (InvalidCastException)
            {
                LogMismatch(nameof(Mr0k));
            }
            return 0;
        }

        private int ExtractWebDataFile(FileReader reader, string savePath)
        {
            status($"Decompressing {reader.FileName} ...");
            var file = new WebFile(reader);
            reader.Dispose();
            return ExtractStreamFiles(Unpacked(savePath, reader), file.fileList);
        }

        private int ExtractBlkFile(FileReader reader, string savePath)
        {
            int total = 0;
            status($"Decompressing {reader.FileName} ...");
            try
            {
                using var stream = BlkUtils.Decrypt(reader, (Blk)game);
                do
                {
                    stream.Offset = stream.AbsolutePosition;
                    var dummyPath = Path.Combine(reader.FullPath, stream.AbsolutePosition.ToString("X8"));
                    var subReader = new FileReader(dummyPath, stream, true);
                    var subSavePath = Unpacked(savePath, reader);
                    switch (subReader.FileType)
                    {
                        case FileType.BundleFile:
                            total += ExtractBundleFile(subReader, subSavePath);
                            break;
                        case FileType.MhyFile:
                            total += ExtractMhyFile(subReader, subSavePath);
                            break;
                    }
                } while (stream.Remaining > 0);
            }
            catch (InvalidCastException)
            {
                LogMismatch(nameof(Blk));
            }
            return total;
        }

        private int ExtractBlockFile(FileReader reader, string savePath)
        {
            int total = 0;
            status($"Decompressing {reader.FileName} ...");
            using var stream = new OffsetStream(reader.BaseStream, 0);
            do
            {
                stream.Offset = stream.AbsolutePosition;
                var subSavePath = Unpacked(savePath, reader);
                var dummyPath = Path.Combine(reader.FullPath, stream.AbsolutePosition.ToString("X8"));
                var subReader = new FileReader(dummyPath, stream, true);
                total += subReader.FileType switch
                {
                    FileType.Blb3File => ExtractBlb3File(subReader, subSavePath),
                    FileType.VFSFile => ExtractVFSFile(subReader, subSavePath),
                    _ => ExtractBundleFile(subReader, subSavePath),
                };
            } while (stream.Remaining > 0);
            return total;
        }

        private int ExtractVFSFile(FileReader reader, string savePath)
        {
            status($"Decompressing {reader.FileName} ...");
            try
            {
                var file = new VFSFile(reader, reader.FullPath, game.Type);
                reader.Dispose();
                return ExtractStreamFiles(Unpacked(savePath, reader), file.fileList);
            }
            catch (Exception e)
            {
                Logger.Error($"Error while reading VFS file {reader.FullPath}", e);
            }
            return 0;
        }

        private int ExtractMhyFile(FileReader reader, string savePath)
        {
            status($"Decompressing {reader.FileName} ...");
            try
            {
                var file = new MhyFile(reader, (Mhy)game);
                reader.Dispose();
                return ExtractStreamFiles(Unpacked(savePath, reader), file.fileList);
            }
            catch (InvalidCastException)
            {
                LogMismatch(nameof(Mhy));
            }
            return 0;
        }

        private void LogMismatch(string expected)
            => Logger.Error($"Game type mismatch, Expected {expected} but got {game.Name} ({game.GetType().Name}) !!");

        private static string Unpacked(string savePath, FileReader reader) => Path.Combine(savePath, reader.FileName + "_unpacked");

        private static int ExtractStreamFiles(string extractPath, List<StreamFile> fileList)
        {
            if (fileList == null || fileList.Count == 0)
                return 0;

            int extractedCount = 0;
            foreach (var file in fileList)
            {
                if (file.stream == null)
                    continue;
                var filePath = Path.Combine(extractPath, file.path);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                if (!File.Exists(filePath))
                {
                    using var fileStream = File.Create(filePath);
                    file.stream.CopyTo(fileStream);
                    extractedCount++;
                }
                file.stream.Dispose();
            }
            return extractedCount;
        }
    }
}
