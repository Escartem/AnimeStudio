using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Globalization;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Text;
using MessagePack;
using System.Threading.Tasks;

namespace AnimeStudio
{
    public static class AssetsHelper
    {
        public const string MapName = "Maps";

        public static bool Minimal = true;
        public static CancellationTokenSource tokenSource = new CancellationTokenSource();

        private static string BaseFolder = "";
        private static Dictionary<string, Entry> CABMap = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, HashSet<long>> Offsets = new Dictionary<string, HashSet<long>>();
        private static AssetsManager assetsManager = new AssetsManager() { Silent = true, SkipProcess = true, ResolveDependencies = false };

        public record Entry
        {
            public string Path { get; set; }
            public long Offset { get; set; }
            public List<string> Dependencies { get; set; }
        }

        public static void SetUnityVersion(string version)
        {
            assetsManager.SpecifyUnityVersion = version;
        }

        public static string[] GetMaps()
        {
            Directory.CreateDirectory(MapName);
            var files = Directory.GetFiles(MapName, "*.bin", SearchOption.TopDirectoryOnly);
            var mapNames = files.Select(Path.GetFileNameWithoutExtension).ToArray();
            Logger.Verbose($"Found {mapNames.Length} CABMaps under Maps folder");
            return mapNames;
        }

        public static void Clear()
        {
            CABMap.Clear();
            Offsets.Clear();
            BaseFolder = string.Empty;
            assetsManager.SpecifyUnityVersion = string.Empty;

            tokenSource.Dispose();
            tokenSource = new CancellationTokenSource();

            Logger.Verbose("Cleared AssetsHelper successfully !!");
        }

        public static void ClearOffsets()
        {
            Offsets.Clear();
            Logger.Verbose("Cleared cached offsets");
        }

        public static bool TryGet(string path, out long[] offsets)
        {
            if (Offsets.TryGetValue(path, out var list) && list.Count > 0)
            {
                Logger.Verbose($"Found {list.Count} offsets for path {path}");
                offsets = list.ToArray();
                return true;
            }
            offsets = Array.Empty<long>();
            return false;
        }

        public static void AddCABOffsetsFast(HashSet<string> paths, HashSet<string> cabs)
        {
            Queue<string> work = new Queue<string>(cabs);
            while (work.Count > 0)
            {
                var cab = work.Dequeue();
                if (CABMap.TryGetValue(cab, out var entry))
                {
                    var fullPath = Path.Combine(BaseFolder, entry.Path);
                    Logger.Verbose($"Found {cab} in {fullPath}");
                    if (!paths.Contains(fullPath))
                    {
                        Offsets.TryAdd(fullPath, new HashSet<long>());
                        Offsets[fullPath].Add(entry.Offset);
                        Logger.Verbose($"Added {fullPath} to Offsets, at offset {entry.Offset}");
                    }
                    foreach (var dep in entry.Dependencies)
                    {
                        if (!cabs.Contains(dep))
                        {
                            cabs.Add(dep);
                            work.Enqueue(dep);
                        }
                    }
                }
            }
        }

        public static bool FindCAB(string path, out HashSet<string> cabs)
        {
            var relativePath = Path.GetRelativePath(BaseFolder, path);
            cabs = CABMap.AsParallel().Where(x => x.Value.Path.Equals(relativePath, StringComparison.OrdinalIgnoreCase)).Select(x => x.Key).Distinct().ToHashSet(StringComparer.OrdinalIgnoreCase);
            Logger.Verbose($"Found {cabs.Count} that belongs to {relativePath}");
            return cabs.Count != 0;
        }

        public static string[] ProcessFiles(string[] files_list)
        {
            HashSet<string> files = new HashSet<string>(files_list, StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                Offsets.TryAdd(file, new HashSet<long>());
                Logger.Verbose($"Added {file} to Offsets dictionary");
                if (FindCAB(file, out var cabs))
                {
                    AddCABOffsetsFast(files, cabs);
                }
            }
            Logger.Verbose($"Finished resolving dependncies, the original {files.Count} files will be loaded entirely, and the {Offsets.Count - files.Count} dependicnes will be loaded from cached offsets only");
            return Offsets.Keys.ToArray();
        }

        public static string[] ProcessDependencies(string[] files)
        {
            if (CABMap.Count == 0)
            {
                Logger.Warning("CABMap is not build, skip resolving dependencies...");
            }
            else
            {
                Logger.Info("Resolving Dependencies...");
                files = ProcessFiles(files);
            }
            return files;
        }

        public static void BuildCABMap(string[] files, string mapName, string baseFolder, Game game)
        {
            Logger.Info("Building CABMap...");
            try
            {
                CABMap.Clear();
                Progress.Reset();
                var collision = 0;
                BaseFolder = baseFolder;
                assetsManager.Game = game;
                foreach (var file in LoadFiles(files))
                {
                    BuildCABMap(file, ref collision);
                }

                DumpCABMap(mapName);

                Logger.Info($"CABMap build successfully !! {collision} collisions found");
            }
            catch (Exception e)
            {
                Logger.Warning($"CABMap was not build, {e}");
            }
        }

        private static IEnumerable<string> LoadFiles(string[] files)
        {
            string msg;
            
            var path = Path.GetDirectoryName(Path.GetFullPath(files[0]));
            ImportHelper.MergeSplitAssets(path);
            var toReadFile = ImportHelper.ProcessingSplitFiles(files.ToList());

            var filesList = new List<string>(toReadFile);
            for (int i = 0; i < filesList.Count; i++)
            {
                var file = filesList[i];
                assetsManager.LoadFiles(file);
                if (assetsManager.assetsFileList.Count > 0)
                {
                    yield return file;
                    msg = $"Processed {Path.GetFileName(file)}";
                }
                else
                {
                    msg = $"Removed {Path.GetFileName(file)}, no assets found";
                }
                Logger.Info($"[{i + 1}/{filesList.Count}] {msg}");
                Progress.Report(i + 1, filesList.Count);
                assetsManager.Clear();
            }
        }

        private static void BuildCABMap(string file, ref int collision)
        {
            var relativePath = Path.GetRelativePath(BaseFolder, file);
            foreach (var assetsFile in assetsManager.assetsFileList)
            {
                if (tokenSource.IsCancellationRequested)
                {
                    Logger.Info("Building CABMap has been cancelled !!");
                    return;
                }
                var entry = new Entry()
                {
                    Path = relativePath,
                    Offset = assetsFile.offset,
                    Dependencies = assetsFile.m_Externals.Select(x => x.fileName).ToList()
                };

                if (CABMap.ContainsKey(assetsFile.fileName))
                {
                    collision++;
                    continue;
                }
                CABMap.Add(assetsFile.fileName, entry);
            }
        }

        private static void DumpCABMap(string mapName)
        {
            CABMap = CABMap.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            var outputFile = Path.Combine(MapName, $"{mapName}.bin");

            Directory.CreateDirectory(Path.GetDirectoryName(outputFile));

            using (var binaryFile = File.OpenWrite(outputFile))
            using (var writer = new BinaryWriter(binaryFile))
            {
                writer.Write(BaseFolder);
                writer.Write(CABMap.Count);
                foreach (var kv in CABMap)
                {
                    writer.Write(kv.Key);
                    writer.Write(kv.Value.Path);
                    writer.Write(kv.Value.Offset);
                    writer.Write(kv.Value.Dependencies.Count);
                    foreach (var cab in kv.Value.Dependencies)
                    {
                        writer.Write(cab);
                    }
                }
            }
        }

        public static bool LoadCABMapInternal(string mapName)
        {
            Logger.Info($"Loading {mapName}...");
            try
            {
                CABMap.Clear();
                using var fs = File.OpenRead(Path.Combine(MapName, $"{mapName}.bin"));
                using var reader = new BinaryReader(fs);
                ParseCABMap(reader);
                Logger.Verbose($"Initialized CABMap with {CABMap.Count} entries");
                Logger.Info($"Loaded {mapName} !!");
            }
            catch (Exception e)
            {
                Logger.Warning($"{mapName} was not loaded, {e}");
                return false;
            }

            return true;
        }

        public static bool LoadCABMap(string path)
        {
            var mapName = Path.GetFileNameWithoutExtension(path);
            Logger.Info($"Loading {mapName}...");
            try
            {
                CABMap.Clear();
                using var fs = File.OpenRead(path);
                using var reader = new BinaryReader(fs);
                ParseCABMap(reader);
                Logger.Verbose($"Initialized CABMap with {CABMap.Count} entries");
                Logger.Info($"Loaded {mapName} !!");
            }
            catch (Exception e)
            {
                Logger.Warning($"{mapName} was not loaded, {e}");
                return false;
            }

            return true;
        }

        private static void ParseCABMap(BinaryReader reader)
        {
            BaseFolder = reader.ReadString();
            var count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var cab = reader.ReadString();
                var path = reader.ReadString();
                var offset = reader.ReadInt64();
                var depCount = reader.ReadInt32();
                var dependencies = new List<string>();
                for (int j = 0; j < depCount; j++)
                {
                    dependencies.Add(reader.ReadString());
                }
                var entry = new Entry()
                {
                    Path = path,
                    Offset = offset,
                    Dependencies = dependencies
                };
                CABMap.Add(cab, entry);
            }
        } 

        public static async Task BuildAssetMap(string[] files, string mapName, Game game, string savePath, ExportListType exportListType, ClassIDType[] typeFilters = null, Regex[] nameFilters = null, Regex[] containerFilters = null)
        {
            Logger.Info("Building AssetMap...");
            try
            {
                Progress.Reset();
                assetsManager.Game = game;
                var assets = new List<AssetEntry>();
                foreach (var file in LoadFiles(files))
                {
                    BuildAssetMap(file, assets, typeFilters, nameFilters, containerFilters);
                }

                UpdateContainers(assets, game);

                await ExportAssetsMap(assets, game, mapName, savePath, exportListType);
            }
            catch(Exception e)
            {
                Logger.Warning($"AssetMap was not build, {e}");
            }
            
        }

        private static void BuildAssetMap(string file, List<AssetEntry> assets, ClassIDType[] typeFilters = null, Regex[] nameFilters = null, Regex[] containerFilters = null)
        {
            var matches = new List<AssetEntry>();
            var containers = new List<(PPtr<Object>, string)>();
            var mihoyoBinDataNames = new List<(PPtr<Object>, string)>();
            var objectAssetItemDic = new Dictionary<Object, AssetEntry>();
            var animators = new List<(PPtr<Object>, AssetEntry)>();
            foreach (var assetsFile in assetsManager.assetsFileList)
            {
                foreach (var objInfo in assetsFile.m_Objects)
                {
                    if (tokenSource.IsCancellationRequested)
                    {
                        Logger.Info("Building AssetMap has been cancelled !!");
                        return;
                    }
                    var objectReader = new ObjectReader(assetsFile.reader, assetsFile, objInfo, assetsManager.Game);
                    var obj = new Object(objectReader);
                    var asset = new AssetEntry()
                    {
                        Source = file,
                        PathID = objectReader.m_PathID,
                        Type = objectReader.type,
                        Container = "",
                        SHA256Hash = obj.GetSHA256Hash(),
                        Offset = assetsFile.offset
                    };

                    var exportable = false;
                    try
                    {
                        switch (objectReader.type)
                        {
                            case ClassIDType.AssetBundle when ClassIDType.AssetBundle.CanParse():
                                var assetBundle = new AssetBundle(objectReader);
                                foreach (var m_Container in assetBundle.m_Container)
                                {
                                    var preloadIndex = m_Container.Value.preloadIndex;
                                    var preloadSize = m_Container.Value.preloadSize;
                                    var preloadEnd = preloadIndex + preloadSize;
                                    for (int k = preloadIndex; k < preloadEnd; k++)
                                    {
                                        containers.Add((assetBundle.m_PreloadTable[k], m_Container.Key));
                                    }
                                }

                                obj = null;
                                asset.Name = assetBundle.m_Name;
                                exportable = ClassIDType.AssetBundle.CanExport();
                                break;
                            case ClassIDType.GameObject when ClassIDType.GameObject.CanParse():
                                var gameObject = new GameObject(objectReader);
                                obj = gameObject;
                                asset.Name = gameObject.m_Name;
                                exportable = ClassIDType.GameObject.CanExport();
                                break;
                            case ClassIDType.Shader when ClassIDType.Shader.CanParse():
                                asset.Name = objectReader.ReadAlignedString();
                                if (string.IsNullOrEmpty(asset.Name))
                                {
                                    var m_parsedForm = new SerializedShader(objectReader);
                                    asset.Name = m_parsedForm.m_Name;
                                }

                                exportable = ClassIDType.Shader.CanExport();
                                break;
                            case ClassIDType.Animator when ClassIDType.Animator.CanParse():
                                var component = new PPtr<Object>(objectReader);
                                animators.Add((component, asset));
                                asset.Name = objectReader.type.ToString();
                                exportable = ClassIDType.Animator.CanExport();
                                break;
                            case ClassIDType.MiHoYoBinData when ClassIDType.MiHoYoBinData.CanParse():
                                var MiHoYoBinData = new MiHoYoBinData(objectReader);
                                obj = MiHoYoBinData;
                                asset.Name = objectReader.type.ToString();
                                exportable = ClassIDType.MiHoYoBinData.CanExport();
                                break;
                            case ClassIDType.NapAssetBundleIndexAsset when ClassIDType.NapAssetBundleIndexAsset.CanParse():
                                var NapAssetBundleIndexAsset = new NapAssetBundleIndexAsset(objectReader);
                                obj = NapAssetBundleIndexAsset;
                                asset.Name = obj.Name;
                                exportable = ClassIDType.NapAssetBundleIndexAsset.CanExport();
                                break;
                            case ClassIDType.IndexObject when ClassIDType.IndexObject.CanParse():
                                var indexObject = new IndexObject(objectReader);
                                obj = null;
                                foreach (var index in indexObject.AssetMap)
                                {
                                    mihoyoBinDataNames.Add((index.Value.Object, index.Key));
                                }
                                asset.Name = "IndexObject";
                                exportable = ClassIDType.IndexObject.CanExport();
                                break;
                            case ClassIDType.Font when ClassIDType.Font.CanExport():
                            case ClassIDType.Material when ClassIDType.Material.CanExport():
                            case ClassIDType.Texture when ClassIDType.Texture.CanExport():
                            case ClassIDType.Mesh when ClassIDType.Mesh.CanExport():
                            case ClassIDType.Sprite when ClassIDType.Sprite.CanExport():
                            case ClassIDType.TextAsset when ClassIDType.TextAsset.CanExport():
                            case ClassIDType.Texture2D when ClassIDType.Texture2D.CanExport():
                            case ClassIDType.VideoClip when ClassIDType.VideoClip.CanExport():
                            case ClassIDType.AudioClip when ClassIDType.AudioClip.CanExport():
                            case ClassIDType.AnimationClip when ClassIDType.AnimationClip.CanExport():
                                asset.Name = objectReader.ReadAlignedString();
                                exportable = true;
                                break;
                            case ClassIDType.MonoBehaviour when ClassIDType.MonoBehaviour.CanParse():
                                var monoBehaviour = new MonoBehaviour(objectReader);
                                asset.Name = String.IsNullOrWhiteSpace(monoBehaviour.Name) ? objectReader.type.ToString() : monoBehaviour.Name;
                                exportable = true;
                                break;
                            default:
                                asset.Name = objectReader.type.ToString();
                                exportable = !Minimal;
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("Unable to load object")
                            .AppendLine($"Assets {assetsFile.fileName}")
                            .AppendLine($"Path {assetsFile.originalPath}")
                            .AppendLine($"Type {objectReader.type}")
                            .AppendLine($"PathID {objectReader.m_PathID}")
                            .Append(e);
                        Logger.Error(sb.ToString());
                    }
                    if (obj != null)
                    {
                        objectAssetItemDic.Add(obj, asset);
                        assetsFile.AddObject(obj);
                    }
                    if (exportable)
                    {
                        matches.Add(asset);
                    }
                }
            }
            foreach ((var pptr, var asset) in animators)
            {
                if (pptr.TryGet<GameObject>(out var gameObject))
                {
                    asset.Name = gameObject.m_Name;
                }
            }
            foreach ((var pptr, var name) in mihoyoBinDataNames)
            {
                if (pptr.TryGet<MiHoYoBinData>(out var miHoYoBinData))
                {
                    var asset = objectAssetItemDic[miHoYoBinData];
                    if (int.TryParse(name, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hash))
                    {
                        asset.Name = name;
                        asset.Container = hash.ToString();
                    }
                    else asset.Name = $"BinFile #{asset.PathID}";
                }
            }
            foreach ((var pptr, var container) in containers)
            {
                if (pptr.TryGet(out var obj))
                {
                    objectAssetItemDic[obj].Container = container;
                }
            }

            assets.AddRange(matches.Where(x =>
            {
                var isMatchRegex = nameFilters.IsNullOrEmpty() || nameFilters.Any(y => y.IsMatch(x.Name));
                var isFilteredType = typeFilters.IsNullOrEmpty() || typeFilters.Contains(x.Type);
                var isContainerMatch = containerFilters.IsNullOrEmpty() || containerFilters.Any(y => y.IsMatch(x.Container));
                return isMatchRegex && isFilteredType && isContainerMatch;
            }));
        }

        public static string[] ParseAssetMap(string mapName, ExportListType mapType, ClassIDType[] typeFilter, Regex[] nameFilter, Regex[] containerFilter)
        {
            var matches = new HashSet<string>();

            switch (mapType)
            {
                case ExportListType.MessagePack:
                    {
                        using var stream = File.OpenRead(mapName);
                        var assetMap = MessagePackSerializer.Deserialize<AssetMap>(stream, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
                        foreach(var entry in assetMap.AssetEntries)
                        {
                            var isNameMatch = nameFilter.Length == 0 || nameFilter.Any(x => x.IsMatch(entry.Name));
                            var isContainerMatch = containerFilter.Length == 0 || containerFilter.Any(x => x.IsMatch(entry.Container));
                            var isTypeMatch = typeFilter.Length == 0 || typeFilter.Any(x => x == entry.Type);
                            if (isNameMatch && isContainerMatch && isTypeMatch)
                            {
                                matches.Add(entry.Source);
                            }
                        }
                    }

                    break;
                case ExportListType.XML:
                    {
                        using var stream = File.OpenRead(mapName);
                        using var reader = XmlReader.Create(stream);
                        reader.ReadToFollowing("Assets");
                        reader.ReadToFollowing("Asset");
                        do
                        {
                            reader.ReadToFollowing("Name");
                            var name = reader.ReadInnerXml();

                            var isNameMatch = nameFilter.Length == 0 || nameFilter.Any(x => x.IsMatch(name));

                            reader.ReadToFollowing("Container");
                            var container = reader.ReadInnerXml();

                            var isContainerMatch = containerFilter.Length == 0 || containerFilter.Any(x => x.IsMatch(container));

                            reader.ReadToFollowing("Type");
                            var type = reader.ReadInnerXml();

                            var isTypeMatch = typeFilter.Length == 0 || typeFilter.Any(x => x.ToString().Equals(type, StringComparison.OrdinalIgnoreCase));

                            reader.ReadToFollowing("PathID");
                            var pathID = reader.ReadInnerXml();

                            reader.ReadToFollowing("Source");
                            var source = reader.ReadInnerXml();

                            if (isNameMatch && isContainerMatch && isTypeMatch)
                            {
                                matches.Add(source);
                            }

                            reader.ReadEndElement();
                        } while (reader.ReadToNextSibling("Asset"));
                    }

                    break;
                case ExportListType.JSON:
                    {
                        using var stream = File.OpenRead(mapName);
                        using var file = new StreamReader(stream);
                        using var reader = new JsonTextReader(file);

                        var serializer = new JsonSerializer() { Formatting = Newtonsoft.Json.Formatting.Indented };
                        serializer.Converters.Add(new StringEnumConverter());

                        var entries = serializer.Deserialize<List<AssetEntry>>(reader);
                        foreach (var entry in entries)
                        {
                            var isNameMatch = nameFilter.Length == 0 || nameFilter.Any(x => x.IsMatch(entry.Name));
                            var isContainerMatch = containerFilter.Length == 0 || containerFilter.Any(x => x.IsMatch(entry.Container));
                            var isTypeMatch = typeFilter.Length == 0 || typeFilter.Any(x => x == entry.Type);
                            if (isNameMatch && isContainerMatch && isTypeMatch)
                            {
                                matches.Add(entry.Source);
                            }
                        }
                    }

                    break;
            }

            return matches.ToArray();
        }

        private static void UpdateContainers(List<AssetEntry> assets, Game game)
        {
            if (game.Type.IsGISubGroup() && assets.Count > 0)
            {
                Logger.Info("Updating Containers...");
                foreach (var asset in assets)
                {
                    if (int.TryParse(asset.Container, out var value))
                    {
                        var last = unchecked((uint)value);
                        var name = Path.GetFileNameWithoutExtension(asset.Source);
                        if (uint.TryParse(name, out var id))
                        {
                            var path = ResourceIndex.GetContainer(id, last);
                            if (!string.IsNullOrEmpty(path))
                            {
                                asset.Container = path;
                                if (asset.Type == ClassIDType.MiHoYoBinData)
                                {
                                    asset.Name = Path.GetFileNameWithoutExtension(path);
                                }
                            }
                        }
                    }
                }
                Logger.Info("Updated !!");
            }
        }

        private static Task ExportAssetsMap(List<AssetEntry> toExportAssets, Game game, string name, string savePath, ExportListType exportListType)
        {
            return Task.Run(() =>
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

                Progress.Reset();

                string filename = string.Empty;
                if (exportListType.Equals(ExportListType.None))
                {
                    Logger.Info($"No export list type has been selected, skipping...");
                }
                else
                {
                    if (exportListType.HasFlag(ExportListType.XML))
                    {
                        filename = Path.Combine(savePath, $"{name}.xml");
                        var xmlSettings = new XmlWriterSettings() { Indent = true };
                        using XmlWriter writer = XmlWriter.Create(filename, xmlSettings);
                        writer.WriteStartDocument();
                        writer.WriteStartElement("Assets");
                        writer.WriteAttributeString("filename", filename);
                        writer.WriteAttributeString("createdAt", DateTime.UtcNow.ToString("s"));
                        foreach (var asset in toExportAssets)
                        {
                            writer.WriteStartElement("Asset");
                            writer.WriteElementString("Name", asset.Name);
                            writer.WriteElementString("Container", asset.Container);
                            writer.WriteStartElement("Type");
                            writer.WriteAttributeString("id", ((int)asset.Type).ToString());
                            writer.WriteValue(asset.Type.ToString());
                            writer.WriteEndElement();
                            writer.WriteElementString("PathID", asset.PathID.ToString());
                            writer.WriteElementString("Source", asset.Source);
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();
                        writer.WriteEndDocument();
                    }
                    if (exportListType.HasFlag(ExportListType.JSON))
                    {
                        filename = Path.Combine(savePath, $"{name}.json");
                        using StreamWriter file = File.CreateText(filename);
                        var serializer = new JsonSerializer() { Formatting = Newtonsoft.Json.Formatting.Indented };
                        serializer.Converters.Add(new StringEnumConverter());
                        serializer.Serialize(file, new
                        {
                            GameType = game.Type,
                            AssetEntries = toExportAssets
                        });
                    }
                    if (exportListType.HasFlag(ExportListType.MessagePack))
                    {
                        filename = Path.Combine(savePath, $"{name}.map");
                        using var file = File.Create(filename);
                        var assetMap = new AssetMap
                        {
                            GameType = game.Type,
                            AssetEntries = toExportAssets
                        };
                        MessagePackSerializer.Serialize(file, assetMap, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
                    }

                    Logger.Info($"Finished buidling AssetMap with {toExportAssets.Count} assets.");
                }
            });
        }
        public static async Task BuildBoth(string[] files, string mapName, string baseFolder, Game game, string savePath, ExportListType exportListType, ClassIDType[] typeFilters = null, Regex[] nameFilters = null, Regex[] containerFilters = null)
        {
            Logger.Info($"Building Both...");
            CABMap.Clear();
            Progress.Reset();
            var collision = 0;
            BaseFolder = baseFolder;
            assetsManager.Game = game;
            var assets = new List<AssetEntry>();
            foreach(var file in LoadFiles(files))
            {
                BuildCABMap(file, ref collision);
                BuildAssetMap(file, assets, typeFilters, nameFilters, containerFilters);
            }

            UpdateContainers(assets, game);
            DumpCABMap(mapName);

            Logger.Info($"Map build successfully !! {collision} collisions found");
            await ExportAssetsMap(assets, game, mapName, savePath, exportListType);
        }
    }
}
