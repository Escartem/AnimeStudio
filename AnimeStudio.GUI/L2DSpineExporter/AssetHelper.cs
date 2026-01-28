using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Windows.Forms;

namespace AnimeStudio.GUI
{
    public static class AssetPluginHelper
    {
        public static void Apply<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (T item in source)
            {
                action(item);
            }
        }

        public static IEnumerable<TResult> ApplyFunc<T, TResult>(this IEnumerable<T> source, Func<T, TResult> function)
        {
            foreach (T item in source)
            {
                yield return function(item);
            }
        }

        public static IEnumerable<TResult> ApplyFunc<T, TResult>(this IEnumerable<T> source, Func<T, TResult> function, TResult filter) where TResult : notnull
        {
            foreach (T item in source)
            {
                TResult result = function(item);
                if (!filter.Equals(result))
                {
                    yield return result;
                }
            }
        }

        public static IEnumerable<TResult> ApplyFunc<T, TResult>(this IEnumerable<T> source, Func<T, TResult> function, bool filterNull) where TResult : class
        {
            if (filterNull)
            {
                foreach (T item in source)
                {
                    var result = function(item);
                    if (result != null)
                    {
                        yield return result;
                    }
                }
            }
            else
            {
                foreach (T item in source)
                {
                    yield return function(item);
                }
            }
        }

        public static GameObject GetGameObjectInTreeNode(this AssetItem assetItem)
        {
            return assetItem.TreeNode.gameObject;
        }

        public static HashSet<string> GetOrigionalFiles(this IEnumerable<AssetItem> assetItems)
        {
            // originalFilePath|cabFullName
            return assetItems.Select(x => x.Asset.assetsFile.originalPath ?? x.Asset.assetsFile.fullName).ToHashSet();
        }

        public static OrderedDictionary ToDictionary(this MonoBehaviour monoBehaviour)
        {
            var obj = monoBehaviour.ToType();
            if (obj == null)
            {
                var type = Studio.MonoBehaviourToTypeTree(monoBehaviour);
                obj = monoBehaviour.ToType(type);
            }
            return obj;
        }

        public static TypeTree ToTypeTree(this MonoBehaviour monoBehaviour)
        {
            return Studio.MonoBehaviourToTypeTree(monoBehaviour);
        }

        public static bool TryGetComponent<T>(this Component self, object key, out T component) where T : Object
        {
            try
            {
                var obj = self.ToType();
                if (obj == null)
                {
                    var type = Studio.MonoBehaviourToTypeTree(self as MonoBehaviour);
                    obj = self.ToType(type);
                }
                if (obj.Contains(key))
                {
                    var od = obj[key] as OrderedDictionary;
                    var pptr = new PPtr<T>((int)od["m_FileID"], (long)od["m_PathID"], self.assetsFile);
                    if (pptr.TryGet(out T t))
                    {
                        component = t;
                        return true;
                    }
                }
            }
            catch
            {
                Console.WriteLine($"can't get target{typeof(T)} type in key:{key}");
            }
            component = null;
            return false;
        }

        public static bool TryGetComponent<T>(this Component self, OrderedDictionary od, out T component) where T : Object
        {
            if (od != null)
            {
                var pptr = new PPtr<T>((int)od["m_FileID"], (long)od["m_PathID"], self.assetsFile);
                if (pptr.TryGet(out T t))
                {
                    component = t;
                    return true;
                }
            }
            component = null;
            return false;
        }

        public static bool TryGetComponents<T>(this Component self, object key, out List<T> components) where T : Object
        {
            try
            {
                components = new List<T>();
                var obj = self.ToType();
                if (obj == null)
                {
                    var type = Studio.MonoBehaviourToTypeTree(self as MonoBehaviour);
                    obj = self.ToType(type);
                }
                var token = JToken.FromObject(obj[key]);
                foreach (JObject item in token)
                {
                    var pptr = new PPtr<T>(item["m_FileID"].Value<int>(), item["m_PathID"].Value<long>(), self.assetsFile);
                    if (pptr.TryGet(out T t))
                    {
                        components.Add(t);
                    }
                }

                return true;
            }
            catch
            {
                Console.WriteLine($"can't get target{typeof(T)} type in key:{key}");
            }
            components = null;
            return false;
        }

        public static bool TryGetRefInfo(this MonoBehaviour monoBehaviour, object key, out RefInfo regInfo)
        {
            var obj = monoBehaviour.ToType();
            if (obj == null)
            {
                var type = Studio.MonoBehaviourToTypeTree(monoBehaviour);
                obj = monoBehaviour.ToType(type);
            }
            try
            {
                var od = obj[key] as OrderedDictionary;
                regInfo = new RefInfo((int)od["m_FileID"], (long)od["m_PathID"]);
                return true;
            }
            catch
            {
                regInfo = null;
                return false;
            }
        }

        public static bool TryGetToken(this MonoBehaviour monoBehaviour, object key, out JToken token)
        {
            var obj = monoBehaviour.ToType();
            if (obj == null)
            {
                var type = Studio.MonoBehaviourToTypeTree(monoBehaviour);
                obj = monoBehaviour.ToType(type);
            }
            try
            {
                token = JToken.FromObject(obj[key]);
                return true;
            }
            catch
            {
                token = null;
                return false;
            }
        }
    }

    public class RefInfo : IEqualityComparer<RefInfo>
    {
        public int m_FileID;
        public long m_PathID;

        public RefInfo()
        { }

        public RefInfo(int m_FileID, long m_PathID)
        {
            this.m_FileID = m_FileID;
            this.m_PathID = m_PathID;
        }

        public bool Equals(RefInfo x, RefInfo y)
        {
            return x.m_FileID == y.m_FileID && x.m_PathID == y.m_PathID;
        }

        public int GetHashCode([DisallowNull] RefInfo obj)
        {
            return 0;
        }

        public bool TryGet<T>(Component parent, out T component) where T : Object
        {
            var pptr = new PPtr<T>(m_FileID, m_PathID, parent.assetsFile);
            if (pptr.TryGet(out T t))
            {
                component = t;
                return true;
            }
            component = null;
            return false;
        }
    }
}