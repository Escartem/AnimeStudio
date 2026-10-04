using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using AnimeStudio.GUI.Core;

namespace AnimeStudio.GUI
{
    partial class MainForm
    {
        private const string CabMapOutputTag = "CABMap";
        private bool updatingMapList;

        private void InitializeMapsMenu()
        {
            assetMapTypeMenuItem.DropDownItems.Clear();
            assetMapTypeMenuItem.DropDownItems.Add(new ToolStripMenuItem("CAB Map") { CheckOnClick = true, Tag = CabMapOutputTag });
            assetMapTypeMenuItem.DropDownItems.Add(new ToolStripSeparator());
            foreach (var mapType in Enum.GetValues<ExportListType>()[1..])
                assetMapTypeMenuItem.DropDownItems.Add(new ToolStripMenuItem($"Asset Map ({mapType})") { CheckOnClick = true, Tag = mapType });
            assetMapTypeMenuItem.DropDown.Closing += (_, e) => e.Cancel = e.CloseReason == ToolStripDropDownCloseReason.ItemClicked;
            UpdateMapOutputs();

            MapNameComboBox.SelectedIndexChanged += MapNameComboBox_SelectedIndexChanged;
            RefreshMapList();
        }

        private void UpdateMapOutputs()
        {
            foreach (var item in assetMapTypeMenuItem.DropDownItems.OfType<ToolStripMenuItem>())
            {
                item.Checked = item.Tag is ExportListType type
                    ? Settings.AssetMapType.HasFlag(type)
                    : Settings.BuildCabMap;
            }
        }

        private void assetMapTypeMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (e.ClickedItem is not ToolStripMenuItem item)
                return;
            // CheckOnClick flips Checked after this event.
            var check = !item.Checked;
            if (item.Tag is ExportListType type)
                Settings.AssetMapType = check ? Settings.AssetMapType | type : Settings.AssetMapType & ~type;
            else
                Settings.BuildCabMap = check;
            session.SaveSettings();
        }

        private void RefreshMapList()
        {
            updatingMapList = true;
            MapNameComboBox.Items.Clear();
            MapNameComboBox.Items.Add(new MapEntry(string.Empty));
            foreach (var map in session.Maps.GetKnownCABMaps())
                MapNameComboBox.Items.Add(new MapEntry(map));
            var selected = Settings.SelectedCABMap;
            MapNameComboBox.SelectedItem = MapNameComboBox.Items.Cast<MapEntry>().FirstOrDefault(x => string.Equals(x.Value, selected, StringComparison.OrdinalIgnoreCase)) ?? MapNameComboBox.Items[0];
            updatingMapList = false;
        }

        private void miscToolStripMenuItem_DropDownOpening(object sender, EventArgs e) => RefreshMapList();

        private async void MapNameComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (updatingMapList || MapNameComboBox.SelectedItem is not MapEntry entry)
                return;
            miscToolStripMenuItem.DropDown.Close();
            ResetForm();
            if (string.IsNullOrEmpty(entry.Value))
            {
                AssetsHelper.Clear();
                Settings.SelectedCABMap = string.Empty;
                session.SaveSettings();
                return;
            }
            miscToolStripMenuItem.Enabled = false;
            await session.Maps.LoadCABMapAsync(entry.Value);
            miscToolStripMenuItem.Enabled = true;
        }

        private async void loadCABMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var openFileDialog = new OpenFileDialog { Multiselect = false, Filter = "CABMap File|*.bin", InitialDirectory = MapsService.MapsDirectory };
            if (openFileDialog.ShowDialog(this) != DialogResult.OK)
                return;
            miscToolStripMenuItem.Enabled = false;
            await session.Maps.LoadCABMapAsync(openFileDialog.FileName);
            miscToolStripMenuItem.Enabled = true;
            RefreshMapList();
        }

        private void clearMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MapNameComboBox.SelectedItem is not MapEntry { Value.Length: > 0 } entry)
                return;
            if (MessageBox.Show($"{entry} will be deleted, this can't be undone, continue ?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            session.Maps.DeleteCABMap(entry.Value);
            RefreshMapList();
        }

        // Builds every checked output in one pass over the game folder.
        private async void buildAssetMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var assetTypes = Settings.AssetMapType;
            var buildAssetMap = assetTypes != ExportListType.None;
            if (!Settings.BuildCabMap && !buildAssetMap)
            {
                MessageBox.Show("Select at least one output in Maps > Outputs.", "Build maps", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var gameFolder = PickFolder("Select Game Folder", openDirectoryBackup);
            if (gameFolder == null)
                return;

            using var saveFileDialog = new SaveFileDialog
            {
                Title = "Select the name and location of the maps",
                FileName = session.Game?.Name ?? "map",
                Filter = "Map files|*.bin;*.map;*.xml;*.json",
                InitialDirectory = Settings.BuildCabMap ? MapsService.MapsDirectory : saveDirectoryBackup,
                OverwritePrompt = false,
            };
            Directory.CreateDirectory(MapsService.MapsDirectory);
            if (saveFileDialog.ShowDialog(this) != DialogResult.OK)
                return;

            var output = Path.Combine(Path.GetDirectoryName(saveFileDialog.FileName), Path.GetFileNameWithoutExtension(saveFileDialog.FileName));
            var error = MapsService.ValidateName(Path.GetFileName(output));
            if (error != null)
            {
                Logger.Warning(error);
                return;
            }
            if (Settings.BuildCabMap && File.Exists(output + ".bin")
                && MessageBox.Show("Map already exist, Do you want to override it ?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            if (!Settings.BuildCabMap)
                saveDirectoryBackup = Path.GetDirectoryName(output);
            session.UnityVersion = specifyUnityVersion.Text;
            miscToolStripMenuItem.Enabled = false;
            try
            {
                await session.Maps.BuildAsync(gameFolder, output, Settings.BuildCabMap, assetTypes, buildAssetMap);
            }
            finally
            {
                miscToolStripMenuItem.Enabled = true;
            }
            RefreshMapList();
        }

        private void loadAssetMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (assetBrowser == null || assetBrowser.IsDisposed)
                assetBrowser = new AssetBrowser(this, session);
            assetBrowser.Show();
            assetBrowser.BringToFront();
        }

        private async void loadAIToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Settings.SkipContainer)
            {
                Logger.Info("Skip container is enabled, aborting...");
                return;
            }
            using var openFileDialog = new OpenFileDialog { Multiselect = false, Filter = "Asset Index JSON File|*.json" };
            if (openFileDialog.ShowDialog(this) != DialogResult.OK)
                return;
            Logger.Info("Loading AI...");
            loadAIToolStripMenuItem.Enabled = false;
            await session.LoadAssetIndexAsync(openFileDialog.FileName);
            assetListView.Invalidate();
            loadAIToolStripMenuItem.Enabled = true;
        }

        private async void aiVersionMenu_DropDownOpening(object sender, EventArgs e)
        {
            if (specifyAIVersion.Enabled && await AIVersionManager.FetchVersions())
                UpdateVersionList();
        }

        private void UpdateVersionList()
        {
            specifyAIVersion.SelectedIndexChanged -= specifyAIVersion_SelectedIndexChanged;
            var selectedIndex = specifyAIVersion.SelectedIndex;
            specifyAIVersion.Items.Clear();
            specifyAIVersion.Items.Add("None");
            foreach (var (version, cached) in AIVersionManager.GetVersions())
                specifyAIVersion.Items.Add(version + (cached ? " (cached)" : ""));
            specifyAIVersion.SelectedIndex = Math.Clamp(selectedIndex, 0, specifyAIVersion.Items.Count - 1);
            specifyAIVersion.SelectedIndexChanged += specifyAIVersion_SelectedIndexChanged;
        }

        private async void specifyAIVersion_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (specifyAIVersion.SelectedIndex <= 0)
                return;
            if (Settings.SkipContainer)
            {
                Logger.Info("Skip container is enabled, aborting...");
                return;
            }
            miscToolStripMenuItem.DropDown.Close();
            var version = specifyAIVersion.SelectedItem.ToString().Split(' ')[0];

            Logger.Info($"Loading AI v{version}");
            specifyAIVersion.Enabled = false;
            try
            {
                var path = await AIVersionManager.FetchAI(version);
                await session.LoadAssetIndexAsync(path);
                assetListView.Invalidate();
                UpdateVersionList();
            }
            finally
            {
                specifyAIVersion.Enabled = true;
            }
        }

        private sealed record MapEntry(string Value)
        {
            public override string ToString() => string.IsNullOrEmpty(Value) ? "None" : MapsService.DisplayName(Value);
        }
    }
}
