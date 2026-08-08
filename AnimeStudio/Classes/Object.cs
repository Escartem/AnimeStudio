using System.Collections.Specialized;
using K4os.Hash.xxHash;
using Newtonsoft.Json;
using System;
using System.Buffers;
using System.Collections.Specialized;

namespace AnimeStudio
{
    public class Object
    {
        [JsonIgnore]
        public SerializedFile assetsFile;

        [JsonIgnore]
        protected BuildType buildType;

        [JsonIgnore] public uint byteSize;

        [JsonIgnore] public long m_PathID;

        [JsonIgnore]
        public BuildTarget platform;

        [JsonIgnore] public ObjectReader reader;

        [JsonIgnore]
        public SerializedType serializedType;

        [JsonIgnore] public ClassIDType type;

        [JsonIgnore] public int[] version;

        public Object(ObjectReader reader)
        {
            this.reader = reader;
            reader.Reset();
            assetsFile = reader.assetsFile;
            type = reader.type;
            m_PathID = reader.m_PathID;
            version = reader.version;
            buildType = reader.buildType;
            platform = reader.platform;
            serializedType = reader.serializedType;
            byteSize = reader.byteSize;

            Logger.Verbose($"Attempting to read object {type} with {m_PathID} in file {assetsFile.fileName}, starting from offset 0x{reader.byteStart:X8} with size of 0x{byteSize:X8} !!");

            if (platform == BuildTarget.NoTarget)
            {
                var m_ObjectHideFlags = reader.ReadUInt32();
            }
        }

        public virtual string Name
        {
            get => string.Empty;
        }

        public string GetHash(bool overrides = false)
        {
            if (type == ClassIDType.Texture2D && overrides)
            {
                var pos = reader.Position;
                reader.Reset();
                var hash = Texture2DExtensions.GetImageHash(new Texture2D(reader));
                reader.Position = pos;
                return hash;
            }

            return ComputeRawHash().ToString("x");
        }

        /// <summary>
        /// Stream object bytes through xxHash without allocating a full raw-data buffer.
        /// Avoids multi-GB LOH pressure when building asset maps over large games (e.g. HSR).
        /// </summary>
        private ulong ComputeRawHash()
        {
            Logger.Verbose($"Hashing raw bytes of the object with {m_PathID} in file {assetsFile.fileName}...");
            long pos = reader.Position;
            reader.Reset();

            var hasher = new XXH64();
            int remaining = (int)byteSize;
            // 64KB chunks keep peak temporary memory flat regardless of object size.
            byte[] buffer = ArrayPool<byte>.Shared.Rent(0x10000);
            try
            {
                while (remaining > 0)
                {
                    int toRead = Math.Min(remaining, buffer.Length);
                    int read = reader.Read(buffer, 0, toRead);
                    if (read <= 0)
                    {
                        break;
                    }
                    hasher.Update(buffer, 0, read);
                    remaining -= read;
                }
                return hasher.Digest();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                reader.Position = pos;
            }
        }

        public string Dump()
        {
            if (serializedType?.m_Type != null)
            {
                return TypeTreeHelper.ReadTypeString(serializedType.m_Type, reader);
            }
            return null;
        }

        public string Dump(TypeTree m_Type)
        {
            if (m_Type != null)
            {
                return TypeTreeHelper.ReadTypeString(m_Type, reader);
            }
            return null;
        }

        public OrderedDictionary ToType()
        {
            if (serializedType?.m_Type != null)
            {
                return TypeTreeHelper.ReadType(serializedType.m_Type, reader);
            }
            return null;
        }

        public OrderedDictionary ToType(TypeTree m_Type)
        {
            if (m_Type != null)
            {
                return TypeTreeHelper.ReadType(m_Type, reader);
            }
            return null;
        }

        public byte[] GetRawData()
        {
            Logger.Verbose($"Dumping raw bytes of the object with {m_PathID} in file {assetsFile.fileName}...");
            long pos = reader.Position;
            reader.Reset();
            byte[] data = reader.ReadBytes((int) byteSize);
            reader.Position = pos;
            return data;
        }
    }
}
