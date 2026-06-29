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
        public string InfoText;
        public string UniqueID;
        public GameObjectTreeNode TreeNode;
        private string hash;
        private bool subItemsInitialized;

        public string Hash => hash ??= Asset.GetHash();

        public AssetItem(Object asset)
        {
            Asset = asset;
            Text = asset.Name;
            SourceFile = asset.assetsFile;
            Type = asset.type;
            TypeString = Type.ToString();
            m_PathID = asset.m_PathID;
            FullSize = asset.byteSize;
        }

        public void SetSubItems()
        {
            if (subItemsInitialized)
            {
                return;
            }

            SubItems.AddRange(new[]
            {
                Container, //Container
                TypeString, //Type
                m_PathID.ToString(), //PathID
                FullSize.ToString(), //Size
                Hash, //Hash
            });
            subItemsInitialized = true;
        }

        public string GetColumnText(int column)
        {
            return column switch
            {
                0 => Text,
                1 => Container,
                2 => TypeString,
                3 => m_PathID.ToString(),
                4 => FullSize.ToString(),
                5 => Hash,
                _ => string.Empty,
            };
        }

        public bool MatchesListSearch(System.Text.RegularExpressions.Regex regex)
        {
            return regex.IsMatch(Text)
                || regex.IsMatch(Container)
                || regex.IsMatch(m_PathID.ToString());
        }
    }
}
