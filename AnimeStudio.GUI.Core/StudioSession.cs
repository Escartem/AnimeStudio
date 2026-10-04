using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnimeStudio.App;
using AnimeStudio.GUI.Core.Preview;
using static AnimeStudio.AssetsManager;

namespace AnimeStudio.GUI.Core
{
    public sealed record LoadResult(string Title, int FileCount, int AssetCount, string Summary);

    // Everything the GUI does with the core, without any UI type.
    public sealed class StudioSession
    {
        public StudioContext Context { get; } = new StudioContext();
        public StatusHub Status { get; } = new StatusHub();
        public StudioSettings Settings { get; private set; }
        public AssetCatalog Catalog { get; private set; } = AssetCatalog.Empty;
        public PreviewService Previews { get; }
        public ExportService Exports { get; }
        public MapsService Maps { get; }
        public string UnityVersion { get; set; } = string.Empty;

        private CancellationTokenSource operation = new CancellationTokenSource();

        public AssetsManager AssetsManager => Context.AssetsManager;
        public Game Game => Context.Game;

        public StudioSession()
        {
            Settings = SettingsStore.Load();
            Progress.Default = Status;
            Previews = new PreviewService(Context, () => Settings);
            Exports = new ExportService(Context, () => Settings.ToExportConfig(), Status.SetStatus);
            Maps = new MapsService(this);
        }

        public void Initialize()
        {
            Status.ShowErrorMessages = Settings.ShowErrorMessages;
            Logger.Flags = Settings.LoggerEvents;
            Logger.FileLogging = Settings.EnableFileLogging;
            ApplySettings();

            var game = GameManager.GetGame(Settings.SelectedGame);
            if (game == null)
            {
                Logger.Info("Invalid game index in settings, resetting to default");
                Settings.SelectedGame = 0;
                SaveSettings();
                game = GameManager.GetGame(0);
            }
            Context.Game = game;
            Context.ApplyUnityCNKey(Settings.LastUnityCNKey);
            Logger.Info($"Target Game is {game.Type}");

            Context.LoadZ3Paths();
            Maps.RestoreSelectedCABMap();
        }

        public void ApplySettings()
        {
            Settings.ApplyToCore();
            AssetsManager.ResolveDependencies = Settings.ResolveDependencies;
        }

        public void ReplaceSettings(StudioSettings settings)
        {
            Settings = settings;
            SaveSettings();
            ApplySettings();
        }

        public void SaveSettings() => SettingsStore.Save(Settings);

        public CancellationToken BeginOperation()
        {
            var next = new CancellationTokenSource();
            Interlocked.Exchange(ref operation, next).Dispose();
            return next.Token;
        }

        public void Abort()
        {
            Logger.Info("Aborting....");
            operation.Cancel();
            AssetsManager.tokenSource.Cancel();
            AssetsHelper.tokenSource.Cancel();
        }

        public void Reset()
        {
            AssetsManager.Clear();
            Context.AssemblyLoader.Clear();
            Catalog = AssetCatalog.Empty;
            Previews.ClearCache();
        }

        public void SetGame(Game game)
        {
            Settings.SelectedGame = GameManager.GetGameIndex(game);
            SaveSettings();
            Reset();
            Context.Game = game;
            Logger.Info($"Target Game is {game.Name}");
            Context.ApplyUnityCNKey(game.Type == GameType.UnityCNCustomKey ? Settings.LastUnityCNKey : null);
        }

        public static Task<long> GetTotalSizeAsync(IEnumerable<string> paths) => Task.Run(() =>
        {
            long total = 0;
            foreach (var path in paths)
            {
                if (File.Exists(path))
                    total += new FileInfo(path).Length;
                else if (Directory.Exists(path))
                    total += new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
            }
            return total;
        });

        // Returns a warning when the load is likely to exhaust free RAM, null otherwise.
        public static string CheckMemory(long totalSize)
        {
            // Measured average of RAM used per byte of input over several games.
            var estimated = (long)(totalSize * 8.5);
            var free = MemoryInfo.GetAvailablePhysicalMemory();
            if (free == null || estimated <= (long)free.Value)
                return null;
            return $"You are trying to load {FormatBytes(totalSize)} of data, which will make the tool use approximately {FormatBytes(estimated)} of ram, but you have {FormatBytes(free.Value)} left, continue ?";
        }

        public static string FormatBytes(double bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int i;
            for (i = 0; i < suffixes.Length - 1 && bytes >= 1024; i++)
                bytes /= 1024;
            return $"{bytes:0.##} {suffixes[i]}";
        }

        public async Task<LoadResult> LoadAsync(string[] paths, List<AssetFilterDataItem> filter = null)
        {
            Reset();
            var token = BeginOperation();
            AssetsManager.SpecifyUnityVersion = UnityVersion;
            AssetsManager.Game = Game;
            AssetsManager.FilterData = new AssetFilterData { Items = filter ?? new List<AssetFilterDataItem>() };

            if (paths.Length == 1 && File.Exists(paths[0]) && Path.GetExtension(paths[0]).Equals(".txt", StringComparison.OrdinalIgnoreCase))
                paths = await File.ReadAllLinesAsync(paths[0], token);

            await Task.Run(() =>
            {
                if (paths.Length == 1 && Directory.Exists(paths[0]))
                    AssetsManager.LoadFolder(paths[0]);
                else
                    AssetsManager.LoadFiles(paths);
            }, token);

            return await BuildCatalogAsync(paths, token);
        }

        private async Task<LoadResult> BuildCatalogAsync(string[] sourcePaths, CancellationToken token)
        {
            var files = AssetsManager.assetsFileList;
            if (files.Count == 0)
            {
                if (Game?.Type == GameType.AFKJourney)
                {
                    var loose = await Task.Run(() => CatalogBuilder.CollectAfkJourneyFiles(sourcePaths, Array.Empty<AssetRow>()), token);
                    if (loose.Count > 0)
                    {
                        Catalog = new AssetCatalog(loose, new SceneGraph(), Array.Empty<ClassStructureGroup>(), null);
                        return new LoadResult("AFK Journey loose assets", 0, loose.Count, $"Loaded {loose.Count} AFK Journey loose files for preview.");
                    }
                }
                return new LoadResult(null, 0, 0, "No Unity file can be loaded.");
            }

            AssetCatalog catalog;
            try
            {
                var builder = new CatalogBuilder(Context, Settings.ToCatalogOptions(), Status.SetStatus);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, AssetsManager.tokenSource.Token);
                catalog = await Task.Run(() => builder.Build(linked.Token), linked.Token);
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Building asset list has been cancelled !!");
                return new LoadResult(null, files.Count, 0, "Loading cancelled");
            }

            if (Game?.Type == GameType.AFKJourney)
                catalog.Rows.AddRange(CatalogBuilder.CollectAfkJourneyFiles(sourcePaths, catalog.Rows));
            Catalog = catalog;

            var productName = catalog.ProductName;
            if (string.IsNullOrEmpty(productName))
            {
                productName = !Game.Type.IsNormal() ? Game.Name
                    : Game is UnityCNGame unityCN ? unityCN.Name
                    : "no productName";
            }
            var title = $"{productName} - {files[0].unityVersion} - {files[0].m_TargetPlatform}";

            var summary = $"Finished loading {files.Count} files with {catalog.Rows.Count} exportable assets";
            var declared = files.Sum(x => x.m_Objects.Count);
            var read = files.Sum(x => x.Objects.Count);
            if (declared != read)
                summary += $" and {declared - read} assets failed to read";
            return new LoadResult(title, files.Count, catalog.Rows.Count, summary);
        }

        public Task<int> ExtractAsync(string[] files, string folder, string savePath)
        {
            var token = BeginOperation();
            var extractor = new Extractor(Game, Status.SetStatus);
            return Task.Run(() =>
            {
                try
                {
                    return folder != null ? extractor.ExtractFolder(folder, savePath, token) : extractor.ExtractFiles(files, savePath, token);
                }
                catch (OperationCanceledException)
                {
                    return 0;
                }
            }, token);
        }

        public Task UpdateContainersAsync() => Task.Run(() =>
        {
            if (!Settings.SkipContainer)
                ContainerResolver.Update(Catalog.Rows, Game, Context.Z3Paths);
        });

        public Task LoadAssetIndexAsync(string path) => Task.Run(() => ResourceIndex.FromFile(path)).ContinueWith(_ => UpdateContainersAsync()).Unwrap();

        // Fills the hash of every row, the hash column is otherwise computed lazily for visible rows.
        public Task ComputeHashesAsync(IReadOnlyList<AssetRow> rows, CancellationToken token) => Task.Run(() =>
        {
            Progress.Reset();
            for (var i = 0; i < rows.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var row = rows[i];
                if (!row.HasHash)
                    AssetIo.Run(() => _ = row.Hash, token);
                Progress.Report(i + 1, rows.Count);
            }
        }, token);
    }
}
