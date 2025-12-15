using System;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace AnimeStudio
{
    public class UnityTexEnv
    {
        private static bool HasMaxMipLevel(SerializedType type) => type.Match("E1EE5B1091AC21B95BAED1351F8FB1C2");

        public PPtr<Texture> m_Texture;
        public Vector2 m_Scale;
        public Vector2 m_Offset;

        public UnityTexEnv(ObjectReader reader)
        {
            m_Texture = new PPtr<Texture>(reader);
            m_Scale = reader.ReadVector2();
            m_Offset = reader.ReadVector2();
            if (HasMaxMipLevel(reader.serializedType))
            {
                reader.ReadBytes(4);
            }
            if (reader.Game.Type.IsArknightsEndfieldGroup())
            {
                var m_UVSetIndex = reader.ReadInt32();
            }
        }
    }

    public class HGSubsurfaceProfile : NamedObject
    {
        public ColorRGBA m_SurfaceAlbedo;
        public Vector4 m_diffuseMeanFreePath;
        public Vector3 m_subsurfaceNormalLerp;
        public float m_curvatureScale;
        public float m_penumbraScale;
        public PPtr<Texture2D> m_scatterLut;
        public PPtr<Texture2D> m_penumbraLut;
        public PPtr<Texture2D> m_indirectLut;

        public HGSubsurfaceProfile(ObjectReader reader) : base(reader)
        {
            m_SurfaceAlbedo = new ColorRGBA(reader);
            m_diffuseMeanFreePath = new Vector4(
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32()
            );
            m_subsurfaceNormalLerp = new Vector3(
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32()
            );
            m_curvatureScale = new Float(reader.ReadUInt32());
            m_penumbraScale = new Float(reader.ReadUInt32());
            m_scatterLut = new PPtr<Texture2D>(reader);
            m_penumbraLut = new PPtr<Texture2D>(reader);
            m_indirectLut = new PPtr<Texture2D>(reader);
        }
    }

    // technically a vector4 with other names
    public class ColorRGBA
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public ColorRGBA(ObjectReader reader)
        {
            r = new Float(reader.ReadUInt32());
            g = new Float(reader.ReadUInt32());
            b = new Float(reader.ReadUInt32());
            a = new Float(reader.ReadUInt32());
        }
    }

    public class UnityPropertySheet
    {
        [JsonConverter(typeof(KVPConverter<UnityTexEnv>))]
        public List<KeyValuePair<string, UnityTexEnv>> m_TexEnvs;
        [JsonConverter(typeof(KVPConverter<int>))]
        public List<KeyValuePair<string, int>> m_Ints;
        [JsonConverter(typeof(KVPConverter<float>))]
        public List<KeyValuePair<string, float>> m_Floats;
        [JsonConverter(typeof(KVPConverter<Color>))]
        public List<KeyValuePair<string, Color>> m_Colors;

        public UnityPropertySheet(ObjectReader reader)
        {
            var version = reader.version;

            int m_TexEnvsSize = reader.ReadInt32();
            m_TexEnvs = new List<KeyValuePair<string, UnityTexEnv>>();
            for (int i = 0; i < m_TexEnvsSize; i++)
            {
                m_TexEnvs.Add(new(reader.ReadAlignedString(), new UnityTexEnv(reader)));
            }

            if (version[0] >= 2021) //2021.1 and up
            {
                int m_IntsSize = reader.ReadInt32();
                m_Ints = new List<KeyValuePair<string, int>>();
                for (int i = 0; i < m_IntsSize; i++)
                {
                    m_Ints.Add(new(reader.ReadAlignedString(), reader.ReadInt32()));
                }
            }

            int m_FloatsSize = reader.ReadInt32();
            m_Floats = new List<KeyValuePair<string, float>>();
            for (int i = 0; i < m_FloatsSize; i++)
            {
                m_Floats.Add(new(reader.ReadAlignedString(), reader.ReadSingle()));
            }

            int m_ColorsSize = reader.ReadInt32();
            m_Colors = new List<KeyValuePair<string, Color>>();
            for (int i = 0; i < m_ColorsSize; i++)
            {
                m_Colors.Add(new(reader.ReadAlignedString(), reader.ReadColor4()));
            }

            if (reader.Game.Type.IsArknightsEndfieldCB3())
            {
                var m_SubsurfaceProfile = new PPtr<HGSubsurfaceProfile>(reader);
            }
        }
    }

    public sealed class Material : NamedObject
    {
        private static bool HasEnabledPassMask(SerializedType type) => type.Match("6BDB1CD05E80C82ABB24930CD37AEE88");

        public PPtr<Shader> m_Shader;
        public UnityPropertySheet m_SavedProperties;

        public Material(ObjectReader reader) : base(reader)
        {
            m_Shader = new PPtr<Shader>(reader);

            if (version[0] == 4 && version[1] >= 1) //4.x
            {
                var m_ShaderKeywords = reader.ReadStringArray();
            }

            if (version[0] > 2021 || (version[0] == 2021 && version[1] >= 3)) //2021.3 and up
            {
                var m_ValidKeywords = reader.ReadStringArray();
                var m_InvalidKeywords = reader.ReadStringArray();
            }
            else if (version[0] >= 5) //5.0 ~ 2021.2
            {
                var m_ShaderKeywords = reader.ReadAlignedString();
            }

            if (version[0] >= 5) //5.0 and up
            {
                var m_LightmapFlags = reader.ReadUInt32();
            }

            if (version[0] > 5 || (version[0] == 5 && version[1] >= 6)) //5.6 and up
            {
                var m_EnableInstancingVariants = reader.ReadBoolean();
                //var m_DoubleSidedGI = a_Stream.ReadBoolean(); //2017 and up
                //var m_HighShadingRate -> boolean //ZZZ
                reader.AlignStream();
            }

            if (version[0] > 4 || (version[0] == 4 && version[1] >= 3)) //4.3 and up
            {
                var m_CustomRenderQueue = reader.ReadInt32();
            }

            if (reader.Game.Type.IsLoveAndDeepspace())
            {
                var m_MaterialType = reader.ReadUInt32();
            }

            if (version[0] > 5 || (version[0] == 5 && version[1] >= 1)) //5.1 and up
            {
                var stringTagMapSize = reader.ReadInt32();
                for (int i = 0; i < stringTagMapSize; i++)
                {
                    var first = reader.ReadAlignedString();
                    var second = reader.ReadAlignedString();
                }
            }

            if (reader.Game.Type.IsNaraka())
            {
                var value = reader.ReadInt32();
            }

            if (version[0] > 5 || (version[0] == 5 && version[1] >= 6)) //5.6 and up
            {
                var disabledShaderPasses = reader.ReadStringArray();
            }

            if (reader.Game.Type.IsZZZ() && HasEnabledPassMask(reader.serializedType))
            {
                var enabledPassMask = reader.ReadUInt32();
            }

            m_SavedProperties = new UnityPropertySheet(reader);

            //vector m_BuildTextureStacks 2020 and up
        }
    }
}
