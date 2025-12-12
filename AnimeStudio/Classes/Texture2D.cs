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
            if (reader.Game.Type.IsArknightsEndfieldCB3())
            {
                var m_TextureGroup = reader.ReadUInt32();
            }
            if (reader.Game.Type.IsHYGCB1())
            {
                var m_UseGlobalTrilinearSetting = reader.ReadInt32();
                reader.AlignStream();
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
                if (reader.Game.Type.IsHYGCB1())
                {
                    reader.AlignStream();
                }
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
            if (reader.Game.Type.IsHYGCB1())
            {
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
        Alpha8 = 1, // Alpha-only texture format, 8 bit integer.
        ARGB4444 = 2, // A 16 bits/pixel texture format. Texture stores color with an alpha channel.
        RGB24 = 3, // Three channel (RGB) texture format, 8-bits unsigned integer per channel.
        RGBA32 = 4, // Four channel (RGBA) texture format, 8-bits unsigned integer per channel.
        ARGB32 = 5, // Color with alpha texture format, 8-bits per channel.
        RGB565 = 7, // A 16 bit color texture format.
        R16_Alt = 8, // FAKE
        R16 = 9, // Single channel (R) texture format, 16-bits unsigned integer.
        DXT1 = 10, // Compressed color texture format.
        DXT3 = 11, // FAKE
        DXT5 = 12, // Compressed color with alpha channel texture format.
        RGBA4444 = 13, // Color and alpha texture format, 4 bit per channel.
        BGRA32 = 14, // Color with alpha texture format, 8-bits per channel.
        RHalf = 15, // Scalar (R) texture format, 16 bit floating point.
        RGHalf = 16, // Two color (RG) texture format, 16 bit floating point per channel.
        RGBAHalf = 17, // RGB color and alpha texture format, 16 bit floating point per channel.
        RFloat = 18, // Scalar (R) texture format, 32 bit floating point.
        RGFloat = 19, // Two color (RG) texture format, 32 bit floating point per channel.
        RGBAFloat = 20, // RGB color and alpha texture format, 32-bit floats per channel.
        YUY2 = 21, // A format that uses the YUV color space and is often used for video encoding or playback.
        RGB9e5Float = 22, // RGB HDR format, with 9 bit mantissa per channel and a 5 bit shared exponent.
        BC4 = 26, // Compressed one channel (R) texture format.
        BC5 = 27, // Compressed two-channel (RG) texture format.
        BC6H = 24, // HDR compressed color texture format.
        BC7 = 25, // High quality compressed color texture format.
        DXT1Crunched = 28, // Compressed color texture format with Crunch compression for smaller storage sizes.
        DXT5Crunched = 29, // Compressed color with alpha channel texture format with Crunch compression for smaller storage sizes.
        PVRTC_RGB2 = 30, // PowerVR (iOS) 2 bits/pixel compressed color texture format.
        PVRTC_RGBA2 = 31, // PowerVR (iOS) 2 bits/pixel compressed with alpha channel texture format.
        PVRTC_RGB4 = 32, // PowerVR (iOS) 4 bits/pixel compressed color texture format.
        PVRTC_RGBA4 = 33, // PowerVR (iOS) 4 bits/pixel compressed with alpha channel texture format.
        ETC_RGB4 = 34, // ETC (GLES2.0) 4 bits/pixel compressed RGB texture format.
        ATC_RGB4 = 35, // FAKE
        ATC_RGBA8 = 36, // FAKE
        EAC_R = 41, // ETC2 EAC (GL ES 3.0) 4 bitspixel compressed unsigned single-channel texture format.
        EAC_R_SIGNED = 42, // ETC2 EAC (GL ES 3.0) 4 bitspixel compressed signed single-channel texture format.
        EAC_RG = 43, // ETC2 EAC (GL ES 3.0) 8 bitspixel compressed unsigned dual-channel (RG) texture format.
        EAC_RG_SIGNED = 44, // ETC2 EAC (GL ES 3.0) 8 bitspixel compressed signed dual-channel (RG) texture format.
        ETC2_RGB = 45, // ETC2 (GL ES 3.0) 4 bits/pixel compressed RGB texture format.
        ETC2_RGBA1 = 46, // ETC2 (GL ES 3.0) 4 bits/pixel RGB+1-bit alpha texture format.
        ETC2_RGBA8 = 47, // ETC2 (GL ES 3.0) 8 bits/pixel compressed RGBA texture format.
        ASTC_4x4 = 48, // ASTC (4x4 pixel block in 128 bits) compressed RGB(A) texture format.
        ASTC_RGB_4x4 = -48, // Same as above
        ASTC_5x5 = 49, // ASTC (5x5 pixel block in 128 bits) compressed RGB(A) texture format.
        ASTC_RGB_5x5 = -49, // Same as above
        ASTC_6x6 = 50, // ASTC (6x6 pixel block in 128 bits) compressed RGB(A) texture format.
        ASTC_RGB_6x6 = -50, // Same as above
        ASTC_8x8 = 51, // ASTC (8x8 pixel block in 128 bits) compressed RGB(A) texture format.
        ASTC_RGB_8x8 = -51, // Same as above
        ASTC_10x10 = 52, // ASTC (10x10 pixel block in 128 bits) compressed RGB(A) texture format.
        ASTC_RGB_10x10 = -52, // Same as above
        ASTC_12x12 = 53, // ASTC (12x12 pixel block in 128 bits) compressed RGB(A) texture format.
        ASTC_RGB_12x12 = -53, // Same as above
        ASTC_RGBA_4x4 = 54, // Obsolete. Use TextureFormat.ASTC_4x4 instead.
        ASTC_RGBA_5x5 = 55, // Obsolete. Use TextureFormat.ASTC_5x5 instead.
        ASTC_RGBA_6x6 = 56, // Obsolete. Use TextureFormat.ASTC_6x6 instead.
        ASTC_RGBA_8x8 = 57, // Obsolete. Use TextureFormat.ASTC_8x8 instead.
        ASTC_RGBA_10x10 = 58, // Obsolete. Use TextureFormat.ASTC_10x10 instead.
        ASTC_RGBA_12x12 = 59, // Obsolete. Use TextureFormat.ASTC_12x12 instead.
        ETC_RGB4_3DS = 60, // Obsolete. Enum member ETC_RGB4_3DS is obsolete. Nintendo 3DS is no longer supported.
        ETC_RGBA8_3DS = 61, // Obsolete. Enum member ETC_RGB4_3DS is obsolete. Nintendo 3DS is no longer supported.
        RG16 = 62, // Two channel (RG) texture format, 8-bits unsigned integer per channel.
        R8 = 63, // Single channel (R) texture format, 8-bits unsigned integer.
        ETC_RGB4Crunched = 64, // Compressed color texture format with Crunch compression for smaller storage sizes.
        ETC2_RGBA8Crunched = 65, // Compressed color with alpha channel texture format using Crunch compression for smaller storage sizes.
        ASTC_HDR_4x4 = 66, // ASTC (4x4 pixel block in 128 bits) compressed RGB(A) HDR texture format.
        ASTC_HDR_5x5 = 67, // ASTC (5x5 pixel block in 128 bits) compressed RGB(A) HDR texture format.
        ASTC_HDR_6x6 = 68, // ASTC (6x6 pixel block in 128 bits) compressed RGB(A) HDR texture format.
        ASTC_HDR_8x8 = 69, // ASTC (8x8 pixel block in 128 bits) compressed RGB(A) texture format.
        ASTC_HDR_10x10 = 70, // ASTC (10x10 pixel block in 128 bits) compressed RGB(A) HDR texture format.
        ASTC_HDR_12x12 = 71, // ASTC (12x12 pixel block in 128 bits) compressed RGB(A) HDR texture format.
        RG32 = 72, // Two channel (RG) texture format, 16-bits unsigned integer per channel.
        RGB48 = 73, // Three channel (RGB) texture format, 16-bits unsigned integer per channel.
        RGBA64 = 74, // Four channel (RGBA) texture format, 16-bits unsigned integer per channel.
        R8_SIGNED = 75, // Single channel (R) texture format, 8-bits signed integer.
        RG16_SIGNED = 76, // Two channel (RG) texture format, 8-bits signed integer per channel.
        RGB24_SIGNED = 77, // Three channel (RGB) texture format, 8-bits signed integer per channel.
        RGBA32_SIGNED = 78, // Four channel (RGBA) texture format, 8-bits signed integer per channel.
        R16_SIGNED = 79, // Single channel (R) texture format, 16-bits signed integer.
        RG32_SIGNED = 80, // Two channel (RG) texture format, 16-bits signed integer per channel.
        RGB48_SIGNED = 81, // Three color (RGB) texture format, 16-bits signed integer per channel.
        RGBA64_SIGNED = 82, // Four channel (RGBA) texture format, 16-bits signed integer per channel.
    }
}