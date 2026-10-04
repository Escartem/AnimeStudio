using System.Text.RegularExpressions;

namespace AnimeStudio
{
    public sealed class MapBuildRequest
    {
        public string[] Files { get; init; }
        public string BaseFolder { get; init; }
        public Game Game { get; init; }

        public string CabMapPath { get; init; }

        public bool BuildAssetMap { get; init; }
        public string AssetMapDirectory { get; init; }
        public string AssetMapName { get; init; }
        public ExportListType AssetMapTypes { get; init; }

        public ClassIDType[] TypeFilters { get; init; }
        public Regex[] NameFilters { get; init; }
        public Regex[] ContainerFilters { get; init; }
    }
}
