using System.Collections.Generic;

namespace AnimeStudio.GUI
{
    internal static partial class Studio
    {
        public static Dictionary<string, SortedDictionary<int, TypeTreeItem>> BuildClassStructure()
        {
            var typeMap = new Dictionary<string, SortedDictionary<int, TypeTreeItem>>();
            foreach (var assetsFile in assetsManager.assetsFileList)
            {
                if (assetsManager.tokenSource.IsCancellationRequested)
                {
                    Logger.Info("Processing class structure been cancelled !!");
                    return new Dictionary<string, SortedDictionary<int, TypeTreeItem>>();
                }

                var items = GetOrCreateTypeTreeItems(typeMap, assetsFile.unityVersion);
                AddTypeTreeItems(items, assetsFile.m_Types);
            }

            return typeMap;
        }

        private static SortedDictionary<int, TypeTreeItem> GetOrCreateTypeTreeItems(Dictionary<string, SortedDictionary<int, TypeTreeItem>> typeMap, string unityVersion)
        {
            if (!typeMap.TryGetValue(unityVersion, out var items))
            {
                items = new SortedDictionary<int, TypeTreeItem>();
                typeMap.Add(unityVersion, items);
            }

            return items;
        }

        private static void AddTypeTreeItems(SortedDictionary<int, TypeTreeItem> items, List<SerializedType> types)
        {
            foreach (var type in types)
            {
                if (type.m_Type == null)
                {
                    continue;
                }

                var key = GetTypeTreeItemKey(type);
                items[key] = new TypeTreeItem(key, type.m_Type);
            }
        }

        private static int GetTypeTreeItemKey(SerializedType type)
        {
            return type.m_ScriptTypeIndex >= 0
                ? -1 - type.m_ScriptTypeIndex
                : type.classID;
        }
    }
}

