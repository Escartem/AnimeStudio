using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AnimeStudio.GUI.Core
{
    public static class SettingsStore
    {
        private static readonly JsonSerializerSettings jsonSettings = new()
        {
            Formatting = Formatting.Indented,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            Converters = { new StringEnumConverter() },
        };

        public static StudioSettings Load()
        {
            var path = AppPaths.SettingsFile;
            if (File.Exists(path))
            {
                try
                {
                    return JsonConvert.DeserializeObject<StudioSettings>(File.ReadAllText(path), jsonSettings) ?? new StudioSettings();
                }
                catch (Exception e)
                {
                    Logger.Warning($"Settings file is invalid, using defaults. A copy was kept as {path}.bak : {e.Message}");
                    File.Copy(path, path + ".bak", true);
                    return new StudioSettings();
                }
            }

            var imported = LegacySettingsImporter.TryImport();
            if (imported != null)
            {
                Save(imported);
                return imported;
            }
            return new StudioSettings();
        }

        public static void Save(StudioSettings settings)
        {
            try
            {
                var path = AppPaths.SettingsFile;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var temp = path + ".tmp";
                File.WriteAllText(temp, JsonConvert.SerializeObject(settings, jsonSettings));
                File.Move(temp, path, true);
            }
            catch (Exception e)
            {
                Logger.Warning($"Unable to save settings : {e.Message}");
            }
        }

        public static StudioSettings Clone(StudioSettings settings)
            => JsonConvert.DeserializeObject<StudioSettings>(JsonConvert.SerializeObject(settings, jsonSettings), jsonSettings);
    }
}
