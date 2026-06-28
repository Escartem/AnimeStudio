using MessagePack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MemoryPack;
using MemoryPack.Streaming;

namespace AnimeStudio
{
    public static class ResourceMap
    {
        private static AssetMap Instance = new() { GameType = GameType.Normal, AssetEntries = new List<AssetEntry>() };
        public static List<AssetEntry> GetEntries() => Instance.AssetEntries;
        public static GameType GetGameType() => Instance.GameType;

        public static List<String> GetTypes()
        {
            var types = new List<String>();
            foreach (var entry in Instance.AssetEntries)
            {
                if (!types.Contains(entry.Type.ToString()))
                {
                    types.Add(entry.Type.ToString());
                }
            }
            return types;
        }
        public static int FromFile(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                Logger.Info(string.Format("Parsing...."));
                try
                {
                    string             extension = Path.GetExtension(path).ToLower();
                    using FileStream   stream    = File.OpenRead(path);
                    AssetMap           newMap    = null;

                    switch(extension)
                    {
                        case ".map":
                        {
                            // Deserialize map
                            newMap = MessagePackSerializer.Deserialize<AssetMap>
                                    (stream,
                                     MessagePackSerializerOptions.Standard.WithCompression
                                             (MessagePackCompression.Lz4BlockArray));
                            break;
                        }

                        case ".json":
                        {
                            // Deserialize json
                            using var reader      = new StreamReader(stream);
                            string    jsonContent = reader.ReadToEnd();
                            AssetMap  parsed      = JsonConvert.DeserializeObject<AssetMap>(jsonContent);

                            newMap = new AssetMap
                            {
                                    GameType     = parsed.GameType,
                                    AssetEntries = parsed.AssetEntries
                            };
                            break;
                        }
                        case ".memory":
                        {
                            ReadOnlySpan<byte> bytes = File.ReadAllBytes(path);
                            List<AssetMap> assetMaps = MemoryPackSerializer.Deserialize<List<AssetMap>>
                                    (bytes);

                            AssetMap assetMap = assetMaps.FirstOrDefault();
                            newMap = assetMap;
                            break;
                        }
                    }

                    if (newMap == null)
                    {
                        Logger.Error("AssetMap was not loaded");
                        return -1;
                    }

                    StringCache.Clear();
                    Instance = newMap;
                }
                catch (Exception e)
                {
                    Logger.Error("AssetMap was not loaded");
                    Console.WriteLine(e.ToString());
                    return -1;
                }
                Logger.Info("Loaded !!");
                return 1;
            } else
            {
                Logger.Error("AssetMap was not loaded");
                return -1;
            }
        }

        public static void Clear()
        {
            Instance.GameType = GameType.Normal;
            Instance.AssetEntries = new List<AssetEntry>();
            StringCache.Clear();
        }
    }
}
