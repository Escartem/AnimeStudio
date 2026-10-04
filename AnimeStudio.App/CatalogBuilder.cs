using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using static AnimeStudio.AssetsManager;

namespace AnimeStudio.App
{
    public sealed class CatalogOptions
    {
        // GUI "Show hidden assets": list every object, not only the exportable ones.
        public bool DisplayAll { get; init; }
        // CLI: any type with its export flag set is exportable, even without a dedicated exporter.
        public bool IncludeExportableTypes { get; init; }
        // CLI: GameObjects that carry a model are exported directly.
        public bool IncludeModelGameObjects { get; init; }
        public bool SkipContainer { get; init; }
        public bool UseBundleContainerName { get; init; }
        public bool BuildScene { get; init; } = true;
    }

    public sealed class CatalogBuilder
    {
        private readonly StudioContext context;
        private readonly CatalogOptions options;
        private readonly Action<string> status;

        public CatalogBuilder(StudioContext context, CatalogOptions options, Action<string> status = null)
        {
            this.context = context;
            this.options = options;
            this.status = status ?? (_ => { });
        }

        public AssetCatalog Build(CancellationToken token, int startIndex = 0)
        {
            var manager = context.AssetsManager;
            var rows = BuildRows(manager, token, startIndex, out var productName);
            var scene = options.BuildScene ? BuildScene(manager, rows, token) : new SceneGraph();
            var classes = options.BuildScene ? BuildClassStructures(manager, token) : Array.Empty<ClassStructureGroup>();
            return new AssetCatalog(rows, scene, classes, productName);
        }

        private List<AssetRow> BuildRows(AssetsManager manager, CancellationToken token, int index, out string productName)
        {
            status("Building asset list...");
            productName = null;

            var objectCount = manager.assetsFileList.Sum(x => x.Objects.Count);
            var rows = new List<AssetRow>();
            var rowByObject = new Dictionary<Object, AssetRow>();
            var binDataNames = new List<(PPtr<Object>, string)>();
            var containers = new List<(PPtr<Object>, string)>();
            var assetBundleName = string.Empty;
            var filter = new HashSet<AssetFilterDataItem>(manager.FilterData.Items, new AssetFilterDataItemEqualityComparer());
            var probe = new AssetFilterDataItem();
            var processed = 0;

            Progress.Reset();
            Logger.Info($"Loading {objectCount} objects from {manager.assetsFileList.Count} files.");
            foreach (var assetsFile in manager.assetsFileList)
            {
                foreach (var asset in assetsFile.Objects)
                {
                    token.ThrowIfCancellationRequested();
                    Progress.Report(++processed, objectCount);

                    if (filter.Count > 0 && asset is not AssetBundle && asset is not ResourceManager)
                    {
                        probe.Source = assetsFile.fullName;
                        probe.Name = asset.Name;
                        probe.PathID = asset.m_PathID;
                        probe.Type = asset.type;
                        if (!filter.Contains(probe))
                            continue;
                    }

                    var rowIndex = index++;
                    long fullSize = asset.byteSize;
                    var exportable = false;
                    switch (asset)
                    {
                        case GameObject m_GameObject:
                            exportable = options.IncludeModelGameObjects && ClassIDType.GameObject.CanExport() && m_GameObject.HasModel();
                            break;
                        case Texture2D m_Texture2D:
                            if (!string.IsNullOrEmpty(m_Texture2D.m_StreamData?.path))
                                fullSize += m_Texture2D.m_StreamData.size;
                            exportable = ClassIDType.Texture2D.CanExport();
                            break;
                        case AudioClip m_AudioClip:
                            if (!string.IsNullOrEmpty(m_AudioClip.m_Source))
                                fullSize += m_AudioClip.m_Size;
                            exportable = ClassIDType.AudioClip.CanExport();
                            break;
                        case VideoClip m_VideoClip:
                            if (!string.IsNullOrEmpty(m_VideoClip.m_OriginalPath))
                                fullSize += m_VideoClip.m_ExternalResources.m_Size;
                            exportable = ClassIDType.VideoClip.CanExport();
                            break;
                        case PlayerSettings m_PlayerSettings:
                            productName = m_PlayerSettings.productName;
                            exportable = ClassIDType.PlayerSettings.CanExport();
                            break;
                        case AssetBundle m_AssetBundle:
                            assetBundleName = m_AssetBundle.Name;
                            if (!options.SkipContainer)
                                CollectBundleContainers(m_AssetBundle, containers);
                            exportable = ClassIDType.AssetBundle.CanExport();
                            break;
                        case IndexObject m_IndexObject:
                            foreach (var entry in m_IndexObject.AssetMap)
                                binDataNames.Add((entry.Value.Object, entry.Key));
                            exportable = ClassIDType.IndexObject.CanExport();
                            break;
                        case ResourceManager m_ResourceManager:
                            foreach (var m_Container in m_ResourceManager.m_Container)
                                containers.Add((m_Container.Value, m_Container.Key));
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
                    if (!exportable && options.IncludeExportableTypes && asset.type.CanExport())
                        exportable = true;
                    if (!exportable && !options.DisplayAll)
                        continue;

                    var row = new AssetRow(asset) { Index = rowIndex, FullSize = fullSize };
                    if (row.Name == "")
                        row.Name = row.TypeString + "#" + rowIndex;
                    rows.Add(row);
                    rowByObject.Add(asset, row);
                }
            }

            foreach (var (pptr, name) in binDataNames)
            {
                token.ThrowIfCancellationRequested();
                if (pptr.TryGet<MiHoYoBinData>(out var obj) && rowByObject.TryGetValue(obj, out var row))
                {
                    if (int.TryParse(name, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hash))
                    {
                        row.Name = name;
                        row.Container = options.UseBundleContainerName ? assetBundleName : hash.ToString();
                    }
                    else
                    {
                        row.Name = $"BinFile #{row.PathID}";
                    }
                }
            }

            if (!options.SkipContainer)
            {
                foreach (var (pptr, container) in containers)
                {
                    token.ThrowIfCancellationRequested();
                    if (pptr.TryGet(out var obj) && rowByObject.TryGetValue(obj, out var row))
                        row.Container = container;
                }
                if (context.Game.Type.IsGISubGroup() || context.Game.Type.IsZZZ())
                    ContainerResolver.Update(rows, context.Game, context.Z3Paths);
            }

            return rows;
        }

        private void CollectBundleContainers(AssetBundle m_AssetBundle, List<(PPtr<Object>, string)> containers)
        {
            foreach (var m_Container in m_AssetBundle.m_Container)
            {
                var preloadIndex = m_Container.Value.preloadIndex;
                if (preloadIndex < 0)
                {
                    Logger.Warning($"preloadIndex {preloadIndex} is out of preloadTable range");
                    continue;
                }

                var containerName = options.UseBundleContainerName && int.TryParse(m_Container.Key, out _)
                    ? m_AssetBundle.Name
                    : m_Container.Key;
                var preloadEnd = Math.Min(preloadIndex + m_Container.Value.preloadSize, m_AssetBundle.m_PreloadTable.Count);
                if (preloadEnd < preloadIndex + m_Container.Value.preloadSize)
                    Logger.Info($"Failed to add container {m_Container.Key}");
                for (int k = preloadIndex; k < preloadEnd; k++)
                    containers.Add((m_AssetBundle.m_PreloadTable[k], containerName));
            }
        }

        private SceneGraph BuildScene(AssetsManager manager, List<AssetRow> rows, CancellationToken token)
        {
            status("Building tree structure...");

            var rowByObject = new Dictionary<Object, AssetRow>(rows.Count);
            foreach (var row in rows)
                rowByObject.Add(row.Asset, row);

            var scene = new SceneGraph();
            var nodeByGameObject = new Dictionary<GameObject, int>();
            int NodeOf(GameObject gameObject)
            {
                if (!nodeByGameObject.TryGetValue(gameObject, out var id))
                {
                    id = scene.AddNode(gameObject.m_Name, gameObject);
                    nodeByGameObject.Add(gameObject, id);
                }
                return id;
            }
            void Link(Object obj, int node)
            {
                if (obj != null && rowByObject.TryGetValue(obj, out var row))
                    row.SceneNode = node;
            }

            var files = manager.assetsFileList.GroupBy(x => x.originalPath ?? string.Empty).OrderBy(x => x.Key).ToList();
            Progress.Reset();
            for (var f = 0; f < files.Count; f++)
            {
                var group = files[f];
                var fileNode = !string.IsNullOrEmpty(group.Key) ? scene.AddNode(Path.GetFileName(group.Key)) : -1;
                foreach (var assetsFile in group)
                {
                    var assetsFileNode = scene.AddNode(assetsFile.fileName);
                    foreach (var obj in assetsFile.Objects)
                    {
                        token.ThrowIfCancellationRequested();
                        if (obj is not GameObject m_GameObject)
                            continue;

                        var current = NodeOf(m_GameObject);
                        foreach (var pptr in m_GameObject.m_Components)
                        {
                            if (!pptr.TryGet(out var m_Component))
                                continue;
                            Link(m_Component, current);
                            if (m_Component is MeshFilter m_MeshFilter && m_MeshFilter.m_Mesh.TryGet(out var filterMesh))
                                Link(filterMesh, current);
                            else if (m_Component is SkinnedMeshRenderer m_Skinned && m_Skinned.m_Mesh.TryGet(out var skinnedMesh))
                                Link(skinnedMesh, current);
                        }

                        var parent = assetsFileNode;
                        if (m_GameObject.m_Transform != null
                            && m_GameObject.m_Transform.m_Father.TryGet(out var m_Father)
                            && m_Father.m_GameObject.TryGet(out var parentGameObject))
                        {
                            parent = NodeOf(parentGameObject);
                        }
                        scene.AddChild(parent, current);
                    }

                    if (scene.GetChildCount(assetsFileNode) > 0)
                    {
                        if (fileNode == -1)
                            scene.AddRoot(assetsFileNode);
                        else
                            scene.AddChild(fileNode, assetsFileNode);
                    }
                }

                if (fileNode != -1 && scene.GetChildCount(fileNode) > 0)
                    scene.AddRoot(fileNode);

                Progress.Report(f + 1, files.Count);
            }

            scene.Seal();
            return scene;
        }

        private static IReadOnlyList<ClassStructureGroup> BuildClassStructures(AssetsManager manager, CancellationToken token)
        {
            var typeMap = new Dictionary<string, SortedDictionary<int, ClassStructure>>();
            foreach (var assetsFile in manager.assetsFileList)
            {
                token.ThrowIfCancellationRequested();
                if (!typeMap.TryGetValue(assetsFile.unityVersion, out var items))
                {
                    items = new SortedDictionary<int, ClassStructure>();
                    typeMap.Add(assetsFile.unityVersion, items);
                }
                foreach (var type in assetsFile.m_Types.Where(x => x.m_Type != null))
                {
                    var key = type.m_ScriptTypeIndex >= 0 ? -1 - type.m_ScriptTypeIndex : type.classID;
                    items[key] = new ClassStructure(key, type.m_Type);
                }
            }
            return typeMap.Select(x => new ClassStructureGroup(x.Key, x.Value.Values.ToList())).ToList();
        }

        // Loose AFK Journey files (.jsone text, DXT textures) that are not Unity files but can be previewed.
        public static List<AssetRow> CollectAfkJourneyFiles(IEnumerable<string> sourcePaths, IEnumerable<AssetRow> existing)
        {
            var known = new HashSet<string>(existing.Where(x => x.IsVirtual).Select(x => x.ExternalPath), StringComparer.OrdinalIgnoreCase);
            var result = new List<AssetRow>();
            foreach (var sourcePath in sourcePaths.Where(path => !string.IsNullOrEmpty(path)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (Directory.Exists(sourcePath))
                {
                    var root = Path.GetFullPath(sourcePath);
                    foreach (var file in Directory.EnumerateFiles(sourcePath, "*.*", SearchOption.AllDirectories))
                    {
                        if (!AFKJourneyUtils.IsPreviewableSpecialFile(file))
                            continue;
                        var directory = Path.GetDirectoryName(Path.GetFullPath(file)) ?? root;
                        var container = Path.GetRelativePath(root, directory);
                        Add(file, container == "." ? string.Empty : container);
                    }
                }
                else if (File.Exists(sourcePath) && AFKJourneyUtils.IsPreviewableSpecialFile(sourcePath))
                {
                    Add(sourcePath, string.Empty);
                }
            }
            return result;

            void Add(string file, string container)
            {
                if (!known.Add(file))
                    return;
                var type = Path.GetExtension(file).Equals(".jsone", StringComparison.OrdinalIgnoreCase) ? ClassIDType.TextAsset : ClassIDType.Texture2D;
                result.Add(new AssetRow(Path.GetFileName(file), type, file, new FileInfo(file).Length, container) { InfoText = $"Path: {file}" });
            }
        }
    }
}
