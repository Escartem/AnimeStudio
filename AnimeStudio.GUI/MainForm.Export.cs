using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AnimeStudio.App;

namespace AnimeStudio.GUI
{
    partial class MainForm
    {
        private enum ExportFilter
        {
            All,
            Selected,
            Filtered
        }

        private void InitializeExportMenus()
        {
            void Bind(ToolStripMenuItem item, ExportFilter filter, ExportType? type)
                => item.Click += (_, _) => _ = type is { } t ? ExportAssetsAsync(filter, t) : ExportAssetListAsync(filter);

            Bind(exportAllAssetsMenuItem, ExportFilter.All, ExportType.Convert);
            Bind(exportSelectedAssetsMenuItem, ExportFilter.Selected, ExportType.Convert);
            Bind(exportFilteredAssetsMenuItem, ExportFilter.Filtered, ExportType.Convert);
            Bind(toolStripMenuItem4, ExportFilter.All, ExportType.Raw);
            Bind(toolStripMenuItem5, ExportFilter.Selected, ExportType.Raw);
            Bind(toolStripMenuItem6, ExportFilter.Filtered, ExportType.Raw);
            Bind(toolStripMenuItem7, ExportFilter.All, ExportType.Dump);
            Bind(toolStripMenuItem8, ExportFilter.Selected, ExportType.Dump);
            Bind(toolStripMenuItem9, ExportFilter.Filtered, ExportType.Dump);
            Bind(toolStripMenuItem17, ExportFilter.All, ExportType.JSON);
            Bind(toolStripMenuItem24, ExportFilter.Selected, ExportType.JSON);
            Bind(toolStripMenuItem25, ExportFilter.Filtered, ExportType.JSON);
            Bind(toolStripMenuItem11, ExportFilter.All, null);
            Bind(toolStripMenuItem12, ExportFilter.Selected, null);
            Bind(toolStripMenuItem13, ExportFilter.Filtered, null);
        }

        private IReadOnlyList<AssetRow> GetAssets(ExportFilter filter) => filter switch
        {
            ExportFilter.Selected => GetSelectedAssets(),
            ExportFilter.Filtered => GetVisibleAssets(),
            _ => Catalog.Rows,
        };

        private IReadOnlyList<AssetRow> SelectedAnimationClips(bool include)
        {
            if (!include)
                return null;
            var clips = GetSelectedAssets().Where(x => x.Type == ClassIDType.AnimationClip).ToList();
            return clips.Count > 0 ? clips : null;
        }

        private async Task ExportAssetsAsync(ExportFilter filter, ExportType type)
        {
            if (Catalog.Rows.Count == 0)
            {
                SetStatus("No exportable assets loaded");
                return;
            }
            var folder = PickSaveFolder();
            if (folder != null)
                await session.Exports.ExportAssetsAsync(folder, GetAssets(filter), type, session.BeginOperation());
        }

        private async Task ExportAssetListAsync(ExportFilter filter)
        {
            if (Catalog.Rows.Count == 0)
            {
                SetStatus("No exportable assets loaded");
                return;
            }
            var folder = PickSaveFolder();
            if (folder != null)
                await session.Exports.ExportAssetListAsync(folder, GetAssets(filter), session.BeginOperation());
        }

        private void exportSelectedAssetsToolStripMenuItem_Click(object sender, EventArgs e) => _ = ExportAssetsAsync(ExportFilter.Selected, ExportType.Convert);

        private async void exportAnimatorwithAnimationClipMenuItem_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedAssets();
            var animator = selected.LastOrDefault(x => x.Type == ClassIDType.Animator);
            if (animator == null)
                return;
            var folder = PickSaveFolder();
            if (folder == null)
                return;
            var clips = selected.Where(x => x.Type == ClassIDType.AnimationClip).ToList();
            await session.Exports.ExportAnimatorAsync(animator, clips, Path.Combine(folder, "Animator") + Path.DirectorySeparatorChar, session.BeginOperation());
        }

        private async void modelsObjectsExportAll_Click(object sender, EventArgs e)
        {
            if (Scene.Roots.Count == 0)
            {
                SetStatus("No Objects available for export");
                return;
            }
            var folder = PickSaveFolder();
            if (folder != null)
                await session.Exports.ExportSplitObjectsAsync(folder + Path.DirectorySeparatorChar, Scene, session.BeginOperation());
        }

        private async void modelsObjectsExportSelected_Click(object sender, EventArgs e)
        {
            if (Scene.Roots.Count == 0)
            {
                SetStatus("No Objects available for export");
                return;
            }
            var clips = SelectedAnimationClips(modelsIncludeAnimationClips.Checked);
            var gameObjects = Scene.CollectCheckedGameObjects();

            if (!modelsMerge.Checked)
            {
                var folder = PickSaveFolder();
                if (folder != null)
                    await session.Exports.ExportObjectsAsync(Path.Combine(folder, "GameObject") + Path.DirectorySeparatorChar, gameObjects, clips, session.BeginOperation());
                return;
            }

            if (gameObjects.Count == 0)
            {
                SetStatus("No Object selected for export.");
                return;
            }
            using var saveFileDialog = new SaveFileDialog
            {
                FileName = gameObjects[0].m_Name + " (merge).fbx",
                AddExtension = false,
                Filter = "Fbx file (*.fbx)|*.fbx",
                InitialDirectory = saveDirectoryBackup,
            };
            if (saveFileDialog.ShowDialog(this) != DialogResult.OK)
                return;
            saveDirectoryBackup = Path.GetDirectoryName(saveFileDialog.FileName);
            await session.Exports.ExportMergedAsync(saveFileDialog.FileName, gameObjects, clips, session.BeginOperation());
        }

        private async void modelsNodesExportSelected_Click(object sender, EventArgs e)
        {
            if (Scene.Roots.Count == 0)
                return;
            var folder = PickSaveFolder();
            if (folder == null)
                return;
            var roots = Scene.Roots.Where(Scene.IsChecked).ToList();
            if (roots.Count == 0)
            {
                Logger.Info("No root nodes found selected.");
                return;
            }
            var clips = SelectedAnimationClips(modelsIncludeAnimationClips.Checked);
            await session.Exports.ExportNodesAsync(Path.Combine(folder, "GameObject") + Path.DirectorySeparatorChar, Scene, roots, clips, session.BeginOperation());
        }

        private async void sceneHierarchy_Click(object sender, EventArgs e)
        {
            using var saveFileDialog = new SaveFileDialog { FileName = "scene.json", Filter = "Scene Hierarchy dump | *.json" };
            if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
                await session.Exports.ExportSceneHierarchyAsync(saveFileDialog.FileName, Scene);
        }

        private async void exportClassStructuresMenuItem_Click(object sender, EventArgs e)
        {
            if (Catalog.Classes.Count == 0)
                return;
            var folder = PickFolder();
            if (folder != null)
                await session.Exports.ExportClassStructuresAsync(folder, Catalog.Classes, session.BeginOperation());
        }
    }
}
