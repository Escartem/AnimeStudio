using System;
using System.Collections.Concurrent;

namespace AnimeStudio.App
{
    public sealed class AssetRow
    {
        private static readonly ConcurrentDictionary<ClassIDType, string> typeNames = new();

        public readonly Object Asset;
        public readonly SerializedFile SourceFile;
        public readonly ClassIDType Type;
        public readonly long PathID;
        public string Name;
        public string Container = string.Empty;
        public long FullSize;
        public int Index;
        public int SceneNode = -1;
        public string InfoText;

        public readonly bool IsVirtual;
        public readonly string ExternalPath;
        public string VirtualContent;

        private string hash;

        public AssetRow(Object asset)
        {
            Asset = asset;
            Name = asset.Name;
            SourceFile = asset.assetsFile;
            Type = asset.type;
            PathID = asset.m_PathID;
            FullSize = asset.byteSize;
        }

        public AssetRow(string name, ClassIDType type, string externalPath, long fullSize, string container = "")
        {
            Name = name;
            Type = type;
            FullSize = fullSize;
            Container = container ?? string.Empty;
            IsVirtual = true;
            ExternalPath = externalPath;
            hash = string.Empty;
        }

        public string TypeString => GetTypeName(Type);

        // Hashing reads the object bytes, so callers must hold AssetIo when it is not computed yet.
        public string Hash => hash ??= Asset.GetHash();
        public bool HasHash => hash != null;
        public string CachedHash => hash;

        public string SourcePath => SourceFile?.originalPath ?? SourceFile?.fullName ?? ExternalPath;

        public static string GetTypeName(ClassIDType type) => typeNames.GetOrAdd(type, t => t.ToString());
    }
}
