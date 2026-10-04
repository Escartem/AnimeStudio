using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace AnimeStudio.GUI.Core
{
    // Reads the user.config written by the WinForms ApplicationSettings of older versions.
    public static class LegacySettingsImporter
    {
        public static StudioSettings TryImport()
        {
            try
            {
                var file = FindUserConfig();
                if (file == null)
                    return null;

                var values = XDocument.Load(file)
                    .Descendants("setting")
                    .ToDictionary(x => (string)x.Attribute("name"), x => x.Element("value")?.Value ?? string.Empty);
                var settings = new StudioSettings();
                Apply(settings, values);
                Logger.Info($"Imported settings from {file}");
                return settings;
            }
            catch (Exception e)
            {
                Logger.Warning($"Unable to import previous settings : {e.Message}");
                return null;
            }
        }

        private static string FindUserConfig()
        {
            if (!OperatingSystem.IsWindows())
                return null;
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!Directory.Exists(root))
                return null;

            return Directory.EnumerateDirectories(root, "AnimeStudio*")
                .SelectMany(company => Directory.EnumerateDirectories(company, "AnimeStudio.GUI*"))
                .SelectMany(app => Directory.EnumerateFiles(app, "user.config", SearchOption.AllDirectories))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        private static void Apply(StudioSettings s, Dictionary<string, string> v)
        {
            bool B(string key, bool fallback) => v.TryGetValue(key, out var x) && bool.TryParse(x, out var r) ? r : fallback;
            int I(string key, int fallback) => v.TryGetValue(key, out var x) && int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) ? r : fallback;
            decimal D(string key, decimal fallback) => v.TryGetValue(key, out var x) && decimal.TryParse(x, NumberStyles.Number, CultureInfo.InvariantCulture, out var r) ? r : fallback;
            string S(string key, string fallback) => v.TryGetValue(key, out var x) ? x : fallback;
            T J<T>(string key, T fallback) where T : class
            {
                try { return v.TryGetValue(key, out var x) && !string.IsNullOrEmpty(x) ? JsonConvert.DeserializeObject<T>(x) ?? fallback : fallback; }
                catch { return fallback; }
            }

            s.DisplayAll = B("displayAll", s.DisplayAll);
            s.EnablePreview = B("enablePreview", s.EnablePreview);
            s.DisplayInfo = B("displayInfo", s.DisplayInfo);
            s.EnableModelPreview = B("enableModelPreview", s.EnableModelPreview);
            s.ModelsOnly = B("modelsOnly", s.ModelsOnly);
            s.Theme = (GuiColorTheme)I("guiTheme", (int)s.Theme);
            s.EnableConsole = B("enableConsole", s.EnableConsole);
            s.EnableFileLogging = B("enableFileLogging", s.EnableFileLogging);
            s.LoggerEvents = (LoggerEvent)I("loggerEventType", (int)s.LoggerEvents);
            s.SelectedGame = I("selectedGame", s.SelectedGame);
            s.LastUnityCNKey = S("lastUnityCNKey", s.LastUnityCNKey);
            s.ResolveDependencies = B("enableResolveDependencies", s.ResolveDependencies);
            s.SkipContainer = B("skipContainer", s.SkipContainer);
            s.UseBundleContainerName = B("useBundleContainerName", s.UseBundleContainerName);
            s.Types = J("types", s.Types);
            s.OpenAfterExport = B("openAfterExport", s.OpenAfterExport);
            s.AssetGroupOption = (AssetGroupOption)I("assetGroupOption", (int)s.AssetGroupOption);
            s.RestoreExtensionName = B("restoreExtensionName", s.RestoreExtensionName);
            s.AllowDuplicates = B("allowDuplicates", s.AllowDuplicates);
            s.ConvertTexture = B("convertTexture", s.ConvertTexture);
            s.EnableHDR = B("enableHDR", s.EnableHDR);
            s.ConvertType = Enum.TryParse<ImageFormat>(S("convertType", ""), out var format) ? format : s.ConvertType;
            s.ConvertAudio = B("convertAudio", s.ConvertAudio);
            s.Encrypted = B("encrypted", s.Encrypted);
            s.Key = (byte)I("key", s.Key);
            s.EulerFilter = B("eulerFilter", s.EulerFilter);
            s.FilterPrecision = D("filterPrecision", s.FilterPrecision);
            s.ExportAllNodes = B("exportAllNodes", s.ExportAllNodes);
            s.ExportSkins = B("exportSkins", s.ExportSkins);
            s.ExportAnimations = B("exportAnimations", s.ExportAnimations);
            s.ExportBlendShape = B("exportBlendShape", s.ExportBlendShape);
            s.CastToBone = B("castToBone", s.CastToBone);
            s.BoneSize = D("boneSize", s.BoneSize);
            s.ScaleFactor = D("scaleFactor", s.ScaleFactor);
            s.FbxVersion = I("fbxVersion", s.FbxVersion);
            s.FbxFormat = I("fbxFormat", s.FbxFormat);
            s.CollectAnimations = B("collectAnimations", s.CollectAnimations);
            s.ExportMaterials = B("exportMaterials", s.ExportMaterials);
            s.Uvs = J("uvs", s.Uvs);
            s.Texs = J("texs", s.Texs);
            s.AssetMapType = (ExportListType)I("assetMapType", (int)s.AssetMapType);
            s.MinimalAssetMap = B("minimalAssetMap", s.MinimalAssetMap);
            s.SelectedCABMap = S("selectedCABMapName", s.SelectedCABMap);
        }
    }
}
