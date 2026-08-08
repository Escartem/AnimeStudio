using System;

namespace AnimeStudio
{
    [Flags]
    public enum ExportListType
    {
        None,
        MessagePack,
        XML,
        JSON       = 4,
        MemoryPack = 8
    }
}
