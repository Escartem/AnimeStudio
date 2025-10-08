using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using static AnimeStudio.AssetsManager;

namespace AnimeStudio.GUIV2
{
    class Studio
    {
        public static Game Game;
        public static AssetsManager assetsManager = new AssetsManager();
        public static List<AssetItem> exportableAssets = new List<AssetItem>();
        public static ObservableCollection<AssetItem> visibleAssets { get; } = new();

        public static (string, ObservableCollection<object>) BuildAssetData()
        {
            //StatusStripUpdate("Building asset list...");

            int i = 0;
            string productName = null;
            var objectCount = assetsManager.assetsFileList.Sum(x => x.Objects.Count);
            var objectAssetItemDic = new Dictionary<Object, AssetItem>(objectCount);
            var mihoyoBinDataNames = new List<(PPtr<Object>, string)>();
            var containers = new List<(PPtr<Object>, string)>();
            Progress.Reset();
            Logger.Info($"Loading {objectCount} objects from {assetsManager.assetsFileList.Count} files.");

            var fastAssetItemFilterData = new HashSet<AssetFilterDataItem>(assetsManager.FilterData.Items, new AssetFilterDataItemEqualityComparer());
            foreach (var assetsFile in assetsManager.assetsFileList)
            {
                foreach (var asset in assetsFile.Objects)
                {
                    if (assetsManager.tokenSource.IsCancellationRequested)
                    {
                        Logger.Info("Building asset list has been cancelled !!");
                        //return;
                        return (string.Empty, new ObservableCollection<object>());
                    }

                    var assetItem = new AssetItem(asset);

                    if (asset is not AssetBundle && asset is not ResourceManager)
                    {
                        if (fastAssetItemFilterData.Count > 0 && !fastAssetItemFilterData.Contains(new AssetFilterDataItem { Source = assetItem.SourceFile.fullName, Name = assetItem.Text, PathID = assetItem.PathID, Type = assetItem.Type }))
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
                            //if (!SkipContainer)
                            if (true)
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
                                                containers.Add((m_AssetBundle.m_PreloadTable[k], m_Container.Key));
                                            }
                                            break;
                                    }
                                }
                            }

                            exportable = ClassIDType.AssetBundle.CanExport();
                            break;
                        case IndexObject m_IndexObject:
                            foreach (var index in m_IndexObject.AssetMap)
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
            foreach ((var pptr, var name) in mihoyoBinDataNames)
            {
                if (assetsManager.tokenSource.IsCancellationRequested)
                {
                    Logger.Info("Processing asset names has been cancelled !!");
                    return (string.Empty, new ObservableCollection<object>());
                }
                if (pptr.TryGet<MiHoYoBinData>(out var obj))
                {
                    var assetItem = objectAssetItemDic[obj];
                    if (int.TryParse(name, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hash))
                    {
                        assetItem.Text = name;
                        assetItem.Container = hash.ToString();
                    }
                    else assetItem.Text = $"BinFile #{assetItem.PathID}";
                }
            }

            //if (!SkipContainer)
            if (true)
            {
                foreach ((var pptr, var container) in containers)
                {
                    if (assetsManager.tokenSource.IsCancellationRequested)
                    {
                        Logger.Info("Processing containers been cancelled !!");
                        return (string.Empty, new ObservableCollection<object>());
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
                    // TODO
                    //UpdateContainers();
                }
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                visibleAssets.Clear();
                foreach (var item in exportableAssets)
                    visibleAssets.Add(item);
            });

            //StatusStripUpdate("Building tree structure...");

            var treeNodeCollection = new ObservableCollection<object>();
            var treeNodeDictionary = new Dictionary<GameObject, GameObjectTreeItem>();
            int j = 0;
            Progress.Reset();
            var files = assetsManager.assetsFileList.GroupBy(x => x.originalPath ?? string.Empty).OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.ToList());

            foreach (var (file, assetsFiles) in files)
            {
                var fileNode = !string.IsNullOrEmpty(file) ? new FileTreeItem(Path.GetFileName(file)) : null;

                foreach (var assetsFile in assetsFiles)
                {
                    var assetsFileNode = new AssetsFileTreeItem(assetsFile.fileName);

                    foreach (var obj in assetsFile.Objects)
                    {
                        if (assetsManager.tokenSource.IsCancellationRequested)
                        {
                            Logger.Info("Building tree structure been cancelled !!");
                            return (string.Empty, new ObservableCollection<object>());
                        }

                        var assetItem = new AssetItem(obj);

                        if (obj is not GameObject)
                        {
                            if (fastAssetItemFilterData.Count > 0 && !fastAssetItemFilterData.Contains(new AssetFilterDataItem { Source = assetItem.SourceFile.fullName, Name = assetItem.Text, PathID = assetItem.PathID, Type = assetItem.Type }))
                            {
                                Logger.Verbose($"Skipped {(assetItem.Text.Length > 0 ? assetItem.Text : "an asset")} because filter data was set and it was missing from it");
                                continue;
                            }
                        }

                        if (obj is GameObject m_GameObject)
                        {
                            if (!treeNodeDictionary.TryGetValue(m_GameObject, out var currentNode))
                            {
                                currentNode = new GameObjectTreeItem(m_GameObject);
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

                            GameObjectTreeItem parentNode = null;
                            ObservableCollection<GameObjectTreeItem> parentCollection = assetsFileNode.Children;

                            if (m_GameObject.m_Transform != null)
                            {
                                if (m_GameObject.m_Transform.m_Father.TryGet(out var m_Father))
                                {
                                    if (m_Father.m_GameObject.TryGet(out var parentGameObject))
                                    {
                                        if (!treeNodeDictionary.TryGetValue(parentGameObject, out parentNode))
                                        {
                                            parentNode = new GameObjectTreeItem(parentGameObject);
                                            treeNodeDictionary.Add(parentGameObject, parentNode);
                                        }
                                        parentCollection = parentNode.Children;
                                    }
                                }
                            }

                            parentCollection.Add(currentNode);
                        }
                    }

                    if (assetsFileNode.Children.Count > 0)
                    {
                        if (fileNode == null)
                        {
                            treeNodeCollection.Add(assetsFileNode);
                        }
                        else
                        {
                            fileNode.Children.Add(assetsFileNode);
                        }
                    }
                }

                if (fileNode?.Children.Count > 0)
                {
                    treeNodeCollection.Add(fileNode);
                }

                Progress.Report(++j, files.Count);
            }

            treeNodeDictionary.Clear();
            objectAssetItemDic.Clear();

            return (productName, treeNodeCollection);
        }
    }
}
