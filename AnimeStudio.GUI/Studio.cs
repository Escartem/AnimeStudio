using System;
using System.Collections.Generic;

namespace AnimeStudio.GUI
{
    internal enum GuiColorTheme
    {
        System = 0,
        Dark = 1,
        Light = 2
    }

    internal enum ExportFilter
    {
        All,
        Selected,
        Filtered
    }

    internal static partial class Studio
    {
        public static Game Game;
        public static bool SkipContainer = false;
        public static AssetsManager assetsManager = new AssetsManager();
        public static AssemblyLoader assemblyLoader = new AssemblyLoader();
        public static List<AssetItem> exportableAssets = new List<AssetItem>();
        public static List<AssetItem> visibleAssets = new List<AssetItem>();
        internal static Action<string> StatusStripUpdate = x => { };
        public static Dictionary<ulong, string> Paths { get; set; } = new Dictionary<ulong, string>();
    }
}

