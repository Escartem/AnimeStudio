using static System.Net.Mime.MediaTypeNames;

namespace AnimeStudio.GUIV2
{
    public class AssetItem
    {
        public Object Asset { get; set; }
        public SerializedFile SourceFile { get; set; }
        public string Container { get; set; } = string.Empty;
        public string TypeString { get; set; }
        public string Text { get; set; }
        public long PathID { get; set; }
        public long FullSize { get; set; }
        public ClassIDType Type { get; set; }
        public string SHA256Hash { get; set; }
        public string InfoText { get; set; }
        public string UniqueID { get; set; }
        public GameObjectTreeItem TreeNode { get; set; }

        public AssetItem(Object asset)
        {
            Asset = asset;
            Text = asset.Name;
            SourceFile = asset.assetsFile;
            Type = asset.type;
            TypeString = Type.ToString();
            PathID = asset.m_PathID;
            FullSize = asset.byteSize;
            SHA256Hash = asset.GetSHA256Hash();
        }

        public AssetItem() { } // Needed for binding
    }
}
