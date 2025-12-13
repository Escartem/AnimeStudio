using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AnimeStudio
{
    public class MinMaxAABB
    {
        public Vector3 m_Min;
        public Vector3 m_Max;

        public MinMaxAABB(ObjectReader reader)
        {
            m_Min = reader.ReadVector3();
            m_Max = reader.ReadVector3();
        }
    }

    public class CompressedMesh
    {
        public PackedFloatVector m_Vertices;
        public PackedFloatVector m_UV;
        public PackedFloatVector m_BindPoses;
        public PackedFloatVector m_Normals;
        public PackedFloatVector m_Tangents;
        public PackedIntVector m_Weights;
        public PackedIntVector m_NormalSigns;
        public PackedIntVector m_TangentSigns;
        public PackedFloatVector m_FloatColors;
        public PackedIntVector m_BoneIndices;
        public PackedIntVector m_Triangles;
        public PackedIntVector m_Colors;
        public uint m_UVInfo;

        public CompressedMesh(ObjectReader reader)
        {
            var version = reader.version;

            m_Vertices = new PackedFloatVector(reader);
            m_UV = new PackedFloatVector(reader);
            if (version[0] < 5)
            {
                m_BindPoses = new PackedFloatVector(reader);
            }
            m_Normals = new PackedFloatVector(reader);
            m_Tangents = new PackedFloatVector(reader);
            m_Weights = new PackedIntVector(reader);
            m_NormalSigns = new PackedIntVector(reader);
            m_TangentSigns = new PackedIntVector(reader);
            if (version[0] >= 5)
            {
                m_FloatColors = new PackedFloatVector(reader);
            }
            m_BoneIndices = new PackedIntVector(reader);
            m_Triangles = new PackedIntVector(reader);
            if (version[0] > 3 || (version[0] == 3 && version[1] >= 5)) //3.5 and up
            {
                if (version[0] < 5)
                {
                    m_Colors = new PackedIntVector(reader);
                }
                else
                {
                    m_UVInfo = reader.ReadUInt32();
                }
            }
        }
    }

    public class StreamInfo
    {
        public uint channelMask;
        public uint offset;
        public uint stride;
        public uint align;
        public byte dividerOp;
        public ushort frequency;

        public StreamInfo() { }

        public StreamInfo(ObjectReader reader)
        {
            var version = reader.version;

            channelMask = reader.ReadUInt32();
            offset = reader.ReadUInt32();

            if (version[0] < 4) //4.0 down
            {
                stride = reader.ReadUInt32();
                align = reader.ReadUInt32();
            }
            else
            {
                stride = reader.ReadByte();
                dividerOp = reader.ReadByte();
                frequency = reader.ReadUInt16();
            }
        }
    }

    public class ChannelInfo
    {
        public byte stream;
        public byte offset;
        public byte format;
        public byte dimension;

        public ChannelInfo() { }

        public ChannelInfo(ObjectReader reader)
        {
            stream = reader.ReadByte();
            offset = reader.ReadByte();
            format = reader.ReadByte();
            dimension = (byte)(reader.ReadByte() & 0xF);
        }
    }

    public class VertexData
    {
        public uint m_CurrentChannels;
        public uint m_VertexCount;
        public List<ChannelInfo> m_Channels;
        public List<StreamInfo> m_Streams;
        public byte[] m_DataSize;

        public VertexData(ObjectReader reader)
        {
            var version = reader.version;

            if (version[0] < 2018)//2018 down
            {
                m_CurrentChannels = reader.ReadUInt32();
            }

            m_VertexCount = reader.ReadUInt32();

            if (version[0] >= 4) //4.0 and up
            {
                var m_ChannelsSize = reader.ReadInt32();
                m_Channels = new List<ChannelInfo>();
                for (int i = 0; i < m_ChannelsSize; i++)
                {
                    m_Channels.Add(new ChannelInfo(reader));
                }
            }

            if (version[0] < 5) //5.0 down
            {
                var numStreams = version[0] < 4 ? 4 : reader.ReadInt32();
                m_Streams = new List<StreamInfo>();
                for (int i = 0; i < numStreams; i++)
                {
                    m_Streams.Add(new StreamInfo(reader));
                }

                if (version[0] < 4) //4.0 down
                {
                    GetChannels(version);
                }
            }
            else //5.0 and up
            {
                GetStreams(version);
            }

            m_DataSize = reader.ReadUInt8Array();
            reader.AlignStream();
        }

        private void GetStreams(int[] version)
        {
            var streamCount = m_Channels.Max(x => x.stream) + 1;
            m_Streams = new List<StreamInfo>();
            uint offset = 0;
            for (int s = 0; s < streamCount; s++)
            {
                uint chnMask = 0;
                uint stride = 0;
                for (int chn = 0; chn < m_Channels.Count; chn++)
                {
                    var m_Channel = m_Channels[chn];
                    if (m_Channel.stream == s)
                    {
                        if (m_Channel.dimension > 0)
                        {
                            chnMask |= 1u << chn;
                            stride += m_Channel.dimension * MeshHelper.GetFormatSize(MeshHelper.ToVertexFormat(m_Channel.format, version));
                        }
                    }
                }
                m_Streams.Add(new StreamInfo
                {
                    channelMask = chnMask,
                    offset = offset,
                    stride = stride,
                    dividerOp = 0,
                    frequency = 0
                });
                offset += m_VertexCount * stride;
                //static size_t AlignStreamSize (size_t size) { return (size + (kVertexStreamAlign-1)) & ~(kVertexStreamAlign-1); }
                offset = (offset + (16u - 1u)) & ~(16u - 1u);
            }
        }

        private void GetChannels(int[] version)
        {
            m_Channels = new List<ChannelInfo>(6);
            for (int i = 0; i < 6; i++)
            {
                m_Channels.Add(new ChannelInfo());
            }
            for (var s = 0; s < m_Streams.Count; s++)
            {
                var m_Stream = m_Streams[s];
                var channelMask = new BitArray(new[] { (int)m_Stream.channelMask });
                byte offset = 0;
                for (int i = 0; i < 6; i++)
                {
                    if (channelMask.Get(i))
                    {
                        var m_Channel = m_Channels[i];
                        m_Channel.stream = (byte)s;
                        m_Channel.offset = offset;
                        switch (i)
                        {
                            case 0: //kShaderChannelVertex
                            case 1: //kShaderChannelNormal
                                m_Channel.format = 0; //kChannelFormatFloat
                                m_Channel.dimension = 3;
                                break;
                            case 2: //kShaderChannelColor
                                m_Channel.format = 2; //kChannelFormatColor
                                m_Channel.dimension = 4;
                                break;
                            case 3: //kShaderChannelTexCoord0
                            case 4: //kShaderChannelTexCoord1
                                m_Channel.format = 0; //kChannelFormatFloat
                                m_Channel.dimension = 2;
                                break;
                            case 5: //kShaderChannelTangent
                                m_Channel.format = 0; //kChannelFormatFloat
                                m_Channel.dimension = 4;
                                break;
                        }
                        offset += (byte)(m_Channel.dimension * MeshHelper.GetFormatSize(MeshHelper.ToVertexFormat(m_Channel.format, version)));
                    }
                }
            }
        }
    }

    public class BoneWeights4
    {
        public float[] weight;
        public int[] boneIndex;

        public BoneWeights4()
        {
            weight = new float[4];
            boneIndex = new int[4];
        }

        public BoneWeights4(ObjectReader reader)
        {
            weight = reader.ReadSingleArray(4);
            boneIndex = reader.ReadInt32Array(4);
        }
    }

    public class BlendShapeVertex
    {
        public Vector3 vertex;
        public Vector3 normal;
        public Vector3 tangent;
        public uint index;

        public BlendShapeVertex() { }

        public BlendShapeVertex(ObjectReader reader)
        {
            vertex = reader.ReadVector3();
            normal = reader.ReadVector3();
            tangent = reader.ReadVector3();
            index = reader.ReadUInt32();
        }
    }

    public class BlendShapeAdditionalVertex
    {
        public Vector3 additionalNormal;

        public BlendShapeAdditionalVertex() { }

        public BlendShapeAdditionalVertex(ObjectReader reader)
        {
            additionalNormal = reader.ReadVector3();
        }
    }

    public class MeshBlendShape
    {
        public string name;
        public uint firstVertex;
        public uint vertexCount;
        public bool hasNormals;
        public bool hasTangents;
        public bool hasAdditionalNormals;

        public MeshBlendShape(ObjectReader reader)
        {
            var version = reader.version;

            if (version[0] == 4 && version[1] < 3) //4.3 down
            {
                name = reader.ReadAlignedString();
            }
            firstVertex = reader.ReadUInt32();
            vertexCount = reader.ReadUInt32();
            if (version[0] == 4 && version[1] < 3) //4.3 down
            {
                var aabbMinDelta = reader.ReadVector3();
                var aabbMaxDelta = reader.ReadVector3();
            }
            hasNormals = reader.ReadBoolean();
            hasTangents = reader.ReadBoolean();
            if (reader.Game.Type.IsHNACB1())
            {
                // god fears nothing but this simple function
                reader.AlignStream();
                hasAdditionalNormals = reader.ReadBoolean();
            }
            if (!reader.Game.Type.IsLoveAndDeepspace() && version[0] > 4 || (version[0] == 4 && version[1] >= 3)) //4.3 and up
            {
                reader.AlignStream();
            }
        }
    }

    public class MeshBlendShapeChannel
    {
        public string name;
        public uint nameHash;
        public int frameIndex;
        public int frameCount;

        public MeshBlendShapeChannel(ObjectReader reader)
        {
            name = reader.ReadAlignedString();
            nameHash = reader.ReadUInt32();
            frameIndex = reader.ReadInt32();
            frameCount = reader.ReadInt32();
        }
    }

    public class BlendShapeData
    {
        public List<BlendShapeVertex> vertices;
        public List<MeshBlendShape> shapes;
        public List<MeshBlendShapeChannel> channels;
        public float[] fullWeights;
        public List<BlendShapeAdditionalVertex> additionalVertices;

        public BlendShapeData(ObjectReader reader)
        {
            var version = reader.version;

            if (version[0] > 4 || (version[0] == 4 && version[1] >= 3)) //4.3 and up
            {
                int numVerts = reader.ReadInt32();
                vertices = new List<BlendShapeVertex>();
                for (int i = 0; i < numVerts; i++)
                {
                    vertices.Add(new BlendShapeVertex(reader));
                }

                int numShapes = reader.ReadInt32();
                shapes = new List<MeshBlendShape>();
                for (int i = 0; i < numShapes; i++)
                {
                    shapes.Add(new MeshBlendShape(reader));
                }

                if (reader.Game.Type.IsLoveAndDeepspace())
                {
                    reader.AlignStream();
                }

                int numChannels = reader.ReadInt32();
                channels = new List<MeshBlendShapeChannel>();
                for (int i = 0; i < numChannels; i++)
                {
                    channels.Add(new MeshBlendShapeChannel(reader));
                }

                fullWeights = reader.ReadSingleArray();

                if (reader.Game.Type.IsHNACB1())
                {
                    additionalVertices = new List<BlendShapeAdditionalVertex>();
                    var length = reader.ReadInt32();
                    for (int i = 0; i < length; i++)
                    {
                        var additionalVertex = new BlendShapeAdditionalVertex(reader);
                        if (additionalVertex != null)
                        {
                            additionalVertices.Add(additionalVertex);
                        }
                    }
                }

                if (reader.Game.Type.IsLoveAndDeepspace())
                {
                    var varintVerticesSize = reader.ReadInt32();
                    if (varintVerticesSize > 0)
                    {
                        var pos = reader.Position;
                        while (reader.Position < pos + varintVerticesSize)
                        {
                            var value = reader.ReadUInt32();
                            var index = value & 0x0FFFFFFF;
                            var flags = value >> 0x1D;
                            var blendShapeVertex = new BlendShapeVertex
                            {
                                index = index,
                                vertex = (flags & 4) != 0 ? reader.ReadVector3() : Vector3.Zero,
                                normal = (flags & 2) != 0 ? reader.ReadVector3() : Vector3.Zero,
                                tangent = (flags & 1) != 0 ? reader.ReadVector3() : Vector3.Zero,
                            };
                            vertices.Add(blendShapeVertex);
                        }
                        reader.AlignStream();

                        var stride = (uint)(varintVerticesSize / vertices.Count);
                        foreach (var shape in shapes)
                        {
                            shape.firstVertex /= stride;
                        }
                    }
                }
            }
            else
            {
                var m_ShapesSize = reader.ReadInt32();
                var m_Shapes = new List<MeshBlendShape>();
                for (int i = 0; i < m_ShapesSize; i++)
                {
                    m_Shapes.Add(new MeshBlendShape(reader));
                }
                reader.AlignStream();
                var m_ShapeVerticesSize = reader.ReadInt32();
                var m_ShapeVertices = new List<BlendShapeVertex>(); //MeshBlendShapeVertex
                for (int i = 0; i < m_ShapeVerticesSize; i++)
                {
                    m_ShapeVertices.Add(new BlendShapeVertex(reader));
                }
            }
        }
    }

    public enum GfxPrimitiveType
    {
        Triangles = 0,
        TriangleStrip = 1,
        Quads = 2,
        Lines = 3,
        LineStrip = 4,
        Points = 5
    };

    public class SubMesh
    {
        public uint firstByte;
        public uint indexCount;
        public GfxPrimitiveType topology;
        public uint triangleCount;
        public uint baseVertex;
        public uint firstVertex;
        public uint vertexCount;
        public AABB localAABB;

        public SubMesh(ObjectReader reader)
        {
            var version = reader.version;

            firstByte = reader.ReadUInt32();
            indexCount = reader.ReadUInt32();
            topology = (GfxPrimitiveType)reader.ReadInt32();

            if (version[0] < 4) //4.0 down
            {
                triangleCount = reader.ReadUInt32();
            }

            if (version[0] > 2017 || (version[0] == 2017 && version[1] >= 3)) //2017.3 and up
            {
                baseVertex = reader.ReadUInt32();
            }

            if (version[0] >= 3) //3.0 and up
            {
                firstVertex = reader.ReadUInt32();
                vertexCount = reader.ReadUInt32();
                localAABB = new AABB(reader);
            }
        }
    }

    public sealed class Mesh : NamedObject
    {
        private bool m_Use16BitIndices = true;
        public List<SubMesh> m_SubMeshes;
        private uint[] m_IndexBuffer;
        public BlendShapeData m_Shapes;
        public Matrix4x4[] m_BindPose;
        public uint[] m_BoneNameHashes;
        public int m_VertexCount;
        public float[] m_Vertices;
        public List<BoneWeights4> m_Skin;
        public float[] m_Normals;
        public float[] m_Colors;
        public float[] m_UV0;
        public float[] m_UV1;
        public float[] m_UV2;
        public float[] m_UV3;
        public float[] m_UV4;
        public float[] m_UV5;
        public float[] m_UV6;
        public float[] m_UV7;
        public float[] m_Tangents;
        private VertexData m_VertexData;
        private CompressedMesh m_CompressedMesh;
        private StreamingInfo m_StreamData;
        private bool m_CollisionMeshBaked = false;

        public static bool HasVertexColorSkinning(SerializedType type) => type.Match("413A501B79022BF2DF389A82002FC81F");

        public List<uint> m_Indices = new List<uint>();

        public Mesh(ObjectReader reader) : base(reader)
        {
            if (version[0] < 3 || (version[0] == 3 && version[1] < 5)) //3.5 down
            {
                m_Use16BitIndices = reader.ReadInt32() > 0;
            }

            if (version[0] == 2 && version[1] <= 5) //2.5 and down
            {
                int m_IndexBuffer_size = reader.ReadInt32();

                if (m_Use16BitIndices)
                {
                    m_IndexBuffer = new uint[m_IndexBuffer_size / 2];
                    for (int i = 0; i < m_IndexBuffer_size / 2; i++)
                    {
                        m_IndexBuffer[i] = reader.ReadUInt16();
                    }
                    reader.AlignStream();
                }
                else
                {
                    m_IndexBuffer = reader.ReadUInt32Array(m_IndexBuffer_size / 4);
                }
            }

            int m_SubMeshesSize = reader.ReadInt32();
            m_SubMeshes = new List<SubMesh>();
            for (int i = 0; i < m_SubMeshesSize; i++)
            {
                m_SubMeshes.Add(new SubMesh(reader));
            }

            if (version[0] > 4 || (version[0] == 4 && version[1] >= 1)) //4.1 and up
            {
                m_Shapes = new BlendShapeData(reader);
            }

            if (version[0] > 4 || (version[0] == 4 && version[1] >= 3)) //4.3 and up
            {
                m_BindPose = reader.ReadMatrixArray();
                m_BoneNameHashes = reader.ReadUInt32Array();
                var m_RootBoneNameHash = reader.ReadUInt32();
            }

            if (version[0] > 2 || (version[0] == 2 && version[1] >= 6)) //2.6.0 and up
            {
                if (version[0] >= 2019) //2019 and up
                {
                    var m_BonesAABBSize = reader.ReadInt32();
                    var m_BonesAABB = new List<MinMaxAABB>();
                    for (int i = 0; i < m_BonesAABBSize; i++)
                    {
                        m_BonesAABB.Add(new MinMaxAABB(reader));
                    }

                    var m_VariableBoneCountWeights = reader.ReadUInt32Array();
                }

                var m_MeshCompression = reader.ReadByte();
                if (version[0] >= 4)
                {
                    if (version[0] < 5)
                    {
                        var m_StreamCompression = reader.ReadByte();
                    }
                    var m_IsReadable = reader.ReadBoolean();
                    if (reader.Game.Type.IsBH3())
                    {
                        var m_IsHighPrecisionPosition = reader.ReadBoolean();
                        var m_IsHighPrecisionTangent = reader.ReadBoolean();
                        var m_IsHighPrecisionUv = reader.ReadBoolean();
                    }
                    var m_KeepVertices = reader.ReadBoolean();
                    var m_KeepIndices = reader.ReadBoolean();
                    if (reader.Game.Type.IsArknightsEndfieldCB3())
                    {
                        var m_CollisionMeshOnly = reader.ReadBoolean();
                        m_CollisionMeshBaked = reader.ReadBoolean();
                        var m_CollisionMeshConvex = reader.ReadBoolean();
                    }
                    if (reader.Game.Type.IsHYGCB1())
                    {
                        var m_FullPrecisionVertexPosition = reader.ReadBoolean();
                    }
                    if (reader.Game.Type.IsBH3() && HasVertexColorSkinning(reader.serializedType)) 
                    {
                        var m_VertexColorSkinning = reader.ReadBoolean();
                    }
                    if (reader.Game.Type.IsArknightsEndfield())
                    {
                        var m_CollisionMeshOnly = reader.ReadBoolean();
                        m_CollisionMeshBaked = reader.ReadBoolean();
                        var m_CollisionMeshConvex = reader.ReadBoolean();
                    }
                }
                reader.AlignStream();
                if (reader.Game.Type.IsGISubGroup() || (reader.Game.Type.IsBH3() && HasVertexColorSkinning(reader.serializedType)))
                {
                    var m_PackSkinDataToUV2UV3 = reader.ReadBoolean();
                    reader.AlignStream();
                }
                if (reader.Game.Type.IsHYGCB1())
                {
                    var m_UseForSoftBody = reader.ReadBoolean();
                    reader.AlignStream();
                }

                //Unity fixed it in 2017.3.1p1 and later versions
                if ((version[0] > 2017 || (version[0] == 2017 && version[1] >= 4)) || //2017.4
                    ((version[0] == 2017 && version[1] == 3 && version[2] == 1) && buildType.IsPatch) || //fixed after 2017.3.1px
                    ((version[0] == 2017 && version[1] == 3) && m_MeshCompression == 0))//2017.3.xfx with no compression
                {
                    var m_IndexFormat = reader.ReadInt32();
                    m_Use16BitIndices = m_IndexFormat == 0;
                }

                int m_IndexBuffer_size = reader.ReadInt32();
                if (m_Use16BitIndices)
                {
                    m_IndexBuffer = new uint[m_IndexBuffer_size / 2];
                    for (int i = 0; i < m_IndexBuffer_size / 2; i++)
                    {
                        m_IndexBuffer[i] = reader.ReadUInt16();
                    }
                    reader.AlignStream();
                }
                else
                {
                    m_IndexBuffer = reader.ReadUInt32Array(m_IndexBuffer_size / 4);
                }
            }

            if (version[0] < 3 || (version[0] == 3 && version[1] < 5)) //3.4.2 and earlier
            {
                m_VertexCount = reader.ReadInt32();
                m_Vertices = reader.ReadSingleArray(m_VertexCount * 3); //Vector3

                var skinNum = reader.ReadInt32();
                m_Skin = new List<BoneWeights4>();
                for (int s = 0; s < skinNum; s++)
                {
                    m_Skin.Add(new BoneWeights4(reader));
                }

                m_BindPose = reader.ReadMatrixArray();

                m_UV0 = reader.ReadSingleArray(reader.ReadInt32() * 2); //Vector2

                m_UV1 = reader.ReadSingleArray(reader.ReadInt32() * 2); //Vector2

                if (version[0] == 2 && version[1] <= 5) //2.5 and down
                {
                    int m_TangentSpace_size = reader.ReadInt32();
                    m_Normals = new float[m_TangentSpace_size * 3];
                    m_Tangents = new float[m_TangentSpace_size * 4];
                    for (int v = 0; v < m_TangentSpace_size; v++)
                    {
                        m_Normals[v * 3] = reader.ReadSingle();
                        m_Normals[v * 3 + 1] = reader.ReadSingle();
                        m_Normals[v * 3 + 2] = reader.ReadSingle();
                        m_Tangents[v * 3] = reader.ReadSingle();
                        m_Tangents[v * 3 + 1] = reader.ReadSingle();
                        m_Tangents[v * 3 + 2] = reader.ReadSingle();
                        m_Tangents[v * 3 + 3] = reader.ReadSingle(); //handedness
                    }
                }
                else //2.6.0 and later
                {
                    m_Tangents = reader.ReadSingleArray(reader.ReadInt32() * 4); //Vector4

                    m_Normals = reader.ReadSingleArray(reader.ReadInt32() * 3); //Vector3
                }
            }
            else
            {
                if (version[0] < 2018 || (version[0] == 2018 && version[1] < 2)) //2018.2 down
                {
                    var skinNum = reader.ReadInt32();
                    m_Skin = new List<BoneWeights4>();
                    for (int s = 0; s < skinNum; s++)
                    {
                        m_Skin.Add(new BoneWeights4(reader));
                    }
                }

                if (version[0] == 3 || (version[0] == 4 && version[1] <= 2)) //4.2 and down
                {
                    m_BindPose = reader.ReadMatrixArray();
                }

                m_VertexData = new VertexData(reader);
            }

            if ((version[0] > 2 || (version[0] == 2 && version[1] >= 6)) && !m_CollisionMeshBaked) //2.6.0 and later
            {
                m_CompressedMesh = new CompressedMesh(reader);
            }

            reader.Position += 24; //AABB m_LocalAABB

            if (version[0] < 3 || (version[0] == 3 && version[1] <= 4)) //3.4.2 and earlier
            {
                int m_Colors_size = reader.ReadInt32();
                m_Colors = new float[m_Colors_size * 4];
                for (int v = 0; v < m_Colors_size * 4; v++)
                {
                    m_Colors[v] = (float)reader.ReadByte() / 0xFF;
                }

                int m_CollisionTriangles_size = reader.ReadInt32();
                reader.Position += m_CollisionTriangles_size * 4; //UInt32 indices
                int m_CollisionVertexCount = reader.ReadInt32();
            }

            if (reader.Game.Type.IsExAstris())
            {
                var m_ColliderType = reader.ReadInt32();
            }

            int m_MeshUsageFlags = reader.ReadInt32();

            if (version[0] > 2022 || (version[0] == 2022 && version[1] >= 1)) //2022.1 and up
            {
                int m_CookingOptions = reader.ReadInt32();
            }

            if (version[0] >= 5) //5.0 and up
            {
                var m_BakedConvexCollisionMesh = reader.ReadUInt8Array();
                reader.AlignStream();
                var m_BakedTriangleCollisionMesh = reader.ReadUInt8Array();
                reader.AlignStream();
                if (reader.Game.Type.IsBH3())
                {
                    var m_MeshOptimized = reader.ReadBoolean();
                }
            }

            if (reader.Game.Type.IsZZZCB1())
            {
                var m_CloseMeshDynamicCompression = reader.ReadBoolean();
                reader.AlignStream();

                var m_CompressLevelVertexData = reader.ReadInt32();
                var m_CompressLevelNormalAndTangent = reader.ReadInt32();
                var m_CompressLevelTexCoordinates = reader.ReadInt32();
            }

            if (reader.Game.Type.IsGIGroup() || version[0] > 2018 || (version[0] == 2018 && version[1] >= 2)) //2018.2 and up
            {
                var m_MeshMetrics = new float[2];
                m_MeshMetrics[0] = reader.ReadSingle();
                m_MeshMetrics[1] = reader.ReadSingle();
                if (reader.Game.Type.IsArknightsEndfield() || reader.Game.Type.IsArknightsEndfieldCB3())
                {
                    var m_MeshMetrics2 = reader.ReadSingle();
                }
            }

            if (reader.Game.Type.IsGIGroup())
            {
                var m_MetricsDirty = reader.ReadBoolean();
                reader.AlignStream();
                var m_CloseMeshDynamicCompression = reader.ReadBoolean();
                reader.AlignStream();
                if (!reader.Game.Type.IsGICB1() && !reader.Game.Type.IsGIPack())
                {
                    var m_IsStreamingMesh = reader.ReadBoolean();
                    reader.AlignStream();
                }
            }

            if (reader.Game.Type.IsZZZ())
            {
                var m_MetricsDirty = reader.ReadBoolean();
                reader.AlignStream();
                var m_CloseMeshDynamicCompression = reader.ReadBoolean();
                reader.AlignStream();

                var m_CompressLevelVertexData = reader.ReadInt32();
                var m_CompressLevelNormalAndTangent = reader.ReadInt32();
                var m_CompressLevelTexCoordinates = reader.ReadInt32();

                var m_PackSkinDataToUV2UV3 = reader.ReadBoolean();
                reader.AlignStream();
                var m_BakeBVHData = reader.ReadBoolean();
                var m_BakeRefittableBVH = reader.ReadBoolean();
                reader.AlignStream();
                var m_BVHBakeLevels = reader.ReadUInt8Array();
                reader.AlignStream();
                var m_BakedBVHSize = reader.ReadUInt64();
                var m_BVHDataBuffer = reader.ReadUInt8Array();
                reader.AlignStream();
            }

            if (version[0] > 2018 || (version[0] == 2018 && version[1] >= 3)) //2018.3 and up
            {
                reader.AlignStream();
                m_StreamData = new StreamingInfo(reader);
            }

            ProcessData();
        }

        private void ProcessData()
        {
            if (!string.IsNullOrEmpty(m_StreamData?.path))
            {
                if (m_VertexData.m_VertexCount > 0)
                {
                    var resourceReader = new ResourceReader(m_StreamData.path, assetsFile, m_StreamData.offset, m_StreamData.size);
                    m_VertexData.m_DataSize = resourceReader.GetData();
                }
            }
            if (version[0] > 3 || (version[0] == 3 && version[1] >= 5)) //3.5 and up
            {
                ReadVertexData();
            }

            if (m_CollisionMeshBaked)
            {
                return;
            }

            if ((version[0] > 2 || (version[0] == 2 && version[1] >= 6))) //2.6.0 and later
            {
                DecompressCompressedMesh();
            }

            GetTriangles();
        }


        private void ReadVertexData()
        {
            m_VertexCount = (int)m_VertexData.m_VertexCount;

            for (var chn = 0; chn < m_VertexData.m_Channels.Count; chn++)
            {
                var m_Channel = m_VertexData.m_Channels[chn];
                if (m_Channel.dimension > 0)
                {
                    var m_Stream = m_VertexData.m_Streams[m_Channel.stream];
                    var channelMask = new BitArray(new[] { (int)m_Stream.channelMask });
                    if (channelMask.Get(chn))
                    {
                        if (version[0] < 2018 && chn == 2 && m_Channel.format == 2) //kShaderChannelColor && kChannelFormatColor
                        {
                            m_Channel.dimension = 4;
                        }

                        var vertexFormat = MeshHelper.ToVertexFormat(m_Channel.format, version);
                        var componentByteSize = (int)MeshHelper.GetFormatSize(vertexFormat);
                        var componentBytes = new byte[m_VertexCount * m_Channel.dimension * componentByteSize];
                        for (int v = 0; v < m_VertexCount; v++)
                        {
                            var vertexOffset = (int)m_Stream.offset + m_Channel.offset + (int)m_Stream.stride * v;
                            for (int d = 0; d < m_Channel.dimension; d++)
                            {
                                var componentOffset = vertexOffset + componentByteSize * d;
                                Buffer.BlockCopy(m_VertexData.m_DataSize, componentOffset, componentBytes, componentByteSize * (v * m_Channel.dimension + d), componentByteSize);
                            }
                        }

                        if (reader.Endian == EndianType.BigEndian && componentByteSize > 1) //swap bytes
                        {
                            for (var i = 0; i < componentBytes.Length / componentByteSize; i++)
                            {
                                var buff = new byte[componentByteSize];
                                Buffer.BlockCopy(componentBytes, i * componentByteSize, buff, 0, componentByteSize);
                                buff = buff.Reverse().ToArray();
                                Buffer.BlockCopy(buff, 0, componentBytes, i * componentByteSize, componentByteSize);
                            }
                        }

                        int[] componentsIntArray = null;
                        float[] componentsFloatArray = null;
                        if (reader.Game.Type.IsArknightsEndfieldGroup() && chn == 1 && m_Channel.dimension == 1)
                        {
                            //componentsFloatArray = MeshHelper.BytesToFloatArray(componentBytes, vertexFormat);
                            componentsFloatArray = MeshHelper.DecompressEndfieldNormal(componentBytes, vertexFormat);
                        }
                        else
                        {
                            if (MeshHelper.IsIntFormat(vertexFormat))
                                componentsIntArray = MeshHelper.BytesToIntArray(componentBytes, vertexFormat);
                            else
                                componentsFloatArray = MeshHelper.BytesToFloatArray(componentBytes, vertexFormat);
                        }

                        if (version[0] >= 2018)
                        {
                            switch (chn)
                            {
                                case 0: //kShaderChannelVertex
                                    m_Vertices = componentsFloatArray;
                                    break;
                                case 1: //kShaderChannelNormal
                                    m_Normals = componentsFloatArray;
                                    break;
                                case 2: //kShaderChannelTangent
                                    m_Tangents = componentsFloatArray;
                                    break;
                                case 3: //kShaderChannelColor
                                    m_Colors = componentsFloatArray;
                                    break;
                                case 4: //kShaderChannelTexCoord0
                                    m_UV0 = componentsFloatArray;
                                    break;
                                case 5: //kShaderChannelTexCoord1
                                    m_UV1 = componentsFloatArray;
                                    break;
                                case 6: //kShaderChannelTexCoord2
                                    m_UV2 = componentsFloatArray;
                                    break;
                                case 7: //kShaderChannelTexCoord3
                                    m_UV3 = componentsFloatArray;
                                    break;
                                case 8: //kShaderChannelTexCoord4
                                    m_UV4 = componentsFloatArray;
                                    break;
                                case 9: //kShaderChannelTexCoord5
                                    m_UV5 = componentsFloatArray;
                                    break;
                                case 10: //kShaderChannelTexCoord6
                                    m_UV6 = componentsFloatArray;
                                    break;
                                case 11: //kShaderChannelTexCoord7
                                    m_UV7 = componentsFloatArray;
                                    break;
                                //2018.2 and up
                                case 12: //kShaderChannelBlendWeight
                                    if (m_Skin == null)
                                    {
                                        InitMSkin();
                                    }
                                    for (int i = 0; i < m_VertexCount; i++)
                                    {
                                        for (int j = 0; j < m_Channel.dimension; j++)
                                        {
                                            m_Skin[i].weight[j] = componentsFloatArray[i * m_Channel.dimension + j];
                                        }

                                        m_Skin[i].weight[0] = 1.0f - m_Skin[i].weight.Sum();

                                    }
                                    break;
                                case 13: //kShaderChannelBlendIndices
                                    if (m_Skin == null)
                                    {
                                        InitMSkin();
                                    }
                                    for (int i = 0; i < m_VertexCount; i++)
                                    {
                                        for (int j = 0; j < m_Channel.dimension; j++)
                                        {
                                            m_Skin[i].boneIndex[j] = componentsIntArray[i * m_Channel.dimension + j];
                                        }

                                        m_Skin[i].weight[0] = 1.0f - m_Skin[i].weight.Sum();
                                    }
                                    break;
                            }
                        }
                        else
                        {
                            switch (chn)
                            {
                                case 0: //kShaderChannelVertex
                                    m_Vertices = componentsFloatArray;
                                    break;
                                case 1: //kShaderChannelNormal
                                    m_Normals = componentsFloatArray;
                                    break;
                                case 2: //kShaderChannelColor
                                    m_Colors = componentsFloatArray;
                                    break;
                                case 3: //kShaderChannelTexCoord0
                                    m_UV0 = componentsFloatArray;
                                    break;
                                case 4: //kShaderChannelTexCoord1
                                    m_UV1 = componentsFloatArray;
                                    break;
                                case 5:
                                    if (version[0] >= 5) //kShaderChannelTexCoord2
                                    {
                                        m_UV2 = componentsFloatArray;
                                    }
                                    else //kShaderChannelTangent
                                    {
                                        m_Tangents = componentsFloatArray;
                                    }
                                    break;
                                case 6: //kShaderChannelTexCoord3
                                    m_UV3 = componentsFloatArray;
                                    break;
                                case 7: //kShaderChannelTangent
                                    m_Tangents = componentsFloatArray;
                                    break;
                            }
                        }
                    }
                }
            }
        }

        private void DecompressCompressedMesh()
        {
            //Vertex
            if (m_CompressedMesh.m_Vertices.m_NumItems > 0)
            {
                m_VertexCount = (int)m_CompressedMesh.m_Vertices.m_NumItems / 3;
                m_Vertices = m_CompressedMesh.m_Vertices.UnpackFloats(3, 3 * 4);
            }
            //UV
            if (m_CompressedMesh.m_UV.m_NumItems > 0)
            {
                var m_UVInfo = m_CompressedMesh.m_UVInfo;
                if (m_UVInfo != 0)
                {
                    const int kInfoBitsPerUV = 4;
                    const int kUVDimensionMask = 3;
                    const int kUVChannelExists = 4;
                    const int kMaxTexCoordShaderChannels = 8;

                    int uvSrcOffset = 0;
                    for (int uv = 0; uv < kMaxTexCoordShaderChannels; uv++)
                    {
                        var texCoordBits = m_UVInfo >> (uv * kInfoBitsPerUV);
                        texCoordBits &= (1u << kInfoBitsPerUV) - 1u;
                        if ((texCoordBits & kUVChannelExists) != 0)
                        {
                            var uvDim = 1 + (int)(texCoordBits & kUVDimensionMask);
                            var m_UV = m_CompressedMesh.m_UV.UnpackFloats(uvDim, uvDim * 4, uvSrcOffset, m_VertexCount);
                            SetUV(uv, m_UV);
                            uvSrcOffset += uvDim * m_VertexCount;
                        }
                    }
                }
                else
                {
                    m_UV0 = m_CompressedMesh.m_UV.UnpackFloats(2, 2 * 4, 0, m_VertexCount);
                    if (m_CompressedMesh.m_UV.m_NumItems >= m_VertexCount * 4)
                    {
                        m_UV1 = m_CompressedMesh.m_UV.UnpackFloats(2, 2 * 4, m_VertexCount * 2, m_VertexCount);
                    }
                }
            }
            //BindPose
            if (version[0] < 5)
            {
                if (m_CompressedMesh.m_BindPoses.m_NumItems > 0)
                {
                    m_BindPose = new Matrix4x4[m_CompressedMesh.m_BindPoses.m_NumItems / 16];
                    var m_BindPoses_Unpacked = m_CompressedMesh.m_BindPoses.UnpackFloats(16, 4 * 16);
                    var buffer = new float[16];
                    for (int i = 0; i < m_BindPose.Length; i++)
                    {
                        Array.Copy(m_BindPoses_Unpacked, i * 16, buffer, 0, 16);
                        m_BindPose[i] = new Matrix4x4(buffer);
                    }
                }
            }
            //Normal
            if (m_CompressedMesh.m_Normals.m_NumItems > 0)
            {
                var normalData = m_CompressedMesh.m_Normals.UnpackFloats(2, 4 * 2);
                var signs = m_CompressedMesh.m_NormalSigns.UnpackInts();
                m_Normals = new float[m_CompressedMesh.m_Normals.m_NumItems / 2 * 3];
                for (int i = 0; i < m_CompressedMesh.m_Normals.m_NumItems / 2; ++i)
                {
                    var x = normalData[i * 2 + 0];
                    var y = normalData[i * 2 + 1];
                    var zsqr = 1 - x * x - y * y;
                    float z;
                    if (zsqr >= 0f)
                        z = (float)Math.Sqrt(zsqr);
                    else
                    {
                        z = 0;
                        var normal = new Vector3(x, y, z);
                        normal.Normalize();
                        x = normal.X;
                        y = normal.Y;
                        z = normal.Z;
                    }
                    if (signs[i] == 0)
                        z = -z;
                    m_Normals[i * 3] = x;
                    m_Normals[i * 3 + 1] = y;
                    m_Normals[i * 3 + 2] = z;
                }
            }
            //Tangent
            if (m_CompressedMesh.m_Tangents.m_NumItems > 0)
            {
                var tangentData = m_CompressedMesh.m_Tangents.UnpackFloats(2, 4 * 2);
                var signs = m_CompressedMesh.m_TangentSigns.UnpackInts();
                m_Tangents = new float[m_CompressedMesh.m_Tangents.m_NumItems / 2 * 4];
                for (int i = 0; i < m_CompressedMesh.m_Tangents.m_NumItems / 2; ++i)
                {
                    var x = tangentData[i * 2 + 0];
                    var y = tangentData[i * 2 + 1];
                    var zsqr = 1 - x * x - y * y;
                    float z;
                    if (zsqr >= 0f)
                        z = (float)Math.Sqrt(zsqr);
                    else
                    {
                        z = 0;
                        var vector3f = new Vector3(x, y, z);
                        vector3f.Normalize();
                        x = vector3f.X;
                        y = vector3f.Y;
                        z = vector3f.Z;
                    }
                    if (signs[i * 2 + 0] == 0)
                        z = -z;
                    var w = signs[i * 2 + 1] > 0 ? 1.0f : -1.0f;
                    m_Tangents[i * 4] = x;
                    m_Tangents[i * 4 + 1] = y;
                    m_Tangents[i * 4 + 2] = z;
                    m_Tangents[i * 4 + 3] = w;
                }
            }
            //FloatColor
            if (version[0] >= 5)
            {
                if (m_CompressedMesh.m_FloatColors.m_NumItems > 0)
                {
                    m_Colors = m_CompressedMesh.m_FloatColors.UnpackFloats(1, 4);
                }
            }
            //Skin
            if (m_CompressedMesh.m_Weights.m_NumItems > 0)
            {
                var weights = m_CompressedMesh.m_Weights.UnpackInts();
                var boneIndices = m_CompressedMesh.m_BoneIndices.UnpackInts();

                InitMSkin();

                int bonePos = 0;
                int boneIndexPos = 0;
                int j = 0;
                int sum = 0;

                for (int i = 0; i < m_CompressedMesh.m_Weights.m_NumItems; i++)
                {
                    //read bone index and weight.
                    m_Skin[bonePos].weight[j] = weights[i] / 31.0f;
                    m_Skin[bonePos].boneIndex[j] = boneIndices[boneIndexPos++];
                    j++;
                    sum += weights[i];

                    //the weights add up to one. fill the rest for this vertex with zero, and continue with next one.
                    if (sum >= 31)
                    {
                        for (; j < 4; j++)
                        {
                            m_Skin[bonePos].weight[j] = 0;
                            m_Skin[bonePos].boneIndex[j] = 0;
                        }
                        bonePos++;
                        j = 0;
                        sum = 0;
                    }
                    //we read three weights, but they don't add up to one. calculate the fourth one, and read
                    //missing bone index. continue with next vertex.
                    else if (j == 3)
                    {
                        m_Skin[bonePos].weight[j] = (31 - sum) / 31.0f;
                        m_Skin[bonePos].boneIndex[j] = boneIndices[boneIndexPos++];
                        bonePos++;
                        j = 0;
                        sum = 0;
                    }
                }
            }
            //IndexBuffer
            if (m_CompressedMesh.m_Triangles.m_NumItems > 0)
            {
                m_IndexBuffer = Array.ConvertAll(m_CompressedMesh.m_Triangles.UnpackInts(), x => (uint)x);
            }
            //Color
            if (m_CompressedMesh.m_Colors?.m_NumItems > 0)
            {
                m_CompressedMesh.m_Colors.m_NumItems *= 4;
                m_CompressedMesh.m_Colors.m_BitSize /= 4;
                var tempColors = m_CompressedMesh.m_Colors.UnpackInts();
                m_Colors = new float[m_CompressedMesh.m_Colors.m_NumItems];
                for (int v = 0; v < m_CompressedMesh.m_Colors.m_NumItems; v++)
                {
                    m_Colors[v] = tempColors[v] / 255f;
                }
            }
        }

        private void GetTriangles()
        {
            foreach (var m_SubMesh in m_SubMeshes)
            {
                var firstIndex = m_SubMesh.firstByte / 2;
                if (!m_Use16BitIndices)
                {
                    firstIndex /= 2;
                }
                var indexCount = m_SubMesh.indexCount;
                var topology = m_SubMesh.topology;
                if (topology == GfxPrimitiveType.Triangles)
                {
                    for (int i = 0; i < indexCount; i += 3)
                    {
                        m_Indices.Add(m_IndexBuffer[firstIndex + i]);
                        m_Indices.Add(m_IndexBuffer[firstIndex + i + 1]);
                        m_Indices.Add(m_IndexBuffer[firstIndex + i + 2]);
                    }
                }
                else if (version[0] < 4 || topology == GfxPrimitiveType.TriangleStrip)
                {
                    // de-stripify :
                    uint triIndex = 0;
                    for (int i = 0; i < indexCount - 2; i++)
                    {
                        var a = m_IndexBuffer[firstIndex + i];
                        var b = m_IndexBuffer[firstIndex + i + 1];
                        var c = m_IndexBuffer[firstIndex + i + 2];

                        // skip degenerates
                        if (a == b || a == c || b == c)
                            continue;

                        // do the winding flip-flop of strips :
                        if ((i & 1) == 1)
                        {
                            m_Indices.Add(b);
                            m_Indices.Add(a);
                        }
                        else
                        {
                            m_Indices.Add(a);
                            m_Indices.Add(b);
                        }
                        m_Indices.Add(c);
                        triIndex += 3;
                    }
                    //fix indexCount
                    m_SubMesh.indexCount = triIndex;
                }
                else if (topology == GfxPrimitiveType.Quads)
                {
                    for (int q = 0; q < indexCount; q += 4)
                    {
                        m_Indices.Add(m_IndexBuffer[firstIndex + q]);
                        m_Indices.Add(m_IndexBuffer[firstIndex + q + 1]);
                        m_Indices.Add(m_IndexBuffer[firstIndex + q + 2]);
                        m_Indices.Add(m_IndexBuffer[firstIndex + q]);
                        m_Indices.Add(m_IndexBuffer[firstIndex + q + 2]);
                        m_Indices.Add(m_IndexBuffer[firstIndex + q + 3]);
                    }
                    //fix indexCount
                    m_SubMesh.indexCount = indexCount / 2 * 3;
                }
                else
                {
                    throw new NotSupportedException("Failed getting triangles. Submesh topology is lines or points.");
                }
            }
        }

        private void InitMSkin()
        {
            m_Skin = new List<BoneWeights4>();
            for (int i = 0; i < m_VertexCount; i++)
            {
                m_Skin.Add(new BoneWeights4());
            }
        }

        private void SetUV(int uv, float[] m_UV)
        {
            switch (uv)
            {
                case 0:
                    m_UV0 = m_UV;
                    break;
                case 1:
                    m_UV1 = m_UV;
                    break;
                case 2:
                    m_UV2 = m_UV;
                    break;
                case 3:
                    m_UV3 = m_UV;
                    break;
                case 4:
                    m_UV4 = m_UV;
                    break;
                case 5:
                    m_UV5 = m_UV;
                    break;
                case 6:
                    m_UV6 = m_UV;
                    break;
                case 7:
                    m_UV7 = m_UV;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public float[] GetUV(int uv)
        {
            switch (uv)
            {
                case 0:
                    return m_UV0;
                case 1:
                    return m_UV1;
                case 2:
                    return m_UV2;
                case 3:
                    return m_UV3;
                case 4:
                    return m_UV4;
                case 5:
                    return m_UV5;
                case 6:
                    return m_UV6;
                case 7:
                    return m_UV7;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public static class MeshHelper
    {
        public enum VertexChannelFormat
        {
            Float,
            Float16,
            Color,
            Byte,
            UInt32
        }

        public enum VertexFormat2017
        {
            Float,
            Float16,
            Color,
            UNorm8,
            SNorm8,
            UNorm16,
            SNorm16,
            UInt8,
            SInt8,
            UInt16,
            SInt16,
            UInt32,
            SInt32
        }

        public enum VertexFormat
        {
            Float,
            Float16,
            UNorm8,
            SNorm8,
            UNorm16,
            SNorm16,
            UInt8,
            SInt8,
            UInt16,
            SInt16,
            UInt32,
            SInt32
        }

        public static VertexFormat ToVertexFormat(int format, int[] version)
        {
            if (version[0] < 2017)
            {
                switch ((VertexChannelFormat)format)
                {
                    case VertexChannelFormat.Float:
                        return VertexFormat.Float;
                    case VertexChannelFormat.Float16:
                        return VertexFormat.Float16;
                    case VertexChannelFormat.Color: //in 4.x is size 4
                        return VertexFormat.UNorm8;
                    case VertexChannelFormat.Byte:
                        return VertexFormat.UInt8;
                    case VertexChannelFormat.UInt32: //in 5.x
                        return VertexFormat.UInt32;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(format), format, null);
                }
            }
            else if (version[0] < 2019)
            {
                switch ((VertexFormat2017)format)
                {
                    case VertexFormat2017.Float:
                        return VertexFormat.Float;
                    case VertexFormat2017.Float16:
                        return VertexFormat.Float16;
                    case VertexFormat2017.Color:
                    case VertexFormat2017.UNorm8:
                        return VertexFormat.UNorm8;
                    case VertexFormat2017.SNorm8:
                        return VertexFormat.SNorm8;
                    case VertexFormat2017.UNorm16:
                        return VertexFormat.UNorm16;
                    case VertexFormat2017.SNorm16:
                        return VertexFormat.SNorm16;
                    case VertexFormat2017.UInt8:
                        return VertexFormat.UInt8;
                    case VertexFormat2017.SInt8:
                        return VertexFormat.SInt8;
                    case VertexFormat2017.UInt16:
                        return VertexFormat.UInt16;
                    case VertexFormat2017.SInt16:
                        return VertexFormat.SInt16;
                    case VertexFormat2017.UInt32:
                        return VertexFormat.UInt32;
                    case VertexFormat2017.SInt32:
                        return VertexFormat.SInt32;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(format), format, null);
                }
            }
            else
            {
                return (VertexFormat)format;
            }
        }


        public static uint GetFormatSize(VertexFormat format)
        {
            switch (format)
            {
                case VertexFormat.Float:
                case VertexFormat.UInt32:
                case VertexFormat.SInt32:
                    return 4u;
                case VertexFormat.Float16:
                case VertexFormat.UNorm16:
                case VertexFormat.SNorm16:
                case VertexFormat.UInt16:
                case VertexFormat.SInt16:
                    return 2u;
                case VertexFormat.UNorm8:
                case VertexFormat.SNorm8:
                case VertexFormat.UInt8:
                case VertexFormat.SInt8:
                    return 1u;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }

        public static bool IsIntFormat(VertexFormat format)
        {
            return format >= VertexFormat.UInt8;
        }

        public static float[] BytesToFloatArray(byte[] inputBytes, VertexFormat format)
        {
            var size = GetFormatSize(format);
            var len = inputBytes.Length / size;
            var result = new float[len];
            for (int i = 0; i < len; i++)
            {
                switch (format)
                {
                    case VertexFormat.Float:
                        result[i] = BinaryPrimitives.ReadSingleLittleEndian(inputBytes.AsSpan(i * 4));
                        break;
                    case VertexFormat.Float16:
                        result[i] = Half.ToHalf(inputBytes, i * 2);
                        break;
                    case VertexFormat.UNorm8:
                        result[i] = inputBytes[i] / 255f;
                        break;
                    case VertexFormat.SNorm8:
                        result[i] = Math.Max((sbyte)inputBytes[i] / 127f, -1f);
                        break;
                    case VertexFormat.UNorm16:
                        result[i] = BinaryPrimitives.ReadUInt16LittleEndian(inputBytes.AsSpan(i * 2)) / 65535f;
                        break;
                    case VertexFormat.SNorm16:
                        result[i] = Math.Max(BinaryPrimitives.ReadInt16LittleEndian(inputBytes.AsSpan(i * 2)) / 32767f, -1f);
                        break;
                }
            }
            return result;
        }

        public static int[] BytesToIntArray(byte[] inputBytes, VertexFormat format)
        {
            var size = GetFormatSize(format);
            var len = inputBytes.Length / size;
            var result = new int[len];
            for (int i = 0; i < len; i++)
            {
                switch (format)
                {
                    case VertexFormat.UInt8:
                    case VertexFormat.SInt8:
                        result[i] = inputBytes[i];
                        break;
                    case VertexFormat.UInt16:
                    case VertexFormat.SInt16:
                        result[i] = BinaryPrimitives.ReadInt16LittleEndian(inputBytes.AsSpan(i * 2));
                        break;
                    case VertexFormat.UInt32:
                    case VertexFormat.SInt32:
                        result[i] = BinaryPrimitives.ReadInt32LittleEndian(inputBytes.AsSpan(i * 4));
                        break;
                }
            }
            return result;
        }

        // From: https://github.com/Hororiya/YarikStudio/blob/main/AssetStudio/Classes/Mesh.cs#L1535
        public static float[] DecompressEndfieldNormal(byte[] inputBytes, VertexFormat format) // 8bits per component
        {
            var size = GetFormatSize(format);
            var len = inputBytes.Length / size;
            var result = new float[len * 3];
            var readFloat = new float[len];
            readFloat = BytesToFloatArray(inputBytes, format);

            for (int i = 0; i < len; i++)
            {
                float value = readFloat[i];

                float r0x = BitConverter.ToInt32(BitConverter.GetBytes(value)) & 0x40000000;
                r0x = (BitConverter.ToUInt32(BitConverter.GetBytes(r0x)) > 0) ? 1.0f : 0.0f;

                // (((int3)v2.xxx << (32 - int3(10,10,10) - int3(0,10,20))) >> (32 - int3(10,10,10)))
                float r0y = (BitConverter.ToInt32(BitConverter.GetBytes(value)) << 22) >> 22;
                float r0z = (BitConverter.ToInt32(BitConverter.GetBytes(value)) << 12) >> 22;
                float r0w = (BitConverter.ToInt32(BitConverter.GetBytes(value)) << 2) >> 22;

                float r1x = (BitConverter.ToUInt32(BitConverter.GetBytes(value))) >> 31;

                float r1y = 0.00195694715f * r0y;
                float r1z = 0.00195694715f * r0z;
                float r1w = 0.00195694715f * r0w;

                float leng = r1x * r1x + r1y * r1y + r1z * r1z + r1w * r1w;

                float r2x = 1.0f - Math.Abs(r1y);
                float r2y = 1.0f - Math.Abs(r1z);
                float r2z = 1.0f - Math.Abs(r1y);

                float r3z = r2x - Math.Abs(r1z);

                r2x = r3z < 0.0f ? 1.0f : 0.0f;

                r0y = r0y >= 0.0f ? 1.0f : 0.0f;
                r0z = r0z >= 0.0f ? 1.0f : 0.0f;

                r0y = r0y * 2.0f - 1.0f;
                r0z = r0z * 2.0f - 1.0f;

                r0y = r2y * r0y;
                r0z = r2z * r0z;

                float r3x = (r2x == 1.0f) ? r0y : r1y;
                float r3y = (r2x == 1.0f) ? r0z : r1z;

                r0y = r3x * r3x + r3y * r3y + r3z * r3z;
                r0y = 1.0f / (float)Math.Sqrt(r0y);

                r2x = r3x * r0y;
                r2y = r3y * r0y;
                r2z = r3z * r0y;

                // 计算result[i * 3]的值
                result[i * 3] = r2x;
                result[i * 3 + 1] = r2y;
                result[i * 3 + 2] = r2z;
            }
            return result;
        }
    }
}
