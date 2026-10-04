using System;
using System.Buffers;
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
        public static string MapsDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "Maps");

        public static bool Minimal = true;
        public static CancellationTokenSource tokenSource = new CancellationTokenSource();

        private static string BaseFolder = "";
        private static Dictionary<string, Entry> CABMap = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, HashSet<long>> Offsets = new Dictionary<string, HashSet<long>>();
        private static AssetsManager assetsManager = new AssetsManager() { Silent = true, SkipProcess = true, ResolveDependencies = false };

        public static Dictionary<ulong, string> Paths { get; set; } = new Dictionary<ulong, string>();

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
            Directory.CreateDirectory(MapsDirectory);
            var files = Directory.GetFiles(MapsDirectory, "*.bin", SearchOption.TopDirectoryOnly);
            var mapNames = files.Select(Path.GetFileNameWithoutExtension).ToArray();
            Logger.Verbose($"Found {mapNames.Length} CABMaps under {MapsDirectory}");
            return mapNames;
        }

        // A bare name refers to a map in MapsDirectory, anything else is a file path.
        public static string ResolveCABMapPath(string nameOrPath)
        {
            if (Path.IsPathRooted(nameOrPath)
                || nameOrPath.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)
                || nameOrPath.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) >= 0)
            {
                return Path.GetFullPath(nameOrPath);
            }
            return Path.Combine(MapsDirectory, $"{nameOrPath}.bin");
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

        public static void BuildMaps(MapBuildRequest request)
        {
            var buildCab = !string.IsNullOrEmpty(request.CabMapPath);
            if (!buildCab && !request.BuildAssetMap)
            {
                Logger.Warning("Nothing to build, select a CABMap and/or an AssetMap output");
                return;
            }

            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Logger.Info(buildCab && request.BuildAssetMap ? "Building CABMap and AssetMap..." : buildCab ? "Building CABMap..." : "Building AssetMap...");
            try
            {
                CABMap.Clear();
                Progress.Reset();
                var collision = 0;
                BaseFolder = request.BaseFolder ?? string.Empty;
                assetsManager.Game = request.Game;

                using var assetMap = request.BuildAssetMap ? new AssetMapWriter(request) : null;
                ForEachLoadedBundle(request.Files, file =>
                {
                    if (buildCab)
                        BuildCABMap(file, ref collision);
                    assetMap?.Add(file);
                });

                if (buildCab)
                {
                    DumpCABMap(request.CabMapPath);
                    Logger.Info($"CABMap build successfully !! {collision} collisions found");
                }
                assetMap?.Complete();
            }
            catch (Exception e)
            {
                Logger.Warning($"Map was not build, {e}");
            }
        }

        /// <summary>
        /// Walk input files and invoke <paramref name="process"/> each time a bundle has been
        /// loaded. For multi-bundle blocks (HSR ENCR .block) this fires once per inner bundle
        /// so callers can flush map entries and release streams before the next bundle loads.
        /// </summary>
        private static void ForEachLoadedBundle(string[] files, Action<string> process)
        {
            var path = Path.GetDirectoryName(Path.GetFullPath(files[0]));
            // Merge splits once for the whole batch — not once per file inside AssetsManager.LoadFiles.
            ImportHelper.MergeSplitAssets(path);
            var toReadFile = ImportHelper.ProcessingSplitFiles(files.ToList());

            var filesList = new List<string>(toReadFile);
            for (int i = 0; i < filesList.Count; i++)
            {
                var file = filesList[i];
                var processedViaCallback = false;

                assetsManager.AfterBundleLoaded = () =>
                {
                    // Always run for any loaded content. Resource-only bundles previously
                    // skipped this path, so their streams (and the shared decompressed
                    // block buffer they pin via zero-copy views) accumulated across every
                    // ENCR in a multi-bundle HSR .block — the OOM after ~file 3580.
                    if (assetsManager.assetsFileList.Count == 0
                        && assetsManager.ResourceFileCount == 0)
                    {
                        return;
                    }
                    processedViaCallback = true;
                    if (assetsManager.assetsFileList.Count > 0)
                    {
                        process(file);
                    }
                    // Free CAB/resource streams before the next bundle in this block loads.
                    // Keep assetsFileListHash so duplicate CAB names in later bundles are skipped.
                    assetsManager.ClearLoadedAssets();
                    // Drop interned Container/Source strings from the bundle we just flushed.
                    // A single HSR .block can contain 100+ ENCRs; without this the cache grows
                    // for the entire outer file and pins multi-GB of unique path strings.
                    StringCache.Clear();
                    // Multi-bundle HSR blocks decompress LOH buffers per ENCR. Without an
                    // explicit gen-2 collection those buffers stay alive until a later GC,
                    // and private bytes climb by tens of MB × hundreds of ENCRs (OOM).
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, blocking: false, compacting: false);
                };

                try
                {
                    assetsManager.LoadFiles(new[] { file }, mergeSplitAssets: false);
                }
                finally
                {
                    assetsManager.AfterBundleLoaded = null;
                }

                // Non-bundle assets files never trip AfterBundleLoaded — process once here.
                if (!processedViaCallback && assetsManager.assetsFileList.Count > 0)
                {
                    process(file);
                    processedViaCallback = true;
                }

                var msg = processedViaCallback
                    ? $"Processed {Path.GetFileName(file)}"
                    : $"Removed {Path.GetFileName(file)}, no assets found";
                Logger.Info($"[{i + 1}/{filesList.Count}] {msg}");
                Progress.Report(i + 1, filesList.Count);
                assetsManager.Clear();

                // Drop interned Source/Container strings from already-flushed entries so the
                // cache cannot grow without bound across multi-thousand-file HSR dumps.
                StringCache.Clear();

                // Large multi-bundle blocks (HSR .block) allocate many LOH streams per file.
                // A periodic gen-2 collection keeps the working set from ratcheting up across
                // thousands of files until the OS OOM-kills the process.
                if ((i + 1) % 32 == 0)
                {
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, blocking: false, compacting: false);
                }
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

        private static void DumpCABMap(string outputFile)
        {
            CABMap = CABMap.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputFile)));

            using (var binaryFile = File.Create(outputFile))
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

        public static bool LoadCABMap(string nameOrPath)
        {
            var path = ResolveCABMapPath(nameOrPath);
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

        // Genshin needs every entry in memory so containers can be rewritten after the scan, and JSON
        // is serialized in one go. Everything else streams to disk so peak RAM stays flat on HSR-sized builds.
        private sealed class AssetMapWriter : IDisposable
        {
            private readonly MapBuildRequest request;
            private readonly List<AssetEntry> entries;
            private readonly List<AssetEntry> batch = new List<AssetEntry>(256);
            private FileStream tempEntries;
            private string tempEntriesPath;
            private XmlWriter xmlWriter;
            private long entryCount;

            public AssetMapWriter(MapBuildRequest request)
            {
                this.request = request;
                var types = request.AssetMapTypes;
                if (request.Game.Type.IsGISubGroup() || types.HasFlag(ExportListType.JSON))
                {
                    entries = new List<AssetEntry>();
                    return;
                }

                Directory.CreateDirectory(request.AssetMapDirectory);
                if (types.HasFlag(ExportListType.MessagePack))
                {
                    tempEntriesPath = Path.Combine(Path.GetTempPath(), $"animestudio-map-{Guid.NewGuid():N}.tmp");
                    tempEntries = new FileStream(tempEntriesPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 64, FileOptions.SequentialScan);
                }
                if (types.HasFlag(ExportListType.XML))
                {
                    var xmlPath = Path.Combine(request.AssetMapDirectory, $"{request.AssetMapName}.xml");
                    xmlWriter = XmlWriter.Create(xmlPath, new XmlWriterSettings { Indent = true });
                    WriteXmlHeader(xmlWriter, xmlPath);
                }
                if (types == ExportListType.None)
                {
                    Logger.Info("No export list type has been selected, counting assets only...");
                }
            }

            public void Add(string file)
            {
                if (entries != null)
                {
                    BuildAssetMap(file, entries, request.TypeFilters, request.NameFilters, request.ContainerFilters);
                    return;
                }

                batch.Clear();
                BuildAssetMap(file, batch, request.TypeFilters, request.NameFilters, request.ContainerFilters);
                foreach (var asset in batch)
                {
                    entryCount++;
                    if (tempEntries != null)
                    {
                        MessagePackSerializer.Serialize(tempEntries, asset, MessagePackSerializerOptions.Standard);
                    }
                    if (xmlWriter != null)
                    {
                        WriteXmlEntry(xmlWriter, asset);
                    }
                }
            }

            public void Complete()
            {
                if (entries != null)
                {
                    UpdateContainers(entries, request.Game);
                    WriteAssetMap(entries, request.Game, request.AssetMapName, request.AssetMapDirectory, request.AssetMapTypes);
                    return;
                }

                if (xmlWriter != null)
                {
                    xmlWriter.WriteEndElement();
                    xmlWriter.WriteEndDocument();
                    xmlWriter.Flush();
                }

                if (tempEntries != null)
                {
                    tempEntries.Flush();
                    tempEntries.Dispose();
                    tempEntries = null;

                    // [GameType, AssetEntries[]] written uncompressed, the Lz4BlockArray reader still accepts it.
                    var mapPath = Path.Combine(request.AssetMapDirectory, $"{request.AssetMapName}.map");
                    using var output = new FileStream(mapPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 64);
                    var header = new ArrayBufferWriter<byte>(16);
                    var writer = new MessagePackWriter(header);
                    writer.WriteArrayHeader(2);
                    writer.Write((int)request.Game.Type);
                    if (entryCount > int.MaxValue)
                    {
                        throw new InvalidOperationException($"Asset map has {entryCount} entries which exceeds MessagePack array limits.");
                    }
                    writer.WriteArrayHeader((int)entryCount);
                    writer.Flush();
                    output.Write(header.WrittenSpan);

                    using (var input = new FileStream(tempEntriesPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 64, FileOptions.SequentialScan | FileOptions.DeleteOnClose))
                    {
                        input.CopyTo(output);
                    }
                    tempEntriesPath = null;
                }

                if (request.AssetMapTypes != ExportListType.None)
                {
                    Logger.Info($"Finished building AssetMap with {entryCount} assets.");
                }
            }

            public void Dispose()
            {
                xmlWriter?.Dispose();
                tempEntries?.Dispose();
                if (tempEntriesPath != null && File.Exists(tempEntriesPath))
                {
                    try { File.Delete(tempEntriesPath); } catch { }
                }
            }
        }

        private static void WriteXmlHeader(XmlWriter writer, string filename)
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("Assets");
            writer.WriteAttributeString("filename", filename);
            writer.WriteAttributeString("createdAt", DateTime.UtcNow.ToString("s"));
        }

        private static void WriteXmlEntry(XmlWriter writer, AssetEntry asset)
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
                    // Keep a stable reference for hashing — some branches below set obj = null
                    // (AssetBundle / IndexObject) while the entry may still be exportable.
                    var hashSource = obj;
                    var asset = new AssetEntry()
                    {
                        Source = file,
                        PathID = objectReader.m_PathID,
                        Type = objectReader.type,
                        Container = "",
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

                                    string container = m_Container.Key;
                                    var pathTest = Paths;
                                    if(ulong.TryParse(container, out var hash) && Paths.TryGetValue(hash, out var path))
                                    {
                                        container = path;
                                    }
                                    for (int k = preloadIndex; k < preloadEnd; k++)
                                    {
                                        containers.Add((assetBundle.m_PreloadTable[k], container));
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
                                    // Do NOT run full SerializedShader parsing during map builds.
                                    // HSR ships individual shaders >100MB; the nested parse allocates
                                    // multi-GB of temporary structures and is what OOMs map builds
                                    // around multi-bundle blocks (e.g. after file ~3580).
                                    asset.Name = $"Shader #{objectReader.m_PathID}";
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
                        // Hash only exportable entries. Skip multi-dozen-MB blobs (large HSR
                        // shaders/meshes): streaming hash is correct but pointless for map
                        // identity when the raw payload dwarfs the rest of the entry.
                        const uint largeHashSkip = 16u * 1024 * 1024;
                        asset.Hash = hashSource.byteSize >= largeHashSkip
                            ? $"size:{hashSource.byteSize:x}"
                            : hashSource.GetHash();
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

        private static void WriteAssetMap(List<AssetEntry> toExportAssets, Game game, string name, string savePath, ExportListType exportListType)
        {
            Progress.Reset();

            if (exportListType == ExportListType.None)
            {
                Logger.Info($"No export list type has been selected, skipping...");
                return;
            }

            Directory.CreateDirectory(savePath);
            if (exportListType.HasFlag(ExportListType.XML))
            {
                var filename = Path.Combine(savePath, $"{name}.xml");
                using var writer = XmlWriter.Create(filename, new XmlWriterSettings() { Indent = true });
                WriteXmlHeader(writer, filename);
                foreach (var asset in toExportAssets)
                {
                    WriteXmlEntry(writer, asset);
                }
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
            if (exportListType.HasFlag(ExportListType.JSON))
            {
                using var file = File.CreateText(Path.Combine(savePath, $"{name}.json"));
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
                using var file = File.Create(Path.Combine(savePath, $"{name}.map"));
                var assetMap = new AssetMap
                {
                    GameType = game.Type,
                    AssetEntries = toExportAssets
                };
                MessagePackSerializer.Serialize(file, assetMap, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
            }

            Logger.Info($"Finished building AssetMap with {toExportAssets.Count} assets.");
        }
    }
}
