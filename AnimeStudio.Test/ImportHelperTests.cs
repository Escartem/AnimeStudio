using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using AnimeStudio;

namespace AnimeStudio.Test;

public sealed class ImportHelperTests : IDisposable
{
    private readonly TestTempDirectory tempDirectory = new();

    public void Dispose()
    {
        tempDirectory.Dispose();
    }

    [Fact]
    public void DecompressGZip_returns_the_original_bytes()
    {
        var expected = Encoding.UTF8.GetBytes("gzip payload");
        using var result = ImportHelper.DecompressGZip(CreateReader("payload.gz", Compress(expected, stream => new GZipStream(stream, CompressionLevel.SmallestSize, leaveOpen: true))));

        Assert.Equal(expected, result.ReadBytes((int)result.Length));
    }

    [Fact]
    public void DecompressBrotli_returns_the_original_bytes()
    {
        var expected = Encoding.UTF8.GetBytes("brotli payload");
        using var result = ImportHelper.DecompressBrotli(CreateReader("payload.br", Compress(expected, stream => new BrotliStream(stream, CompressionLevel.SmallestSize, leaveOpen: true))));

        Assert.Equal(expected, result.ReadBytes((int)result.Length));
    }

    [Fact]
    public void Split_asset_workflow_merges_parts_and_replaces_split_paths()
    {
        var destination = tempDirectory.GetPath("sharedassets0.assets");
        var split0 = destination + ".split0";
        var split1 = destination + ".split1";
        File.WriteAllBytes(split0, new byte[] { 1, 2 });
        File.WriteAllBytes(split1, new byte[] { 3, 4 });

        ImportHelper.MergeSplitAssets(tempDirectory.Path);
        var paths = ImportHelper.ProcessingSplitFiles(new List<string> { split0, split1, "other.assets" });

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(destination));
        Assert.Contains(destination, paths);
        Assert.Contains("other.assets", paths);
        Assert.DoesNotContain(paths, path => path.Contains(".split", StringComparison.Ordinal));
    }

    private FileReader CreateReader(string fileName, byte[] bytes) =>
        new(tempDirectory.GetPath(fileName), new MemoryStream(bytes));

    private static byte[] Compress(byte[] input, Func<Stream, Stream> createCompressor)
    {
        using var output = new MemoryStream();
        using (var compressor = createCompressor(output))
        {
            compressor.Write(input);
        }
        return output.ToArray();
    }
}
