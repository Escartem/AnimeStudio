using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnimeStudio.App;
using AnimeStudio.CLI.Properties;
using Newtonsoft.Json;

namespace AnimeStudio.CLI
{
    [Flags]
    public enum MapOpType
    {
        None,
        Load,
        CABMap,
        AssetMap = 4,
        Both = 8,
        All = Both | Load,
    }

    public class Program
    {
        public static void Main(string[] args) => CommandLine.Init(args);

        public static void Run(Options o)
        {
            try
            {
                var game = GameManager.GetGame(o.GameName);
                var context = new StudioContext(new AssetsManager() { ResolveDependencies = false });
                context.LoadZ3Paths();

                if (game == null)
                {
                    Console.WriteLine("Invalid Game !!");
                    Console.WriteLine(GameManager.SupportedGames());
                    return;
                }

                if (game is UnityCNGame unityCNGame)
                {
                    UnityCN.SetKey(unityCNGame.Key);
                    Logger.Info($"[UnityCN] Selected Key is {unityCNGame.Key.Name} - {unityCNGame.Key.Key}");
                }

                context.Game = game;
                Logger.Default = new ConsoleLogger();
                Logger.Flags = o.LoggerFlags.Aggregate((e, x) => e |= x);
                Logger.FileLogging = Settings.Default.enableFileLogging;
                AssetsHelper.Minimal = Settings.Default.minimalAssetMap;
                AssetsHelper.SetUnityVersion(o.UnityVersion);

                TypeFlags.SetTypes(JsonConvert.DeserializeObject<Dictionary<ClassIDType, (bool, bool)>>(Settings.Default.types));

                var classTypeFilter = ParseTypeFilter(o);

                if (o.GroupAssetsType == AssetGroupOption.ByContainer)
                {
                    TypeFlags.SetType(ClassIDType.AssetBundle, true, false);
                }

                var assetsManager = context.AssetsManager;
                assetsManager.Silent = o.Silent;
                assetsManager.Game = game;
                assetsManager.SpecifyUnityVersion = o.UnityVersion;
                o.Output.Create();

                if (o.Key != default)
                {
                    MiHoYoBinData.Encrypted = true;
                    MiHoYoBinData.Key = o.Key;
                }

                if (o.AIFile != null && game.Type.IsGISubGroup())
                {
                    ResourceIndex.FromFile(o.AIFile.FullName);
                }

                if (o.DummyDllFolder != null)
                {
                    context.AssemblyLoader.Load(o.DummyDllFolder.FullName);
                }

                Logger.Info("Scanning for files...");
                var files = o.Input.Attributes.HasFlag(FileAttributes.Directory) ? Directory.GetFiles(o.Input.FullName, "*.*", SearchOption.AllDirectories).OrderBy(x => x.Length).ToArray() : new string[] { o.Input.FullName };
                Logger.Info($"Found {files.Length} files");

                MapBuildRequest MapRequest(bool cabMap, bool assetMap) => new MapBuildRequest
                {
                    Files = files,
                    BaseFolder = o.Input.FullName,
                    Game = game,
                    CabMapPath = cabMap ? AssetsHelper.ResolveCABMapPath(o.MapName) : null,
                    BuildAssetMap = assetMap,
                    AssetMapDirectory = o.Output.FullName,
                    AssetMapName = o.MapName,
                    AssetMapTypes = o.MapType,
                    TypeFilters = classTypeFilter,
                    NameFilters = o.NameFilter,
                    ContainerFilters = o.ContainerFilter,
                };

                // Load + CABMap builds the CABMap, plain CABMap loads it. Kept as-is for existing scripts.
                if (o.MapOp.HasFlag(MapOpType.CABMap))
                {
                    if (o.MapOp.HasFlag(MapOpType.Load))
                    {
                        AssetsHelper.BuildMaps(MapRequest(cabMap: true, assetMap: false));
                    }
                    else
                    {
                        AssetsHelper.LoadCABMap(o.MapName);
                        assetsManager.ResolveDependencies = true;
                    }
                }
                if (o.MapOp.HasFlag(MapOpType.AssetMap))
                {
                    if (o.MapOp.HasFlag(MapOpType.Load))
                    {
                        files = AssetsHelper.ParseAssetMap(o.MapName, o.MapType, classTypeFilter, o.NameFilter, o.ContainerFilter);
                    }
                    else
                    {
                        AssetsHelper.BuildMaps(MapRequest(cabMap: false, assetMap: true));
                    }
                }
                if (o.MapOp.HasFlag(MapOpType.Both))
                {
                    AssetsHelper.BuildMaps(MapRequest(cabMap: true, assetMap: true));
                }
                if (o.MapOp.Equals(MapOpType.None) || o.MapOp.HasFlag(MapOpType.Load))
                {
                    ExportFiles(context, o, files, classTypeFilter);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static void ExportFiles(StudioContext context, Options o, string[] files, ClassIDType[] classTypeFilter)
        {
            var config = Settings.Default.ToExportConfig(o.GroupAssetsType);
            var scraper = config.ScrapeMonos ? new MonoStringScraper() : null;
            var exportService = new ExportService(context, () => config) { ContainerFallbackToType = true, Scraper = scraper };
            var catalogOptions = new CatalogOptions { IncludeExportableTypes = true, IncludeModelGameObjects = true, BuildScene = false };
            var index = 0;

            var path = Path.GetDirectoryName(Path.GetFullPath(files[0]));
            ImportHelper.MergeSplitAssets(path);
            var toReadFile = ImportHelper.ProcessingSplitFiles(files.ToList());

            foreach (var file in toReadFile)
            {
                context.AssetsManager.LoadFiles(file);
                if (context.AssetsManager.assetsFileList.Count > 0)
                {
                    var catalog = new CatalogBuilder(context, catalogOptions).Build(default, index);
                    index += catalog.Rows.Count;
                    var rows = catalog.Rows.Where(x =>
                        (o.NameFilter.IsNullOrEmpty() || o.NameFilter.Any(y => y.IsMatch(x.Name)))
                        && (classTypeFilter.IsNullOrEmpty() || classTypeFilter.Contains(x.Type))
                        && (o.ContainerFilter.IsNullOrEmpty() || o.ContainerFilter.Any(y => y.IsMatch(x.Container)))).ToList();
                    exportService.ExportAssets(o.Output.FullName, rows, o.AssetExportType, openAfterExport: false);
                }
                context.AssetsManager.Clear();
            }

            scraper?.Save(AssetsHelper.MapsDirectory);
        }

        private static ClassIDType[] ParseTypeFilter(Options o)
        {
            if (o.TypeFilter.IsNullOrEmpty())
                return Array.Empty<ClassIDType>();

            var exportTexture2D = false;
            var exportMaterial = false;
            var classTypeFilterList = new List<ClassIDType>();
            foreach (var entry in o.TypeFilter)
            {
                var typeStr = entry;
                try
                {
                    var flag = TypeFlag.Both;
                    if (typeStr.Contains(':'))
                    {
                        var param = typeStr.Split(':');
                        flag = (TypeFlag)Enum.Parse(typeof(TypeFlag), param[1], true);
                        typeStr = param[0];
                    }

                    var type = (ClassIDType)Enum.Parse(typeof(ClassIDType), typeStr, true);
                    if (type == ClassIDType.Texture2D)
                        exportTexture2D = flag.HasFlag(TypeFlag.Export);
                    else if (type == ClassIDType.Material)
                        exportMaterial = flag.HasFlag(TypeFlag.Export);

                    TypeFlags.SetType(type, flag.HasFlag(TypeFlag.Parse), flag.HasFlag(TypeFlag.Export));
                    classTypeFilterList.Add(type);
                }
                catch (Exception)
                {
                    Logger.Error($"{typeStr} has invalid format, skipping...");
                }
            }

            if (ClassIDType.GameObject.CanExport() || ClassIDType.Animator.CanExport())
            {
                TypeFlags.SetType(ClassIDType.Texture2D, true, exportTexture2D);
                if (Settings.Default.exportMaterials)
                {
                    TypeFlags.SetType(ClassIDType.Material, true, exportMaterial);
                }
                if (ClassIDType.GameObject.CanExport())
                {
                    TypeFlags.SetType(ClassIDType.Animator, true, false);
                }
                else if (ClassIDType.Animator.CanExport())
                {
                    TypeFlags.SetType(ClassIDType.GameObject, true, false);
                }
            }

            return classTypeFilterList.ToArray();
        }
    }
}
