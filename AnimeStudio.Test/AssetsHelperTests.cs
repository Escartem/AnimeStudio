using System;
using System.IO;
using AnimeStudio;

namespace AnimeStudio.Test;

public sealed class AssetsHelperTests : IDisposable
{
    private readonly TestTempDirectory tempDirectory = new();

    public AssetsHelperTests()
    {
        AssetsHelper.Clear();
    }

    public void Dispose()
    {
        AssetsHelper.Clear();
        tempDirectory.Dispose();
    }

    [Fact]
    public void ProcessFiles_removes_case_insensitive_duplicate_paths()
    {
        var mapPath = tempDirectory.GetPath("cabmap.bin");
        using (var stream = File.Create(mapPath))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(tempDirectory.Path);
            writer.Write(0);
        }
        Assert.True(AssetsHelper.LoadCABMap(mapPath));

        var path = tempDirectory.GetPath("duplicate.assets");

        var result = AssetsHelper.ProcessFiles(new[] { path, path.ToUpperInvariant() });

        Assert.Single(result);
        Assert.Equal(path, result[0], StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessDependencies_returns_original_files_when_no_cab_map_is_loaded()
    {
        var files = new[] { "first.assets", "second.assets" };

        var result = AssetsHelper.ProcessDependencies(files);

        Assert.Same(files, result);
    }

    [Fact]
    public void ProcessDependencies_adds_cab_dependencies_with_their_cached_offsets()
    {
        var mapPath = tempDirectory.GetPath("dependencies.bin");
        WriteCABMap(
            mapPath,
            ("cab-main", "main.assets", 0L, new[] { "cab-dependency" }),
            ("cab-dependency", "dependency.assets", 96L, Array.Empty<string>()));
        Assert.True(AssetsHelper.LoadCABMap(mapPath));

        var mainPath = tempDirectory.GetPath("main.assets");
        var dependencyPath = tempDirectory.GetPath("dependency.assets");
        var result = AssetsHelper.ProcessDependencies(new[] { mainPath });

        Assert.Contains(mainPath, result);
        Assert.Contains(dependencyPath, result);
        Assert.True(AssetsHelper.TryGet(dependencyPath, out var offsets));
        Assert.Equal(new long[] { 96 }, offsets);
    }

    [Fact]
    public void TryGet_returns_an_empty_result_for_uncached_offsets()
    {
        var found = AssetsHelper.TryGet("missing.assets", out var offsets);

        Assert.False(found);
        Assert.Empty(offsets);
    }

    private void WriteCABMap(string path, params (string Cab, string RelativePath, long Offset, string[] Dependencies)[] entries)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write(tempDirectory.Path);
        writer.Write(entries.Length);
        foreach (var entry in entries)
        {
            writer.Write(entry.Cab);
            writer.Write(entry.RelativePath);
            writer.Write(entry.Offset);
            writer.Write(entry.Dependencies.Length);
            foreach (var dependency in entry.Dependencies)
            {
                writer.Write(dependency);
            }
        }
    }
}
