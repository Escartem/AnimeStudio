using System.Collections.Generic;
using AnimeStudio;

namespace AnimeStudio.Test;

public sealed class TypeFlagsTests : IDisposable
{
    public TypeFlagsTests()
    {
        TypeFlags.SetTypes(null);
    }

    public void Dispose()
    {
        TypeFlags.SetTypes(null);
    }

    [Fact]
    public void Unconfigured_types_can_be_parsed_and_exported()
    {
        Assert.True(ClassIDType.Texture2D.CanParse());
        Assert.True(ClassIDType.Texture2D.CanExport());
    }

    [Fact]
    public void Configured_type_uses_its_parse_and_export_flags()
    {
        TypeFlags.SetType(ClassIDType.Texture2D, parse: true, export: false);

        Assert.True(ClassIDType.Texture2D.CanParse());
        Assert.False(ClassIDType.Texture2D.CanExport());
    }

    [Fact]
    public void Missing_type_is_rejected_after_types_are_configured()
    {
        TypeFlags.SetTypes(new Dictionary<ClassIDType, (bool, bool)>
        {
            [ClassIDType.Texture2D] = (true, true),
        });

        Assert.False(ClassIDType.AudioClip.CanParse());
        Assert.False(ClassIDType.AudioClip.CanExport());
    }
}
