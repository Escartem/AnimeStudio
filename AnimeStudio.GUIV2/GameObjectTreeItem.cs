using System.Collections.ObjectModel;
using System.ComponentModel;

namespace AnimeStudio.GUIV2
{
    public class GameObjectTreeItem : INotifyPropertyChanged
    {
        public GameObject GameObject { get; }
        public string Name => GameObject.m_Name;
        public bool HasModel => GameObject.HasModel();
        public ObservableCollection<GameObjectTreeItem> Children { get; }

        public GameObjectTreeItem(GameObject gameObject)
        {
            GameObject = gameObject;
            Children = new ObservableCollection<GameObjectTreeItem>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class AssetsFileTreeItem : INotifyPropertyChanged
    {
        public string Name { get; }
        public ObservableCollection<GameObjectTreeItem> Children { get; }

        public AssetsFileTreeItem(string name)
        {
            Name = name;
            Children = new ObservableCollection<GameObjectTreeItem>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class FileTreeItem : INotifyPropertyChanged
    {
        public string Name { get; }
        public ObservableCollection<AssetsFileTreeItem> Children { get; }

        public FileTreeItem(string name)
        {
            Name = name;
            Children = new ObservableCollection<AssetsFileTreeItem>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}