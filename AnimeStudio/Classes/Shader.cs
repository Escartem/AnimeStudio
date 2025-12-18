using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AnimeStudio
{
    public class Hash128
    {
        public byte[] bytes;

        public Hash128(EndianBinaryReader reader)
        {
            bytes = reader.ReadBytes(16);
        }
    }

    public class StructParameter
    {
        public List<MatrixParameter> m_MatrixParams;
        public List<VectorParameter> m_VectorParams;

        public StructParameter(EndianBinaryReader reader)
        {
            var m_NameIndex = reader.ReadInt32();
            var m_Index = reader.ReadInt32();
            var m_ArraySize = reader.ReadInt32();
            var m_StructSize = reader.ReadInt32();

            int numVectorParams = reader.ReadInt32();
            m_VectorParams = new List<VectorParameter>();
            for (int i = 0; i < numVectorParams; i++)
            {
                m_VectorParams.Add(new VectorParameter(reader));
            }

            int numMatrixParams = reader.ReadInt32();
            m_MatrixParams = new List<MatrixParameter>();
            for (int i = 0; i < numMatrixParams; i++)
            {
                m_MatrixParams.Add(new MatrixParameter(reader));
            }
        }
    }

    public class SamplerParameter
    {
        public uint sampler;
        public int bindPoint;

        public SamplerParameter(EndianBinaryReader reader)
        {
            sampler = reader.ReadUInt32();
            bindPoint = reader.ReadInt32();
        }
    }
    public enum TextureDimension
    {
        Unknown = -1,
        None = 0,
        Any = 1,
        Tex2D = 2,
        Tex3D = 3,
        Cube = 4,
        Tex2DArray = 5,
        CubeArray = 6
    };

    public class SerializedTextureProperty
    {
        public string m_DefaultName;
        public TextureDimension m_TexDim;

        public SerializedTextureProperty(EndianBinaryReader reader)
        {
            m_DefaultName = reader.ReadAlignedString();
            m_TexDim = (TextureDimension)reader.ReadInt32();
        }
    }

    public enum SerializedPropertyType
    {
        Color = 0,
        Vector = 1,
        Float = 2,
        Range = 3,
        Texture = 4,
        Int = 5
    };

    [Flags]
    public enum SerializedPropertyFlag
    {
        HideInInspector = 1 << 0,
        PerRendererData = 1 << 1,
        NoScaleOffset = 1 << 2,
        Normal = 1 << 3,
        HDR = 1 << 4,
        Gamma = 1 << 5,
        NonModifiableTextureData = 1 << 6,
        MainTexture = 1 << 7,
        MainColor = 1 << 8,
    }

    public class SerializedProperty
    {
        public string m_Name;
        public string m_Description;
        public string[] m_Attributes;
        public SerializedPropertyType m_Type;
        public SerializedPropertyFlag m_Flags;
        public float[] m_DefValue;
        public SerializedTextureProperty m_DefTexture;

        public SerializedProperty(EndianBinaryReader reader)
        {
            m_Name = reader.ReadAlignedString();
            m_Description = reader.ReadAlignedString();
            m_Attributes = reader.ReadStringArray();
            m_Type = (SerializedPropertyType)reader.ReadInt32();
            m_Flags = (SerializedPropertyFlag)reader.ReadUInt32();
            m_DefValue = reader.ReadSingleArray(4);
            m_DefTexture = new SerializedTextureProperty(reader);
        }
    }

    public class SerializedProperties
    {
        public List<SerializedProperty> m_Props;

        public SerializedProperties(EndianBinaryReader reader)
        {
            int numProps = reader.ReadInt32();
            m_Props = new List<SerializedProperty>();
            for (int i = 0; i < numProps; i++)
            {
                m_Props.Add(new SerializedProperty(reader));
            }
        }
    }

    public class SerializedShaderFloatValue
    {
        public float val;
        public string name;

        public SerializedShaderFloatValue(EndianBinaryReader reader)
        {
            val = reader.ReadSingle();
            name = reader.ReadAlignedString();
        }
    }

    public class SerializedShaderRTBlendState
    {
        public SerializedShaderFloatValue srcBlend;
        public SerializedShaderFloatValue destBlend;
        public SerializedShaderFloatValue srcBlendAlpha;
        public SerializedShaderFloatValue destBlendAlpha;
        public SerializedShaderFloatValue blendOp;
        public SerializedShaderFloatValue blendOpAlpha;
        public SerializedShaderFloatValue colMask;

        public SerializedShaderRTBlendState(EndianBinaryReader reader)
        {
            srcBlend = new SerializedShaderFloatValue(reader);
            destBlend = new SerializedShaderFloatValue(reader);
            srcBlendAlpha = new SerializedShaderFloatValue(reader);
            destBlendAlpha = new SerializedShaderFloatValue(reader);
            blendOp = new SerializedShaderFloatValue(reader);
            blendOpAlpha = new SerializedShaderFloatValue(reader);
            colMask = new SerializedShaderFloatValue(reader);
        }
    }

    public class SerializedStencilOp
    {
        public SerializedShaderFloatValue pass;
        public SerializedShaderFloatValue fail;
        public SerializedShaderFloatValue zFail;
        public SerializedShaderFloatValue comp;

        public SerializedStencilOp(EndianBinaryReader reader)
        {
            pass = new SerializedShaderFloatValue(reader);
            fail = new SerializedShaderFloatValue(reader);
            zFail = new SerializedShaderFloatValue(reader);
            comp = new SerializedShaderFloatValue(reader);
        }
    }

    public class SerializedShaderVectorValue
    {
        public SerializedShaderFloatValue x;
        public SerializedShaderFloatValue y;
        public SerializedShaderFloatValue z;
        public SerializedShaderFloatValue w;
        public string name;

        public SerializedShaderVectorValue(EndianBinaryReader reader)
        {
            x = new SerializedShaderFloatValue(reader);
            y = new SerializedShaderFloatValue(reader);
            z = new SerializedShaderFloatValue(reader);
            w = new SerializedShaderFloatValue(reader);
            name = reader.ReadAlignedString();
        }
    }

    public enum FogMode
    {
        Unknown = -1,
        Disabled = 0,
        Linear = 1,
        Exp = 2,
        Exp2 = 3
    };

    public class SerializedShaderState
    {
        public string m_Name;
        public List<SerializedShaderRTBlendState> rtBlend;
        public bool rtSeparateBlend;
        public SerializedShaderFloatValue zClip;
        public SerializedShaderFloatValue zTest;
        public SerializedShaderFloatValue zWrite;
        public SerializedShaderFloatValue culling;
        public SerializedShaderFloatValue conservative;
        public SerializedShaderFloatValue offsetFactor;
        public SerializedShaderFloatValue offsetUnits;
        public SerializedShaderFloatValue alphaToMask;
        public SerializedStencilOp stencilOp;
        public SerializedStencilOp stencilOpFront;
        public SerializedStencilOp stencilOpBack;
        public SerializedShaderFloatValue stencilReadMask;
        public SerializedShaderFloatValue stencilWriteMask;
        public SerializedShaderFloatValue stencilRef;
        public SerializedShaderFloatValue fogStart;
        public SerializedShaderFloatValue fogEnd;
        public SerializedShaderFloatValue fogDensity;
        public SerializedShaderVectorValue fogColor;
        public FogMode fogMode;
        public int gpuProgramID;
        public SerializedTagMap m_Tags;
        public int m_LOD;
        public bool lighting;

        public SerializedShaderState(ObjectReader reader)
        {
            var version = reader.version;

            m_Name = reader.ReadAlignedString();
            rtBlend = new List<SerializedShaderRTBlendState>();
            for (int i = 0; i < 8; i++)
            {
                rtBlend.Add(new SerializedShaderRTBlendState(reader));
            }
            rtSeparateBlend = reader.ReadBoolean();
            reader.AlignStream();
            if (version[0] > 2017 || (version[0] == 2017 && version[1] >= 2)) //2017.2 and up
            {
                zClip = new SerializedShaderFloatValue(reader);
            }
            zTest = new SerializedShaderFloatValue(reader);
            zWrite = new SerializedShaderFloatValue(reader);
            culling = new SerializedShaderFloatValue(reader);
            if (version[0] >= 2020) //2020.1 and up
            {
                conservative = new SerializedShaderFloatValue(reader);
            }
            offsetFactor = new SerializedShaderFloatValue(reader);
            offsetUnits = new SerializedShaderFloatValue(reader);
            alphaToMask = new SerializedShaderFloatValue(reader);
            stencilOp = new SerializedStencilOp(reader);
            stencilOpFront = new SerializedStencilOp(reader);
            stencilOpBack = new SerializedStencilOp(reader);
            stencilReadMask = new SerializedShaderFloatValue(reader);
            stencilWriteMask = new SerializedShaderFloatValue(reader);
            stencilRef = new SerializedShaderFloatValue(reader);
            fogStart = new SerializedShaderFloatValue(reader);
            fogEnd = new SerializedShaderFloatValue(reader);
            fogDensity = new SerializedShaderFloatValue(reader);
            fogColor = new SerializedShaderVectorValue(reader);
            fogMode = (FogMode)reader.ReadInt32();
            gpuProgramID = reader.ReadInt32();
            m_Tags = new SerializedTagMap(reader);
            m_LOD = reader.ReadInt32();
            if (reader.Game.Type.IsLoveAndDeepspace())
            {
                int numOverrideKeywordAndStage = reader.ReadInt32();
                var m_OverrideKeywordAndStage = new List<KeyValuePair<string, uint>>();
                for (int i = 0; i < numOverrideKeywordAndStage; i++)
                {
                    m_OverrideKeywordAndStage.Add(new KeyValuePair<string, uint>(reader.ReadAlignedString(), reader.ReadUInt32()));
                }
            }
            lighting = reader.ReadBoolean();
            reader.AlignStream();
        }
    }

    public class ShaderBindChannel
    {
        public sbyte source;
        public sbyte target;

        public ShaderBindChannel(EndianBinaryReader reader)
        {
            source = reader.ReadSByte();
            target = reader.ReadSByte();
        }
    }

    public class ParserBindChannels
    {
        public List<ShaderBindChannel> m_Channels;
        public uint m_SourceMap;

        public ParserBindChannels(EndianBinaryReader reader)
        {
            int numChannels = reader.ReadInt32();
            m_Channels = new List<ShaderBindChannel>();
            for (int i = 0; i < numChannels; i++)
            {
                m_Channels.Add(new ShaderBindChannel(reader));
            }
            reader.AlignStream();

            m_SourceMap = reader.ReadUInt32();
        }
    }

    public class VectorParameter
    {
        public int m_NameIndex;
        public int m_Index;
        public int m_ArraySize;
        public sbyte m_Type;
        public sbyte m_Dim;

        public VectorParameter(EndianBinaryReader reader)
        {
            m_NameIndex = reader.ReadInt32();
            m_Index = reader.ReadInt32();
            m_ArraySize = reader.ReadInt32();
            m_Type = reader.ReadSByte();
            m_Dim = reader.ReadSByte();
            reader.AlignStream();
        }
    }

    public class MatrixParameter
    {
        public int m_NameIndex;
        public int m_Index;
        public int m_ArraySize;
        public sbyte m_Type;
        public sbyte m_RowCount;

        public MatrixParameter(EndianBinaryReader reader)
        {
            m_NameIndex = reader.ReadInt32();
            m_Index = reader.ReadInt32();
            m_ArraySize = reader.ReadInt32();
            m_Type = reader.ReadSByte();
            m_RowCount = reader.ReadSByte();
            reader.AlignStream();
        }
    }

    public class TextureParameter
    {
        public int m_NameIndex;
        public int m_Index;
        public int m_SamplerIndex;
        public int m_SamplerSpace;
        public sbyte m_Dim;

        public TextureParameter(ObjectReader reader)
        {
            var version = reader.version;

            if (reader.Remaining > 0)
            {
                m_NameIndex = reader.ReadInt32();
                m_Index = reader.ReadInt32();
                if (reader.Game.Type.IsSRGroup())
                {
                    var m_RegisterSpace = reader.ReadInt32();
                    var m_BindCount = reader.ReadInt32();
                }
                m_SamplerIndex = reader.ReadInt32();
                if (reader.Game.Type.IsSR())
                {
                    m_SamplerSpace = reader.ReadInt32();
                }
                if (version[0] > 2017 || (version[0] == 2017 && version[1] >= 3)) //2017.3 and up
                {
                    var m_MultiSampled = reader.ReadBoolean();
                }
                m_Dim = reader.ReadSByte();
                reader.AlignStream();
            }
        }
    }

    public class BufferBinding
    {
        public int m_NameIndex;
        public int m_Index;
        public int m_ArraySize;

        public BufferBinding(ObjectReader reader)
        {
            var version = reader.version;

            m_NameIndex = reader.ReadInt32();
            m_Index = reader.ReadInt32();
            if (reader.Game.Type.IsSRGroup())
            {
                var m_RegisterSpace = reader.ReadInt32();
                var m_BindCount = reader.ReadInt32();
            }
            if (version[0] >= 2020) //2020.1 and up
            {
                m_ArraySize = reader.ReadInt32();
            }
        }
    }

    public class ConstantBuffer
    {
        public int m_NameIndex;
        public List<MatrixParameter> m_MatrixParams;
        public List<VectorParameter> m_VectorParams;
        public List<StructParameter> m_StructParams;
        public int m_Size;
        public bool m_IsPartialCB;

        public ConstantBuffer(ObjectReader reader)
        {
            var version = reader.version;

            m_NameIndex = reader.ReadInt32();

            int numMatrixParams = reader.ReadInt32();
            m_MatrixParams = new List<MatrixParameter>();
            for (int i = 0; i < numMatrixParams; i++)
            {
                m_MatrixParams.Add(new MatrixParameter(reader));
            }

            int numVectorParams = reader.ReadInt32();
            m_VectorParams = new List<VectorParameter>();
            for (int i = 0; i < numVectorParams; i++)
            {
                m_VectorParams.Add(new VectorParameter(reader));
            }
            if (version[0] > 2017 || (version[0] == 2017 && version[1] >= 3)) //2017.3 and up
            {
                int numStructParams = reader.ReadInt32();
                m_StructParams = new List<StructParameter>();
                for (int i = 0; i < numStructParams; i++)
                {
                    m_StructParams.Add(new StructParameter(reader));
                }
            }
            m_Size = reader.ReadInt32();

            if ((version[0] == 2020 && version[1] > 3) ||
               (version[0] == 2020 && version[1] == 3 && version[2] >= 2) || //2020.3.2f1 and up
               (version[0] > 2021) ||
               (version[0] == 2021 && version[1] > 1) ||
               (version[0] == 2021 && version[1] == 1 && version[2] >= 4)) //2021.1.4f1 and up
            {
                m_IsPartialCB = reader.ReadBoolean();
                reader.AlignStream();
            }
        }
    }

    public class UAVParameter
    {
        public int m_NameIndex;
        public int m_Index;
        public int m_OriginalIndex;

        public UAVParameter(EndianBinaryReader reader)
        {
            m_NameIndex = reader.ReadInt32();
            m_Index = reader.ReadInt32();
            m_OriginalIndex = reader.ReadInt32();
        }
    }

    public enum ShaderGpuProgramType
    {
        Unknown = 0,
        GLLegacy = 1,
        GLES31AEP = 2,
        GLES31 = 3,
        GLES3 = 4,
        GLES = 5,
        GLCore32 = 6,
        GLCore41 = 7,
        GLCore43 = 8,
        DX9VertexSM20 = 9,
        DX9VertexSM30 = 10,
        DX9PixelSM20 = 11,
        DX9PixelSM30 = 12,
        DX10Level9Vertex = 13,
        DX10Level9Pixel = 14,
        DX11VertexSM40 = 15,
        DX11VertexSM50 = 16,
        DX11PixelSM40 = 17,
        DX11PixelSM50 = 18,
        DX11GeometrySM40 = 19,
        DX11GeometrySM50 = 20,
        DX11HullSM50 = 21,
        DX11DomainSM50 = 22,
        MetalVS = 23,
        MetalFS = 24,
        SPIRV = 25,
        ConsoleVS = 26,
        ConsoleFS = 27,
        ConsoleHS = 28,
        ConsoleDS = 29,
        ConsoleGS = 30,
        RayTracing = 31,
        PS5NGGC = 32
    };

    public class SerializedProgramParameters
    {
        public List<VectorParameter> m_VectorParams;
        public List<MatrixParameter> m_MatrixParams;
        public List<TextureParameter> m_TextureParams;
        public List<BufferBinding> m_BufferParams;
        public List<ConstantBuffer> m_ConstantBuffers;
        public List<BufferBinding> m_ConstantBufferBindings;
        public List<UAVParameter> m_UAVParams;
        public List<SamplerParameter> m_Samplers;

        public SerializedProgramParameters(ObjectReader reader)
        {
            int numVectorParams = reader.ReadInt32();
            m_VectorParams = new List<VectorParameter>();
            for (int i = 0; i < numVectorParams; i++)
            {
                m_VectorParams.Add(new VectorParameter(reader));
            }

            int numMatrixParams = reader.ReadInt32();
            m_MatrixParams = new List<MatrixParameter>();
            for (int i = 0; i < numMatrixParams; i++)
            {
                m_MatrixParams.Add(new MatrixParameter(reader));
            }

            int numTextureParams = reader.ReadInt32();
            m_TextureParams = new List<TextureParameter>();
            for (int i = 0; i < numTextureParams; i++)
            {
                m_TextureParams.Add(new TextureParameter(reader));
            }

            int numBufferParams = reader.ReadInt32();
            m_BufferParams = new List<BufferBinding>();
            for (int i = 0; i < numBufferParams; i++)
            {
                m_BufferParams.Add(new BufferBinding(reader));
            }

            int numConstantBuffers = reader.ReadInt32();
            m_ConstantBuffers = new List<ConstantBuffer>();
            for (int i = 0; i < numConstantBuffers; i++)
            {
                m_ConstantBuffers.Add(new ConstantBuffer(reader));
            }

            int numConstantBufferBindings = reader.ReadInt32();
            m_ConstantBufferBindings = new List<BufferBinding>();
            for (int i = 0; i < numConstantBufferBindings; i++)
            {
                m_ConstantBufferBindings.Add(new BufferBinding(reader));
            }

            int numUAVParams = reader.ReadInt32();
            m_UAVParams = new List<UAVParameter>();
            for (int i = 0; i < numUAVParams; i++)
            {
                m_UAVParams.Add(new UAVParameter(reader));
            }

            int numSamplers = reader.ReadInt32();
            m_Samplers = new List<SamplerParameter>();
            for (int i = 0; i < numSamplers; i++)
            {
                m_Samplers.Add(new SamplerParameter(reader));
            }
        }
    }

    public class SerializedSubProgram
    {
        public uint m_BlobIndex;
        public ParserBindChannels m_Channels;
        public ushort[] m_KeywordIndices;
        public sbyte m_ShaderHardwareTier;
        public ShaderGpuProgramType m_GpuProgramType;
        public SerializedProgramParameters m_Parameters;
        public List<VectorParameter> m_VectorParams;
        public List<MatrixParameter> m_MatrixParams;
        public List<TextureParameter> m_TextureParams;
        public List<BufferBinding> m_BufferParams;
        public List<ConstantBuffer> m_ConstantBuffers;
        public List<BufferBinding> m_ConstantBufferBindings;
        public List<UAVParameter> m_UAVParams;
        public List<SamplerParameter> m_Samplers;

        public static bool HasGlobalLocalKeywordIndices(SerializedType type) => type.Match("E99740711222CD922E9A6F92FF1EB07A", "450A058C218DAF000647948F2F59DA6D", "B239746E4EC6E4D6D7BA27C84178610A", "3FD560648A91A99210D5DDF2BE320536", "66839B5040F09A101A02DDDC9E522F23", "0B07D09734C07EBABF387D3CBC8BEBF0");
        public static bool HasInstancedStructuredBuffers(SerializedType type) => type.Match("E99740711222CD922E9A6F92FF1EB07A", "B239746E4EC6E4D6D7BA27C84178610A", "3FD560648A91A99210D5DDF2BE320536", "66839B5040F09A101A02DDDC9E522F23", "0B07D09734C07EBABF387D3CBC8BEBF0");
        public static bool HasIsAdditionalBlob(SerializedType type) => type.Match("B239746E4EC6E4D6D7BA27C84178610A", "66839B5040F09A101A02DDDC9E522F23", "0B07D09734C07EBABF387D3CBC8BEBF0");
        public static bool HasProgramHash(SerializedType type) => type.Match("66839B5040F09A101A02DDDC9E522F23", "0B07D09734C07EBABF387D3CBC8BEBF0");

        public SerializedSubProgram(ObjectReader reader)
        {
            var version = reader.version;
            
            if (reader.Game.Type.IsLoveAndDeepspace())
            {
                var m_CodeHash = new Hash128(reader);
            }

            m_BlobIndex = reader.ReadUInt32();
            if (HasIsAdditionalBlob(reader.serializedType))
            {
                var m_IsAdditionalBlob = reader.ReadBoolean();
                reader.AlignStream();
            }

            if (HasProgramHash(reader.serializedType))
            {
                var m_ProgramHash = new Hash128(reader);
                var m_DependentBlobIndex = reader.ReadInt32();
            }

            m_Channels = new ParserBindChannels(reader);

            if ((version[0] >= 2019 && version[0] < 2021) || (version[0] == 2021 && version[1] < 2) || HasGlobalLocalKeywordIndices(reader.serializedType)) //2019 ~2021.1
            {
                var m_GlobalKeywordIndices = reader.ReadUInt16Array();
                reader.AlignStream();
                var m_LocalKeywordIndices = reader.ReadUInt16Array();
                reader.AlignStream();
            }
            else
            {
                m_KeywordIndices = reader.ReadUInt16Array();
                if (version[0] >= 2017) //2017 and up
                {
                    reader.AlignStream();
                }
            }

            m_ShaderHardwareTier = reader.ReadSByte();
            m_GpuProgramType = (ShaderGpuProgramType)reader.ReadSByte();
            reader.AlignStream();

            if (reader.Game.Name == "GI" && (m_GpuProgramType == ShaderGpuProgramType.Unknown || !Enum.IsDefined(typeof(ShaderGpuProgramType), m_GpuProgramType)))
            {
                reader.Position -= 4;
                var m_LocalKeywordIndices = reader.ReadUInt16Array();
                reader.AlignStream();

                m_ShaderHardwareTier = reader.ReadSByte();
                m_GpuProgramType = (ShaderGpuProgramType)reader.ReadSByte();
                reader.AlignStream();
            }

            if ((version[0] == 2020 && version[1] > 3) ||
               (version[0] == 2020 && version[1] == 3 && version[2] >= 2) || //2020.3.2f1 and up
               (version[0] > 2021) ||
               (version[0] == 2021 && version[1] > 1) ||
               (version[0] == 2021 && version[1] == 1 && version[2] >= 1)) //2021.1.1f1 and up
            {
                m_Parameters = new SerializedProgramParameters(reader);
            }
            else
            {
                try
                {
                    int numVectorParams = reader.ReadInt32();
                    m_VectorParams = new List<VectorParameter>();
                    for (int i = 0; i < numVectorParams; i++)
                    {
                        m_VectorParams.Add(new VectorParameter(reader));
                    }

                    int numMatrixParams = reader.ReadInt32();
                    m_MatrixParams = new List<MatrixParameter>();
                    for (int i = 0; i < numMatrixParams; i++)
                    {
                        m_MatrixParams.Add(new MatrixParameter(reader));
                    }

                    int numTextureParams = reader.ReadInt32();
                    m_TextureParams = new List<TextureParameter>();
                    for (int i = 0; i < numTextureParams; i++)
                    {
                        m_TextureParams.Add(new TextureParameter(reader));
                    }

                    int numBufferParams = reader.ReadInt32();
                    m_BufferParams = new List<BufferBinding>();
                    for (int i = 0; i < numBufferParams; i++)
                    {
                        m_BufferParams.Add(new BufferBinding(reader));
                    }

                    int numConstantBuffers = reader.ReadInt32();
                    m_ConstantBuffers = new List<ConstantBuffer>();
                    for (int i = 0; i < numConstantBuffers; i++)
                    {
                        m_ConstantBuffers.Add(new ConstantBuffer(reader));
                    }

                    int numConstantBufferBindings = reader.ReadInt32();
                    m_ConstantBufferBindings = new List<BufferBinding>();
                    for (int i = 0; i < numConstantBufferBindings; i++)
                    {
                        m_ConstantBufferBindings.Add(new BufferBinding(reader));
                    }

                    int numUAVParams = reader.ReadInt32();
                    m_UAVParams = new List<UAVParameter>();
                    for (int i = 0; i < numUAVParams; i++)
                    {
                        m_UAVParams.Add(new UAVParameter(reader));
                    }

                    if (version[0] >= 2017) //2017 and up
                    {
                        int numSamplers = reader.ReadInt32();
                        m_Samplers = new List<SamplerParameter>();
                        for (int i = 0; i < numSamplers; i++)
                        {
                            m_Samplers.Add(new SamplerParameter(reader));
                        }
                    }
                } catch (System.IO.EndOfStreamException)
                {
                    Logger.Error($"Cannot parse shader, no more bytes left for asset {reader.assetsFile.fileName} of {reader.assetsFile.originalPath} at path {reader.m_PathID}.");
                    return;
                }
            }

            if (reader.Remaining > 0)
            {
                if (version[0] > 2017 || (version[0] == 2017 && version[1] >= 2)) //2017.2 and up
                {
                    if (version[0] >= 2021) //2021.1 and up
                    {
                        var m_ShaderRequirements = reader.ReadInt64();
                    }
                    else
                    {
                        var m_ShaderRequirements = reader.ReadInt32();
                    }
                }

                if (HasInstancedStructuredBuffers(reader.serializedType))
                {
                    int numInstancedStructuredBuffers = reader.ReadInt32();
                    var m_InstancedStructuredBuffers = new List<ConstantBuffer>();
                    for (int i = 0; i < numInstancedStructuredBuffers; i++)
                    {
                        m_InstancedStructuredBuffers.Add(new ConstantBuffer(reader));
                    }
                }
            }
        }
    }

    public class SerializedPlayerSubProgram
    {
        public uint m_BlobIndex;
        public ushort[] m_KeywordIndices;
        public long m_ShaderRequirements;
        public ShaderGpuProgramType m_GpuProgramType;

        public SerializedPlayerSubProgram(ObjectReader reader)
        {
            m_BlobIndex = reader.ReadUInt32();

            m_KeywordIndices = reader.ReadUInt16Array();
            reader.AlignStream();

            m_ShaderRequirements = reader.ReadInt64();
            m_GpuProgramType = (ShaderGpuProgramType)reader.ReadSByte();
            reader.AlignStream();
        }
    }

    public class SerializedProgram
    {
        public List<SerializedSubProgram> m_SubPrograms;
        public List<List<SerializedPlayerSubProgram>> m_PlayerSubPrograms;
        public uint[][] m_ParameterBlobIndices;
        public SerializedProgramParameters m_CommonParameters;
        public ushort[] m_SerializedKeywordStateMask;

        public SerializedProgram(ObjectReader reader)
        {
            var version = reader.version;

            if (reader.Remaining < 4)
            {
                return;
            }

            int numSubPrograms = reader.ReadInt32();
            m_SubPrograms = new List<SerializedSubProgram>();
            for (int i = 0; i < numSubPrograms; i++)
            {
                m_SubPrograms.Add(new SerializedSubProgram(reader));
            }

            if ((version[0] == 2021 && version[1] > 3) ||
               version[0] == 2021 && version[1] == 3 && version[2] >= 10 || //2021.3.10f1 and up
               (version[0] == 2022 && version[1] > 1) ||
               version[0] == 2022 && version[1] == 1 && version[2] >= 13) //2022.1.13f1 and up
            {
                int numPlayerSubPrograms = reader.ReadInt32();
                m_PlayerSubPrograms = new List<List<SerializedPlayerSubProgram>>();
                for (int i = 0; i < numPlayerSubPrograms; i++)
                {
                    m_PlayerSubPrograms.Add(new List<SerializedPlayerSubProgram>());
                    int numPlatformPrograms = reader.ReadInt32();
                    for (int j = 0; j < numPlatformPrograms; j++)
                    {
                        m_PlayerSubPrograms[i].Add(new SerializedPlayerSubProgram(reader));
                    }
                }

                m_ParameterBlobIndices = reader.ReadUInt32ArrayArray();
            }

            if ((version[0] == 2020 && version[1] > 3) ||
               (version[0] == 2020 && version[1] == 3 && version[2] >= 2) || //2020.3.2f1 and up
               (version[0] > 2021) ||
               (version[0] == 2021 && version[1] > 1) ||
               (version[0] == 2021 && version[1] == 1 && version[2] >= 1)) //2021.1.1f1 and up
            {
                m_CommonParameters = new SerializedProgramParameters(reader);
            }

            if (version[0] > 2022 || (version[0] == 2022 && version[1] >= 1)) //2022.1 and up
            {
                m_SerializedKeywordStateMask = reader.ReadUInt16Array();
                reader.AlignStream();
            }
        }
    }

    public enum PassType
    {
        Normal = 0,
        Use = 1,
        Grab = 2
    };

    public class SerializedPass
    {
        public List<Hash128> m_EditorDataHash;
        public byte[] m_Platforms;
        public ushort[] m_LocalKeywordMask;
        public ushort[] m_GlobalKeywordMask;
        public List<KeyValuePair<string, int>> m_NameIndices;
        public PassType m_Type;
        public SerializedShaderState m_State;
        public uint m_ProgramMask;
        public SerializedProgram progVertex;
        public SerializedProgram progFragment;
        public SerializedProgram progGeometry;
        public SerializedProgram progHull;
        public SerializedProgram progDomain;
        public SerializedProgram progRayTracing;
        public bool m_HasInstancingVariant;
        public string m_UseName;
        public string m_Name;
        public string m_TextureName;
        public SerializedTagMap m_Tags;
        public ushort[] m_SerializedKeywordStateMask;

        public SerializedPass(ObjectReader reader)
        {
            var version = reader.version;

            if (version[0] > 2020 || (version[0] == 2020 && version[1] >= 2)) //2020.2 and up
            {
                int numEditorDataHash = reader.ReadInt32();
                m_EditorDataHash = new List<Hash128>();
                for (int i = 0; i < numEditorDataHash; i++)
                {
                    m_EditorDataHash.Add(new Hash128(reader));
                }
                reader.AlignStream();
                m_Platforms = reader.ReadUInt8Array();
                reader.AlignStream();
                if (version[0] < 2021 || (version[0] == 2021 && version[1] < 2)) //2021.1 and down
                {
                    m_LocalKeywordMask = reader.ReadUInt16Array();
                    reader.AlignStream();
                    m_GlobalKeywordMask = reader.ReadUInt16Array();
                    reader.AlignStream();
                }
            }

            int numIndices = reader.ReadInt32();
            m_NameIndices = new List<KeyValuePair<string, int>>();
            for (int i = 0; i < numIndices; i++)
            {
                m_NameIndices.Add(new KeyValuePair<string, int>(reader.ReadAlignedString(), reader.ReadInt32()));
            }

            m_Type = (PassType)reader.ReadInt32();
            m_State = new SerializedShaderState(reader);
            m_ProgramMask = reader.ReadUInt32();
            progVertex = new SerializedProgram(reader);
            progFragment = new SerializedProgram(reader);
            progGeometry = new SerializedProgram(reader);
            progHull = new SerializedProgram(reader);
            progDomain = new SerializedProgram(reader);
            if (version[0] > 2019 || (version[0] == 2019 && version[1] >= 3)) //2019.3 and up
            {
                progRayTracing = new SerializedProgram(reader);
            }
            m_HasInstancingVariant = reader.ReadBoolean();
            if (version[0] >= 2018) //2018 and up
            {
                var m_HasProceduralInstancingVariant = reader.ReadBoolean();
            }
            if (reader.Game.Type.IsSR())
            {
                var m_HasMultiDrawVariant = reader.ReadBoolean();
            }
            reader.AlignStream();
            m_UseName = reader.ReadAlignedString();
            m_Name = reader.ReadAlignedString();
            m_TextureName = reader.ReadAlignedString();
            m_Tags = new SerializedTagMap(reader);
            if (version[0] == 2021 && version[1] >= 2) //2021.2 ~2021.x
            {
                m_SerializedKeywordStateMask = reader.ReadUInt16Array();
                reader.AlignStream();
            }
        }
    }

    public class SerializedTagMap
    {
        public List<KeyValuePair<string, string>> tags;

        public SerializedTagMap(EndianBinaryReader reader)
        {
            int numTags = reader.ReadInt32();
            tags = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < numTags; i++)
            {
                tags.Add(new KeyValuePair<string, string>(reader.ReadAlignedString(), reader.ReadAlignedString()));
            }
        }
    }

    public class SerializedSubShader
    {
        public List<SerializedPass> m_Passes;
        public SerializedTagMap m_Tags;
        public int m_LOD;

        public SerializedSubShader(ObjectReader reader)
        {
            int numPasses = reader.ReadInt32();
            m_Passes = new List<SerializedPass>();
            for (int i = 0; i < numPasses; i++)
            {
                m_Passes.Add(new SerializedPass(reader));
            }

            m_Tags = new SerializedTagMap(reader);
            m_LOD = reader.ReadInt32();
        }
    }

    public class SerializedShaderDependency
    {
        public string from;
        public string to;

        public SerializedShaderDependency(EndianBinaryReader reader)
        {
            from = reader.ReadAlignedString();
            to = reader.ReadAlignedString();
        }
    }

    public class SerializedCustomEditorForRenderPipeline
    {
        public string customEditorName;
        public string renderPipelineType;

        public SerializedCustomEditorForRenderPipeline(EndianBinaryReader reader)
        {
            customEditorName = reader.ReadAlignedString();
            renderPipelineType = reader.ReadAlignedString();
        }
    }

    public class SerializedShader
    {
        public SerializedProperties m_PropInfo;
        public List<SerializedSubShader> m_SubShaders;
        public string[] m_KeywordNames;
        public byte[] m_KeywordFlags;
        public string m_Name;
        public string m_CustomEditorName;
        public string m_FallbackName;
        public List<SerializedShaderDependency> m_Dependencies;
        public List<SerializedCustomEditorForRenderPipeline> m_CustomEditorForRenderPipelines;
        public bool m_DisableNoSubshadersMessage;

        public SerializedShader(ObjectReader reader)
        {
            var version = reader.version;

            m_PropInfo = new SerializedProperties(reader);

            int numSubShaders = reader.ReadInt32();
            m_SubShaders = new List<SerializedSubShader>();
            for (int i = 0; i < numSubShaders; i++)
            {
                m_SubShaders.Add(new SerializedSubShader(reader));
            }

            if (version[0] > 2021 || (version[0] == 2021 && version[1] >= 2)) //2021.2 and up
            {
                m_KeywordNames = reader.ReadStringArray();
                m_KeywordFlags = reader.ReadUInt8Array();
                reader.AlignStream();
            }

            m_Name = reader.ReadAlignedString();
            m_CustomEditorName = reader.ReadAlignedString();
            m_FallbackName = reader.ReadAlignedString();

            int numDependencies = reader.ReadInt32();
            m_Dependencies = new List<SerializedShaderDependency>();
            for (int i = 0; i < numDependencies; i++)
            {
                m_Dependencies.Add(new SerializedShaderDependency(reader));
            }

            if (version[0] >= 2021) //2021.1 and up
            {
                int m_CustomEditorForRenderPipelinesSize = reader.ReadInt32();
                m_CustomEditorForRenderPipelines = new List<SerializedCustomEditorForRenderPipeline>();
                for (int i = 0; i < m_CustomEditorForRenderPipelinesSize; i++)
                {
                    m_CustomEditorForRenderPipelines.Add(new SerializedCustomEditorForRenderPipeline(reader));
                }
            }

            m_DisableNoSubshadersMessage = reader.ReadBoolean();
            reader.AlignStream();
        }
    }

    public enum ShaderCompilerPlatform
    {
        None = -1,
        GL = 0,
        D3D9 = 1,
        Xbox360 = 2,
        PS3 = 3,
        D3D11 = 4,
        GLES20 = 5,
        NaCl = 6,
        Flash = 7,
        D3D11_9x = 8,
        GLES3Plus = 9,
        PSP2 = 10,
        PS4 = 11,
        XboxOne = 12,
        PSM = 13,
        Metal = 14,
        OpenGLCore = 15,
        N3DS = 16,
        WiiU = 17,
        Vulkan = 18,
        Switch = 19,
        XboxOneD3D12 = 20,
        GameCoreXboxOne = 21,
        GameCoreScarlett = 22,
        PS5 = 23,
        PS5NGGC = 24
    };

    public class ShaderPlatformInfos
    {
        public uint[] m_SubProgramInfoOffsets;
        public uint[] m_SubProgramInfoLengths;
        public byte[] m_SubProgramInfoBlob;
        public byte[] m_CompressDictionary;
        public uint[] m_SubProgramCodeOffsets;
        public uint[] m_SubProgramCodeCompressedLengths;
        public uint[] m_SubProgramCodeDecompressedLengths;
        public ulong[] m_CompressMask;
        public byte[] m_CompressedCodeBlob;

        public ShaderPlatformInfos(ObjectReader reader)
        {
            m_SubProgramInfoOffsets = reader.ReadUInt32Array();
            m_SubProgramInfoLengths = reader.ReadUInt32Array();
            m_SubProgramInfoBlob = reader.ReadUInt8Array();
            m_CompressDictionary = reader.ReadUInt8Array();
            reader.AlignStream();
            m_SubProgramCodeOffsets = reader.ReadUInt32Array();
            m_SubProgramCodeCompressedLengths = reader.ReadUInt32Array();
            m_SubProgramCodeDecompressedLengths = reader.ReadUInt32Array();
            m_CompressMask = reader.ReadUInt64Array();
            m_CompressedCodeBlob = reader.ReadUInt8Array();
            reader.AlignStream();
        }
    }

    public class Shader : NamedObject
    {
        public byte[] m_Script;
        //5.3 - 5.4
        public uint decompressedSize;
        public byte[] m_SubProgramBlob;
        //5.5 and up
        public SerializedShader m_ParsedForm;
        public ShaderCompilerPlatform[] platforms;
        public uint[][] offsets;
        public uint[][] compressedLengths;
        public uint[][] decompressedLengths;
        public byte[] compressedBlob;
        public uint[] stageCounts;
        public ShaderPlatformInfos[] platformInfos;

        public override string Name => m_ParsedForm?.m_Name ?? m_Name;
        // public static bool HasPlatformInfos(SerializedType type) => type.Match("D114ED797139152A2E4A42339CF4AA8E"); // Star Rail

        public Shader(ObjectReader reader) : base(reader)
        {
            Logger.Info($"Reading Shader: {Convert.ToHexString(reader.serializedType.m_OldTypeHash)}");
            if (version[0] == 5 && version[1] >= 5 || version[0] > 5) //5.5 and up
            {
                try
                {
                    m_ParsedForm = new SerializedShader(reader);
                } catch (System.IO.EndOfStreamException)
                {
                    Logger.Error($"Cannot parse shader, no more bytes left for asset {reader.assetsFile.fileName} of {reader.assetsFile.originalPath} at path {reader.m_PathID}.");
                    return;
                }
                platforms = reader.ReadUInt32Array().Select(x => (ShaderCompilerPlatform)x).ToArray();
                if (reader.Game.Type.IsSRGroup())
                {
                    int numPlatformInfos = reader.ReadInt32();
                    platformInfos = new ShaderPlatformInfos[numPlatformInfos];

                    for (int i = 0; i < numPlatformInfos; i++)
                    {
                        platformInfos[i] = new ShaderPlatformInfos(reader);
                    }
                }
                else
                {
                    if (version[0] > 2019 || (version[0] == 2019 && version[1] >= 3)) //2019.3 and up
                    {
                        offsets = reader.ReadUInt32ArrayArray();
                        compressedLengths = reader.ReadUInt32ArrayArray();
                        decompressedLengths = reader.ReadUInt32ArrayArray();
                    }
                    else
                    {
                        offsets = reader.ReadUInt32Array().Select(x => new[] { x }).ToArray();
                        compressedLengths = reader.ReadUInt32Array().Select(x => new[] { x }).ToArray();
                        decompressedLengths = reader.ReadUInt32Array().Select(x => new[] { x }).ToArray();
                    }

                    compressedBlob = reader.ReadUInt8Array();
                    reader.AlignStream();
                    if (reader.Game.Type.IsGISubGroup())
                    {
                        if (BinaryPrimitives.ReadInt32LittleEndian(compressedBlob) == -1)
                        {
                            compressedBlob = reader.ReadUInt8Array(); //blobDataBlocks
                            reader.AlignStream();
                        }
                    }

                    if (reader.Game.Type.IsLoveAndDeepspace())
                    {
                        var codeOffsets = reader.ReadUInt32ArrayArray();
                        var codeCompressedLengths = reader.ReadUInt32ArrayArray();
                        var codeDecompressedLengths = reader.ReadUInt32ArrayArray();
                        var codeCompressedBlob = reader.ReadUInt8Array();
                        reader.AlignStream();
                    }

                    if ((version[0] == 2021 && version[1] > 3) ||
                        version[0] == 2021 && version[1] == 3 && version[2] >= 12 || //2021.3.12f1 and up
                        (version[0] == 2022 && version[1] > 1) ||
                        version[0] == 2022 && version[1] == 1 && version[2] >= 21) //2022.1.21f1 and up
                    {
                        stageCounts = reader.ReadUInt32Array();
                    }
                }

                var m_DependenciesCount = reader.ReadInt32();
                for (int i = 0; i < m_DependenciesCount; i++)
                {
                    new PPtr<Shader>(reader);
                }

                if (version[0] >= 2018)
                {
                    var m_NonModifiableTexturesCount = reader.ReadInt32();
                    for (int i = 0; i < m_NonModifiableTexturesCount; i++)
                    {
                        var first = reader.ReadAlignedString();
                        new PPtr<Texture>(reader);
                    }
                }

                var m_ShaderIsBaked = reader.ReadBoolean();
                reader.AlignStream();
            }
            else
            {
                m_Script = reader.ReadUInt8Array();
                reader.AlignStream();
                var m_PathName = reader.ReadAlignedString();
                if (version[0] == 5 && version[1] >= 3) //5.3 - 5.4
                {
                    decompressedSize = reader.ReadUInt32();
                    m_SubProgramBlob = reader.ReadUInt8Array();
                }
            }
        }
    }
}
