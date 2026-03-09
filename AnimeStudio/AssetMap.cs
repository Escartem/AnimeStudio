using MessagePack;
using SevenZip;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AnimeStudio
{
    public static class StringCache
    {
        private static readonly Dictionary<uint, string> _cache = new();
        public static string Get(string value)
        {
            if (value == null) return null;

            uint key = CRC.CalculateDigestUTF8(value);

            if (_cache.TryGetValue(key, out var cached))
                return cached;

            _cache[key] = value;
            return value;
        }
    }

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
        private string _name;
        private string _container;
        private string _source;
        private string _hash;

        [Key(0)]
        public string Name { 
            get => _name;
            set => _name = StringCache.Get(value); 
        }
        [Key(1)]
        public string Container {
            get => _container;
            set => _container = StringCache.Get(value);
        }
        [Key(2)]
        public string Source {
            get => _source;
            set => _source = StringCache.Get(value);
        }
        [Key(3)]
        public long PathID { get; set; }
        [Key(4)]
        public ClassIDType Type { get; set; }
        [Key(5)]
        public string Hash {
            get => _hash;
            set => _hash = StringCache.Get(value);
        }
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
                    string value when value.Equals(nameof(Hash), StringComparison.OrdinalIgnoreCase) || value.Equals("SHA256Hash", StringComparison.OrdinalIgnoreCase) => filter.Value.IsMatch(Hash ?? String.Empty)
                });
            }
            return matches.Count(x => x == true) == filters.Count;
        }
    }
}
