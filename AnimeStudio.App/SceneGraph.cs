using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace AnimeStudio.App
{
    // Flat scene hierarchy. Node ids are indices into the arrays, children are linked lists.
    public sealed class SceneGraph
    {
        private struct Node
        {
            public string Name;
            public GameObject GameObject;
            public int Parent;
            public int FirstChild;
            public int LastChild;
            public int NextSibling;
            public int ChildCount;
            public bool HasModel;
        }

        private readonly List<Node> nodes = new();
        private readonly List<int> roots = new();
        private BitArray checkedNodes = new(0);

        public int Count => nodes.Count;
        public IReadOnlyList<int> Roots => roots;

        public string GetName(int id) => nodes[id].Name;
        public GameObject GetGameObject(int id) => nodes[id].GameObject;
        public bool IsGameObject(int id) => nodes[id].GameObject != null;
        public bool HasModel(int id) => nodes[id].HasModel;
        public int GetParent(int id) => nodes[id].Parent;
        public int GetChildCount(int id) => nodes[id].ChildCount;
        public bool IsChecked(int id) => checkedNodes[id];

        public IEnumerable<int> GetChildren(int id)
        {
            for (var child = nodes[id].FirstChild; child != -1; child = nodes[child].NextSibling)
                yield return child;
        }

        public int GetDepth(int id)
        {
            var depth = 0;
            for (var p = nodes[id].Parent; p != -1; p = nodes[p].Parent)
                depth++;
            return depth;
        }

        public int GetRoot(int id)
        {
            while (nodes[id].Parent != -1)
                id = nodes[id].Parent;
            return id;
        }

        internal int AddNode(string name, GameObject gameObject = null)
        {
            nodes.Add(new Node
            {
                Name = name,
                GameObject = gameObject,
                Parent = -1,
                FirstChild = -1,
                LastChild = -1,
                NextSibling = -1,
                HasModel = gameObject?.HasModel() ?? false,
            });
            return nodes.Count - 1;
        }

        internal void AddChild(int parent, int child)
        {
            var span = CollectionsMarshal.AsSpan(nodes);
            ref var p = ref span[parent];
            span[child].Parent = parent;
            if (p.LastChild == -1)
                p.FirstChild = child;
            else
                span[p.LastChild].NextSibling = child;
            p.LastChild = child;
            p.ChildCount++;
        }

        internal void AddRoot(int id) => roots.Add(id);

        internal void Seal() => checkedNodes = new BitArray(nodes.Count);

        // Same as the old TreeView behaviour: checking a node checks its whole subtree.
        public void SetChecked(int id, bool value)
        {
            var stack = new Stack<int>();
            stack.Push(id);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                checkedNodes[node] = value;
                foreach (var child in GetChildren(node))
                    stack.Push(child);
            }
        }

        public void ClearChecks() => checkedNodes.SetAll(false);

        public List<int> Search(Regex regex)
        {
            var results = new List<int>();
            var stack = new Stack<int>();
            for (var i = roots.Count - 1; i >= 0; i--)
                stack.Push(roots[i]);
            var children = new List<int>();
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (regex.IsMatch(nodes[node].Name))
                    results.Add(node);
                children.Clear();
                children.AddRange(GetChildren(node));
                for (var i = children.Count - 1; i >= 0; i--)
                    stack.Push(children[i]);
            }
            return results;
        }

        // Topmost checked GameObjects, a checked GameObject hides its descendants.
        public List<GameObject> CollectCheckedGameObjects(IEnumerable<int> from)
        {
            var result = new List<GameObject>();
            void Visit(int id)
            {
                if (nodes[id].GameObject != null && checkedNodes[id])
                {
                    result.Add(nodes[id].GameObject);
                    return;
                }
                foreach (var child in GetChildren(id))
                    Visit(child);
            }
            foreach (var id in from)
                Visit(id);
            return result;
        }

        public List<GameObject> CollectCheckedGameObjects() => CollectCheckedGameObjects(roots);

        public void CollectSubtree(int id, List<GameObject> gameObjects)
        {
            if (nodes[id].GameObject != null)
                gameObjects.Add(nodes[id].GameObject);
            foreach (var child in GetChildren(id))
                CollectSubtree(child, gameObjects);
        }

        // Nested name dictionaries of the animated hierarchy, as written by "Export > Scene hierarchy".
        public Dictionary<string, object> BuildHierarchyDump()
        {
            var result = new Dictionary<string, object>();
            foreach (var root in roots)
                result.TryAdd(nodes[root].Name, DumpNode(root));
            return result;
        }

        private object DumpNode(int id)
        {
            var dict = new Dictionary<string, object>();
            foreach (var child in GetChildren(id))
            {
                if (HasAnimatedGameObject(child))
                    dict.TryAdd(nodes[child].Name, DumpNode(child));
            }
            return dict.Count == 0 ? string.Empty : dict;
        }

        private bool HasAnimatedGameObject(int id)
        {
            var gameObject = nodes[id].GameObject;
            if (gameObject?.m_Transform != null && !gameObject.m_Transform.m_Father.IsNull)
                return gameObject.m_Animator != null;
            var first = nodes[id].FirstChild;
            return first != -1 && HasAnimatedGameObject(first);
        }
    }
}
