using System;
using System.Collections.Generic;
using System.IO;

namespace AnimeStudio.GUI
{
    internal static partial class Studio
    {
        public static int ExtractFolder(string path, string savePath)
        {
            int extractedCount = 0;
            Progress.Reset();
            var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                var fileOriPath = Path.GetDirectoryName(file);
                var fileSavePath = fileOriPath.Replace(path, savePath);
                extractedCount += ExtractFile(file, fileSavePath);
                Progress.Report(i + 1, files.Length);
            }
            return extractedCount;
        }

        public static int ExtractFile(string[] fileNames, string savePath)
        {
            int extractedCount = 0;
            Progress.Reset();
            for (var i = 0; i < fileNames.Length; i++)
            {
                var fileName = fileNames[i];
                extractedCount += ExtractFile(fileName, savePath);
                Progress.Report(i + 1, fileNames.Length);
            }
            return extractedCount;
        }

        public static int ExtractFile(string fileName, string savePath)
        {
            var reader = new FileReader(fileName);
            reader = reader.PreProcessing(Game);
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

        private static int ExtractBlb3File(FileReader reader, string savePath)
        {
            try
            {
                return ExtractFileList(reader, savePath, currentReader => new Blb3File(currentReader, currentReader.FullPath).fileList);
            }
            catch (InvalidCastException)
            {
                LogGameTypeMismatch(nameof(Mr0k));
            }
            return 0;
        }

        private static int ExtractBundleFile(FileReader reader, string savePath)
        {
            try
            {
                return ExtractFileList(reader, savePath, currentReader => new BundleFile(currentReader, Game).fileList);
            }
            catch (InvalidCastException)
            {
                LogGameTypeMismatch(nameof(Mr0k));
            }
            return 0;
        }

        private static int ExtractWebDataFile(FileReader reader, string savePath)
        {
            return ExtractFileList(reader, savePath, currentReader => new WebFile(currentReader).fileList);
        }

        private static int ExtractBlkFile(FileReader reader, string savePath)
        {
            int total = 0;
            StatusStripUpdate($"Decompressing {reader.FileName} ...");
            try
            {
                using var stream = BlkUtils.Decrypt(reader, (Blk)Game);
                do
                {
                    stream.Offset = stream.AbsolutePosition;
                    var subReader = CreateSubReader(reader.FullPath, stream);
                    var subSavePath = GetUnpackedPath(savePath, reader.FileName);
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
                LogGameTypeMismatch(nameof(Blk));
            }
            return total;
        }

        private static int ExtractBlockFile(FileReader reader, string savePath)
        {
            int total = 0;
            StatusStripUpdate($"Decompressing {reader.FileName} ...");
            using var stream = new OffsetStream(reader.BaseStream, 0);
            do
            {
                stream.Offset = stream.AbsolutePosition;
                var subSavePath = GetUnpackedPath(savePath, reader.FileName);
                var subReader = CreateSubReader(reader.FullPath, stream);
                total += ExtractBlockSubFile(subReader, subSavePath);
            } while (stream.Remaining > 0);
            return total;
        }

        private static int ExtractVFSFile(FileReader reader, string savePath)
        {
            try
            {
                return ExtractFileList(reader, savePath, currentReader => new VFSFile(currentReader, currentReader.FullPath, Game.Type).fileList);
            }
            catch (Exception e)
            {
                Logger.Error($"Error while reading VFS file {reader.FullPath}", e);
            }
            return 0;
        }

        private static int ExtractMhyFile(FileReader reader, string savePath)
        {
            try
            {
                return ExtractFileList(reader, savePath, currentReader => new MhyFile(currentReader, (Mhy)Game).fileList);
            }
            catch (InvalidCastException)
            {
                LogGameTypeMismatch(nameof(Mhy));
            }
            return 0;
        }

        private static int ExtractBlockSubFile(FileReader subReader, string subSavePath)
        {
            switch (subReader.FileType)
            {
                case FileType.Blb3File:
                    return ExtractBlb3File(subReader, subSavePath);
                case FileType.VFSFile:
                    return ExtractVFSFile(subReader, subSavePath);
                default:
                    return ExtractBundleFile(subReader, subSavePath);
            }
        }

        private static int ExtractFileList(FileReader reader, string savePath, Func<FileReader, List<StreamFile>> readFileList)
        {
            StatusStripUpdate($"Decompressing {reader.FileName} ...");
            var extractPath = GetUnpackedPath(savePath, reader.FileName);
            List<StreamFile> fileList;
            try
            {
                fileList = readFileList(reader);
            }
            finally
            {
                reader.Dispose();
            }

            return ExtractStreamFile(extractPath, fileList);
        }

        private static string GetUnpackedPath(string savePath, string fileName)
        {
            return Path.Combine(savePath, fileName + "_unpacked");
        }

        private static FileReader CreateSubReader(string parentPath, OffsetStream stream)
        {
            var dummyPath = Path.Combine(parentPath, stream.AbsolutePosition.ToString("X8"));
            return new FileReader(dummyPath, stream, true);
        }

        private static void LogGameTypeMismatch(string expectedGameType)
        {
            Logger.Error($"Game type mismatch, Expected {expectedGameType} but got {Game.Name} ({Game.GetType().Name}) !!");
        }

        private static int ExtractStreamFile(string extractPath, List<StreamFile> fileList)
        {
            int extractedCount = 0;
            if (fileList == null || fileList.Count == 0)
            {
                return 0;
            }
            foreach (var file in fileList)
            {
                var filePath = Path.Combine(extractPath, file.path);
                var fileDirectory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(fileDirectory))
                {
                    Directory.CreateDirectory(fileDirectory);
                }
                if (!File.Exists(filePath))
                {
                    if (file.stream == null)
                    {
                        continue;
                    }
                    using (var fileStream = File.Create(filePath))
                    {
                        file.stream.CopyTo(fileStream);
                    }
                    extractedCount += 1;
                }
                file.stream.Dispose();
            }
            return extractedCount;
        }

        public static void UpdateContainers()
        {
            if (exportableAssets.Count == 0)
            {
                return;
            }

            Logger.Info("Updating Containers...");
            var isZZZ = Game.Type.IsZZZ();
            var sourceFileIds = new Dictionary<string, uint?>();

            foreach (var asset in exportableAssets)
            {
                if (isZZZ && TryUpdateZZZContainer(asset))
                {
                    continue;
                }

                TryUpdateIndexedContainer(asset, sourceFileIds);
            }

            Logger.Info("Updated !!");
        }

        private static bool TryUpdateZZZContainer(AssetItem asset)
        {
            if (!ulong.TryParse(asset.Container, out var hash) || !Paths.TryGetValue(hash, out var z3Path))
            {
                return false;
            }

            asset.Container = z3Path;
            return true;
        }

        private static void TryUpdateIndexedContainer(AssetItem asset, Dictionary<string, uint?> sourceFileIds)
        {
            if (!int.TryParse(asset.Container, out var value))
            {
                return;
            }

            var id = GetSourceFileId(asset.SourceFile.originalPath, sourceFileIds);
            if (!id.HasValue)
            {
                return;
            }

            var last = unchecked((uint)value);
            var path = ResourceIndex.GetContainer(id.Value, last);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            asset.Container = path;
            if (asset.Type == ClassIDType.MiHoYoBinData)
            {
                asset.Text = Path.GetFileNameWithoutExtension(path);
            }
        }

        private static uint? GetSourceFileId(string originalPath, Dictionary<string, uint?> sourceFileIds)
        {
            originalPath ??= string.Empty;
            if (sourceFileIds.TryGetValue(originalPath, out var id))
            {
                return id;
            }

            var name = Path.GetFileNameWithoutExtension(originalPath);
            id = uint.TryParse(name, out var parsedId) ? parsedId : null;
            sourceFileIds.Add(originalPath, id);
            return id;
        }
    }
}

