using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MemoryPack;
using MessagePack;

namespace AnimeStudio
{
    public static class StringCache
    {
        private static readonly HashSet<string> _cache = new(StringComparer.Ordinal);

        public static string Get(string value)
        {
            if (value == null) return null;

            if (_cache.TryGetValue(value, out var cached))
                return cached;

            _cache.Add(value);
            return value;
        }
    }

    [MessagePackObject, MemoryPackable]
    public partial record AssetMap
    {
        [Key(0)]
        public GameType GameType { get; set; }

        [Key(1)]
        public List<AssetEntry> AssetEntries { get; set; }
    }

    [MessagePackObject, MemoryPackable]
    public partial record AssetEntry
    {
        private string _container;
        private string _hash;
        private string _name;
        private string _source;

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
            if(filters is null || filters.Count == 0)
                return true;

            foreach (KeyValuePair<string, Regex> kvp in filters)
            {
                var regex = kvp.Value;
                if (string.IsNullOrEmpty(regex.ToString()))
                    continue;

                if(!TryGetFilterValue(kvp.Key, out var value))
                    return false;

                if(!regex.IsMatch(value))
                    return false;
            }

            return true;
        }

        private bool TryGetFilterValue(string key, out string value)
        {
            if (string.Equals(key, nameof(Name), StringComparison.OrdinalIgnoreCase))
            {
                value = Name;
                return true;
            }
            if (string.Equals(key, nameof(Container), StringComparison.OrdinalIgnoreCase))
            {
                value = Container;
                return true;
            }
            if (string.Equals(key, nameof(Source), StringComparison.OrdinalIgnoreCase))
            {
                value = Source;
                return true;
            }
            if (string.Equals(key, nameof(PathID), StringComparison.OrdinalIgnoreCase))
            {
                value = PathID.ToString();
                return true;
            }
            if (string.Equals(key, nameof(Type), StringComparison.OrdinalIgnoreCase))
            {
                value = Type.ToString();
                return true;
            }
            if (string.Equals(key, nameof(Hash), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "SHA256Hash", StringComparison.OrdinalIgnoreCase))
            {
                value = Hash ?? string.Empty;
                return true;
            }

            value = null;
            return false;
        }
    }
}
