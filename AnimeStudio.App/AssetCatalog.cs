using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AnimeStudio.App
{
    public sealed record ClassStructure(int Id, TypeTree Type)
    {
        public string Name => Type.m_Nodes[0].m_Type + " " + Type.m_Nodes[0].m_Name;

        public string Dump()
        {
            var sb = new StringBuilder();
            foreach (var i in Type.m_Nodes)
            {
                sb.AppendFormat("{0}{1} {2} {3} {4}\r\n", new string('\t', i.m_Level), i.m_Type, i.m_Name, i.m_ByteSize, (i.m_MetaFlag & 0x4000) != 0);
            }
            return sb.ToString();
        }
    }

    public sealed record ClassStructureGroup(string Version, IReadOnlyList<ClassStructure> Classes);

    public sealed class AssetCatalog
    {
        public static readonly AssetCatalog Empty = new AssetCatalog(new List<AssetRow>(), new SceneGraph(), new List<ClassStructureGroup>(), null);

        public List<AssetRow> Rows { get; }
        public SceneGraph Scene { get; }
        public IReadOnlyList<ClassStructureGroup> Classes { get; }
        public string ProductName { get; }

        public AssetCatalog(List<AssetRow> rows, SceneGraph scene, IReadOnlyList<ClassStructureGroup> classes, string productName)
        {
            Rows = rows;
            Scene = scene;
            Classes = classes;
            ProductName = productName;
        }

        public ClassIDType[] GetTypes() => Rows.Select(x => x.Type).Distinct().OrderBy(AssetRow.GetTypeName).ToArray();
    }
}
