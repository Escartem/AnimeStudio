using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AnimeStudio
{
    [MessagePackObject]
    public record AssetMap
    {
        [Key(0)]
        public GameType GameType { get; set; }
        [Key(1)]
        public List<AssetEntry> AssetEntries { get; set; }
    }
    [MessagePackObject]
    public record AssetEntry
    {
        [Key(0)]
        public string Name { get; set; }
        [Key(1)]
        public string Container { get; set; }
        [Key(2)]
        public string Source { get; set; }
        [Key(3)]
        public long PathID { get; set; }
        [Key(4)]
        public ClassIDType Type { get; set; }
        [Key(5)]
        public string SHA256Hash { get; set; }
        [Key(6)]
        public long Offset { get; set; } = -1;

        public bool Matches(Dictionary<string, Regex> filters)
        {
            var matches = new List<bool>();
            foreach(var filter in filters)
            {
                matches.Add(filter.Key switch
                {
                    string value when value.Equals(nameof(Name), StringComparison.OrdinalIgnoreCase) => filter.Value.IsMatch(Name),
                    string value when value.Equals(nameof(Container), StringComparison.OrdinalIgnoreCase) => filter.Value.IsMatch(Container),
                    string value when value.Equals(nameof(Source), StringComparison.OrdinalIgnoreCase) => filter.Value.IsMatch(Source),
                    string value when value.Equals(nameof(PathID), StringComparison.OrdinalIgnoreCase) => filter.Value.IsMatch(PathID.ToString()),
                    string value when value.Equals(nameof (Type), StringComparison.OrdinalIgnoreCase) => filter.Value.IsMatch(Type.ToString()),
                    string value when value.Equals(nameof(SHA256Hash), StringComparison.OrdinalIgnoreCase) => filter.Value.IsMatch(SHA256Hash),
                    _ => throw new NotImplementedException()
                });
            }
            return matches.Count(x => x == true) == filters.Count;
        }
    }
}
