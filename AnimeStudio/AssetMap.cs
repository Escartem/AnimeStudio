using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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

        /// <summary>
        /// Drop interned strings. Call between map-build files so unique asset names
        /// from already-flushed entries do not accumulate across an entire game dump.
        /// </summary>
        public static void Clear()
        {
            _cache.Clear();
        }

        public static int Count => _cache.Count;
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
    public partial record AssetEntry
    {
        private string _container;
        private string _hash;
        private string _name;
        private string _source;

        // Names are usually unique per asset — interning them only grows the cache.
        [Key(0)]
        public string Name {
            get => _name;
            set => _name = value;
        }

        // Containers and sources repeat heavily across entries; keep interning those.
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

        // Hash is effectively unique per asset — interning it only grows the cache without reuse.
        [Key(5)]
        public string Hash {
            get => _hash;
            set => _hash = value;
        }

        [Key(6)]
        public long Offset { get; set; } = -1;

        private string GetPropertyValue(string propertyName)
        {
            return propertyName switch
            {
                    nameof(Name)      => Name,
                    nameof(Container) => Container,
                    nameof(Source)    => Source,
                    nameof(PathID)    => PathID.ToString(),
                    nameof(Type)      => Type.ToString(),
                    nameof(Hash)      => Hash ?? string.Empty,
                    "SHA256Hash"      => Hash ?? string.Empty,
                    _                 => null
            };
        }

        public bool Matches(Dictionary<string, Regex> filters)
        {
            if(filters is null || filters.Count == 0)
                return true;

            foreach ((string key, Regex regex) in filters)
            {
                string value = this.GetPropertyValue(key);
                if(value is null || !regex.IsMatch(value))
                    return false;
            }
            return true;
        }
    }
}
