using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AnimeStudio.GUIV2.Views;
using AvalonDock.Layout;
using AvalonDock.Layout.Serialization;
using ControlzEx.Theming;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace AnimeStudio.GUIV2
{
    public partial class MainWindow : Window
    {
        public static MainWindow Instance { get; private set; }
        private string openDirectoryBackup = string.Empty;
        private string saveDirectoryBackup = string.Empty;
        //public ObservableCollection<object> sceneHierarchy { get; set; } = new ObservableCollection<object>();

        // views
        string layoutPath = "layout.config";
        Dictionary<string, UserControl> _views = new();

        public MainWindow()
        {
            //ThemeManager.Current.GetTheme();

            InitializeComponent();
            DataContext = this;

            //LoadLayout();

            Title = $"AnimeStudio v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}";

            InitializeWindow();
            InitializeConsole();
            InitializeOptions();
        }

        private void InitializeWindow()
        {
            // Set initial status
            UpdateStatus("Ready to go");

            // Subscribe to window events
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        private void InitializeConsole()
        {
            ConsoleHelper.AllocConsole();
            ConsoleHelper.EnableAnsi();
            ConsoleHelper.SetConsoleTitle("AnimeStudio Console");
            Logger.Default = new ConsoleLogger();
            var handle = ConsoleHelper.GetConsoleWindow();
            ConsoleHelper.ShowWindow(handle, ConsoleHelper.SW_SHOW);
            Logger.Flags = LoggerEvent.Info | LoggerEvent.Warning | LoggerEvent.Error | LoggerEvent.Debug;

            Logger.Info("Welcome back~");
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                ToggleMaximize();
            else
                DragMove();
        }

        private void MaxRestore_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void InitializeOptions()
        {
            try
            {
                Studio.Game = GameManager.GetGame(Properties.Settings.Default.selectedGame);
            }
            catch
            {
                Logger.Info($"Invalid game index in settings, resetting to default");
                Properties.Settings.Default.selectedGame = 0;
                Properties.Settings.Default.Save();
                Studio.Game = GameManager.GetGame(Properties.Settings.Default.selectedGame);
            }

            // temp
            Studio.Game = GameManager.GetGame(GameType.GI);
            Studio.assetsManager.Game = Studio.Game;

            Logger.Info($"Target Game is {Studio.Game.Type}");

            TypeFlags.SetTypes(JsonConvert.DeserializeObject<Dictionary<ClassIDType, (bool, bool)>>(Properties.Settings.Default.types));
        }

        // views

        void LoadLayout()
        {
            if (!File.Exists("layout.config")) return;

            var serializer = new XmlLayoutSerializer(dockManager);
            serializer.LayoutSerializationCallback += (s, e) =>
            {
                e.Content = GetOrCreateView(e.Model.ContentId);
            };

            using var reader = new StreamReader("layout.config");
            serializer.Deserialize(reader);
        }

        void SaveLayout()
        {
            var serializer = new XmlLayoutSerializer(dockManager);
            using var writer = new StreamWriter("layout.config");
            serializer.Serialize(writer);
        }

        void OpenView(string id)
        {
            var doc = dockManager.Layout.Descendents()
                .OfType<LayoutDocument>()
                .FirstOrDefault(d => d.ContentId == id);

            if (doc != null) { doc.IsSelected = true; return; }

            var view = GetOrCreateView(id);
            doc = new LayoutDocument { Content = view, ContentId = id, Title = id };

            // find or create a document pane
            var pane = dockManager.Layout.Descendents()
                .OfType<LayoutDocumentPane>()
                .FirstOrDefault();

            if (pane == null)
            {
                pane = new LayoutDocumentPane();
                dockManager.Layout.RootPanel.Children.Add(pane);
            }

            pane.Children.Add(doc);
            doc.IsSelected = true;
        }

        UserControl GetOrCreateView(string id)
        {
            if (_views.TryGetValue(id, out var v)) return v;

            v = id switch
            {
                "AssetList" => new AssetListView(),
                "SceneHierarchy" => new SceneHierarchyView(),
                "Settings" => new SettingsView(),
            };
            _views[id] = v;
            return v;
        }

        // bindings

        private void Menu_OpenAssetList_Click(object sender, RoutedEventArgs e) => OpenView("AssetList");
        private void Menu_OpenSceneHierarchy_Click(object sender, RoutedEventArgs e) => OpenView("SceneHierarchy");
        private void Menu_OpenSettings_Click(object sender, RoutedEventArgs e) => OpenView("Settings");


        // helpers

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveLayout();
        }

        public void UpdateStatus(string message)
        {
            if (statusText != null)
            {
                statusText.Text = message;
            }
        }

        public void ResetForm()
        {
            // TODO
        }

        #region Asset loading
        private async void LoadPaths(string[] paths)
        {
            await Task.Run(() =>
            {
                Studio.assetsManager.LoadFiles(paths);
            });

            BuildAssetStructure();
        }

        private async void BuildAssetStructure()
        {
            if (Studio.assetsManager.assetsFileList.Count == 0)
            {
                UpdateStatus("No Unity file can be loaded.");
                return;
            }

            var productName = await Task.Run(Studio.BuildAssetData);

            //sceneHierarchyView.Items.Clear();

            //sceneHierarchyView.ItemsSource = treeNodeCollection;

            //sceneHierarchy = treeNodeCollection;
            //sceneHierarchy.
        }
        #endregion

        #region Menu Event Handlers - Placeholders

        // File Menu
        private void LoadFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Unity Asset File",
                Multiselect = true,
                InitialDirectory = openDirectoryBackup
            };

            if (openFileDialog.ShowDialog() == true)
            {
                openDirectoryBackup = System.IO.Path.GetDirectoryName(openFileDialog.FileName);
                var paths = openFileDialog.FileNames;
                LoadPaths(paths);
            }
        }

        private void LoadFolder_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatus("Load folder clicked");
            // TODO: Implement folder loading
        }

        private void ExtractFile_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatus("Extract file clicked");
            // TODO: Implement file extraction
        }

        private void ExtractFolder_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatus("Extract folder clicked");
            // TODO: Implement folder extraction
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatus("Resetting...");
            // TODO: Implement reset logic
        }

        private void Abort_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info("Aborting...");
            
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // Options Menu
        private void SelectGame_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatus("Select game clicked");
            // TODO: Open game selector dialog
        }

        private void ExportOptions_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatus("Export options clicked");
            // TODO: Open export options dialog
        }

        // Export Menu
        private void ExportAllAssets_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatus("Exporting all assets...");
            // TODO: Implement export all assets
        }

        private void ExportSelectedAssets_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatus("Exporting selected assets...");
            // TODO: Implement export selected assets
        }

        private void ExportFilteredAssets_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatus("Exporting filtered assets...");
            // TODO: Implement export filtered assets
        }

        // About Menu
        private void About_Click(object sender, RoutedEventArgs e)
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            MessageBox.Show(
                $"AnimeStudio v{version}\n\nA Unity asset extraction tool",
                "About AnimeStudio",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        #endregion

        #region Drag and Drop Support

        protected override void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (files != null && files.Length > 0)
                {
                    UpdateStatus($"Dropped {files.Length} file(s)");
                    // TODO: Process dropped files
                    // LoadPaths(files);
                }
            }
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            base.OnDragOver(e);
            e.Handled = true;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Shows a simple info message box
        /// </summary>
        private void ShowInfo(string message, string title = "Information")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Shows a warning message box
        /// </summary>
        private void ShowWarning(string message, string title = "Warning")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>
        /// Shows an error message box
        /// </summary>
        private void ShowError(string message, string title = "Error")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// Shows a yes/no confirmation dialog
        /// </summary>
        private bool ShowConfirmation(string message, string title = "Confirm")
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }

        #endregion
    }
}