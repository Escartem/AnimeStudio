using K4os.Hash.xxHash;
using Newtonsoft.Json;
using System;
using System.Collections.Specialized;

namespace AnimeStudio
{
    public class Object
    {
        [JsonIgnore]
        public SerializedFile assetsFile;
        [JsonIgnore]
        public ObjectReader reader;
        [JsonIgnore]
        public long m_PathID;
        [JsonIgnore]
        public int[] version;
        [JsonIgnore]
        protected BuildType buildType;
        [JsonIgnore]
        public BuildTarget platform;
        [JsonIgnore]
        public ClassIDType type;
        [JsonIgnore]
        public SerializedType serializedType;
        [JsonIgnore]
        public uint byteSize;

        public virtual string Name => string.Empty;

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

        public string GetHash(bool overrides = false)
        {
            if (type == ClassIDType.Texture2D && overrides)
            {
                var pos = reader.Position;
                reader.Reset();
                var hash = Texture2DExtensions.GetImageHash(new Texture2D(reader));
                reader.Position = pos;
                return hash;
            } else
            {
                return XXH64.DigestOf(GetRawData()).ToString("x");
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
            byte[] data = reader.ReadBytes((int)byteSize);
            reader.Position = pos;
            return data;
        }
    }
}
