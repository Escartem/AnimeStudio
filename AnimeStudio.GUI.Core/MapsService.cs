using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AnimeStudio.GUI.Core
{
    // CAB maps live in the Maps folder by default but can sit anywhere; known ones are listed by name or path.
    public sealed class MapsService
    {
        private const int MaxRecent = 10;
        private readonly StudioSession session;

        public MapsService(StudioSession session) => this.session = session;

        public static string MapsDirectory => AssetsHelper.MapsDirectory;

        public string SelectedCABMap => session.Settings.SelectedCABMap;

        public IReadOnlyList<string> GetKnownCABMaps()
        {
            var settings = session.Settings;
            settings.RecentCABMaps.RemoveAll(path => !File.Exists(path));
            return AssetsHelper.GetMaps().Concat(settings.RecentCABMaps).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static string DisplayName(string nameOrPath) => Path.IsPathRooted(nameOrPath) ? $"{Path.GetFileNameWithoutExtension(nameOrPath)} ({Path.GetDirectoryName(nameOrPath)})" : nameOrPath;

        public static string ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Map name is empty";
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
                return "Name has invalid characters !!";
            return null;
        }

        public void RestoreSelectedCABMap()
        {
            var selected = session.Settings.SelectedCABMap;
            if (!string.IsNullOrEmpty(selected) && !AssetsHelper.LoadCABMap(selected))
            {
                session.Settings.SelectedCABMap = string.Empty;
                session.SaveSettings();
            }
        }

        public async Task<bool> LoadCABMapAsync(string nameOrPath)
        {
            var loaded = await Task.Run(() => AssetsHelper.LoadCABMap(nameOrPath));
            if (loaded)
            {
                Select(nameOrPath);
            }
            return loaded;
        }

        public void Select(string nameOrPath)
        {
            var settings = session.Settings;
            var isInMapsFolder = string.Equals(Path.GetDirectoryName(AssetsHelper.ResolveCABMapPath(nameOrPath)), Path.GetFullPath(MapsDirectory), StringComparison.OrdinalIgnoreCase);
            settings.SelectedCABMap = isInMapsFolder ? Path.GetFileNameWithoutExtension(nameOrPath) : Path.GetFullPath(nameOrPath);
            if (!isInMapsFolder)
            {
                settings.RecentCABMaps.RemoveAll(x => string.Equals(x, settings.SelectedCABMap, StringComparison.OrdinalIgnoreCase));
                settings.RecentCABMaps.Insert(0, settings.SelectedCABMap);
                if (settings.RecentCABMaps.Count > MaxRecent)
                    settings.RecentCABMaps.RemoveRange(MaxRecent, settings.RecentCABMaps.Count - MaxRecent);
            }
            session.SaveSettings();
        }

        public bool DeleteCABMap(string nameOrPath)
        {
            var path = AssetsHelper.ResolveCABMapPath(nameOrPath);
            if (!File.Exists(path))
                return false;
            File.Delete(path);
            var settings = session.Settings;
            settings.RecentCABMaps.RemoveAll(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase));
            if (string.Equals(AssetsHelper.ResolveCABMapPath(settings.SelectedCABMap), path, StringComparison.OrdinalIgnoreCase))
            {
                settings.SelectedCABMap = string.Empty;
                AssetsHelper.Clear();
            }
            session.SaveSettings();
            Logger.Info($"{Path.GetFileNameWithoutExtension(path)} deleted successfully !!");
            return true;
        }

        // Builds the CABMap and/or the AssetMap of a game folder. outputFile is "<folder>/<name>" without extension,
        // every requested map is written there: <name>.bin for the CABMap, <name>.map/.xml/.json for the AssetMap.
        public async Task BuildAsync(string gameFolder, string outputFile, bool cabMap, ExportListType assetMapTypes, bool assetMap)
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(outputFile));
            var name = Path.GetFileNameWithoutExtension(outputFile);
            Logger.Info("Scanning for files...");
            var files = await Task.Run(() => Directory.GetFiles(gameFolder, "*.*", SearchOption.AllDirectories));
            Logger.Info($"Found {files.Length} files");
            if (files.Length == 0)
                return;

            AssetsHelper.SetUnityVersion(session.UnityVersion);
            var cabPath = cabMap ? Path.Combine(folder, $"{name}.bin") : null;
            await Task.Run(() => AssetsHelper.BuildMaps(new MapBuildRequest
            {
                Files = files,
                BaseFolder = gameFolder,
                Game = session.Game,
                CabMapPath = cabPath,
                BuildAssetMap = assetMap,
                AssetMapDirectory = folder,
                AssetMapName = name,
                AssetMapTypes = assetMapTypes,
            }));

            if (cabPath != null && File.Exists(cabPath))
                Select(cabPath);
        }
    }
}
