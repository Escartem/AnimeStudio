﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AnimeStudio
{
    public abstract class Texture : NamedObject
    {
        protected Texture(ObjectReader reader) : base(reader)
        {
            if ((version[0] > 2017 || (version[0] == 2017 && version[1] >= 3)) && version[0] < 6000) //2017.3 and up to 5999
            {
                var m_ForcedFallbackFormat = reader.ReadInt32();
                var m_DownscaleFallback = reader.ReadBoolean();
            }
            if (version[0] > 2020 || (version[0] == 2020 && version[1] >= 2)) //2020.2 and up
            {
                var m_IsAlphaChannelOptional = reader.ReadBoolean();
            }
            reader.AlignStream();
        }
    }
}
