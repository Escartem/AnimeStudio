using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnimeStudio
{
    public sealed class HGSubsurfaceProfile : NamedObject
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
    public sealed class ColorRGBA
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
}
