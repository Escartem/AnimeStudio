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
        public static void LoadDirect(AssetMap map)
        {
            Instance = map ?? new AssetMap { GameType = GameType.Normal, AssetEntries = new List<AssetEntry>() };
        }

        public static int FromFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                Logger.Error("AssetMap was not loaded");
                return -1;
            }
            Logger.Info("Parsing....");
            try
            {
                var ext = Path.GetExtension(path).ToLower();
                var bytes = File.ReadAllBytes(path);

                if (ext == ".map")
                {
                    try
                    {
                        var bundle = MessagePackSerializer.Deserialize<MapBundle>(bytes, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
                        if (bundle.HasAssetMap && bundle.AssetData != null)
                        {
                            Instance = bundle.AssetData;
                            Logger.Info("Loaded !!");
                            return 1;
                        }
                    }
                    catch { }
                    Instance = MessagePackSerializer.Deserialize<AssetMap>(bytes, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
                }
                else if (ext == ".json")
                {
                    var jsonContent = System.Text.Encoding.UTF8.GetString(bytes);
                    if (jsonContent.Contains("\"HasAssetMap\""))
                    {
                        var bundle = JsonConvert.DeserializeObject<MapBundle>(jsonContent);
                        if (bundle?.HasAssetMap == true && bundle.AssetData != null)
                        {
                            Instance = bundle.AssetData;
                            Logger.Info("Loaded !!");
                            return 1;
                        }
                    }
                    var parsed = JsonConvert.DeserializeObject<AssetMap>(jsonContent);
                    Instance = new AssetMap { GameType = parsed.GameType, AssetEntries = parsed.AssetEntries };
                }
                else
                {
                    Logger.Error($"Unsupported format: {ext}");
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
        }

        public static void Clear()
        {
            Instance.GameType = GameType.Normal;
            Instance.AssetEntries = new List<AssetEntry>();
        }
    }
}
