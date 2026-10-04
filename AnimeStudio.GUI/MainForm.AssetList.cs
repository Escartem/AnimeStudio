using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AnimeStudio.App;
using AnimeStudio.GUI.Core;

namespace AnimeStudio.GUI
{
    partial class MainForm
    {
        private const int HashQueueLimit = 512;

        // Indices into Catalog.Rows, in display order.
        private int[] visible = Array.Empty<int>();
        private AssetSort? sort;
        private CancellationTokenSource queryCts;
        private string tempClipboard;

        private readonly LinkedList<AssetRow> hashQueue = new();
        private readonly HashSet<AssetRow> hashQueued = new();
        private bool hashWorkerRunning;

        private AssetRow VisibleRow(int index) => Catalog.Rows[visible[index]];

        private List<AssetRow> GetSelectedAssets()
        {
            var selected = new List<AssetRow>(assetListView.SelectedIndices.Count);
            foreach (int index in assetListView.SelectedIndices)
                selected.Add(VisibleRow(index));
            return selected;
        }

        private IReadOnlyList<AssetRow> GetVisibleAssets() => visible.Select(i => Catalog.Rows[i]).ToList();

        private void ResetAssetList()
        {
            queryCts?.Cancel();
            visible = Array.Empty<int>();
            sort = null;
            assetListView.VirtualListSize = 0;
            listSearch.Text = string.Empty;
            lock (hashQueue)
            {
                hashQueue.Clear();
                hashQueued.Clear();
            }
            while (filterTypeToolStripMenuItem.DropDownItems.Count > 1)
                filterTypeToolStripMenuItem.DropDownItems.RemoveAt(1);
        }

        private void assetListView_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (e.ItemIndex >= visible.Length)
            {
                e.Item = new ListViewItem(new[] { "", "", "", "", "", "" });
                return;
            }
            var row = VisibleRow(e.ItemIndex);
            if (!row.HasHash)
                RequestHash(row);
            e.Item = new ListViewItem(new[]
            {
                row.Name,
                row.Container,
                row.TypeString,
                row.PathID.ToString(),
                row.FullSize.ToString(),
                row.CachedHash ?? string.Empty,
            });
        }

        // Hashes read the object bytes, so they are computed lazily for rows that get displayed.
        private void RequestHash(AssetRow row)
        {
            lock (hashQueue)
            {
                if (!hashQueued.Add(row))
                    return;
                hashQueue.AddFirst(row);
                if (hashQueue.Count > HashQueueLimit)
                {
                    hashQueued.Remove(hashQueue.Last.Value);
                    hashQueue.RemoveLast();
                }
                if (hashWorkerRunning)
                    return;
                hashWorkerRunning = true;
            }
            Task.Run(HashWorker);
        }

        private void HashWorker()
        {
            while (true)
            {
                AssetRow row;
                lock (hashQueue)
                {
                    if (hashQueue.Count == 0)
                    {
                        hashWorkerRunning = false;
                        break;
                    }
                    row = hashQueue.First.Value;
                    hashQueue.RemoveFirst();
                    hashQueued.Remove(row);
                }
                try
                {
                    AssetIo.Run(() => _ = row.Hash);
                }
                catch (Exception e)
                {
                    Logger.Verbose($"Unable to hash {row.Name}: {e.Message}");
                }
            }
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(() => assetListView.Invalidate());
        }

        private void PopulateTypeFilters()
        {
            foreach (var type in Catalog.GetTypes())
            {
                var typeItem = new ToolStripMenuItem(AssetRow.GetTypeName(type)) { CheckOnClick = true, Tag = type };
                typeItem.Click += typeToolStripMenuItem_Click;
                filterTypeToolStripMenuItem.DropDownItems.Add(typeItem);
            }
            allToolStripMenuItem.Checked = true;
        }

        private void typeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var typeItem = (ToolStripMenuItem)sender;
            if (typeItem != allToolStripMenuItem)
            {
                allToolStripMenuItem.Checked = false;
            }
            else if (allToolStripMenuItem.Checked)
            {
                foreach (var item in filterTypeToolStripMenuItem.DropDownItems.OfType<ToolStripMenuItem>().Skip(1))
                    item.Checked = false;
            }
            _ = ApplyQueryAsync();
        }

        private void listSearch_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
                _ = ApplyQueryAsync();
        }

        private AssetFilter CurrentFilter()
        {
            HashSet<ClassIDType> types = null;
            if (!allToolStripMenuItem.Checked)
            {
                types = filterTypeToolStripMenuItem.DropDownItems.OfType<ToolStripMenuItem>()
                    .Where(x => x.Checked && x.Tag is ClassIDType)
                    .Select(x => (ClassIDType)x.Tag)
                    .ToHashSet();
            }
            return new AssetFilter(types, Settings.ModelsOnly, listSearch.Text);
        }

        private async Task ApplyQueryAsync()
        {
            var filter = CurrentFilter();
            try
            {
                AssetQuery.CreateRegex(filter.Search);
            }
            catch (ArgumentException ex)
            {
                Logger.Error("Invalid Regex.\n" + ex.Message);
                listSearch.Text = string.Empty;
                filter = filter with { Search = string.Empty };
            }

            queryCts?.Cancel();
            var cts = queryCts = new CancellationTokenSource();
            var rows = Catalog.Rows;
            var currentSort = sort;
            int[] result;
            try
            {
                result = await Task.Run(() => AssetQuery.Run(rows, filter, currentSort, cts.Token), cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (cts.IsCancellationRequested || rows != Catalog.Rows)
                return;

            assetListView.BeginUpdate();
            assetListView.SelectedIndices.Clear();
            visible = result;
            assetListView.VirtualListSize = visible.Length;
            assetListView.EndUpdate();
            assetListView.Invalidate();
        }

        private async void assetListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            var column = (AssetColumn)e.Column;
            sort = new AssetSort(column, sort?.Column == column && !sort.Value.Descending);
            if (column == AssetColumn.Hash && Catalog.Rows.Any(x => !x.HasHash))
            {
                SetStatus("Computing hashes...");
                try
                {
                    await session.ComputeHashesAsync(Catalog.Rows, session.BeginOperation());
                }
                catch (OperationCanceledException)
                {
                    SetStatus("Cancelled");
                    return;
                }
                SetStatus(string.Empty);
            }
            await ApplyQueryAsync();
        }

        private void selectAsset(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (e.IsSelected && e.ItemIndex < visible.Length)
                OnAssetSelected(VisibleRow(e.ItemIndex));
        }

        private void assetListView_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || assetListView.SelectedIndices.Count == 0)
                return;

            var selected = GetSelectedAssets();
            var single = selected.Count == 1 && !selected[0].IsVirtual;
            goToSceneHierarchyToolStripMenuItem.Visible = single;
            showOriginalFileToolStripMenuItem.Visible = single;
            exportAnimatorwithselectedAnimationClipMenuItem.Visible = selected.Any(x => x.Type == ClassIDType.Animator) && selected.Any(x => x.Type == ClassIDType.AnimationClip);

            tempClipboard = assetListView.HitTest(e.X, e.Y).SubItem?.Text;
            copyToolStripMenuItem.Enabled = !string.IsNullOrEmpty(tempClipboard);
            contextMenuStrip1.Show(assetListView, e.X, e.Y);
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(tempClipboard))
                Clipboard.SetDataObject(tempClipboard);
        }

        private void showOriginalFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var row = GetSelectedAssets().FirstOrDefault();
            if (row?.SourceFile != null)
                Shell.RevealFile(row.SourceFile.originalPath ?? row.SourceFile.fullName);
        }

        private void goToSceneHierarchyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var row = GetSelectedAssets().FirstOrDefault();
            if (row == null || row.SceneNode < 0)
            {
                MessageBox.Show("Asset does not exist in hierarchy !", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            sceneTreeView.SelectedNode = EnsureTreeNode(row.SceneNode);
            tabControl1.SelectedTab = tabPage1;
        }

        private void tabPageSelected(object sender, TabControlEventArgs e)
        {
            switch (e.TabPageIndex)
            {
                case 0:
                    treeSearch.Select();
                    break;
                case 1:
                    listSearch.Select();
                    break;
            }
        }

        private void PopulateClasses()
        {
            classesListView.BeginUpdate();
            foreach (var version in Catalog.Classes)
            {
                var group = new ListViewGroup(version.Version);
                classesListView.Groups.Add(group);
                foreach (var item in version.Classes)
                {
                    classesListView.Items.Add(new ListViewItem(new[] { item.Name, item.Id.ToString() }, group) { Tag = item });
                }
            }
            classesListView.EndUpdate();
        }

        private void classesListView_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            ClearPreview();
            classTextBox.Visible = true;
            if (e.IsSelected && e.Item.Tag is ClassStructure item)
                classTextBox.Text = item.Dump();
        }
    }
}
