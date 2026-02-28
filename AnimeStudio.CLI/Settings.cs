using System;
using System.ComponentModel;
using System.Configuration;

namespace AnimeStudio.CLI.Properties {
    public static class AppSettings
    {
        public static string Get(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }

        public static TValue Get<TValue>(string key, TValue defaultValue)
        {
            try
            {
                var value = Get(key);

                if (string.IsNullOrEmpty(value)) 
                    return defaultValue;

                return (TValue)TypeDescriptor.GetConverter(typeof(TValue)).ConvertFromInvariantString(value);
            }
            catch (Exception)
            {
                Console.WriteLine($"Invalid value at \"{key}\", switching to default value [{defaultValue}] !!");
                return defaultValue;
            }
            
        }
    }

    public class Settings
    {
        private static Settings defaultInstance = new Settings();

        public static Settings Default => defaultInstance;

        public bool convertTexture => AppSettings.Get("convertTexture", true);
        public bool convertAudio => AppSettings.Get("convertAudio", true);
        public ImageFormat convertType => AppSettings.Get("convertType", ImageFormat.Png);
        public bool eulerFilter => AppSettings.Get("eulerFilter", true);
        public decimal filterPrecision => AppSettings.Get("filterPrecision", (decimal)0.25);
        public bool exportAllNodes => AppSettings.Get("exportAllNodes", true);
        public bool exportSkins => AppSettings.Get("exportSkins", true);
        public bool exportMaterials => AppSettings.Get("exportMaterials", false);
        public bool collectAnimations => AppSettings.Get("collectAnimations", true);
        public bool exportAnimations => AppSettings.Get("exportAnimations", true);
        public decimal boneSize => AppSettings.Get("boneSize", (decimal)10);
        public int fbxVersion => AppSettings.Get("fbxVersion", 3);
        public int fbxFormat => AppSettings.Get("fbxFormat", 0);
        public decimal scaleFactor => AppSettings.Get("scaleFactor", (decimal)1);
        public bool exportBlendShape => AppSettings.Get("exportBlendShape", true);
        public bool castToBone => AppSettings.Get("castToBone", false);
        public bool restoreExtensionName => AppSettings.Get("restoreExtensionName", true);
        public bool enableFileLogging => AppSettings.Get("enableFileLogging", false);
        public bool minimalAssetMap => AppSettings.Get("minimalAssetMap", true);
        public bool allowDuplicates => AppSettings.Get("allowDuplicates", false);
        public bool scrapeMonos => AppSettings.Get("scrapeMonos", false);
        public string types => AppSettings.Get("types", string.Empty);
        public string texs => AppSettings.Get("texs", string.Empty);
        public string uvs => AppSettings.Get("uvs", string.Empty);

    }
}
