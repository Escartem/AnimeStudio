using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;

namespace AnimeStudio.App
{
    public sealed class ExportService
    {
        private readonly StudioContext context;
        private readonly Func<ExportConfig> config;
        private readonly Action<string> status;

        // CLI groups container-less assets by type when exporting by container, the GUI leaves them at the root.
        public bool ContainerFallbackToType { get; init; }
        // Shared across calls when set, the owner saves it.
        public MonoStringScraper Scraper { get; init; }

        public ExportService(StudioContext context, Func<ExportConfig> config, Action<string> status = null)
        {
            this.context = context;
            this.config = config;
            this.status = status ?? Logger.Info;
        }

        public Task<int> ExportAssetsAsync(string savePath, IReadOnlyList<AssetRow> assets, ExportType exportType, CancellationToken token)
            => Run(() => ExportAssets(savePath, assets, exportType, token));

        public int ExportAssets(string savePath, IReadOnlyList<AssetRow> assets, ExportType exportType, CancellationToken token = default, bool openAfterExport = true)
        {
            var cfg = config();
            var exporter = new AssetExporter(context, cfg, Scraper);
            var total = assets.Count;
            var exported = 0;
            var processed = 0;
            Progress.Reset();
            try
            {
                foreach (var asset in assets)
                {
                    token.ThrowIfCancellationRequested();
                    var exportPath = GetExportPath(savePath, asset, cfg.GroupOption) + Path.DirectorySeparatorChar;
                    status($"[{exported}/{total}] Exporting {asset.TypeString}: {asset.Name}");
                    try
                    {
                        if (AssetIo.Run(() => exporter.Export(asset, exportPath, exportType), token))
                            exported++;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Export {asset.Type}:{asset.Name} error\r\n{ex.Message}\r\n{ex.StackTrace}");
                    }
                    Progress.Report(++processed, total);
                }
            }
            catch (OperationCanceledException)
            {
                status($"Export cancelled, {exported} assets exported.");
                return exported;
            }
            finally
            {
                if (Scraper == null)
                    exporter.Scraper?.Save(AssetsHelper.MapsDirectory);
            }

            var statusText = exported == 0 ? "Nothing exported." : $"Finished exporting {exported} assets.";
            if (total > exported)
                statusText += $" {total - exported} assets skipped (not extractable or files already exist)";
            status(statusText);

            if (openAfterExport && cfg.OpenAfterExport && exported > 0)
                Shell.OpenFolder(savePath);
            return exported;
        }

        private string GetExportPath(string savePath, AssetRow asset, AssetGroupOption option)
        {
            switch (option)
            {
                case AssetGroupOption.ByType:
                    return Path.Combine(savePath, asset.TypeString);
                case AssetGroupOption.ByContainer:
                    if (!string.IsNullOrEmpty(asset.Container))
                        return Path.HasExtension(asset.Container) ? Path.Combine(savePath, Path.GetDirectoryName(asset.Container)) : Path.Combine(savePath, asset.Container);
                    return ContainerFallbackToType ? Path.Combine(savePath, asset.TypeString) : savePath;
                case AssetGroupOption.BySource:
                    if (asset.IsVirtual)
                        return Path.Combine(savePath, Path.GetFileName(asset.ExternalPath) + "_export");
                    if (string.IsNullOrEmpty(asset.SourceFile.originalPath))
                        return Path.Combine(savePath, asset.SourceFile.fileName + "_export");
                    return Path.Combine(savePath, Path.GetFileName(asset.SourceFile.originalPath) + "_export", asset.SourceFile.fileName);
                default:
                    return savePath;
            }
        }

        public Task ExportAssetListAsync(string savePath, IReadOnlyList<AssetRow> assets, CancellationToken token) => Run(() =>
        {
            Progress.Reset();
            var filename = Path.Combine(savePath, "assets.xml");
            using (var writer = XmlWriter.Create(filename, new XmlWriterSettings { Indent = true }))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("Assets");
                writer.WriteAttributeString("filename", filename);
                writer.WriteAttributeString("createdAt", DateTime.UtcNow.ToString("s"));
                for (var i = 0; i < assets.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    var asset = assets[i];
                    writer.WriteStartElement("Asset");
                    writer.WriteElementString("Name", asset.Name);
                    writer.WriteElementString("Container", asset.Container);
                    writer.WriteStartElement("Type");
                    writer.WriteAttributeString("id", ((int)asset.Type).ToString());
                    writer.WriteValue(asset.TypeString);
                    writer.WriteEndElement();
                    writer.WriteElementString("PathID", asset.PathID.ToString());
                    writer.WriteElementString("Source", asset.SourceFile?.fullName ?? asset.ExternalPath);
                    writer.WriteElementString("Size", asset.FullSize.ToString());
                    writer.WriteEndElement();
                    Progress.Report(i + 1, assets.Count);
                }
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
            status($"Finished exporting asset list with {assets.Count} items.");
            OpenAfter(savePath, assets.Count > 0);
        });

        // One FBX per top-level object of each file, objects without any mesh are skipped.
        public Task ExportSplitObjectsAsync(string savePath, SceneGraph scene, CancellationToken token) => Run(() =>
        {
            var exporter = new AssetExporter(context, config());
            var targets = scene.Roots.SelectMany(root => scene.GetChildCount(root) == 0 ? new[] { root } : scene.GetChildren(root)).ToList();
            var count = targets.Sum(scene.GetChildCount);
            var k = 0;
            Progress.Reset();
            foreach (var node in targets)
            {
                foreach (var child in scene.GetChildren(node).ToList())
                {
                    token.ThrowIfCancellationRequested();
                    if (!scene.IsGameObject(child))
                    {
                        Progress.Report(++k, count);
                        continue;
                    }

                    var gameObjects = new List<GameObject>();
                    scene.CollectSubtree(child, gameObjects);
                    if (gameObjects.All(x => x.m_SkinnedMeshRenderer == null && x.m_MeshFilter == null))
                    {
                        Progress.Report(++k, count);
                        continue;
                    }

                    var filename = AssetExporter.FixFileName(scene.GetName(child));
                    var parent = scene.GetParent(node);
                    if (parent != -1)
                        filename = Path.Combine(AssetExporter.FixFileName(scene.GetName(parent)), filename);

                    var targetPath = $"{savePath}{filename}{Path.DirectorySeparatorChar}";
                    for (int i = 1; Directory.Exists(targetPath); i++)
                        targetPath = $"{savePath}{filename} ({i}){Path.DirectorySeparatorChar}";
                    Directory.CreateDirectory(targetPath);

                    status($"Exporting {filename}.fbx");
                    try
                    {
                        AssetIo.Run(() => exporter.ExportGameObject(scene.GetGameObject(child), targetPath), token);
                        status($"Finished exporting {filename}.fbx");
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Logger.Error($"Export GameObject:{scene.GetName(child)} error\r\n{ex.Message}\r\n{ex.StackTrace}");
                    }
                    Progress.Report(++k, count);
                }
            }
            OpenAfter(savePath, true);
            status("Finished");
        });

        public Task ExportAnimatorAsync(AssetRow animator, IReadOnlyList<AssetRow> animations, string exportPath, CancellationToken token) => Run(() =>
        {
            Progress.Reset();
            status($"Exporting {animator.Name}");
            try
            {
                AssetIo.Run(() => new AssetExporter(context, config()).ExportAnimator(animator, exportPath, animations), token);
                OpenAfter(exportPath, true);
                Progress.Report(1, 1);
                status($"Finished exporting {animator.Name}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.Error($"Export Animator:{animator.Name} error\r\n{ex.Message}\r\n{ex.StackTrace}");
                status("Error in export");
            }
        });

        public Task ExportObjectsAsync(string exportPath, List<GameObject> gameObjects, IReadOnlyList<AssetRow> animations, CancellationToken token) => Run(() =>
        {
            if (gameObjects.Count == 0)
            {
                status("No Object selected for export.");
                return;
            }

            var exporter = new AssetExporter(context, config());
            Progress.Reset();
            for (var i = 0; i < gameObjects.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var gameObject = gameObjects[i];
                status($"Exporting {gameObject.m_Name}");
                try
                {
                    var subExportPath = Path.Combine(exportPath, gameObject.m_Name) + Path.DirectorySeparatorChar;
                    AssetIo.Run(() => exporter.ExportGameObject(gameObject, subExportPath, animations), token);
                    status($"Finished exporting {gameObject.m_Name}");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Logger.Error($"Export GameObject:{gameObject.m_Name} error\r\n{ex.Message}\r\n{ex.StackTrace}");
                    status("Error in export");
                }
                Progress.Report(i + 1, gameObjects.Count);
            }
            OpenAfter(exportPath, true);
        });

        public Task ExportMergedAsync(string exportPath, List<GameObject> gameObjects, IReadOnlyList<AssetRow> animations, CancellationToken token) => Run(() =>
        {
            var name = Path.GetFileName(exportPath);
            Progress.Reset();
            status($"Exporting {name}");
            try
            {
                AssetIo.Run(() => new AssetExporter(context, config()).ExportGameObjectMerge(gameObjects, exportPath, animations), token);
                Progress.Report(1, 1);
                status($"Finished exporting {name}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.Error($"Export Model:{name} error\r\n{ex.Message}\r\n{ex.StackTrace}");
                status("Error in export");
            }
            OpenAfter(Path.GetDirectoryName(exportPath), true);
        });

        // One merged FBX per checked root node.
        public Task ExportNodesAsync(string exportPath, SceneGraph scene, IReadOnlyList<int> roots, IReadOnlyList<AssetRow> animations, CancellationToken token) => Run(() =>
        {
            var exporter = new AssetExporter(context, config());
            Progress.Reset();
            for (var i = 0; i < roots.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var name = scene.GetName(roots[i]);
                status($"Exporting {name}");
                var gameObjects = scene.CollectCheckedGameObjects(scene.GetChildren(roots[i]));
                if (gameObjects.Count == 0)
                {
                    status("Empty node selected for export.");
                }
                else
                {
                    var subExportPath = exportPath + Path.Combine(name, AssetExporter.FixFileName(name) + ".fbx");
                    try
                    {
                        AssetIo.Run(() => exporter.ExportGameObjectMerge(gameObjects, subExportPath, animations), token);
                        status($"Finished exporting {name}");
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Logger.Error($"Export Model:{name} error\r\n{ex.Message}\r\n{ex.StackTrace}");
                        status("Error in export");
                    }
                }
                Progress.Report(i + 1, roots.Count);
            }
            OpenAfter(exportPath, true);
        });

        public Task ExportClassStructuresAsync(string savePath, IReadOnlyList<ClassStructureGroup> groups, CancellationToken token) => Run(() =>
        {
            var count = groups.Sum(x => x.Classes.Count);
            var i = 0;
            Progress.Reset();
            foreach (var group in groups)
            {
                var versionPath = Path.Combine(savePath, group.Version);
                Directory.CreateDirectory(versionPath);
                foreach (var item in group.Classes)
                {
                    token.ThrowIfCancellationRequested();
                    File.WriteAllText(Path.Combine(versionPath, $"{item.Id} {AssetExporter.FixFileName(item.Name)}.txt"), item.Dump());
                    Progress.Report(++i, count);
                }
            }
            status("Finished exporting class structures");
        });

        public Task ExportSceneHierarchyAsync(string path, SceneGraph scene) => Run(() =>
        {
            var json = AssetIo.Run(() => JsonConvert.SerializeObject(scene.BuildHierarchyDump(), Newtonsoft.Json.Formatting.Indented));
            File.WriteAllText(path, json);
            Logger.Info("Scene Hierarchy dumped sucessfully !!");
        });

        private void OpenAfter(string path, bool any)
        {
            if (any && config().OpenAfterExport)
                Shell.OpenFolder(path);
        }

        private Task Run(Action action) => Task.Run(() =>
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            try
            {
                action();
            }
            catch (OperationCanceledException)
            {
                status("Cancelled");
            }
        });

        private Task<T> Run<T>(Func<T> func) => Task.Run(() =>
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            return func();
        });
    }
}
