using System.Collections.Generic;
using AnimeStudio.App;

namespace AnimeStudio.GUI.Core.Preview
{
    // LRU of decoded previews, bounded by an approximate byte budget.
    public sealed class PreviewCache
    {
        private readonly long capacity;
        private readonly LinkedList<(AssetRow Row, PreviewResult Result)> order = new();
        private readonly Dictionary<AssetRow, LinkedListNode<(AssetRow, PreviewResult)>> map = new();
        private long size;

        public PreviewCache(long capacityBytes) => capacity = capacityBytes;

        public bool TryGet(AssetRow row, out PreviewResult result)
        {
            lock (map)
            {
                if (map.TryGetValue(row, out var node))
                {
                    order.Remove(node);
                    order.AddFirst(node);
                    result = node.Value.Item2;
                    return true;
                }
            }
            result = null;
            return false;
        }

        public void Add(AssetRow row, PreviewResult result)
        {
            if (result.Cost > capacity / 2)
                return;
            lock (map)
            {
                if (map.Remove(row, out var existing))
                {
                    order.Remove(existing);
                    size -= existing.Value.Item2.Cost;
                }
                map[row] = order.AddFirst((row, result));
                size += result.Cost;
                while (size > capacity && order.Last != null)
                {
                    var last = order.Last.Value;
                    order.RemoveLast();
                    map.Remove(last.Row);
                    size -= last.Result.Cost;
                }
            }
        }

        public void Clear()
        {
            lock (map)
            {
                map.Clear();
                order.Clear();
                size = 0;
            }
        }
    }
}
