using System.Windows.Forms;

namespace AnimeStudio.GUI
{
    public class AssetItem : ListViewItem
    {
        public Object Asset;
        public SerializedFile SourceFile;
        public string Container = string.Empty;
        public string TypeString;
        public long m_PathID;
        public long FullSize;
        public ClassIDType Type;
        public string Hash;
        public string InfoText;
        public string UniqueID;
        public GameObjectTreeNode TreeNode;
        public bool IsVirtual;
        public string ExternalPath;
        public string VirtualContent;

        public AssetItem(Object asset)
        {
            Asset = asset;
            Text = asset.Name;
            SourceFile = asset.assetsFile;
            Type = asset.type;
            TypeString = Type.ToString();
            m_PathID = asset.m_PathID;
            FullSize = asset.byteSize;
            Hash = asset.GetHash();
        }

        public AssetItem(string name, ClassIDType type, string externalPath, long fullSize, string container = "")
        {
            Asset = null;
            SourceFile = null;
            Text = name;
            Type = type;
            TypeString = type.ToString();
            m_PathID = 0;
            FullSize = fullSize;
            Hash = string.Empty;
            Container = container;
            IsVirtual = true;
            ExternalPath = externalPath;
        }

        public void SetSubItems()
        {
            SubItems.AddRange(new[]
            {
                Container, //Container
                TypeString, //Type
                m_PathID.ToString(), //PathID
                FullSize.ToString(), //Size
                Hash, //Hash
            });
        }
    }
}
