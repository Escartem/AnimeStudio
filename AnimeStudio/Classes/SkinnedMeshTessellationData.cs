using System.Collections.Generic;

namespace AnimeStudio
{
    public sealed class SkinnedMeshTessellationData : NamedObject
    {
        public int m_SubmeshCount;
        public byte[] m_CompressedBinaryData;
        public uint m_UncompressedDataSize;
        public int m_HalfEdgeCount_new;
        public List<ChannelInfo> m_Channels;
        public int m_ChannelMask;
        public PPtr<Mesh> m_OriginalMesh;
        public bool m_IsCompressed;

        public SkinnedMeshTessellationData(ObjectReader reader) : base(reader)
        {
            m_SubmeshCount = reader.ReadInt32();

            var vSize = reader.ReadInt32();
            for (int i = 0; i < vSize; i++)
            {
                reader.ReadInt32();
            }

            var eSize = reader.ReadInt32();
            for (int i = 0; i < eSize; i++)
            {
                reader.ReadInt32();
            }

            var nSize = reader.ReadInt32();
            for (int i = 0; i < nSize; i++)
            {
                reader.ReadInt32();
            }

            var fSize = reader.ReadInt32();
            for (int i = 0; i < fSize; i++)
            {
                reader.ReadInt32();
            }

            var compressedSize = reader.ReadInt32();
            m_CompressedBinaryData = reader.ReadBytes(compressedSize);
            reader.AlignStream();

            m_UncompressedDataSize = reader.ReadUInt32();
            m_HalfEdgeCount_new = reader.ReadInt32();

            var channelCount = reader.ReadInt32();
            m_Channels = new List<ChannelInfo>(channelCount);
            for (int i = 0; i < channelCount; i++)
            {
                var channel = new ChannelInfo
                {
                    stream = reader.ReadByte(),
                    offset = reader.ReadByte(),
                    format = reader.ReadByte(),
                    dimension = reader.ReadByte()
                };
                m_Channels.Add(channel);
            }

            m_ChannelMask = reader.ReadInt32();

            var vertToRawiSize = reader.ReadInt32();
            for (int i = 0; i < vertToRawiSize; i++)
            {
                reader.ReadInt32();
            }

            var rawiToVertSize = reader.ReadInt32();
            for (int i = 0; i < rawiToVertSize; i++)
            {
                reader.ReadInt32();
            }

            var vertFlagsSize = reader.ReadInt32();
            for (int i = 0; i < vertFlagsSize; i++)
            {
                reader.ReadUInt32();
            }

            var vertLengthSize = reader.ReadInt32();
            for (int i = 0; i < vertLengthSize; i++)
            {
                reader.ReadUInt16();
            }
            reader.AlignStream();

            var edgeToFirstHalfEdgeSize = reader.ReadInt32();
            for (int i = 0; i < edgeToFirstHalfEdgeSize; i++)
            {
                reader.ReadInt32();
            }

            var faceToSubmeshIndexSize = reader.ReadInt32();
            for (int i = 0; i < faceToSubmeshIndexSize; i++)
            {
                reader.ReadInt32();
            }

            var submeshIndexBufferOffsetSize = reader.ReadInt32();
            for (int i = 0; i < submeshIndexBufferOffsetSize; i++)
            {
                reader.ReadInt32();
            }

            m_OriginalMesh = new PPtr<Mesh>(reader);
            m_IsCompressed = reader.ReadBoolean();
            reader.AlignStream();
        }
    }
}