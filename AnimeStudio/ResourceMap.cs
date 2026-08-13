using MessagePack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

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
                    var extension = Path.GetExtension(path).ToLower();
                    using var stream = File.OpenRead(path);
                    
                    if (extension == ".map")
                    {
                        // Deserialize map
                        Instance = MessagePackSerializer.Deserialize<AssetMap>(stream, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
                    }
                    else if (extension == ".json")
                    {
                        // Deserialize json
                        using var reader = new StreamReader(stream);
                        var jsonContent = reader.ReadToEnd();
                        var parsed = JsonConvert.DeserializeObject<AssetMap>(jsonContent);

                        Instance = new AssetMap
                        {
                            GameType = parsed.GameType,
                            AssetEntries = parsed.AssetEntries
                        };
                    }
                    else
                    {
                        Logger.Error("AssetMap was not loaded");
                        return -1;
                    }
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
        }
    }
}
