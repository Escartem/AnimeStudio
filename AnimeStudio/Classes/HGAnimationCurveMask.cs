using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnimeStudio
{
    public sealed class HGAnimationCurveMask : NamedObject
    {
        public uint[] m_Mask;
        public List<string> m_CurveNames;
        public GenericBinding[] m_GenericBindings;

        public HGAnimationCurveMask(ObjectReader reader) : base(reader)
        {
            m_Mask = reader.ReadUInt32Array();

            var curveNamesCount = reader.ReadInt32();
            m_CurveNames = new List<string>(curveNamesCount);
            for (int i = 0; i < curveNamesCount; i++)
            {
                m_CurveNames.Add(reader.ReadAlignedString());
            }
            reader.AlignStream();

            var genericBindingsCount = reader.ReadInt32();
            m_GenericBindings = new GenericBinding[genericBindingsCount];
            reader.AlignStream();
        }
    }
}
