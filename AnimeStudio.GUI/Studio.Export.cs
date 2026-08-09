using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using static AnimeStudio.GUI.Exporter;

namespace AnimeStudio.GUI
{
    internal static partial class Studio
    {
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

            return EnsureTrailingDirectorySeparator(exportPath);
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

                var statusText = $"Finished exporting asset list with {toExportAssets.Count} items.";

                StatusStripUpdate(statusText);

                if (Properties.Settings.Default.openAfterExport && toExportAssets.Count > 0)
                {
                    OpenFolderInExplorer(savePath);
                }
            });
        }

        public static Task ExportSplitObjects(string savePath, TreeNodeCollection nodes)
        {
            return Task.Run(() =>
            {
                var exportNodes = GetSplitExportNodes(nodes);
                var count = CountSplitExportObjects(exportNodes);
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

        private static List<TreeNode> GetSplitExportNodes(TreeNodeCollection nodes)
        {
            var exportNodes = new List<TreeNode>();
            foreach (TreeNode node in nodes)
            {
                if (node.Nodes.Count == 0)
                {
                    exportNodes.Add(node);
                    continue;
                }

                foreach (TreeNode subNode in node.Nodes)
                {
                    exportNodes.Add(subNode);
                }
            }

            return exportNodes;
        }

        private static void ExportSplitObject(string savePath, TreeNode parentNode, GameObjectTreeNode node)
        {
            var gameObjects = new List<GameObject>();
            CollectNode(node, gameObjects);
            if (!HasAnyModel(gameObjects))
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
                            var subExportPath = EnsureTrailingDirectorySeparator(Path.Combine(exportPath, gameObject.m_Name));
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

        private static int CountSplitExportObjects(List<TreeNode> exportNodes)
        {
            var count = 0;
            foreach (var node in exportNodes)
            {
                count += node.Nodes.Count;
            }

            return count;
        }

        private static bool HasAnyModel(List<GameObject> gameObjects)
        {
            foreach (var gameObject in gameObjects)
            {
                if (gameObject.m_SkinnedMeshRenderer != null || gameObject.m_MeshFilter != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static string EnsureTrailingDirectorySeparator(string path)
        {
            return path.Length > 0 && path[path.Length - 1] == Path.DirectorySeparatorChar
                ? path
                : path + Path.DirectorySeparatorChar;
        }
    }
}

