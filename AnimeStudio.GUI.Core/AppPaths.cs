using System;
using System.IO;

namespace AnimeStudio.GUI.Core
{
    public static class AppPaths
    {
        public const string SettingsFileName = "AnimeStudio.settings.json";

        public static string BaseDirectory => AppContext.BaseDirectory;

        // A settings file next to the executable switches to portable mode.
        public static bool IsPortable => File.Exists(Path.Combine(BaseDirectory, SettingsFileName));

        public static string ConfigDirectory => IsPortable
            ? BaseDirectory
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create), "AnimeStudio");

        public static string SettingsFile => Path.Combine(ConfigDirectory, SettingsFileName);
    }
}
