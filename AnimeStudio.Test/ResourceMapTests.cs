using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnimeStudio;
using MessagePack;
using Newtonsoft.Json;

namespace AnimeStudio.Test;

public sealed class ResourceMapTests : IDisposable
{
    private readonly TestTempDirectory tempDirectory = new();

    public ResourceMapTests()
    {
        ResourceMap.Clear();
        StringCache.Clear();
    }

    public void Dispose()
    {
        ResourceMap.Clear();
        StringCache.Clear();
        tempDirectory.Dispose();
    }

    [Fact]
    public void FromFile_loads_a_messagepack_map()
    {
        var expected = CreateMap();
        var path = tempDirectory.GetPath("map.map");
        File.WriteAllBytes(path, MessagePackSerializer.Serialize(
            expected,
            MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray)));

        var result = ResourceMap.FromFile(path);

        Assert.Equal(1, result);
        Assert.Equal(expected.GameType, ResourceMap.GetGameType());
        AssertEntry(expected.AssetEntries.Single(), ResourceMap.GetEntries().Single());
    }

    [Fact]
    public void FromFile_loads_a_json_map()
    {
        var expected = CreateMap();
        var path = tempDirectory.GetPath("map.json");
        File.WriteAllText(path, JsonConvert.SerializeObject(expected));

        var result = ResourceMap.FromFile(path);

        Assert.Equal(1, result);
        Assert.Equal(expected.GameType, ResourceMap.GetGameType());
        AssertEntry(expected.AssetEntries.Single(), ResourceMap.GetEntries().Single());
    }

    [Fact]
    public void FromFile_rejects_unknown_extensions_without_replacing_a_loaded_map()
    {
        LoadKnownMap();
        var path = tempDirectory.GetPath("map.unknown");
        File.WriteAllText(path, "not a supported AssetMap format");

        var result = ResourceMap.FromFile(path);

        Assert.Equal(-1, result);
        Assert.Equal(GameType.GI, ResourceMap.GetGameType());
        Assert.Single(ResourceMap.GetEntries());
    }

    [Fact]
    public void FromFile_rejects_corrupt_input_without_replacing_a_loaded_map()
    {
        LoadKnownMap();
        var path = tempDirectory.GetPath("broken.map");
        File.WriteAllBytes(path, new byte[] { 0x01, 0x02, 0x03 });

        var result = ResourceMap.FromFile(path);

        Assert.Equal(-1, result);
        Assert.Equal(GameType.GI, ResourceMap.GetGameType());
        Assert.Single(ResourceMap.GetEntries());
    }

    [Fact]
    public void FromFile_rejects_an_empty_path()
    {
        Assert.Equal(-1, ResourceMap.FromFile(string.Empty));
    }

    [Fact]
    public void Clear_resets_the_current_map()
    {
        LoadKnownMap();

        ResourceMap.Clear();

        Assert.Equal(GameType.Normal, ResourceMap.GetGameType());
        Assert.Empty(ResourceMap.GetEntries());
    }

    private void LoadKnownMap()
    {
        var path = tempDirectory.GetPath("known.map");
        File.WriteAllBytes(path, MessagePackSerializer.Serialize(
            CreateMap(),
            MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray)));
        Assert.Equal(1, ResourceMap.FromFile(path));
    }

    private static AssetMap CreateMap() => new()
    {
        GameType = GameType.GI,
        AssetEntries = new List<AssetEntry>
        {
            new()
            {
                Name = "hero",
                Container = "bundle/shared",
                Source = "archive/shared",
                PathID = 42,
                Type = ClassIDType.Texture2D,
                Hash = "abc123",
                Offset = -1,
            },
        },
    };

    private static void AssertEntry(AssetEntry expected, AssetEntry actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Container, actual.Container);
        Assert.Equal(expected.Source, actual.Source);
        Assert.Equal(expected.PathID, actual.PathID);
        Assert.Equal(expected.Type, actual.Type);
        Assert.Equal(expected.Hash, actual.Hash);
        Assert.Equal(expected.Offset, actual.Offset);
    }
}
