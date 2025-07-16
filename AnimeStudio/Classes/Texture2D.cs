using System;

namespace AnimeStudio
{
    public class StreamingInfo
    {
        public long offset; //ulong
        public uint size;
        public string path;

        public StreamingInfo(ObjectReader reader)
        {
            var version = reader.version;

            if (version[0] >= 2020) //2020.1 and up
            {
                offset = reader.ReadInt64();
            }
            else
            {
                offset = reader.ReadUInt32();
            }
            size = reader.ReadUInt32();
            path = reader.ReadAlignedString();
        }
    }

    public class GLTextureSettings
    {
        public int m_FilterMode;
        public int m_Aniso;
        public float m_MipBias;
        public int m_WrapMode;

        public GLTextureSettings(ObjectReader reader)
        {
            var version = reader.version;

            m_FilterMode = reader.ReadInt32();
            m_Aniso = reader.ReadInt32();
            m_MipBias = reader.ReadSingle();
            if (reader.Game.Type.IsExAstris())
            {
                var m_TextureGroup = reader.ReadInt32();
            }
            if (version[0] >= 2017)//2017.x and up
            {
                m_WrapMode = reader.ReadInt32(); //m_WrapU
                int m_WrapV = reader.ReadInt32();
                int m_WrapW = reader.ReadInt32();
            }
            else
            {
                m_WrapMode = reader.ReadInt32();
            }
        }
    }

    public sealed class Texture2D : Texture
    {
        public int m_Width;
        public int m_Height;
        public TextureFormat m_TextureFormat;
        public bool m_MipMap;
        public int m_MipCount;
        public GLTextureSettings m_TextureSettings;
        public ResourceReader image_data;
        public StreamingInfo m_StreamData;

        private static bool HasGNFTexture(SerializedType type) => type.Match("1D52BB98AA5F54C67C22C39E8B2E400F");
        private static bool HasExternalMipRelativeOffset(SerializedType type) => type.Match("1D52BB98AA5F54C67C22C39E8B2E400F", "5390A985F58D5524F95DB240E8789704");
        public Texture2D(ObjectReader reader) : base(reader)
        {
            m_Width = reader.ReadInt32();
            m_Height = reader.ReadInt32();
            var m_CompleteImageSize = reader.ReadInt32();
            if (version[0] >= 2020) //2020.1 and up
            {
                var m_MipsStripped = reader.ReadInt32();
            }
            m_TextureFormat = (TextureFormat)reader.ReadInt32();
  
            if (version[0] < 5 || (version[0] == 5 && version[1] < 2)) //5.2 down
            {
                m_MipMap = reader.ReadBoolean();
            }
            else
            {
                m_MipCount = reader.ReadInt32();
            }
            if (version[0] > 2 || (version[0] == 2 && version[1] >= 6)) //2.6.0 and up
            {
                var m_IsReadable = reader.ReadBoolean();
                if (reader.Game.Type.IsGI() && HasGNFTexture(reader.serializedType))
                {
                    var m_IsGNFTexture = reader.ReadBoolean();
                }
            }
            if (version[0] >= 2020 || reader.Game.Type.IsZZZ()) //2020.1 and up
            {
                var m_IsPreProcessed = reader.ReadBoolean();
            }
            if (version[0] > 2019 || (version[0] == 2019 && version[1] >= 3)) //2019.3 and up
            {
                var m_IgnoreMasterTextureLimit = reader.ReadBoolean();
            }
            if (version[0] > 2022 || (version[0] == 2022 && version[1] >= 2)) //2022.2 and up
            {
                reader.AlignStream(); //m_IgnoreMipmapLimit
                var m_MipmapLimitGroupName = reader.ReadAlignedString();
            }
            if (version[0] >= 3) //3.0.0 - 5.4
            {
                if (version[0] < 5 || (version[0] == 5 && version[1] <= 4))
                {
                    var m_ReadAllowed = reader.ReadBoolean();
                }
            }
            if (version[0] > 2018 || (version[0] == 2018 && version[1] >= 2)) //2018.2 and up
            {
                var m_StreamingMipmaps = reader.ReadBoolean();
            }
            reader.AlignStream();
            if (reader.Game.Type.IsGI() && HasGNFTexture(reader.serializedType))
            {
                var m_TextureGroup = reader.ReadInt32();
            }
            if (version[0] > 2018 || (version[0] == 2018 && version[1] >= 2)) //2018.2 and up
            {
                var m_StreamingMipmapsPriority = reader.ReadInt32();
            }
            if (reader.Game.Type.IsZZZ())
            {
                var m_IsCompressed = reader.ReadBoolean();
                reader.AlignStream();
            }
            var m_ImageCount = reader.ReadInt32();
            var m_TextureDimension = reader.ReadInt32();
            m_TextureSettings = new GLTextureSettings(reader);
            if (version[0] >= 3) //3.0 and up
            {
                var m_LightmapFormat = reader.ReadInt32();
            }
            if (version[0] > 3 || (version[0] == 3 && version[1] >= 5)) //3.5.0 and up
            {
                var m_ColorSpace = reader.ReadInt32();
            }
            if (version[0] > 2020 || (version[0] == 2020 && version[1] >= 2)) //2020.2 and up
            {
                var m_PlatformBlob = reader.ReadUInt8Array();
                reader.AlignStream();
            }
            var image_data_size = reader.ReadInt32();
            if (image_data_size == 0 && ((version[0] == 5 && version[1] >= 3) || version[0] > 5))//5.3.0 and up
            {
                if (reader.Game.Type.IsGI() && HasExternalMipRelativeOffset(reader.serializedType))
                {
                    var m_externalMipRelativeOffset = reader.ReadUInt32();
                }
                if (reader.Game.Type.IsZZZ())
                {
                    var m_ExternalMipRelativeIndex = reader.ReadUInt32();
                }
                m_StreamData = new StreamingInfo(reader);
            }

            ResourceReader resourceReader;
            if (!string.IsNullOrEmpty(m_StreamData?.path))
            {
                resourceReader = new ResourceReader(m_StreamData.path, assetsFile, m_StreamData.offset, m_StreamData.size);
            }
            else
            {
                resourceReader = new ResourceReader(reader, reader.BaseStream.Position, image_data_size);
            }
            image_data = resourceReader;
        }
    }

    public enum TextureFormat
    {
        //
        // Summary:
        //     Alpha-only texture format, 8 bit integer.
        Alpha8 = 1,
        //
        // Summary:
        //     A 16 bits/pixel texture format. Texture stores color with an alpha channel.
        ARGB4444 = 2,
        //
        // Summary:
        //     Three channel (RGB) texture format, 8-bits unsigned integer per channel.
        RGB24 = 3,
        //
        // Summary:
        //     Four channel (RGBA) texture format, 8-bits unsigned integer per channel.
        RGBA32 = 4,
        //
        // Summary:
        //     Color with alpha texture format, 8-bits per channel.
        ARGB32 = 5,
        //
        // Summary:
        //     A 16 bit color texture format.
        RGB565 = 7,
        //
        // FAKE
        R16_Alt = 8,
        // Summary:
        //     Single channel (R) texture format, 16-bits unsigned integer.
        R16 = 9,
        //
        // Summary:
        //     Compressed color texture format.
        DXT1 = 10,
        //
        // FAKE
        DXT3 = 11,
        // Summary:
        //     Compressed color with alpha channel texture format.
        DXT5 = 12,
        //
        // Summary:
        //     Color and alpha texture format, 4 bit per channel.
        RGBA4444 = 13,
        //
        // Summary:
        //     Color with alpha texture format, 8-bits per channel.
        BGRA32 = 14,
        //
        // Summary:
        //     Scalar (R) texture format, 16 bit floating point.
        RHalf = 15,
        //
        // Summary:
        //     Two color (RG) texture format, 16 bit floating point per channel.
        RGHalf = 16,
        //
        // Summary:
        //     RGB color and alpha texture format, 16 bit floating point per channel.
        RGBAHalf = 17,
        //
        // Summary:
        //     Scalar (R) texture format, 32 bit floating point.
        RFloat = 18,
        //
        // Summary:
        //     Two color (RG) texture format, 32 bit floating point per channel.
        RGFloat = 19,
        //
        // Summary:
        //     RGB color and alpha texture format, 32-bit floats per channel.
        RGBAFloat = 20,
        //
        // Summary:
        //     A format that uses the YUV color space and is often used for video encoding or
        //     playback.
        YUY2 = 21,
        //
        // Summary:
        //     RGB HDR format, with 9 bit mantissa per channel and a 5 bit shared exponent.
        RGB9e5Float = 22,
        //
        // Summary:
        //     Compressed one channel (R) texture format.
        BC4 = 26,
        //
        // Summary:
        //     Compressed two-channel (RG) texture format.
        BC5 = 27,
        //
        // Summary:
        //     HDR compressed color texture format.
        BC6H = 24,
        //
        // Summary:
        //     High quality compressed color texture format.
        BC7 = 25,
        //
        // Summary:
        //     Compressed color texture format with Crunch compression for smaller storage sizes.
        DXT1Crunched = 28,
        //
        // Summary:
        //     Compressed color with alpha channel texture format with Crunch compression for
        //     smaller storage sizes.
        DXT5Crunched = 29,
        //
        // Summary:
        //     PowerVR (iOS) 2 bits/pixel compressed color texture format.
        PVRTC_RGB2 = 30,
        //
        // Summary:
        //     PowerVR (iOS) 2 bits/pixel compressed with alpha channel texture format.
        PVRTC_RGBA2 = 31,
        //
        // Summary:
        //     PowerVR (iOS) 4 bits/pixel compressed color texture format.
        PVRTC_RGB4 = 32,
        //
        // Summary:
        //     PowerVR (iOS) 4 bits/pixel compressed with alpha channel texture format.
        PVRTC_RGBA4 = 33,
        //
        // Summary:
        //     ETC (GLES2.0) 4 bits/pixel compressed RGB texture format.
        ETC_RGB4 = 34,
        //
        // FAKE
        ATC_RGB4 = 35,
        // FAKE
        ATC_RGBA8 = 36,
        // Summary:
        //     ETC2 EAC (GL ES 3.0) 4 bitspixel compressed unsigned single-channel texture format.
        EAC_R = 41,
        //
        // Summary:
        //     ETC2 EAC (GL ES 3.0) 4 bitspixel compressed signed single-channel texture format.
        EAC_R_SIGNED = 42,
        //
        // Summary:
        //     ETC2 EAC (GL ES 3.0) 8 bitspixel compressed unsigned dual-channel (RG) texture
        //     format.
        EAC_RG = 43,
        //
        // Summary:
        //     ETC2 EAC (GL ES 3.0) 8 bitspixel compressed signed dual-channel (RG) texture
        //     format.
        EAC_RG_SIGNED = 44,
        //
        // Summary:
        //     ETC2 (GL ES 3.0) 4 bits/pixel compressed RGB texture format.
        ETC2_RGB = 45,
        //
        // Summary:
        //     ETC2 (GL ES 3.0) 4 bits/pixel RGB+1-bit alpha texture format.
        ETC2_RGBA1 = 46,
        //
        // Summary:
        //     ETC2 (GL ES 3.0) 8 bits/pixel compressed RGBA texture format.
        ETC2_RGBA8 = 47,
        //
        // Summary:
        //     ASTC (4x4 pixel block in 128 bits) compressed RGB(A) texture format.
        ASTC_4x4 = 48,
        //
        // Summary:
        //     ASTC (5x5 pixel block in 128 bits) compressed RGB(A) texture format.
        ASTC_5x5 = 49,
        //
        // Summary:
        //     ASTC (6x6 pixel block in 128 bits) compressed RGB(A) texture format.
        ASTC_6x6 = 50,
        //
        // Summary:
        //     ASTC (8x8 pixel block in 128 bits) compressed RGB(A) texture format.
        ASTC_8x8 = 51,
        //
        // Summary:
        //     ASTC (10x10 pixel block in 128 bits) compressed RGB(A) texture format.
        ASTC_10x10 = 52,
        //
        // Summary:
        //     ASTC (12x12 pixel block in 128 bits) compressed RGB(A) texture format.
        ASTC_12x12 = 53,
        //     Obsolete. Enum member ETC_RGB4_3DS is obsolete. Nintendo 3DS is no longer supported.
        ETC_RGB4_3DS = -60,
        //     Obsolete. Enum member ETC_RGB4_3DS is obsolete. Nintendo 3DS is no longer supported.
        ETC_RGBA8_3DS = -61,
        //
        // Summary:
        //     Two channel (RG) texture format, 8-bits unsigned integer per channel.
        RG16 = 62,
        //
        // Summary:
        //     Single channel (R) texture format, 8-bits unsigned integer.
        R8 = 63,
        //
        // Summary:
        //     Compressed color texture format with Crunch compression for smaller storage sizes.
        ETC_RGB4Crunched = 64,
        //
        // Summary:
        //     Compressed color with alpha channel texture format using Crunch compression for
        //     smaller storage sizes.
        ETC2_RGBA8Crunched = 65,
        //
        // Summary:
        //     ASTC (4x4 pixel block in 128 bits) compressed RGB(A) HDR texture format.
        ASTC_HDR_4x4 = 66,
        //
        // Summary:
        //     ASTC (5x5 pixel block in 128 bits) compressed RGB(A) HDR texture format.
        ASTC_HDR_5x5 = 67,
        //
        // Summary:
        //     ASTC (6x6 pixel block in 128 bits) compressed RGB(A) HDR texture format.
        ASTC_HDR_6x6 = 68,
        //
        // Summary:
        //     ASTC (8x8 pixel block in 128 bits) compressed RGB(A) texture format.
        ASTC_HDR_8x8 = 69,
        //
        // Summary:
        //     ASTC (10x10 pixel block in 128 bits) compressed RGB(A) HDR texture format.
        ASTC_HDR_10x10 = 70,
        //
        // Summary:
        //     ASTC (12x12 pixel block in 128 bits) compressed RGB(A) HDR texture format.
        ASTC_HDR_12x12 = 71,
        //
        // Summary:
        //     Two channel (RG) texture format, 16-bits unsigned integer per channel.
        RG32 = 72,
        //
        // Summary:
        //     Three channel (RGB) texture format, 16-bits unsigned integer per channel.
        RGB48 = 73,
        //
        // Summary:
        //     Four channel (RGBA) texture format, 16-bits unsigned integer per channel.
        RGBA64 = 74,
        //
        // Summary:
        //     Single channel (R) texture format, 8-bits signed integer.
        R8_SIGNED = 75,
        //
        // Summary:
        //     Two channel (RG) texture format, 8-bits signed integer per channel.
        RG16_SIGNED = 76,
        //
        // Summary:
        //     Three channel (RGB) texture format, 8-bits signed integer per channel.
        RGB24_SIGNED = 77,
        //
        // Summary:
        //     Four channel (RGBA) texture format, 8-bits signed integer per channel.
        RGBA32_SIGNED = 78,
        //
        // Summary:
        //     Single channel (R) texture format, 16-bits signed integer.
        R16_SIGNED = 79,
        //
        // Summary:
        //     Two channel (RG) texture format, 16-bits signed integer per channel.
        RG32_SIGNED = 80,
        //
        // Summary:
        //     Three color (RGB) texture format, 16-bits signed integer per channel.
        RGB48_SIGNED = 81,
        //
        // Summary:
        //     Four channel (RGBA) texture format, 16-bits signed integer per channel.
        RGBA64_SIGNED = 82,
        //
        // Summary:
        //     Obsolete. Use TextureFormat.ASTC_4x4 instead.        
        ASTC_RGB_4x4 = -48,
        //
        // Summary:
        //     Obsolete. Use TextureFormat.ASTC_5x5 instead.        
        ASTC_RGB_5x5 = -49,
        //
        // Summary:
        //     Obsolete. Use TextureFormat.ASTC_6x6 instead.
        ASTC_RGB_6x6 = -50,
        //
        // Summary:
        //     Obsolete. Use TextureFormat.ASTC_8x8 instead.
        ASTC_RGB_8x8 = -51,
        //
        // Summary:
        //     Obsolete. Use TextureFormat.ASTC_10x10 instead.
        ASTC_RGB_10x10 = -52,
        //
        // Summary:
        //     Obsolete. Use TextureFormat.ASTC_12x12 instead.
        ASTC_RGB_12x12 = -53,
        //
        // Summary:
        //     Obsolete. Use TextureFormat.ASTC_4x4 instead.
        ASTC_RGBA_4x4 = -54,
        //
        // Summary:
        //     Obsolete. Use TextureFormat.ASTC_5x5 instead.
        ASTC_RGBA_5x5 = -55,
        //
        // Summary:
        //     Obsolete. Use TextureFormat.ASTC_6x6 instead.
        ASTC_RGBA_6x6 = -56,
        //
        // Summary:
        //     Obsolete. Use TextureFormat.ASTC_8x8 instead.
        ASTC_RGBA_8x8 = -57,
        //
        // Summary:
        //     Obsolete. Use TextureFormat.ASTC_10x10 instead.
        ASTC_RGBA_10x10 = -58,
        //
        // Summary:
        //     Obsolete. Use TextureFormat.ASTC_12x12 instead.
        ASTC_RGBA_12x12 = -59
    }
    public enum TextureFormatOld
    {
        Alpha8 = 1,
        ARGB4444,
        RGB24,
        RGBA32,
        ARGB32,
        ARGBFloat,
        RGB565,
        BGR24,
        R16,
        DXT1,
        DXT3,
        DXT5,
        RGBA4444,
        BGRA32,
        RHalf,
        RGHalf,
        RGBAHalf,
        RFloat,
        RGFloat,
        RGBAFloat,
        YUY2,
        RGB9e5Float,
        RGBFloat,
        BC6H,
        BC7,
        BC4,
        BC5,
        DXT1Crunched,
        DXT5Crunched,
        PVRTC_RGB2,
        PVRTC_RGBA2,
        PVRTC_RGB4,
        PVRTC_RGBA4,
        ETC_RGB4,
        ATC_RGB4,
        ATC_RGBA8,
        EAC_R = 41,
        EAC_R_SIGNED,
        EAC_RG,
        EAC_RG_SIGNED,
        ETC2_RGB,
        ETC2_RGBA1,
        ETC2_RGBA8,
        ASTC_RGB_4x4,
        ASTC_RGB_5x5,
        ASTC_RGB_6x6,
        ASTC_RGB_8x8,
        ASTC_RGB_10x10,
        ASTC_RGB_12x12,
        ASTC_RGBA_4x4,
        ASTC_RGBA_5x5,
        ASTC_RGBA_6x6,
        ASTC_RGBA_8x8,
        ASTC_RGBA_10x10,
        ASTC_RGBA_12x12,
        ETC_RGB4_3DS,
        ETC_RGBA8_3DS,
        RG16,
        R8,
        ETC_RGB4Crunched,
        ETC2_RGBA8Crunched,
        R16_Alt,
        ASTC_HDR_4x4,
        ASTC_HDR_5x5,
        ASTC_HDR_6x6,
        ASTC_HDR_8x8,
        ASTC_HDR_10x10,
        ASTC_HDR_12x12,
        RG32,
        RGB48,
        RGBA64
    }
}