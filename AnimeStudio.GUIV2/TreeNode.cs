using System.Collections.ObjectModel;
using System.ComponentModel;

namespace AnimeStudio.GUIV2
{
    public interface ITreeNode
    {
        string Name { get; }
        ObservableCollection<ITreeNode> Children { get; }
    }

    public class GameObjectTreeItem : INotifyPropertyChanged, ITreeNode
    {
        public GameObject GameObject { get; }
        public string Name => GameObject.m_Name;
        public bool HasModel => GameObject.HasModel();
        public ObservableCollection<ITreeNode> Children { get; }

        public GameObjectTreeItem(GameObject gameObject)
        {
            GameObject = gameObject;
            Children = new ObservableCollection<ITreeNode>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class AssetsFileTreeItem : INotifyPropertyChanged, ITreeNode
    {
        public string Name { get; }
        public ObservableCollection<ITreeNode> Children { get; }

        public AssetsFileTreeItem(string name)
        {
            Name = name;
            Children = new ObservableCollection<ITreeNode>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class FileTreeItem : INotifyPropertyChanged, ITreeNode
    {
        public string Name { get; }
        public ObservableCollection<ITreeNode> Children { get; }

        public FileTreeItem(string name)
        {
            Name = name;
            Children = new ObservableCollection<ITreeNode>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}