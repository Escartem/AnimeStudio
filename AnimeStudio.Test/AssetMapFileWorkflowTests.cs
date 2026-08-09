using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MemoryPack;
using MessagePack;

namespace AnimeStudio.Test;

public class AssetMapFileWorkflowTests
{
    private static readonly MessagePackSerializerOptions MessagePackOptions =
        MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

    [Fact]
    public async Task ExportAssetMapWritesMessagePackAndMemoryPackFiles()
    {
        var map = AssetMapTestData.Create();
        var directory = Path.Combine(Path.GetTempPath(), $"animestudio-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            await AssetsHelper.ExportAssetsMap(
                    map.AssetEntries,
                    new Game(GameType.GI, "test"),
                    "assets",
                    directory,
                    ExportListType.MessagePack | ExportListType.MemoryPack);

            Assert.True(File.Exists(Path.Combine(directory, "assets.map")));
            Assert.True(File.Exists(Path.Combine(directory, "assets.memory")));

            Assert.Equal(1, ResourceMap.FromFile(Path.Combine(directory, "assets.map")));
            Assert.Equal(GameType.GI, ResourceMap.GetGameType());
            Assert.Equal(1, ResourceMap.FromFile(Path.Combine(directory, "assets.memory")));
            Assert.Equal(GameType.GI, ResourceMap.GetGameType());
        }
        finally
        {
            ResourceMap.Clear();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ParseAssetMapFiltersMessagePackEntries()
    {
        var map = AssetMapTestData.Create();
        var path = Path.Combine(Path.GetTempPath(), $"animestudio-test-{Guid.NewGuid():N}.map");
        File.WriteAllBytes(path, MessagePackSerializer.Serialize(map, MessagePackOptions));

        try
        {
            var sources = AssetsHelper.ParseAssetMap(
                path,
                ExportListType.MessagePack,
                [ClassIDType.Texture2D],
                [new Regex("hero")],
                Array.Empty<Regex>());

            Assert.Equal(["E:/Game/shared.bundle"], sources);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseAssetMapReadsMemoryPackEntriesAndFiltersByContainer()
    {
        var map = AssetMapTestData.Create();
        var path = Path.Combine(Path.GetTempPath(), $"animestudio-test-{Guid.NewGuid():N}.memory");
        File.WriteAllBytes(path, MemoryPackSerializer.Serialize(new[] { map }.ToList()));

        try
        {
            var sources = AssetsHelper.ParseAssetMap(
                path,
                ExportListType.MemoryPack,
                Array.Empty<ClassIDType>(),
                Array.Empty<Regex>(),
                [new Regex("shared\\.bundle")]);

            Assert.Equal(["E:/Game/shared.bundle"], sources.Distinct());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
