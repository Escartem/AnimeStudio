using System;
using System.Runtime.InteropServices;
using AnimeStudio.PInvoke;

namespace AnimeStudio.Test;

public class DllLoaderTests
{
    [Theory]
    [InlineData("AnimeStudio.Ooz", "Windows", "AnimeStudio.Ooz.dll")]
    [InlineData("AnimeStudio.Ooz", "Linux", "libAnimeStudio.Ooz.so")]
    public void GetLibraryFileName_returns_platform_specific_name(string logicalName, string platform, string expected)
    {
        var actual = DllLoader.GetLibraryFileName(logicalName, OSPlatform.Create(platform));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GetLibraryFileName_rejects_an_empty_logical_name()
    {
        Assert.Throws<ArgumentException>(() => DllLoader.GetLibraryFileName(string.Empty, OSPlatform.Linux));
    }

    [Theory]
    [InlineData("OSX")]
    [InlineData("FreeBSD")]
    public void GetLibraryFileName_rejects_an_unsupported_platform(string platform)
    {
        Assert.Throws<PlatformNotSupportedException>(() => DllLoader.GetLibraryFileName("AnimeStudio.Ooz", OSPlatform.Create(platform)));
    }
}
