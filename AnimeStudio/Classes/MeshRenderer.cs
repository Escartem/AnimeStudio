using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AnimeStudio
{
    public sealed class MeshRenderer : Renderer
    {
        public PPtr<Mesh> m_AdditionalVertexStreams;
        public MeshRenderer(ObjectReader reader) : base(reader)
        {
            m_AdditionalVertexStreams = new PPtr<Mesh>(reader);
            if (reader.Game.Type.IsHYGCB1())
            {
                var m_EnlightenVertexStream = new PPtr<Mesh>(reader);
            }
        }
    }
}
