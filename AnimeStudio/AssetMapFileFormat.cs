using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimeStudio
{
    public static class AssetMapFileFormat
    {
        private static readonly ExportListType[] SupportedFormats =
        {
            ExportListType.MessagePack,
            ExportListType.MemoryPack,
            ExportListType.XML,
            ExportListType.JSON,
        };

        public static string GetExtension(ExportListType format) => format switch
        {
            ExportListType.MessagePack => ".map",
            ExportListType.MemoryPack => ".memory",
            ExportListType.XML => ".xml",
            ExportListType.JSON => ".json",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "A single AssetMap format is required."),
        };

        public static string GetDisplayName(ExportListType format) => format switch
        {
            ExportListType.MessagePack => "MessagePack AssetMap",
            ExportListType.MemoryPack => "MemoryPack AssetMap",
            ExportListType.XML => "XML AssetMap",
            ExportListType.JSON => "JSON AssetMap",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "A single AssetMap format is required."),
        };

        public static string BuildSaveFilter(ExportListType selectedFormats)
        {
            var formats = SupportedFormats
                .Where(format => selectedFormats.HasFlag(format))
                .ToArray();

            if (formats.Length == 0)
            {
                formats = SupportedFormats;
            }

            return string.Join("|", formats.Select(format =>
            {
                var extension = GetExtension(format);
                return $"{GetDisplayName(format)} (*{extension})|*{extension}";
            }));
        }

        public static string GetDefaultExtension(ExportListType selectedFormats)
        {
            var format = SupportedFormats.FirstOrDefault(format => selectedFormats.HasFlag(format));
            return GetExtension(format == ExportListType.None ? ExportListType.MessagePack : format);
        }
    }
}
