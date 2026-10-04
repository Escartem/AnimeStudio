using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AnimeStudio.App;
using AnimeStudio.GUI.Core;
using static AnimeStudio.AssetsManager;

namespace AnimeStudio.GUI
{
    partial class MainForm : Form
    {
        private readonly StudioSession session = new StudioSession();
        private readonly ILogger hubLogger;
        private readonly List<(ToolStripMenuItem Item, Func<bool> Get)> optionBindings = new();
        private bool loadingOptions;
        private int lastStatusVersion = -1;
        private bool showingError;

        private AssetBrowser assetBrowser;
        private string openDirectoryBackup = string.Empty;
        private string saveDirectoryBackup = string.Empty;

        private StudioSettings Settings => session.Settings;
        private AssetCatalog Catalog => session.Catalog;
        private static string BaseTitle => $"AnimeStudio v{Application.ProductVersion}";

        public MainForm()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            InitializeComponent();
            hubLogger = new HubLogger(session.Status);
            session.Context.RequestAssemblyFolder = RequestAssemblyFolder;

            ApplyTheme();
            Text = BaseTitle;
            InitializeLogger();
            session.Initialize();
            InitializeOptions();
            InitializeExportMenus();
            InitializeMapsMenu();
            InitializePreview();
            timer.Start();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            session.Abort();
            DisposePreview();
            session.SaveSettings();
        }

        #region Options
        private void InitializeOptions()
        {
            BindOption(displayAll, () => Settings.DisplayAll, v => Settings.DisplayAll = v);
            BindOption(enablePreview, () => Settings.EnablePreview, v => Settings.EnablePreview = v, OnPreviewToggled);
            BindOption(displayInfo, () => Settings.DisplayInfo, v => Settings.DisplayInfo = v, UpdateInfoLabel);
            BindOption(enableModelPreview, () => Settings.EnableModelPreview, v => Settings.EnableModelPreview = v);
            BindOption(modelsOnly, () => Settings.ModelsOnly, v => Settings.ModelsOnly = v, () => _ = ApplyQueryAsync());
            BindOption(enableResolveDependencies, () => Settings.ResolveDependencies, v => Settings.ResolveDependencies = v, session.ApplySettings);
            BindOption(allowDuplicates, () => Settings.AllowDuplicates, v => Settings.AllowDuplicates = v);
            BindOption(useBundleContainerNameToolStripMenuItem, () => Settings.UseBundleContainerName, v => Settings.UseBundleContainerName = v);
            BindOption(skipContainer, () => Settings.SkipContainer, v => Settings.SkipContainer = v);
            BindOption(enableConsole, () => Settings.EnableConsole, v => Settings.EnableConsole = v, UpdateConsole);
            BindOption(enableFileLogging, () => Settings.EnableFileLogging, v => Settings.EnableFileLogging = v, () => Logger.FileLogging = Settings.EnableFileLogging);
            BindOption(toolStripMenuItem15, () => Settings.ShowErrorMessages, v => Settings.ShowErrorMessages = v, () => session.Status.ShowErrorMessages = Settings.ShowErrorMessages);
            RefreshOptions();
        }

        private void RefreshOptions()
        {
            loadingOptions = true;
            foreach (var (item, get) in optionBindings)
                item.Checked = get();
            specifyTheme.SelectedIndex = (int)Settings.Theme;
            loadingOptions = false;
        }

        private void BindOption(ToolStripMenuItem item, Func<bool> get, Action<bool> set, Action changed = null)
        {
            item.CheckOnClick = true;
            optionBindings.Add((item, get));
            item.CheckedChanged += (_, _) =>
            {
                if (loadingOptions)
                    return;
                set(item.Checked);
                session.SaveSettings();
                changed?.Invoke();
            };
        }

        private void showExpOpt_Click(object sender, EventArgs e)
        {
            using var exportOpt = new ExportOptions(session);
            if (exportOpt.ShowDialog(this) == DialogResult.OK && exportOpt.Resetted)
            {
                session.Initialize();
                RefreshOptions();
                InitializeLogger();
                UpdateMapOutputs();
            }
        }

        private void ApplyTheme()
        {
#pragma warning disable WFO5001
            try
            {
                var dark = Settings.Theme switch
                {
                    GuiColorTheme.Dark => true,
                    GuiColorTheme.Light => false,
                    _ => IsSystemInDarkMode(),
                };
                Application.SetColorMode(dark ? SystemColorMode.Dark : SystemColorMode.Classic);
                assetListView.GridLines = !dark;
                assetInfoLabel.ForeColor = dark ? System.Drawing.SystemColors.ControlLightLight : System.Drawing.SystemColors.ControlText;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to apply theme : {ex}");
            }
#pragma warning restore WFO5001
        }

        private static bool IsSystemInDarkMode()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("AppsUseLightTheme") is int useLight && useLight == 0;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Could not read the system app theme, assuming light : {ex.Message}");
                return false;
            }
        }

        private void specifyTheme_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (loadingOptions || specifyTheme.SelectedIndex == (int)Settings.Theme)
                return;

            Settings.Theme = (GuiColorTheme)specifyTheme.SelectedIndex;
            session.SaveSettings();
            Logger.Info("Updated app theme !");

            var text = Settings.Theme == GuiColorTheme.Light
                ? "The application needs to restart in order to apply the theme."
                : "Please keep in mind dark mode is still in beta, a better support is planned in future versions of .NET. The application needs to restart in order to apply the theme.";
            MessageBox.Show(text, "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ApplyTheme();
        }
        #endregion

        #region Logging and status
        private void InitializeLogger()
        {
            loggedEventsMenuItem.DropDown.Closing -= loggedEventsMenuItem_DropDownClosing;
            loggedEventsMenuItem.DropDown.Closing += loggedEventsMenuItem_DropDownClosing;
            ConsoleHelper.AllocConsole();
            ConsoleHelper.SetConsoleTitle("Debug Console");
            UpdateConsole();

            loggedEventsMenuItem.DropDownItems.Clear();
            foreach (var loggerEvent in Enum.GetValues<LoggerEvent>()[1..^1])
            {
                var menuItem = new ToolStripMenuItem(loggerEvent.ToString())
                {
                    CheckOnClick = true,
                    Checked = Settings.LoggerEvents.HasFlag(loggerEvent),
                    Tag = loggerEvent
                };
                menuItem.Click += LoggerEventMenuItem_Click;
                loggedEventsMenuItem.DropDownItems.Add(menuItem);
            }
            Logger.Flags = Settings.LoggerEvents;
            Logger.FileLogging = Settings.EnableFileLogging;
        }

        private void UpdateConsole()
        {
            var handle = ConsoleHelper.GetConsoleWindow();
            Logger.Default = Settings.EnableConsole ? new ConsoleLogger() : hubLogger;
            ConsoleHelper.ShowWindow(handle, Settings.EnableConsole ? ConsoleHelper.SW_SHOW : ConsoleHelper.SW_HIDE);
        }

        private void LoggerEventMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is not ToolStripMenuItem { Tag: LoggerEvent clickedEvent } item)
                return;
            Settings.LoggerEvents = item.Checked ? Settings.LoggerEvents | clickedEvent : Settings.LoggerEvents & ~clickedEvent;
            Logger.Flags = Settings.LoggerEvents;
            session.SaveSettings();
            Logger.Info($"Logger events updated: {clickedEvent} set to {item.Checked}");
        }

        private void loggedEventsMenuItem_DropDownClosing(object sender, ToolStripDropDownClosingEventArgs e)
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
                e.Cancel = true;
        }

        private void clearConsoleToolStripMenuItem_Click(object sender, EventArgs e) => Console.Clear();

        private void SetStatus(string text) => session.Status.SetStatus(text);

        // Polls the status hub so workers never wait on the UI thread.
        private void timer_Tick(object sender, EventArgs e)
        {
            var status = session.Status;
            if (status.Version != lastStatusVersion)
            {
                lastStatusVersion = status.Version;
                toolStripStatusLabel1.Text = status.Status;
                progressBar1.Value = status.ProgressValue;
            }
            while (status.TryDequeueLog(out _)) { }
            ShowPendingErrors();
            UpdateAudio();
        }

        private void ShowPendingErrors()
        {
            if (showingError || session.Status.PendingErrors.IsEmpty)
                return;
            var errors = new List<string>();
            while (session.Status.PendingErrors.TryDequeue(out var error))
                errors.Add(error);
            var text = errors.Count == 1 ? errors[0] : $"{errors[0]}\n\n... and {errors.Count - 1} more errors, enable the console or file logging to see them.";
            showingError = true;
            try { MessageBox.Show(this, text); }
            finally { showingError = false; }
        }
        #endregion

        #region Loading
        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (paths.Length > 0)
                LoadPaths(null, paths);
        }

        private void loadFile_Click(object sender, EventArgs e)
        {
            openFileDialog1.InitialDirectory = openDirectoryBackup;
            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                openDirectoryBackup = Path.GetDirectoryName(openFileDialog1.FileNames[0]);
                LoadPaths(null, openFileDialog1.FileNames);
            }
        }

        private void loadFolder_Click(object sender, EventArgs e)
        {
            var folder = PickFolder(initial: openDirectoryBackup);
            if (folder != null)
            {
                openDirectoryBackup = folder;
                LoadPaths(null, folder);
            }
        }

        public async void LoadPaths(List<AssetFilterDataItem> filterData, params string[] paths)
        {
            SetStatus("Computing load size...");
            var totalSize = await StudioSession.GetTotalSizeAsync(paths);
            var warning = StudioSession.CheckMemory(totalSize);
            if (warning != null && MessageBox.Show(warning, "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                SetStatus("Loading cancelled");
                return;
            }

            ResetForm();
            session.UnityVersion = specifyUnityVersion.Text;
            var result = await session.LoadAsync(paths, filterData);
            ShowCatalog(result);
        }

        private void ShowCatalog(LoadResult result)
        {
            Text = result.Title != null ? $"{BaseTitle} - {result.Title}" : BaseTitle;
            PopulateTypeFilters();
            PopulateScene();
            PopulateClasses();
            visible = Enumerable.Range(0, Catalog.Rows.Count).ToArray();
            assetListView.VirtualListSize = visible.Length;
            if (Settings.ModelsOnly)
                _ = ApplyQueryAsync();
            SetStatus(result.Summary);
        }

        private async void extractFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog(this) != DialogResult.OK)
                return;
            var savePath = PickFolder("Select the save folder");
            if (savePath == null)
                return;
            var count = await session.ExtractAsync(openFileDialog1.FileNames, null, savePath);
            SetStatus($"Finished extracting {count} files.");
        }

        private async void extractFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var folder = PickFolder();
            if (folder == null)
                return;
            var savePath = PickFolder("Select the save folder");
            if (savePath == null)
                return;
            var count = await session.ExtractAsync(null, folder, savePath);
            SetStatus($"Finished extracting {count} files.");
        }

        public void ResetForm()
        {
            Text = BaseTitle;
            session.Reset();
            ResetAssetList();
            ResetScene();
            classesListView.Items.Clear();
            classesListView.Groups.Clear();
            classTextBox.Visible = false;
            dumpTextBox.Text = string.Empty;
            ClearPreview();
            selectedRow = null;
            SetStatus("Reset successfully !!");
        }

        private void resetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ResetForm();
            AssetsHelper.Clear();
            assetBrowser?.Clear();
        }

        private void abortStripMenuItem_Click(object sender, EventArgs e) => session.Abort();

        private void exitToolStripMenuItem_Click(object sender, EventArgs e) => Application.Exit();
        #endregion

        #region Dialogs
        private string PickFolder(string title = null, string initial = null)
        {
            var dialog = new OpenFolderDialog { InitialFolder = initial ?? saveDirectoryBackup };
            if (title != null)
                dialog.Title = title;
            return dialog.ShowDialog(this) == DialogResult.OK ? dialog.Folder : null;
        }

        private string PickSaveFolder()
        {
            var folder = PickFolder();
            if (folder != null)
                saveDirectoryBackup = folder;
            return folder;
        }

        // Called from worker threads when a MonoBehaviour needs its assemblies.
        private string RequestAssemblyFolder()
        {
            return (string)Invoke(() => PickFolder("Select Assembly Folder", openDirectoryBackup));
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var about = new AboutForm();
            about.ShowDialog(this);
        }

        private void gameSelectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var gameSelector = new GameSelector(session);
            if (gameSelector.ShowDialog(this) == DialogResult.OK)
                OnGameChanged();
        }

        public void OnGameChanged()
        {
            ResetForm();
            session.AssetsManager.SpecifyUnityVersion = specifyUnityVersion.Text;
        }

        private void editUnityCNKeysToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var unityCNEdit = new UnityCNEdit();
            unityCNEdit.ShowDialog(this);
        }
        #endregion
    }
}
