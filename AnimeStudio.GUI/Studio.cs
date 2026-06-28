using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using static AnimeStudio.GUI.Exporter;
using static AnimeStudio.AssetsManager;

namespace AnimeStudio.GUI
{
    internal enum GuiColorTheme
    {
        System = 0,
        Dark = 1,
        Light = 2
    }

    internal enum ExportFilter
    {
        All,
        Selected,
        Filtered
    }

    internal static class Studio
    {
        public static Game Game;
        public static bool SkipContainer = false;
        public static AssetsManager assetsManager = new AssetsManager();
        public static AssemblyLoader assemblyLoader = new AssemblyLoader();
        public static List<AssetItem> exportableAssets = new List<AssetItem>();
        public static List<AssetItem> visibleAssets = new List<AssetItem>();
        internal static Action<string> StatusStripUpdate = x => { };
        public static Dictionary<ulong, string> Paths { get; set; } = new Dictionary<ulong, string>();

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
            int extractedCount = 0;
            var reader = new FileReader(fileName);
            reader = reader.PreProcessing(Game);
            if (reader.FileType == FileType.BundleFile)
                extractedCount += ExtractBundleFile(reader, savePath);
            else if (reader.FileType == FileType.WebFile)
                extractedCount += ExtractWebDataFile(reader, savePath);
            else if (reader.FileType == FileType.BlkFile)
                extractedCount += ExtractBlkFile(reader, savePath);
            else if (reader.FileType == FileType.BlockFile)
                extractedCount += ExtractBlockFile(reader, savePath);
            else if (reader.FileType == FileType.Blb3File)
                extractedCount += ExtractBlb3File(reader, savePath);
            else if (reader.FileType == FileType.VFSFile)
                extractedCount += ExtractVFSFile(reader, savePath);
            else
                reader.Dispose();
            return extractedCount;
        }
        private static int ExtractBlb3File(FileReader reader, string savePath)
        {
            StatusStripUpdate($"Decompressing {reader.FileName} ...");
            try
            {
                var bundleFile = new Blb3File(reader, reader.FullPath);
                reader.Dispose();
                if (bundleFile.fileList != null && bundleFile.fileList.Count > 0)
                {
                    var extractPath = Path.Combine(savePath, reader.FileName + "_unpacked");
                    return ExtractStreamFile(extractPath, bundleFile.fileList);
                }
            }
            catch (InvalidCastException)
            {
                Logger.Error($"Game type mismatch, Expected {nameof(Mr0k)} but got {Game.Name} ({Game.GetType().Name}) !!");
            }
            return 0;
        }
        private static int ExtractBundleFile(FileReader reader, string savePath)
        {
            StatusStripUpdate($"Decompressing {reader.FileName} ...");
            try
            {
                var bundleFile = new BundleFile(reader, Game);
                reader.Dispose();
                if (bundleFile.fileList != null && bundleFile.fileList.Count > 0)
                {
                    var extractPath = Path.Combine(savePath, reader.FileName + "_unpacked");
                    return ExtractStreamFile(extractPath, bundleFile.fileList);
                }
            }
            catch (InvalidCastException)
            {
                Logger.Error($"Game type mismatch, Expected {nameof(Mr0k)} but got {Game.Name} ({Game.GetType().Name}) !!");
            }
            return 0;
        }

        private static int ExtractWebDataFile(FileReader reader, string savePath)
        {
            StatusStripUpdate($"Decompressing {reader.FileName} ...");
            var webFile = new WebFile(reader);
            reader.Dispose();
            if (webFile.fileList != null && webFile.fileList.Count > 0)
            {
                var extractPath = Path.Combine(savePath, reader.FileName + "_unpacked");
                return ExtractStreamFile(extractPath, webFile.fileList);
            }
            return 0;
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
                    var dummyPath = Path.Combine(reader.FullPath, stream.AbsolutePosition.ToString("X8"));
                    var subReader = new FileReader(dummyPath, stream, true);
                    var subSavePath = Path.Combine(savePath, reader.FileName + "_unpacked");
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
                Logger.Error($"Game type mismatch, Expected {nameof(Blk)} but got {Game.Name} ({Game.GetType().Name}) !!");
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
                var subSavePath = Path.Combine(savePath, reader.FileName + "_unpacked");
                var dummyPath = Path.Combine(reader.FullPath, stream.AbsolutePosition.ToString("X8"));
                var subReader = new FileReader(dummyPath, stream, true);
                if (subReader.FileType == FileType.Blb3File)
                    total += ExtractBlb3File(subReader, subSavePath);
                else if (subReader.FileType == FileType.VFSFile)
                    total += ExtractVFSFile(subReader, subSavePath);
                else
                    total += ExtractBundleFile(subReader, subSavePath);
            } while (stream.Remaining > 0);
            return total;
        }

        private static int ExtractVFSFile(FileReader reader, string savePath)
        {
            StatusStripUpdate($"Decompressing {reader.FileName} ...");
            try
            {
                var vfsFile = new VFSFile(reader, reader.FullPath, Studio.Game.Type);
                reader.Dispose();
                if (vfsFile.fileList != null && vfsFile.fileList.Count > 0)
                {
                    var extractPath = Path.Combine(savePath, reader.FileName + "_unpacked");
                    return ExtractStreamFile(extractPath, vfsFile.fileList);
                }
            }
            catch (Exception e)
            {
                Logger.Error($"Error while reading VFS file {reader.FullPath}", e);
            }
            return 0;
        }

        private static int ExtractMhyFile(FileReader reader, string savePath)
        {
            StatusStripUpdate($"Decompressing {reader.FileName} ...");
            try
            {
                var mhy0File = new MhyFile(reader, (Mhy)Game);
                reader.Dispose();
                if (mhy0File.fileList != null && mhy0File.fileList.Count > 0)
                {
                    var extractPath = Path.Combine(savePath, reader.FileName + "_unpacked");
                    return ExtractStreamFile(extractPath, mhy0File.fileList);
                }
            }
            catch (InvalidCastException)
            {
                Logger.Error($"Game type mismatch, Expected {nameof(Mhy)} but got {Game.Name} ({Game.GetType().Name}) !!");
            }
            return 0;
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

        #region Build asset data

        private sealed class AssetDataBuildContext
        {
            public int ObjectCount { get; }
            public int GameObjectCount { get; }
            public Dictionary<Object, AssetItem> ObjectAssetItems { get; }
            public List<(PPtr<Object> PPtr, string Name)> MiHoYoBinDataNames { get; } = new List<(PPtr<Object>, string)>();
            public List<(PPtr<Object> PPtr, string Container)> Containers { get; } = new List<(PPtr<Object>, string)>();
            public HashSet<AssetFilterKey> FastAssetFilterKeys { get; }
            private readonly Dictionary<ClassIDType, bool> canExportCache = new Dictionary<ClassIDType, bool>();
            public string AssetBundleName { get; set; } = "";
            public string ProductName { get; set; }

            public AssetDataBuildContext()
            {
                foreach (var assetsFile in assetsManager.assetsFileList)
                {
                    ObjectCount += assetsFile.Objects.Count;
                    foreach (var asset in assetsFile.Objects)
                    {
                        if (asset is GameObject)
                        {
                            GameObjectCount++;
                        }
                    }
                }

                ObjectAssetItems = new Dictionary<Object, AssetItem>(ObjectCount);
                FastAssetFilterKeys = assetsManager.FilterData.Items
                    .Select(x => new AssetFilterKey(x.Name, x.PathID, x.Type))
                    .ToHashSet();
            }

            public bool CanExport(ClassIDType type)
            {
                if (!canExportCache.TryGetValue(type, out var canExport))
                {
                    canExport = type.CanExport();
                    canExportCache.Add(type, canExport);
                }

                return canExport;
            }
        }

        private readonly struct AssetFilterKey : IEquatable<AssetFilterKey>
        {
            private readonly string name;
            private readonly long pathID;
            private readonly ClassIDType type;

            public AssetFilterKey(string name, long pathID, ClassIDType type)
            {
                this.name = name;
                this.pathID = pathID;
                this.type = type;
            }

            public bool Equals(AssetFilterKey other)
            {
                return type == other.type
                    && pathID == other.pathID
                    && string.Equals(name, other.name, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object obj)
            {
                return obj is AssetFilterKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(name ?? string.Empty), pathID, type);
            }
        }

        public static (string, List<TreeNode>) BuildAssetData()
        {
            StatusStripUpdate("Building asset list...");

            var context = new AssetDataBuildContext();
            Progress.Reset();
            Logger.Info($"Loading {context.ObjectCount} objects from {assetsManager.assetsFileList.Count} files.");

            if (!BuildExportableAssetItems(context) || !ApplyAssetPostProcessing(context))
            {
                return (string.Empty, Array.Empty<TreeNode>().ToList());
            }

            visibleAssets = exportableAssets;

            StatusStripUpdate("Building tree structure...");
            var treeNodeCollection = BuildSceneTree(context);
            context.ObjectAssetItems.Clear();

            if (treeNodeCollection == null)
            {
                return (string.Empty, Array.Empty<TreeNode>().ToList());
            }

            return (context.ProductName, treeNodeCollection);
        }

        private static List<TreeNode> BuildSceneTree(AssetDataBuildContext context)
        {
            var treeNodeCollection = new List<TreeNode>();
            var treeNodeDictionary = new Dictionary<GameObject, GameObjectTreeNode>(context.GameObjectCount);
            var hasFilterData = context.FastAssetFilterKeys.Count > 0;
            int j = 0;
            Progress.Reset();
            var files = assetsManager.assetsFileList.GroupBy(x => x.originalPath ?? string.Empty).OrderBy(x => x.Key).ToList();
            foreach (var fileGroup in files)
            {
                var file = fileGroup.Key;
                var fileNode = !string.IsNullOrEmpty(file) ? new TreeNode(Path.GetFileName(file)) : null; //RootNode

                foreach (var assetsFile in fileGroup)
                {
                    var assetsFileNode = new TreeNode(assetsFile.fileName);

                    foreach (var obj in assetsFile.Objects)
                    {
                        if (assetsManager.tokenSource.IsCancellationRequested)
                        {
                            Logger.Info("Building tree structure been cancelled !!");
                            return null;
                        }

                        if (obj is not GameObject)
                        {
                            if (hasFilterData && IsMissingFromFilter(obj.Name, obj.m_PathID, obj.type, context.FastAssetFilterKeys))
                            {
                                continue;
                            }

                            continue;
                        }

                        var m_GameObject = (GameObject)obj;
                        var currentNode = GetOrCreateGameObjectNode(m_GameObject, treeNodeDictionary);
                        AssignAssetTreeNodes(m_GameObject, currentNode, context);
                        var parentNode = GetParentTreeNode(m_GameObject, assetsFileNode, treeNodeDictionary);
                        parentNode.Nodes.Add(currentNode);
                    }

                    // TODO: need to do proper cleaning of list to only include filtered assets when required

                    if (assetsFileNode.Nodes.Count > 0)
                    {
                        if (fileNode == null)
                        {
                            treeNodeCollection.Add(assetsFileNode);
                        }
                        else
                        {
                            fileNode.Nodes.Add(assetsFileNode);
                        }
                    }
                }

                if (fileNode?.Nodes.Count > 0)
                {
                    treeNodeCollection.Add(fileNode);
                }

                Progress.Report(++j, files.Count);
            }
            treeNodeDictionary.Clear();

            return treeNodeCollection;
        }

        private static GameObjectTreeNode GetOrCreateGameObjectNode(GameObject gameObject, Dictionary<GameObject, GameObjectTreeNode> treeNodeDictionary)
        {
            if (!treeNodeDictionary.TryGetValue(gameObject, out var currentNode))
            {
                currentNode = new GameObjectTreeNode(gameObject);
                treeNodeDictionary.Add(gameObject, currentNode);
            }

            return currentNode;
        }

        private static void AssignAssetTreeNodes(GameObject gameObject, GameObjectTreeNode currentNode, AssetDataBuildContext context)
        {
            foreach (var pptr in gameObject.m_Components)
            {
                if (!pptr.TryGet(out var m_Component))
                {
                    continue;
                }

                AssignTreeNode(m_Component, currentNode, context);
                AssignMeshTreeNode(m_Component, currentNode, context);
            }
        }

        private static void AssignTreeNode(Object asset, GameObjectTreeNode currentNode, AssetDataBuildContext context)
        {
            if (context.ObjectAssetItems.TryGetValue(asset, out var assetItem))
            {
                assetItem.TreeNode = currentNode;
            }
        }

        private static void AssignMeshTreeNode(Object component, GameObjectTreeNode currentNode, AssetDataBuildContext context)
        {
            if (component is MeshFilter m_MeshFilter && m_MeshFilter.m_Mesh.TryGet(out var meshFromFilter))
            {
                AssignTreeNode(meshFromFilter, currentNode, context);
            }
            else if (component is SkinnedMeshRenderer m_SkinnedMeshRenderer && m_SkinnedMeshRenderer.m_Mesh.TryGet(out var meshFromRenderer))
            {
                AssignTreeNode(meshFromRenderer, currentNode, context);
            }
        }

        private static TreeNode GetParentTreeNode(GameObject gameObject, TreeNode assetsFileNode, Dictionary<GameObject, GameObjectTreeNode> treeNodeDictionary)
        {
            if (gameObject.m_Transform != null)
            {
                if (gameObject.m_Transform.m_Father.TryGet(out var m_Father))
                {
                    if (m_Father.m_GameObject.TryGet(out var parentGameObject))
                    {
                        return GetOrCreateGameObjectNode(parentGameObject, treeNodeDictionary);
                    }
                }
            }

            return assetsFileNode;
        }

        private static bool BuildExportableAssetItems(AssetDataBuildContext context)
        {
            int i = 0;
            var displayAll = Properties.Settings.Default.displayAll;
            var hasFilterData = context.FastAssetFilterKeys.Count > 0;
            if (displayAll && exportableAssets.Capacity < context.ObjectCount)
            {
                exportableAssets.Capacity = context.ObjectCount;
            }

            foreach (var assetsFile in assetsManager.assetsFileList)
            {
                foreach (var asset in assetsFile.Objects)
                {
                    if (assetsManager.tokenSource.IsCancellationRequested)
                    {
                        Logger.Info("Building asset list has been cancelled !!");
                        return false;
                    }

                    if (hasFilterData && asset is not AssetBundle && asset is not ResourceManager && IsMissingFromFilter(asset.Name, asset.m_PathID, asset.type, context.FastAssetFilterKeys))
                    {
                        continue;
                    }

                    AddAssetItem(asset, context, displayAll, i);
                    Progress.Report(++i, context.ObjectCount);
                }
            }

            return true;
        }

        private static void AddAssetItem(Object asset, AssetDataBuildContext context, bool displayAll, int index)
        {
            var assetItem = new AssetItem(asset);

            context.ObjectAssetItems.Add(asset, assetItem);
            assetItem.UniqueID = "#" + index;
            var exportable = ConfigureAssetItemAndReturnExportable(asset, assetItem, context);

            if (assetItem.Text == "")
            {
                assetItem.Text = assetItem.TypeString + assetItem.UniqueID;
            }
            if (displayAll || exportable)
            {
                exportableAssets.Add(assetItem);
            }
        }

        private static bool ApplyAssetPostProcessing(AssetDataBuildContext context)
        {
            foreach ((var pptr, var name) in context.MiHoYoBinDataNames)
            {
                if (assetsManager.tokenSource.IsCancellationRequested)
                {
                    Logger.Info("Processing asset names has been cancelled !!");
                    return false;
                }
                if (pptr.TryGet<MiHoYoBinData>(out var obj))
                {
                    var assetItem = context.ObjectAssetItems[obj];
                    if (int.TryParse(name, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hash))
                    {
                        assetItem.Text = name;
                        assetItem.Container = Properties.Settings.Default.useBundleContainerName ? context.AssetBundleName : hash.ToString();
                    }
                    else assetItem.Text = $"BinFile #{assetItem.m_PathID}";
                }
            }
            if (!SkipContainer)
            {
                foreach ((var pptr, var container) in context.Containers)
                {
                    if (assetsManager.tokenSource.IsCancellationRequested)
                    {
                        Logger.Info("Processing containers been cancelled !!");
                        return false;
                    }
                    if (pptr.TryGet(out var obj))
                    {
                        if (context.ObjectAssetItems.TryGetValue(obj, out var assetItem))
                        {
                            assetItem.Container = container;
                        }
                    }
                }
                context.Containers.Clear();
                if (Game.Type.IsGISubGroup() || Game.Type.IsZZZ())
                {
                    UpdateContainers();
                }
            }
            foreach (var tmp in exportableAssets)
            {
                if (assetsManager.tokenSource.IsCancellationRequested)
                {
                    Logger.Info("Processing subitems been cancelled !!");
                    return false;
                }
                tmp.SetSubItems();
            }

            return true;
        }

        private static bool ConfigureAssetItemAndReturnExportable(Object asset, AssetItem assetItem, AssetDataBuildContext context)
        {
            switch (asset)
            {
                case Texture2D m_Texture2D:
                    if (!string.IsNullOrEmpty(m_Texture2D.m_StreamData?.path))
                        assetItem.FullSize = asset.byteSize + m_Texture2D.m_StreamData.size;
                    return context.CanExport(ClassIDType.Texture2D);
                case AudioClip m_AudioClip:
                    if (!string.IsNullOrEmpty(m_AudioClip.m_Source))
                        assetItem.FullSize = asset.byteSize + m_AudioClip.m_Size;
                    return context.CanExport(ClassIDType.AudioClip);
                case VideoClip m_VideoClip:
                    if (!string.IsNullOrEmpty(m_VideoClip.m_OriginalPath))
                        assetItem.FullSize = asset.byteSize + m_VideoClip.m_ExternalResources.m_Size;
                    return context.CanExport(ClassIDType.VideoClip);
                case PlayerSettings m_PlayerSettings:
                    context.ProductName = m_PlayerSettings.productName;
                    return context.CanExport(ClassIDType.PlayerSettings);
                case AssetBundle m_AssetBundle:
                    context.AssetBundleName = m_AssetBundle.Name;
                    AddAssetBundleContainers(m_AssetBundle, context);
                    return context.CanExport(ClassIDType.AssetBundle);
                case IndexObject m_IndexObject:
                    foreach(var index in m_IndexObject.AssetMap)
                    {
                        context.MiHoYoBinDataNames.Add((index.Value.Object, index.Key));
                    }

                    return context.CanExport(ClassIDType.IndexObject);
                case ResourceManager m_ResourceManager:
                    foreach (var m_Container in m_ResourceManager.m_Container)
                    {
                        context.Containers.Add((m_Container.Value, m_Container.Key));
                    }

                    return context.CanExport(ClassIDType.ResourceManager);
                case Mesh _:
                    return context.CanExport(ClassIDType.Mesh);
                case TextAsset _:
                    return context.CanExport(ClassIDType.TextAsset);
                case AnimationClip _:
                    return context.CanExport(ClassIDType.AnimationClip);
                case Font _:
                    return context.CanExport(ClassIDType.Font);
                case MovieTexture _:
                    return context.CanExport(ClassIDType.MovieTexture);
                case Sprite _:
                    return context.CanExport(ClassIDType.Sprite);
                case Material _:
                    return context.CanExport(ClassIDType.Material);
                case MiHoYoBinData _:
                    return context.CanExport(ClassIDType.MiHoYoBinData);
                case NapAssetBundleIndexAsset _:
                    return context.CanExport(ClassIDType.NapAssetBundleIndexAsset);
                case Shader _:
                    return context.CanExport(ClassIDType.Shader);
                case Animator _:
                    return context.CanExport(ClassIDType.Animator);
                case MonoBehaviour _:
                    return context.CanExport(ClassIDType.MonoBehaviour);
                default:
                    return false;
            }
        }

        private static void AddAssetBundleContainers(AssetBundle assetBundle, AssetDataBuildContext context)
        {
            if (SkipContainer)
            {
                return;
            }

            foreach (var m_Container in assetBundle.m_Container)
            {
                var preloadIndex = m_Container.Value.preloadIndex;
                var preloadSize = m_Container.Value.preloadSize;
                var preloadEnd = preloadIndex + preloadSize;

                switch (preloadIndex)
                {
                    case int n when n < 0:
                        Logger.Warning($"preloadIndex {preloadIndex} is out of preloadTable range");
                        break;
                    default:
                        for (int k = preloadIndex; k < preloadEnd; k++)
                        {
                            try
                            {
                                context.Containers.Add((assetBundle.m_PreloadTable[k], m_Container.Key));
                            } catch
                            {
                                Logger.Info($"Failed to add container {m_Container.Key}");
                            }
                        }
                        break;
                }
            }
        }

        private static bool IsMissingFromFilter(string name, long pathID, ClassIDType type, HashSet<AssetFilterKey> fastAssetFilterKeys)
        {
            if (fastAssetFilterKeys.Count == 0)
            {
                return false;
            }
            if (fastAssetFilterKeys.Contains(new AssetFilterKey(name, pathID, type)))
            {
                return false;
            }

            Logger.Verbose($"Skipped {(name.Length > 0 ? name : "an asset")} because filter data was set and it was missing from it");
            return true;
        }

        #endregion

        public static Dictionary<string, SortedDictionary<int, TypeTreeItem>> BuildClassStructure()
        {
            var typeMap = new Dictionary<string, SortedDictionary<int, TypeTreeItem>>();
            foreach (var assetsFile in assetsManager.assetsFileList)
            {
                if (assetsManager.tokenSource.IsCancellationRequested)
                {
                    Logger.Info("Processing class structure been cancelled !!");
                    return new Dictionary<string, SortedDictionary<int, TypeTreeItem>>();
                }
                if (typeMap.TryGetValue(assetsFile.unityVersion, out var curVer))
                {
                    foreach (var type in assetsFile.m_Types.Where(x => x.m_Type != null))
                    {
                        var key = type.classID;
                        if (type.m_ScriptTypeIndex >= 0)
                        {
                            key = -1 - type.m_ScriptTypeIndex;
                        }
                        curVer[key] = new TypeTreeItem(key, type.m_Type);
                    }
                }
                else
                {
                    var items = new SortedDictionary<int, TypeTreeItem>();
                    foreach (var type in assetsFile.m_Types.Where(x => x.m_Type != null))
                    {
                        var key = type.classID;
                        if (type.m_ScriptTypeIndex >= 0)
                        {
                            key = -1 - type.m_ScriptTypeIndex;
                        }
                        items[key] = new TypeTreeItem(key, type.m_Type);
                    }
                    typeMap.Add(assetsFile.unityVersion, items);
                }
            }

            return typeMap;
        }

        #region Export assets

        public static Task ExportAssets(string savePath, List<AssetItem> toExportAssets, ExportType exportType, bool openAfterExport)
        {
            return Task.Run(() =>
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

                int toExportCount = toExportAssets.Count;
                int exportedCount = 0;
                int i = 0;
                Progress.Reset();
                var assetGroupOption = (AssetGroupOption)Properties.Settings.Default.assetGroupOption;
                foreach (var asset in toExportAssets)
                {
                    var exportPath = GetAssetExportPath(savePath, asset, assetGroupOption);
                    StatusStripUpdate($"[{exportedCount}/{toExportCount}] Exporting {asset.TypeString}: {asset.Text}");

                    if (TryExportAsset(asset, exportPath, exportType))
                    {
                        exportedCount++;
                    }

                    Progress.Report(++i, toExportCount);
                }

                StatusStripUpdate(GetExportAssetsStatus(toExportCount, exportedCount));

                if (openAfterExport && exportedCount > 0)
                {
                    OpenFolderInExplorer(savePath);
                }
            });
        }

        private static string GetAssetExportPath(string savePath, AssetItem asset, AssetGroupOption assetGroupOption)
        {
            string exportPath;
            switch (assetGroupOption)
            {
                case AssetGroupOption.ByType:
                    exportPath = Path.Combine(savePath, asset.TypeString);
                    break;
                case AssetGroupOption.ByContainer:
                    exportPath = GetContainerExportPath(savePath, asset);
                    break;
                case AssetGroupOption.BySource:
                    exportPath = GetSourceExportPath(savePath, asset);
                    break;
                default:
                    exportPath = savePath;
                    break;
            }

            return exportPath + Path.DirectorySeparatorChar;
        }

        private static string GetContainerExportPath(string savePath, AssetItem asset)
        {
            if (string.IsNullOrEmpty(asset.Container))
            {
                return savePath;
            }

            return Path.HasExtension(asset.Container)
                ? Path.Combine(savePath, Path.GetDirectoryName(asset.Container))
                : Path.Combine(savePath, asset.Container);
        }

        private static string GetSourceExportPath(string savePath, AssetItem asset)
        {
            if (string.IsNullOrEmpty(asset.SourceFile.originalPath))
            {
                return Path.Combine(savePath, asset.SourceFile.fileName + "_export");
            }

            return Path.Combine(savePath, Path.GetFileName(asset.SourceFile.originalPath) + "_export", asset.SourceFile.fileName);
        }

        private static bool TryExportAsset(AssetItem asset, string exportPath, ExportType exportType)
        {
            try
            {
                switch (exportType)
                {
                    case ExportType.Raw:
                        return ExportRawFile(asset, exportPath);
                    case ExportType.Dump:
                        return ExportDumpFile(asset, exportPath);
                    case ExportType.Convert:
                        return ExportConvertFile(asset, exportPath);
                    case ExportType.JSON:
                        return ExportJSONFile(asset, exportPath);
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Export {asset.Type}:{asset.Text} error\r\n{ex.Message}\r\n{ex.StackTrace}");
                return false;
            }
        }

        private static string GetExportAssetsStatus(int toExportCount, int exportedCount)
        {
            var statusText = exportedCount == 0 ? "Nothing exported." : $"Finished exporting {exportedCount} assets.";

            if (toExportCount > exportedCount)
            {
                statusText += $" {toExportCount - exportedCount} assets skipped (not extractable or files already exist)";
            }

            return statusText;
        }

        #endregion

        public static Task ExportAssetsList(string savePath, List<AssetItem> toExportAssets, ExportListType exportListType)
        {
            return Task.Run(() =>
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

                Progress.Reset();

                switch (exportListType)
                {
                    case ExportListType.XML:
                        var filename = Path.Combine(savePath, "assets.xml");
                        var settings = new XmlWriterSettings() { Indent = true };
                        using (XmlWriter writer = XmlWriter.Create(filename, settings))
                        {
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
                                writer.WriteValue(asset.TypeString);
                                writer.WriteEndElement();
                                writer.WriteElementString("PathID", asset.m_PathID.ToString());
                                writer.WriteElementString("Source", asset.SourceFile.fullName);
                                writer.WriteElementString("Size", asset.FullSize.ToString());
                                writer.WriteEndElement();
                            }
                            writer.WriteEndElement();
                            writer.WriteEndDocument();
                        }
                        break;
                }

                var statusText = $"Finished exporting asset list with {toExportAssets.Count()} items.";

                StatusStripUpdate(statusText);

                if (Properties.Settings.Default.openAfterExport && toExportAssets.Count() > 0)
                {
                    OpenFolderInExplorer(savePath);
                }
            });
        }

        #region Export split objects

        public static Task ExportSplitObjects(string savePath, TreeNodeCollection nodes)
        {
            return Task.Run(() =>
            {
                var exportNodes = GetSplitExportNodes(nodes).ToList();
                var count = exportNodes.Sum(x => x.Nodes.Count);
                int k = 0;
                Progress.Reset();

                foreach (var node in exportNodes)
                {
                    foreach (GameObjectTreeNode childNode in node.Nodes)
                    {
                        ExportSplitObject(savePath, node, childNode);
                        Progress.Report(++k, count);
                    }
                }

                if (Properties.Settings.Default.openAfterExport)
                {
                    OpenFolderInExplorer(savePath);
                }
                StatusStripUpdate("Finished");
            });
        }

        private static IEnumerable<TreeNode> GetSplitExportNodes(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Nodes.Count == 0)
                {
                    yield return node;
                    continue;
                }

                foreach (TreeNode subNode in node.Nodes)
                {
                    yield return subNode;
                }
            }
        }

        private static void ExportSplitObject(string savePath, TreeNode parentNode, GameObjectTreeNode node)
        {
            var gameObjects = new List<GameObject>();
            CollectNode(node, gameObjects);
            if (gameObjects.All(x => x.m_SkinnedMeshRenderer == null && x.m_MeshFilter == null))
            {
                return;
            }

            var filename = GetSplitObjectFileName(parentNode, node);
            var targetPath = GetUniqueSplitObjectTargetPath(savePath, filename);
            Directory.CreateDirectory(targetPath);

            StatusStripUpdate($"Exporting {filename}.fbx");
            try
            {
                ExportGameObject(node.gameObject, targetPath);
            }
            catch (Exception ex)
            {
                Logger.Error($"Export GameObject:{node.Text} error\r\n{ex.Message}\r\n{ex.StackTrace}");
            }

            StatusStripUpdate($"Finished exporting {filename}.fbx");
        }

        private static string GetSplitObjectFileName(TreeNode parentNode, GameObjectTreeNode node)
        {
            var filename = FixFileName(node.Text);
            if (parentNode.Parent != null)
            {
                filename = Path.Combine(FixFileName(parentNode.Parent.Text), filename);
            }

            return filename;
        }

        private static string GetUniqueSplitObjectTargetPath(string savePath, string filename)
        {
            var targetPath = $"{savePath}{filename}{Path.DirectorySeparatorChar}";
            for (int i = 1; Directory.Exists(targetPath); i++)
            {
                targetPath = $"{savePath}{filename} ({i}){Path.DirectorySeparatorChar}";
            }

            return targetPath;
        }

        private static void CollectNode(GameObjectTreeNode node, List<GameObject> gameObjects)
        {
            gameObjects.Add(node.gameObject);
            foreach (GameObjectTreeNode i in node.Nodes)
            {
                CollectNode(i, gameObjects);
            }
        }

        #endregion

        public static Task ExportAnimatorWithAnimationClip(AssetItem animator, List<AssetItem> animationList, string exportPath)
        {
            return Task.Run(() =>
            {
                Progress.Reset();
                StatusStripUpdate($"Exporting {animator.Text}");
                try
                {
                    ExportAnimator(animator, exportPath, animationList);
                    if (Properties.Settings.Default.openAfterExport)
                    {
                        OpenFolderInExplorer(exportPath);
                    }
                    Progress.Report(1, 1);
                    StatusStripUpdate($"Finished exporting {animator.Text}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Export Animator:{animator.Text} error\r\n{ex.Message}\r\n{ex.StackTrace}");
                    StatusStripUpdate("Error in export");
                }
            });
        }

        public static Task ExportObjectsWithAnimationClip(string exportPath, TreeNodeCollection nodes, List<AssetItem> animationList = null)
        {
            return Task.Run(() =>
            {
                var gameObjects = new List<GameObject>();
                GetSelectedParentNode(nodes, gameObjects);
                if (gameObjects.Count > 0)
                {
                    var count = gameObjects.Count;
                    int i = 0;
                    Progress.Reset();
                    foreach (var gameObject in gameObjects)
                    {
                        StatusStripUpdate($"Exporting {gameObject.m_Name}");
                        try
                        {
                            var subExportPath = Path.Combine(exportPath, gameObject.m_Name) + Path.DirectorySeparatorChar;
                            ExportGameObject(gameObject, subExportPath, animationList);
                            StatusStripUpdate($"Finished exporting {gameObject.m_Name}");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Export GameObject:{gameObject.m_Name} error\r\n{ex.Message}\r\n{ex.StackTrace}");
                            StatusStripUpdate("Error in export");
                        }

                        Progress.Report(++i, count);
                    }
                    if (Properties.Settings.Default.openAfterExport)
                    {
                        OpenFolderInExplorer(exportPath);
                    }
                }
                else
                {
                    StatusStripUpdate("No Object selected for export.");
                }
            });
        }

        public static Task ExportObjectsMergeWithAnimationClip(string exportPath, List<GameObject> gameObjects, List<AssetItem> animationList = null)
        {
            return Task.Run(() =>
            {
                var name = Path.GetFileName(exportPath);
                Progress.Reset();
                StatusStripUpdate($"Exporting {name}");
                try
                {
                    ExportGameObjectMerge(gameObjects, exportPath, animationList);
                    Progress.Report(1, 1);
                    StatusStripUpdate($"Finished exporting {name}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Export Model:{name} error\r\n{ex.Message}\r\n{ex.StackTrace}");
                    StatusStripUpdate("Error in export");
                }
                if (Properties.Settings.Default.openAfterExport)
                {
                    OpenFolderInExplorer(Path.GetDirectoryName(exportPath));
                }
            });
        }

        public static Task ExportNodesWithAnimationClip(string exportPath, List<TreeNode> nodes, List<AssetItem> animationList = null)
        {
            return Task.Run(() =>
            {
                int i = 0;
                Progress.Reset();
                foreach (var node in nodes)
                {
                    var name = node.Text;
                    StatusStripUpdate($"Exporting {name}");
                    var gameObjects = new List<GameObject>();
                    GetSelectedParentNode(node.Nodes, gameObjects);
                    if (gameObjects.Count > 0)
                    {
                        var subExportPath = exportPath + Path.Combine(node.Text, FixFileName(node.Text) + ".fbx");
                        try
                        {
                            ExportGameObjectMerge(gameObjects, subExportPath, animationList);
                            Progress.Report(++i, nodes.Count);
                            StatusStripUpdate($"Finished exporting {name}");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Export Model:{name} error\r\n{ex.Message}\r\n{ex.StackTrace}");
                            StatusStripUpdate("Error in export");
                        }
                    }
                    else
                    {
                        StatusStripUpdate("Empty node selected for export.");
                    }
                }
                if (Properties.Settings.Default.openAfterExport)
                {
                    OpenFolderInExplorer(exportPath);
                }
            });
        }

        public static void GetSelectedParentNode(TreeNodeCollection nodes, List<GameObject> gameObjects)
        {
            foreach (TreeNode i in nodes)
            {
                if (i is GameObjectTreeNode gameObjectTreeNode && i.Checked)
                {
                    gameObjects.Add(gameObjectTreeNode.gameObject);
                }
                else
                {
                    GetSelectedParentNode(i.Nodes, gameObjects);
                }
            }
        }

        public static TypeTree MonoBehaviourToTypeTree(MonoBehaviour m_MonoBehaviour)
        {
            if (!assemblyLoader.Loaded)
            {
                var openFolderDialog = new OpenFolderDialog();
                openFolderDialog.Title = "Select Assembly Folder";
                if (openFolderDialog.ShowDialog() == DialogResult.OK)
                {
                    assemblyLoader.Load(openFolderDialog.Folder);
                }
                else
                {
                    assemblyLoader.Loaded = true;
                }
            }
            return m_MonoBehaviour.ConvertToTypeTree(assemblyLoader);
        }

        public static string DumpAsset(Object obj)
        {
            var str = obj.Dump();
            if (str == null && obj is MonoBehaviour m_MonoBehaviour)
            {
                var type = MonoBehaviourToTypeTree(m_MonoBehaviour);
                str = m_MonoBehaviour.Dump(type);
            }
            if (string.IsNullOrEmpty(str))
            {
                var settings = new JsonSerializerSettings();
                settings.Converters.Add(new StringEnumConverter());
                str = JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented, settings);
            }
            return str;
        }

        public static void OpenFolderInExplorer(string path)
        {
            var info = new ProcessStartInfo(path);
            info.UseShellExecute = true;
            Process.Start(info);
        }
    }
}
