using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace UnityLive2DExtractor
{
    public class CubismModel3Json
    {
        public int Version;
        public SerializableFileReferences FileReferences;
        public SerializableGroup[] Groups;

        public class SerializableFileReferences
        {
            public string Moc;
            public string[] Textures;
            public string Physics;
            public JObject Motions;
        }

        public class SerializableGroup
        {
            public string Target;
            public string Name;
            public string[] Ids;
        }
    }
}
