namespace AnimeStudio.Test;

public class AssetMapFileFormatTests
{
    [Theory]
    [InlineData(ExportListType.MessagePack, ".map")]
    [InlineData(ExportListType.MemoryPack, ".memory")]
    [InlineData(ExportListType.XML, ".xml")]
    [InlineData(ExportListType.JSON, ".json")]
    public void EachAssetMapFormatHasItsCanonicalExtension(ExportListType format, string expectedExtension)
    {
        Assert.Equal(expectedExtension, AssetMapFileFormat.GetExtension(format));
    }

    [Fact]
    public void SaveFilterContainsOnlySelectedFormats()
    {
        var filter = AssetMapFileFormat.BuildSaveFilter(ExportListType.MemoryPack);

        Assert.Equal("MemoryPack AssetMap (*.memory)|*.memory", filter);
    }

    [Fact]
    public void DefaultExtensionUsesMessagePackWhenItIsSelected()
    {
        Assert.Equal(".map", AssetMapFileFormat.GetDefaultExtension(ExportListType.MessagePack));
        Assert.Equal(".memory", AssetMapFileFormat.GetDefaultExtension(ExportListType.MemoryPack));
    }
}
