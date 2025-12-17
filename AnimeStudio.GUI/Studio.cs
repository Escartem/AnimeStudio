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
                var vfsFile = new VFSFile(reader, reader.FullPath);
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
            if (exportableAssets.Count > 0)
            {
                Logger.Info("Updating Containers...");
                foreach (var asset in exportableAssets)
                {
                    if (int.TryParse(asset.Container, out var value))
                    {
                        var last = unchecked((uint)value);
                        var name = Path.GetFileNameWithoutExtension(asset.SourceFile.originalPath);
                        if (uint.TryParse(name, out var id))
                        {
                            var path = ResourceIndex.GetContainer(id, last);
                            if (!string.IsNullOrEmpty(path))
                            {
                                asset.Container = path;
                                if (asset.Type == ClassIDType.MiHoYoBinData)
                                {
                                    asset.Text = Path.GetFileNameWithoutExtension(path);
                                }
                            }
                        }
                    }
                }
                Logger.Info("Updated !!");
            }
        }

        public static (string, List<TreeNode>) BuildAssetData()
        {
            StatusStripUpdate("Building asset list...");

            int i = 0;
            string productName = null;
            var objectCount = assetsManager.assetsFileList.Sum(x => x.Objects.Count);
            var objectAssetItemDic = new Dictionary<Object, AssetItem>(objectCount);
            var mihoyoBinDataNames = new List<(PPtr<Object>, string)>();
            var containers = new List<(PPtr<Object>, string)>();
            Progress.Reset();
            Logger.Info($"Loading {objectCount} objects from {assetsManager.assetsFileList.Count} files.");
            var assetBundleName = "";

            var fastAssetItemFilterData = new HashSet<AssetFilterDataItem>(assetsManager.FilterData.Items, new AssetFilterDataItemEqualityComparer());
            foreach (var assetsFile in assetsManager.assetsFileList)
            {
                foreach (var asset in assetsFile.Objects)
                {
                    if (assetsManager.tokenSource.IsCancellationRequested)
                    {
                        Logger.Info("Building asset list has been cancelled !!");
                        return (string.Empty, Array.Empty<TreeNode>().ToList());
                    }

                    var assetItem = new AssetItem(asset);

                    if (asset is not AssetBundle && asset is not ResourceManager)
                    {
                        if (fastAssetItemFilterData.Count > 0 && !fastAssetItemFilterData.Contains(new AssetFilterDataItem { Source = assetItem.SourceFile.fullName, Name = assetItem.Text, PathID = assetItem.m_PathID, Type = assetItem.Type }))
                        {
                            Logger.Verbose($"Skipped {(assetItem.Text.Length > 0 ? assetItem.Text : "an asset")} because filter data was set and it was missing from it");
                            continue;
                        }
                    }
                    
                    objectAssetItemDic.Add(asset, assetItem);
                    assetItem.UniqueID = "#" + i;
                    var exportable = false;
                    switch (asset)
                    {
                        case Texture2D m_Texture2D:
                            if (!string.IsNullOrEmpty(m_Texture2D.m_StreamData?.path))
                                assetItem.FullSize = asset.byteSize + m_Texture2D.m_StreamData.size;
                            exportable = ClassIDType.Texture2D.CanExport();
                            break;
                        case AudioClip m_AudioClip:
                            if (!string.IsNullOrEmpty(m_AudioClip.m_Source))
                                assetItem.FullSize = asset.byteSize + m_AudioClip.m_Size;
                            exportable = ClassIDType.AudioClip.CanExport();
                            break;
                        case VideoClip m_VideoClip:
                            if (!string.IsNullOrEmpty(m_VideoClip.m_OriginalPath))
                                assetItem.FullSize = asset.byteSize + m_VideoClip.m_ExternalResources.m_Size;
                            exportable = ClassIDType.VideoClip.CanExport();
                            break;
                        case PlayerSettings m_PlayerSettings:
                            productName = m_PlayerSettings.productName;
                            exportable = ClassIDType.PlayerSettings.CanExport();
                            break;
                        case AssetBundle m_AssetBundle:
                            assetBundleName = m_AssetBundle.Name;
                            if (!SkipContainer)
                            {
                                foreach (var m_Container in m_AssetBundle.m_Container)
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
                                                string containerName = m_Container.Key;
                                                if (int.TryParse(m_Container.Key, out _) && Properties.Settings.Default.useBundleContainerName)
                                                {
                                                    containerName = assetBundleName;
                                                }
                                                try
                                                {
                                                    containers.Add((m_AssetBundle.m_PreloadTable[k], m_Container.Key));
                                                } catch
                                                {
                                                    Logger.Info($"Failed to add container {m_Container.Key}");
                                                }
                                            }
                                            break;
                                    }
                                }
                            }

                            exportable = ClassIDType.AssetBundle.CanExport();
                            break;
                        case IndexObject m_IndexObject:
                            foreach(var index in m_IndexObject.AssetMap)
                            {
                                mihoyoBinDataNames.Add((index.Value.Object, index.Key));
                            }

                            exportable = ClassIDType.IndexObject.CanExport();
                            break;
                        case ResourceManager m_ResourceManager:
                            foreach (var m_Container in m_ResourceManager.m_Container)
                            {
                                containers.Add((m_Container.Value, m_Container.Key));
                            }

                            exportable = ClassIDType.ResourceManager.CanExport();
                            break;
                        case Mesh _ when ClassIDType.Mesh.CanExport():
                        case TextAsset _ when ClassIDType.TextAsset.CanExport():
                        case AnimationClip _ when ClassIDType.AnimationClip.CanExport():
                        case Font _ when ClassIDType.Font.CanExport():
                        case MovieTexture _ when ClassIDType.MovieTexture.CanExport():
                        case Sprite _ when ClassIDType.Sprite.CanExport():
                        case Material _ when ClassIDType.Material.CanExport():
                        case MiHoYoBinData _ when ClassIDType.MiHoYoBinData.CanExport():
                        case NapAssetBundleIndexAsset _ when ClassIDType.NapAssetBundleIndexAsset.CanExport():
                        case Shader _ when ClassIDType.Shader.CanExport():
                        case Animator _ when ClassIDType.Animator.CanExport():
                        case MonoBehaviour _ when ClassIDType.MonoBehaviour.CanExport():
                            exportable = true;
                            break;
                    }
                    if (assetItem.Text == "")
                    {
                        assetItem.Text = assetItem.TypeString + assetItem.UniqueID;
                    }
                    if (Properties.Settings.Default.displayAll || exportable)
                    {
                        exportableAssets.Add(assetItem);
                    }
                    Progress.Report(++i, objectCount);
                }
            }
            foreach((var pptr, var name) in mihoyoBinDataNames)
            {
                if (assetsManager.tokenSource.IsCancellationRequested)
                {
                    Logger.Info("Processing asset names has been cancelled !!");
                    return (string.Empty, Array.Empty<TreeNode>().ToList());
                }
                if (pptr.TryGet<MiHoYoBinData>(out var obj))
                {
                    var assetItem = objectAssetItemDic[obj];
                    if (int.TryParse(name, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hash))
                    {
                        assetItem.Text = name;
                        assetItem.Container = Properties.Settings.Default.useBundleContainerName ? assetBundleName : hash.ToString();
                    }
                    else assetItem.Text = $"BinFile #{assetItem.m_PathID}";
                }
            }
            if (!SkipContainer)
            {
                foreach ((var pptr, var container) in containers)
                {
                    if (assetsManager.tokenSource.IsCancellationRequested)
                    {
                        Logger.Info("Processing containers been cancelled !!");
                        return (string.Empty, Array.Empty<TreeNode>().ToList());
                    }
                    if (pptr.TryGet(out var obj))
                    {
                        if (objectAssetItemDic.ContainsKey(obj))
                        {
                            objectAssetItemDic[obj].Container = container;
                        }
                    }
                }
                containers.Clear();
                if (Game.Type.IsGISubGroup())
                {
                    UpdateContainers();
                }
            }
            foreach (var tmp in exportableAssets)
            {
                if (assetsManager.tokenSource.IsCancellationRequested)
                {
                    Logger.Info("Processing subitems been cancelled !!");
                    return (string.Empty, Array.Empty<TreeNode>().ToList());
                }
                tmp.SetSubItems();
            }

            visibleAssets = exportableAssets;

            StatusStripUpdate("Building tree structure...");

            var treeNodeCollection = new List<TreeNode>();
            var treeNodeDictionary = new Dictionary<GameObject, GameObjectTreeNode>();
            int j = 0;
            Progress.Reset();
            var files = assetsManager.assetsFileList.GroupBy(x => x.originalPath ?? string.Empty).OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.ToList());
            foreach (var (file, assetsFiles) in files)
            {
                var fileNode = !string.IsNullOrEmpty(file) ? new TreeNode(Path.GetFileName(file)) : null; //RootNode

                foreach (var assetsFile in assetsFiles)
                {
                    var assetsFileNode = new TreeNode(assetsFile.fileName);

                    foreach (var obj in assetsFile.Objects)
                    {
                        if (assetsManager.tokenSource.IsCancellationRequested)
                        {
                            Logger.Info("Building tree structure been cancelled !!");
                            return (string.Empty, Array.Empty<TreeNode>().ToList());
                        }

                        var assetItem = new AssetItem(obj);

                        if (obj is not GameObject)
                        {
                            if (fastAssetItemFilterData.Count > 0 && !fastAssetItemFilterData.Contains(new AssetFilterDataItem { Source = assetItem.SourceFile.fullName, Name = assetItem.Text, PathID = assetItem.m_PathID, Type = assetItem.Type }))
                            {
                                Logger.Verbose($"Skipped {(assetItem.Text.Length > 0 ? assetItem.Text : "an asset")} because filter data was set and it was missing from it");
                                continue;
                            }
                        }

                        if (obj is GameObject m_GameObject)
                        {
                            if (!treeNodeDictionary.TryGetValue(m_GameObject, out var currentNode))
                            {
                                currentNode = new GameObjectTreeNode(m_GameObject);
                                treeNodeDictionary.Add(m_GameObject, currentNode);
                            }

                            foreach (var pptr in m_GameObject.m_Components)
                            {
                                if (pptr.TryGet(out var m_Component))
                                {
                                    if (objectAssetItemDic.ContainsKey(m_Component))
                                    {
                                        objectAssetItemDic[m_Component].TreeNode = currentNode;
                                    }
                                    
                                    if (m_Component is MeshFilter m_MeshFilter)
                                    {
                                        if (m_MeshFilter.m_Mesh.TryGet(out var m_Mesh))
                                        {
                                            if (objectAssetItemDic.ContainsKey(m_Mesh))
                                            {
                                                objectAssetItemDic[m_Mesh].TreeNode = currentNode;
                                            }
                                        }
                                    }
                                    else if (m_Component is SkinnedMeshRenderer m_SkinnedMeshRenderer)
                                    {
                                        if (m_SkinnedMeshRenderer.m_Mesh.TryGet(out var m_Mesh))
                                        {
                                            if (objectAssetItemDic.ContainsKey(m_Mesh))
                                            {
                                                objectAssetItemDic[m_Mesh].TreeNode = currentNode;
                                            }
                                        }
                                    }
                                }
                            }

                            var parentNode = assetsFileNode;

                            if (m_GameObject.m_Transform != null)
                            {
                                if (m_GameObject.m_Transform.m_Father.TryGet(out var m_Father))
                                {
                                    if (m_Father.m_GameObject.TryGet(out var parentGameObject))
                                    {
                                        if (!treeNodeDictionary.TryGetValue(parentGameObject, out var parentGameObjectNode))
                                        {
                                            parentGameObjectNode = new GameObjectTreeNode(parentGameObject);
                                            treeNodeDictionary.Add(parentGameObject, parentGameObjectNode);
                                        }
                                        parentNode = parentGameObjectNode;
                                    }
                                }
                            }

                            parentNode.Nodes.Add(currentNode);
                        }
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

            objectAssetItemDic.Clear();

            return (productName, treeNodeCollection);
        }

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

        public static Task ExportAssets(string savePath, List<AssetItem> toExportAssets, ExportType exportType, bool openAfterExport)
        {
            return Task.Run(() =>
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

                int toExportCount = toExportAssets.Count;
                int exportedCount = 0;
                int i = 0;
                Progress.Reset();
                foreach (var asset in toExportAssets)
                {
                    string exportPath;
                    switch ((AssetGroupOption)Properties.Settings.Default.assetGroupOption)
                    {
                        case AssetGroupOption.ByType: //type name
                            exportPath = Path.Combine(savePath, asset.TypeString);
                            break;
                        case AssetGroupOption.ByContainer: //container path
                            if (!string.IsNullOrEmpty(asset.Container))
                            {
                                exportPath = Path.HasExtension(asset.Container) ? Path.Combine(savePath, Path.GetDirectoryName(asset.Container)) : Path.Combine(savePath, asset.Container);
                            }
                            else
                            {
                                exportPath = savePath;
                            }
                            break;
                        case AssetGroupOption.BySource: //source file
                            if (string.IsNullOrEmpty(asset.SourceFile.originalPath))
                            {
                                exportPath = Path.Combine(savePath, asset.SourceFile.fileName + "_export");
                            }
                            else
                            {
                                exportPath = Path.Combine(savePath, Path.GetFileName(asset.SourceFile.originalPath) + "_export", asset.SourceFile.fileName);
                            }
                            break;
                        default:
                            exportPath = savePath;
                            break;
                    }
                    exportPath += Path.DirectorySeparatorChar;
                    StatusStripUpdate($"[{exportedCount}/{toExportCount}] Exporting {asset.TypeString}: {asset.Text}");
                    try
                    {
                        switch (exportType)
                        {
                            case ExportType.Raw:
                                if (ExportRawFile(asset, exportPath))
                                {
                                    exportedCount++;
                                }
                                break;
                            case ExportType.Dump:
                                if (ExportDumpFile(asset, exportPath))
                                {
                                    exportedCount++;
                                }
                                break;
                            case ExportType.Convert:
                                if (ExportConvertFile(asset, exportPath))
                                {
                                    exportedCount++;
                                }
                                break;
                            case ExportType.JSON:
                                if (ExportJSONFile(asset, exportPath))
                                {
                                    exportedCount++;
                                }
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Export {asset.Type}:{asset.Text} error\r\n{ex.Message}\r\n{ex.StackTrace}");
                    }

                    Progress.Report(++i, toExportCount);
                }

                var statusText = exportedCount == 0 ? "Nothing exported." : $"Finished exporting {exportedCount} assets.";

                if (toExportCount > exportedCount)
                {
                    statusText += $" {toExportCount - exportedCount} assets skipped (not extractable or files already exist)";
                }

                StatusStripUpdate(statusText);

                if (openAfterExport && exportedCount > 0)
                {
                    OpenFolderInExplorer(savePath);
                }
            });
        }

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

        public static Task ExportSplitObjects(string savePath, TreeNodeCollection nodes)
        {
            return Task.Run(() =>
            {
                var exportNodes = GetNodes(nodes);
                var count = exportNodes.Cast<TreeNode>().Sum(x => x.Nodes.Count);
                int k = 0;
                Progress.Reset();
                foreach (TreeNode node in exportNodes)
                {
                    //遍历一级子节点
                    foreach (GameObjectTreeNode j in node.Nodes)
                    {
                        //收集所有子节点
                        var gameObjects = new List<GameObject>();
                        CollectNode(j, gameObjects);
                        //跳过一些不需要导出的object
                        if (gameObjects.All(x => x.m_SkinnedMeshRenderer == null && x.m_MeshFilter == null))
                        {
                            Progress.Report(++k, count);
                            continue;
                        }
                        //处理非法文件名
                        var filename = FixFileName(j.Text);
                        if (node.Parent != null) 
                        {
                            filename = Path.Combine(FixFileName(node.Parent.Text), filename);
                        }
                        //每个文件存放在单独的文件夹
                        var targetPath = $"{savePath}{filename}{Path.DirectorySeparatorChar}";
                        //重名文件处理
                        for (int i = 1; ; i++)
                        {
                            if (Directory.Exists(targetPath))
                            {
                                targetPath = $"{savePath}{filename} ({i}){Path.DirectorySeparatorChar}";
                            }
                            else
                            {
                                break;
                            }
                        }
                        Directory.CreateDirectory(targetPath);
                        //导出FBX
                        StatusStripUpdate($"Exporting {filename}.fbx");
                        try
                        {
                            ExportGameObject(j.gameObject, targetPath);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Export GameObject:{j.Text} error\r\n{ex.Message}\r\n{ex.StackTrace}");
                        }

                        Progress.Report(++k, count);
                        StatusStripUpdate($"Finished exporting {filename}.fbx");
                    }
                }
                if (Properties.Settings.Default.openAfterExport)
                {
                    OpenFolderInExplorer(savePath);
                }
                StatusStripUpdate("Finished");

                IEnumerable<TreeNode> GetNodes(TreeNodeCollection nodes)
                {
                    foreach(TreeNode node in nodes)
                    {
                        var subNodes = node.Nodes.OfType<TreeNode>().ToArray();
                        if (subNodes.Length == 0)
                        {
                            yield return node;
                        }
                        else
                        {
                            foreach (TreeNode subNode in subNodes)
                            {
                                yield return subNode;
                            }
                        }
                    }
                }
            });
        }

        private static void CollectNode(GameObjectTreeNode node, List<GameObject> gameObjects)
        {
            gameObjects.Add(node.gameObject);
            foreach (GameObjectTreeNode i in node.Nodes)
            {
                CollectNode(i, gameObjects);
            }
        }

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
