using System.IO;
using System.Buffers;
using System.Text;
using MemoryPack;
using MessagePack;
using Newtonsoft.Json;

namespace AnimeStudio.Test;

public class ResourceMapTests
{
    private static readonly MessagePackSerializerOptions MessagePackOptions =
        MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

    [Fact]
    public void FromFileLoadsMessagePackMap()
    {
        var map = AssetMapTestData.Create();
        var path = WriteTempFile(".map", MessagePackSerializer.Serialize(map, MessagePackOptions));

        try
        {
            Assert.Equal(1, ResourceMap.FromFile(path));
            AssetMapAssertions.Equal(map, new AssetMap
            {
                GameType = ResourceMap.GetGameType(),
                AssetEntries = ResourceMap.GetEntries(),
            });
        }
        finally
        {
            ResourceMap.Clear();
            File.Delete(path);
        }
    }

    [Fact]
    public void FromFileLoadsUncompressedStreamingMessagePackMap()
    {
        var map = AssetMapTestData.Create();
        var path = Path.Combine(Path.GetTempPath(), $"animestudio-test-{Guid.NewGuid():N}.map");

        try
        {
            using (var stream = File.Create(path))
            {
                var header = new ArrayBufferWriter<byte>();
                var writer = new MessagePackWriter(header);
                writer.WriteArrayHeader(2);
                writer.Write((int)map.GameType);
                writer.WriteArrayHeader(map.AssetEntries.Count);
                writer.Flush();
                stream.Write(header.WrittenSpan);

                foreach (var entry in map.AssetEntries)
                {
                    MessagePackSerializer.Serialize(stream, entry, MessagePackSerializerOptions.Standard);
                }
            }

            Assert.Equal(1, ResourceMap.FromFile(path));
            Assert.Equal(map.GameType, ResourceMap.GetGameType());
            Assert.Equal(map.AssetEntries.Count, ResourceMap.GetEntries().Count);
        }
        finally
        {
            ResourceMap.Clear();
            File.Delete(path);
        }
    }

    [Fact]
    public void FromFileLoadsMemoryPackMap()
    {
        var map = AssetMapTestData.Create();
        var secondMap = AssetMapTestData.Create();
        secondMap.GameType = GameType.Normal;
        var path = WriteTempFile(".memory", MemoryPackSerializer.Serialize(new[] { map, secondMap }.ToList()));

        try
        {
            Assert.Equal(1, ResourceMap.FromFile(path));
            Assert.Equal(GameType.GI, ResourceMap.GetGameType());
            Assert.Equal(map.AssetEntries.Count, ResourceMap.GetEntries().Count);
            Assert.Equal(map.AssetEntries[0].Source, ResourceMap.GetEntries()[0].Source);
        }
        finally
        {
            ResourceMap.Clear();
            File.Delete(path);
        }
    }

    [Fact]
    public void FromFileLoadsJsonMap()
    {
        var map = AssetMapTestData.Create();
        var path = WriteTempFile(".json", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(map)));

        try
        {
            Assert.Equal(1, ResourceMap.FromFile(path));
            Assert.Equal(map.GameType, ResourceMap.GetGameType());
            Assert.Equal(map.AssetEntries.Count, ResourceMap.GetEntries().Count);
        }
        finally
        {
            ResourceMap.Clear();
            File.Delete(path);
        }
    }

    [Fact]
    public void FromFileRejectsUnknownExtensionAndCorruptData()
    {
        var unknownPath = WriteTempFile(".unknown", [1, 2, 3]);
        var corruptPath = WriteTempFile(".memory", [1, 2, 3]);

        try
        {
            ResourceMap.Clear();
            Assert.Equal(-1, ResourceMap.FromFile(unknownPath));
            Assert.Equal(-1, ResourceMap.FromFile(corruptPath));
            Assert.Empty(ResourceMap.GetEntries());
        }
        finally
        {
            ResourceMap.Clear();
            File.Delete(unknownPath);
            File.Delete(corruptPath);
        }
    }

    [Fact]
    public void EmptyMemoryPackListIsRejected()
    {
        var path = WriteTempFile(".memory", MemoryPackSerializer.Serialize(new System.Collections.Generic.List<AssetMap>()));

        try
        {
            ResourceMap.Clear();
            Assert.Equal(-1, ResourceMap.FromFile(path));
            Assert.Empty(ResourceMap.GetEntries());
        }
        finally
        {
            ResourceMap.Clear();
            File.Delete(path);
        }
    }

    [Fact]
    public void CorruptFileDoesNotReplacePreviouslyLoadedMap()
    {
        var map = AssetMapTestData.Create();
        var validPath = WriteTempFile(".map", MessagePackSerializer.Serialize(map, MessagePackOptions));
        var corruptPath = WriteTempFile(".map", [1, 2, 3]);

        try
        {
            Assert.Equal(1, ResourceMap.FromFile(validPath));
            Assert.Equal(-1, ResourceMap.FromFile(corruptPath));
            Assert.Equal(map.GameType, ResourceMap.GetGameType());
            Assert.Equal(map.AssetEntries.Count, ResourceMap.GetEntries().Count);
        }
        finally
        {
            ResourceMap.Clear();
            File.Delete(validPath);
            File.Delete(corruptPath);
        }
    }

    private static string WriteTempFile(string extension, byte[] data)
    {
        var path = Path.Combine(Path.GetTempPath(), $"animestudio-test-{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(path, data);
        return path;
    }
}
