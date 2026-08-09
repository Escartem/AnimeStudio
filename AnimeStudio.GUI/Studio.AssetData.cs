using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AnimeStudio.GUI
{
    internal static partial class Studio
    {
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
                return (string.Empty, new List<TreeNode>());
            }

            visibleAssets = exportableAssets;

            StatusStripUpdate("Building tree structure...");
            var treeNodeCollection = BuildSceneTree(context);
            context.ObjectAssetItems.Clear();

            if (treeNodeCollection == null)
            {
                return (string.Empty, new List<TreeNode>());
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
    }
}

