using System;
using System.IO;

namespace AnimeStudio.Test;

internal sealed class TestTempDirectory : IDisposable
{
    public TestTempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AnimeStudio.Test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string GetPath(string fileName) => System.IO.Path.Combine(Path, fileName);

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
