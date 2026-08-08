using System.Linq;
using MemoryPack;
using MessagePack;

namespace AnimeStudio.Test;

public class AssetMapSerializationTests
{
    private static readonly MessagePackSerializerOptions MessagePackOptions =
        MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

    [Fact]
    public void MessagePackRoundTripPreservesAssetMapFields()
    {
        var expected = AssetMapTestData.Create();

        var bytes = MessagePackSerializer.Serialize(expected, MessagePackOptions);
        var actual = MessagePackSerializer.Deserialize<AssetMap>(bytes, MessagePackOptions);

        AssetMapAssertions.Equal(expected, actual);
    }

    [Fact]
    public void MemoryPackRoundTripPreservesListEnvelopeAndAssetMapFields()
    {
        var expected = AssetMapTestData.Create();

        var bytes = MemoryPackSerializer.Serialize(new[] { expected }.ToList());
        var actual = MemoryPackSerializer.Deserialize<System.Collections.Generic.List<AssetMap>>(bytes)!
            .Single();

        AssetMapAssertions.Equal(expected, actual);
    }

    [Fact]
    public void StringCacheClearRemovesInternedValues()
    {
        StringCache.Clear();
        var map = AssetMapTestData.Create();

        _ = map.AssetEntries[0].Container;
        _ = map.AssetEntries[1].Container;
        Assert.True(StringCache.Count > 0);

        StringCache.Clear();

        Assert.Equal(0, StringCache.Count);
    }

    [Fact]
    public void StringCacheReusesRepeatedStringsAndIgnoresNull()
    {
        StringCache.Clear();
        var first = new string("shared.bundle".ToCharArray());
        var second = new string("shared.bundle".ToCharArray());

        var cachedFirst = StringCache.Get(first);
        var cachedSecond = StringCache.Get(second);

        Assert.Same(cachedFirst, cachedSecond);
        var count = StringCache.Count;
        Assert.Null(StringCache.Get(null));
        Assert.Equal(count, StringCache.Count);
    }
}

internal static class AssetMapAssertions
{
    public static void Equal(AssetMap expected, AssetMap actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.GameType, actual.GameType);
        Assert.NotNull(actual.AssetEntries);
        Assert.Equal(expected.AssetEntries.Count, actual.AssetEntries.Count);

        for (var i = 0; i < expected.AssetEntries.Count; i++)
        {
            var left = expected.AssetEntries[i];
            var right = actual.AssetEntries[i];
            Assert.Equal(left.Name, right.Name);
            Assert.Equal(left.Container, right.Container);
            Assert.Equal(left.Source, right.Source);
            Assert.Equal(left.PathID, right.PathID);
            Assert.Equal(left.Type, right.Type);
            Assert.Equal(left.Hash, right.Hash);
            Assert.Equal(left.Offset, right.Offset);
        }
    }
}
