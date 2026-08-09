using System.Collections.Generic;

namespace AnimeStudio.Test;

internal static class AssetMapTestData
{
    public static AssetMap Create()
    {
        return new AssetMap
        {
            GameType = GameType.GI,
            AssetEntries = new List<AssetEntry>
            {
                new()
                {
                    Name = "hero",
                    Container = "shared.bundle",
                    Source = "E:/Game/shared.bundle",
                    PathID = 9_007_199_254_740_991,
                    Type = ClassIDType.Texture2D,
                    Hash = "hash-hero",
                    Offset = -1,
                },
                new()
                {
                    Name = string.Empty,
                    Container = "shared.bundle",
                    Source = "E:/Game/shared.bundle",
                    PathID = -42,
                    Type = ClassIDType.AudioClip,
                    Hash = string.Empty,
                    Offset = 1234,
                },
                new()
                {
                    Name = null,
                    Container = null,
                    Source = null,
                    PathID = 0,
                    Type = ClassIDType.MonoBehaviour,
                    Hash = null,
                    Offset = -1,
                },
            },
        };
    }
}
