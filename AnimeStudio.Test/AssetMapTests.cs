using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AnimeStudio;

namespace AnimeStudio.Test;

public sealed class AssetMapTests : IDisposable
{
    public AssetMapTests()
    {
        StringCache.Clear();
    }

    public void Dispose()
    {
        StringCache.Clear();
    }

    [Fact]
    public void Container_and_source_reuse_cached_string_instances()
    {
        var first = CreateEntry();
        var second = CreateEntry();

        first.Container = new string("bundle/shared".ToCharArray());
        first.Source = new string("archive/shared".ToCharArray());
        second.Container = new string("bundle/shared".ToCharArray());
        second.Source = new string("archive/shared".ToCharArray());

        Assert.Same(first.Container, second.Container);
        Assert.Same(first.Source, second.Source);
        Assert.Equal(2, StringCache.Count);
    }

    [Fact]
    public void StringCache_ignores_null_and_clear_removes_cached_values()
    {
        var entry = new AssetEntry();
        entry.Container = null;
        entry.Source = "archive/shared";

        Assert.Equal(1, StringCache.Count);

        StringCache.Clear();

        Assert.Equal(0, StringCache.Count);
    }

    [Theory]
    [InlineData(nameof(AssetEntry.Name), "hero")]
    [InlineData(nameof(AssetEntry.Container), "bundle")]
    [InlineData(nameof(AssetEntry.Source), "archive")]
    [InlineData(nameof(AssetEntry.PathID), "9876543210")]
    [InlineData(nameof(AssetEntry.Type), "Texture2D")]
    [InlineData(nameof(AssetEntry.Hash), "abc123")]
    [InlineData("SHA256Hash", "abc123")]
    public void Matches_accepts_each_supported_filter_field(string field, string pattern)
    {
        var entry = CreateEntry();

        var result = entry.Matches(new Dictionary<string, Regex>
        {
            [field] = new Regex(pattern, RegexOptions.CultureInvariant),
        });

        Assert.True(result);
    }

    [Fact]
    public void Matches_returns_false_without_throwing_for_null_or_unknown_fields()
    {
        var entry = CreateEntry();
        entry.Name = null;
        entry.Hash = null;

        Assert.False(entry.Matches(new Dictionary<string, Regex>
        {
            [nameof(AssetEntry.Name)] = new Regex(".*"),
        }));
        Assert.True(entry.Matches(new Dictionary<string, Regex>
        {
            [nameof(AssetEntry.Hash)] = new Regex("^$"),
        }));
        Assert.False(entry.Matches(new Dictionary<string, Regex>
        {
            ["Unknown"] = new Regex(".*"),
        }));
    }

    [Fact]
    public void AssetEntryComparer_uses_name_container_and_path_id()
    {
        var comparer = new AssetEntryComparer();
        var first = CreateEntry();
        var sameIdentity = CreateEntry();
        sameIdentity.Source = "different-source";
        sameIdentity.Hash = "different-hash";
        var differentPath = CreateEntry();
        differentPath.PathID++;

        Assert.True(comparer.Equals(first, sameIdentity));
        Assert.Equal(comparer.GetHashCode(first), comparer.GetHashCode(sameIdentity));
        Assert.False(comparer.Equals(first, differentPath));
        Assert.False(comparer.Equals(first, null));
        Assert.Equal(0, comparer.GetHashCode(null));
    }

    private static AssetEntry CreateEntry() => new()
    {
        Name = "hero",
        Container = "bundle/shared",
        Source = "archive/shared",
        PathID = 9876543210,
        Type = ClassIDType.Texture2D,
        Hash = "abc123",
        Offset = -1,
    };
}
