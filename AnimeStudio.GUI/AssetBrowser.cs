using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using AnimeStudio.App;
using AnimeStudio.GUI.Core;
using static AnimeStudio.AssetsManager;

namespace AnimeStudio.GUI
{
    partial class AssetBrowser : Form
    {
        private readonly MainForm _parent;
        private readonly StudioSession session;
        private readonly List<AssetEntry> _assetEntries;
        private readonly List<AssetEntry> _backupAssetEntries;
        private readonly List<AssetEntry> _firstAssetEntries;
        private readonly List<AssetEntry> _secondAssetEntries;
        private readonly Dictionary<string, Regex> _filters;

        private SortOrder _sortOrder;
        private DataGridViewColumn _sortedColumn;
        private List<String> types = new();
        private List<String> selectedTypes = new();

        public AssetBrowser(MainForm form, StudioSession session)
        {
            InitializeComponent();
            _parent = form;
            this.session = session;
            _filters = new Dictionary<string, Regex>();
            _assetEntries = new List<AssetEntry>();
            _backupAssetEntries = new List<AssetEntry>();
            _firstAssetEntries = new List<AssetEntry>();
            _secondAssetEntries = new List<AssetEntry>();
            secondMapFilter.SelectedIndex = 0;
        }

        private async void loadAssetMap_Click(object sender, EventArgs e)
        {
            loadAssetMap.Enabled = false;

            var openFileDialog = new OpenFileDialog() { Multiselect = false, Filter = "MessagePack AssetMap File|*.map|JSON AssetMap File|*.json" };
            if (openFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    var path = openFileDialog.FileName;
                    Logger.Info($"Loading AssetMap...");
                    var result = await Task.Run(() => ResourceMap.FromFile(path));

                    if (result == -1)
                    {
                        throw new Exception("Map parse failed");
                    }

                    _sortedColumn = null;

                    _firstAssetEntries.Clear();
                    _firstAssetEntries.AddRange(ResourceMap.GetEntries());

                    updateDisplay();
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to load map : {ex.ToString()}");
                }
            }
            loadAssetMap.Enabled = true;
        }

        private void updateDisplay()
        {
            var names = typeof(AssetEntry).GetProperties().Select(x => x.Name).Where(x => x != "Offset");

            _filters.Clear();
            foreach (var name in names)
            {
                _filters.Add(name, new Regex(""));
            }

            _assetEntries.Clear();

            if (_secondAssetEntries.Count > 0)
            {
                var diffDisplay = secondMapFilter.SelectedIndex;
                switch (diffDisplay)
                {
                    case 0:
                        _assetEntries.AddRange(_firstAssetEntries.Except(_secondAssetEntries, new AssetEntryComparer()));
                        break;
                    case 1:
                        _assetEntries.AddRange(_secondAssetEntries.Except(_firstAssetEntries, new AssetEntryComparer()));
                        break;
                    case 2:
                        var differentAssets = _firstAssetEntries
                        .Join(_secondAssetEntries,
                            first => new { first.Name, first.Container, first.PathID },
                            second => new { second.Name, second.Container, second.PathID },
                            (first, second) => new { first, second })
                        .Where(x => x.first.Hash != x.second.Hash)
                        .Select(x => x.first);

                        _assetEntries.AddRange(differentAssets);
                        break;
                }
            }
            else
            {
                _assetEntries.AddRange(_firstAssetEntries);
            }

            _backupAssetEntries.Clear();
            _backupAssetEntries.AddRange(_assetEntries);

            assetDataGridView.Columns.Clear();
            assetDataGridView.Columns.AddRange(names.Select(x => new DataGridViewTextBoxColumn() { Name = x, HeaderText = x, SortMode = DataGridViewColumnSortMode.Programmatic }).ToArray());
            assetDataGridView.Columns.GetLastColumn(DataGridViewElementStates.None, DataGridViewElementStates.None).AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            assetDataGridView.Rows.Clear();
            assetDataGridView.RowCount = _assetEntries.Count;
            assetDataGridView.Refresh();

            foreach (var entry in _assetEntries)
            {
                if (!types.Contains(entry.Type.ToString()))
                {
                    types.Add(entry.Type.ToString());
                }
            }

            types.Sort();
            if (types.Count() > 0 && types[0] != "All")
            {
                types.Insert(0, "All");
            }

            updateButtons();
        }

        private void updateButtons()
        {
            bool isEnabled = _firstAssetEntries.Count > 0;
            relocateSource.Enabled = isEnabled;
            exportSelected.Enabled = isEnabled;
            loadSelected.Enabled = isEnabled;

            nameTextBox.Enabled = isEnabled;
            containerTextBox.Enabled = isEnabled;
            sourceTextBox.Enabled = isEnabled;
            pathTextBox.Enabled = isEnabled;
            filterSelectTypesBtn.Enabled = isEnabled;
            hashTextBox.Enabled = isEnabled;
            searchBtn.Enabled = isEnabled;

            loadMapTwoBtn.Enabled = isEnabled;

            bool isSecondEnabled = _secondAssetEntries.Count > 0;

            clearMapTwoBtn.Enabled = isSecondEnabled;
            secondMapFilter.Enabled = isSecondEnabled;
        }

        private void bringMainToFront()
        {
            _parent.BringToFront();
            _parent.TopMost = true;
            _parent.TopMost = false;
            _parent.Focus();
        }
        private void clear_Click(object sender, EventArgs e)
        {
            Clear();
            updateButtons();
            Logger.Info($"Cleared !!");
        }
        private void loadSelected_Click(object sender, EventArgs e)
        {
            var files = assetDataGridView.SelectedRows.Cast<DataGridViewRow>()
            .Select(x => _assetEntries[x.Index])
            .Where(entry => entry != null)
            .Select(entry => new AssetFilterDataItem
            {
                Source = entry.Source,
                Type = entry.Type,
                Name = entry.Name,
                PathID = entry.PathID,
                Offset = entry.Offset
            })
            .ToList();

            var filePaths = files.Select(x => x.Source).ToHashSet();

            var missingFiles = filePaths.Where(x => !File.Exists(x));
            foreach (var file in missingFiles)
            {
                Logger.Warning($"Unable to find file {file}, skipping...");
                filePaths.Remove(file);
                files.RemoveAll(x => x.Source == file);
            }
            if (filePaths.Count != 0 && !filePaths.Any(string.IsNullOrEmpty))
            {
                Logger.Info("Loading...");
                bringMainToFront();
                session.SetGame(GameManager.GetGameByType(ResourceMap.GetGameType()));
                _parent.OnGameChanged();
                _parent.LoadPaths(files, filePaths.ToArray());
            }
        }
        private async void exportSelected_Click(object sender, EventArgs e)
        {
            var saveFolderDialog = new OpenFolderDialog();
            if (saveFolderDialog.ShowDialog(this) != DialogResult.OK)
                return;

            var entries = assetDataGridView.SelectedRows.Cast<DataGridViewRow>().Select(x => _assetEntries[x.Index]).ToArray();
            var wanted = entries.Select(x => (x.Container, x.Name, x.Type, x.PathID)).ToHashSet();
            var files = entries.Select(x => x.Source).Distinct().ToList();

            bringMainToFront();
            _parent.ResetForm();
            session.AssetsManager.Game = session.Game;
            session.AssetsManager.FilterData = new AssetFilterData { Items = new List<AssetFilterDataItem>() };
            var options = new CatalogOptions { IncludeModelGameObjects = true, BuildScene = false };
            var token = session.BeginOperation();

            await Task.Run(() =>
            {
                for (int i = 0; i < files.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    session.AssetsManager.LoadFiles(files[i]);
                    if (session.AssetsManager.assetsFileList.Count > 0)
                    {
                        var rows = new CatalogBuilder(session.Context, options).Build(token).Rows
                            .Where(x => wanted.Contains((x.Container, x.Name, x.Type, x.PathID)))
                            .ToList();
                        session.Exports.ExportAssets(saveFolderDialog.Folder, rows, ExportType.Convert, token, openAfterExport: i == files.Count - 1);
                    }
                    session.AssetsManager.Clear();
                }
            }, token).ContinueWith(t => { if (t.IsFaulted) Logger.Error("Export failed", t.Exception); });
        }

        private void FilterAssetDataGrid()
        {
            TryAddFilter("Name", nameTextBox.Text);
            TryAddFilter("Container", containerTextBox.Text);
            TryAddFilter("Source", sourceTextBox.Text);
            TryAddFilter("PathID", pathTextBox.Text);

            var typeFilter = (selectedTypes.Count > 0 && selectedTypes[0] != "All")
                ? string.Join("|", selectedTypes)
                : "";

            TryAddFilter("Type", typeFilter);
            TryAddFilter("SHA256Hash", hashTextBox.Text);

            _assetEntries.Clear();
            _assetEntries.AddRange(_backupAssetEntries.FindAll(x => x.Matches(_filters)));

            assetDataGridView.Rows.Clear();
            assetDataGridView.RowCount = _assetEntries.Count;
            assetDataGridView.Refresh();
        }
        private void TryAddFilter(string name, string value)
        {
            Regex regex;
            try
            {
                regex = new Regex(value, RegexOptions.IgnoreCase);
            }
            catch (Exception)
            {
                Logger.Error($"Invalid regex {value}");
                return;
            }

            if (!_filters.TryGetValue(name, out var filter))
            {
                _filters.Add(name, regex);
            }
            else if (filter != regex)
            {
                _filters[name] = regex;
            }
        }
        private void NameTextBox_KeyPress(object sender, KeyPressEventArgs e) => FilterOnEnter(e);
        private void ContainerTextBox_KeyPress(object sender, KeyPressEventArgs e) => FilterOnEnter(e);
        private void SourceTextBox_KeyPress(object sender, KeyPressEventArgs e) => FilterOnEnter(e);
        private void PathTextBox_KeyPress(object sender, KeyPressEventArgs e) => FilterOnEnter(e);
        private void HashTextBox_KeyPress(object sender, KeyPressEventArgs e) => FilterOnEnter(e);
        private void FilterOnEnter(KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
                FilterAssetDataGrid();
        }

        private void AssetDataGridView_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (_assetEntries.Count != 0 && e.RowIndex <= _assetEntries.Count)
            {
                var assetEntry = _assetEntries[e.RowIndex];
                e.Value = e.ColumnIndex switch
                {
                    0 => assetEntry.Name,
                    1 => assetEntry.Container,
                    2 => assetEntry.Source,
                    3 => assetEntry.PathID,
                    4 => assetEntry.Type,
                    5 => assetEntry.Hash,
                    _ => ""
                };
            }
        }
        private void AssetListView_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex <= assetDataGridView.Columns.Count)
            {
                ListSortDirection direction;
                var column = assetDataGridView.Columns[e.ColumnIndex];

                if (_sortedColumn != null)
                {
                    if (_sortedColumn != column)
                    {
                        direction = ListSortDirection.Ascending;
                        _sortedColumn.HeaderCell.SortGlyphDirection = SortOrder.None;
                        _sortedColumn = column;
                    }
                    else
                    {
                        direction = _sortOrder == SortOrder.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
                    }
                }
                else
                {
                    direction = ListSortDirection.Ascending;
                    _sortedColumn = column;
                }

                _sortedColumn.HeaderCell.SortGlyphDirection = _sortOrder = direction == ListSortDirection.Ascending ? SortOrder.Ascending : SortOrder.Descending;

                Func<AssetEntry, object> keySelector = e.ColumnIndex switch
                {
                    0 => x => x.Name,
                    1 => x => x.Container,
                    2 => x => x.Source,
                    3 => x => x.PathID,
                    4 => x => x.Type.ToString(),
                    5 => x => x.Hash,
                    _ => x => ""
                };

                var sorted = direction == ListSortDirection.Ascending ? _assetEntries.OrderBy(keySelector).ToList() : _assetEntries.OrderByDescending(keySelector).ToList();

                _assetEntries.Clear();
                _assetEntries.AddRange(sorted);

                assetDataGridView.Rows.Clear();
                assetDataGridView.RowCount = _assetEntries.Count;
                assetDataGridView.Refresh();
            }
        }
        private void AssetBrowser_FormClosing(object sender, FormClosingEventArgs e)
        {
            Clear();
            base.OnClosing(e);
        }
        public void Clear()
        {
            ResourceMap.Clear();
            _assetEntries.Clear();
            assetDataGridView.Rows.Clear();
        }

        private void relocateSource_Click(object sender, EventArgs e)
        {
            // TODO: update file
            using var dialog = new FolderBrowserDialog { Description = "Select your new folder" };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                var folder = dialog.SelectedPath;
                var allFiles = Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                                .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                                .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
                int missing = 0;
                foreach (var entry in _assetEntries)
                {
                    var filename = Path.GetFileName(entry.Source);
                    if (allFiles.TryGetValue(filename, out var newPath))
                    {
                        entry.Source = newPath;
                    }
                    else
                    {
                        missing++;
                    }
                }

                assetDataGridView.Refresh();
                Logger.Info($"Relocated sources folder ! Unmoved assets because of missing file : {missing}.");
            }
        }

        private void searchBtn_Click(object sender, EventArgs e)
        {
            FilterAssetDataGrid();
        }

        private async void loadMapTwoBtn_Click(object sender, EventArgs e)
        {
            loadMapTwoBtn.Enabled = false;

            var openFileDialog = new OpenFileDialog() { Multiselect = false, Filter = "MessagePack AssetMap File|*.map|JSON AssetMap File|*.json" };
            if (openFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    var path = openFileDialog.FileName;
                    Logger.Info($"Loading AssetMap...");
                    var result = await Task.Run(() => ResourceMap.FromFile(path));

                    if (result == -1)
                    {
                        throw new Exception("Map parse failed");
                    }

                    _sortedColumn = null;

                    _secondAssetEntries.Clear();
                    _secondAssetEntries.AddRange(ResourceMap.GetEntries());

                    updateDisplay();
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to load map : {ex.ToString()}");
                }
            }
            loadMapTwoBtn.Enabled = true;
        }

        private void secondMapFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            updateDisplay();
        }

        private void clearMapTwoBtn_Click(object sender, EventArgs e)
        {
            _secondAssetEntries.Clear();
            secondMapFilter.SelectedIndex = 0;
            updateDisplay();
        }

        private void filterSelectTypesBtn_Click(object sender, EventArgs e)
        {
            Form popup = new Form
            {
                FormBorderStyle = FormBorderStyle.FixedToolWindow,
                StartPosition = FormStartPosition.Manual,
                Size = new Size(300, Math.Min(50 * types.Count(), 600)),
                Location = this.PointToScreen(new Point(filterSelectTypesBtn.Left, filterSelectTypesBtn.Bottom)),
                ShowInTaskbar = false,
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            CheckedListBox clb = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
            };

            clb.Items.AddRange(types.ToArray());

            if (selectedTypes.Count == 0)
                selectedTypes = clb.Items.Cast<string>().ToList();

            // restore previous selection
            for (int i = 0; i < clb.Items.Count; i++)
            {
                clb.SetItemChecked(i, selectedTypes.Contains(clb.Items[i].ToString()));
            }

            bool updating = false;

            clb.ItemCheck += (s, e) =>
            {
                if (updating) return;
                updating = true;

                if (e.Index == 0) // "All" clicked
                {
                    bool checkAll = e.NewValue == CheckState.Checked;
                    for (int i = 1; i < clb.Items.Count; i++)
                        clb.SetItemChecked(i, checkAll);
                }
                else
                {
                    if (e.NewValue == CheckState.Checked)
                    {
                        bool allChecked = true;
                        for (int i = 1; i < clb.Items.Count; i++)
                            if (i == e.Index ? false : !clb.GetItemChecked(i)) { allChecked = false; break; }

                        if (allChecked)
                            clb.SetItemChecked(0, true);
                    }
                    else // one unchecked
                    {
                        clb.SetItemChecked(0, false);
                        bool anyChecked = false;
                        for (int i = 1; i < clb.Items.Count; i++)
                            if (i == e.Index ? false : clb.GetItemChecked(i)) { anyChecked = true; break; }
                        if (!anyChecked) // none left checked
                            for (int i = 0; i < clb.Items.Count; i++)
                                clb.SetItemChecked(i, false);
                    }
                }

                updating = false;
            };

            Button ok = new Button
            {
                Dock = DockStyle.Fill,
                UseVisualStyleBackColor = false,
                Text = "OK"
            };

            ok.Click += (s, e) =>
            {
                var selected = clb.CheckedItems.Cast<string>().ToList();
                selectedTypes.Clear();
                selectedTypes.AddRange(selected);
                popup.Close();
            };

            layout.Controls.Add(clb, 0, 0);
            layout.Controls.Add(ok, 0, 1);
            popup.Controls.Add(layout);

            popup.ShowDialog(this);
        }

       
    }
}
