using System.Collections.Generic;

namespace AnimeStudio
{
    public class Index
    {
        public PPtr<Object> Object;
        public ulong Size;

        public Index(ObjectReader reader)
        {
            Object = new PPtr<Object>(reader);
            Size = reader.ReadUInt64();
        }
    }

    public sealed class IndexObject : NamedObject
    {
        public int Count;
        public List<KeyValuePair<string, Index>> AssetMap;

        public override string Name => "IndexObject";

        public IndexObject(ObjectReader reader) : base(reader)
        {
            Count = reader.ReadInt32();
            AssetMap = new List<KeyValuePair<string, Index>>();
            for (int i = 0; i < Count; i++)
            {
                string name = "";
                if (reader.Game.Type.IsHYGCB1())
                {
                    reader.ReadBytes(4);
                    name = reader.Position.ToString("X8");
                }
                else
                {
                    name = reader.ReadAlignedString();
                }
                AssetMap.Add(new KeyValuePair<string, Index>(name, new Index(reader)));
            }
        }
    } 
}
