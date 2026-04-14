using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimeStudio
{
    public sealed class GameObject : EditorExtension
    {
        public List<PPtr<Component>> m_Components;

        public string m_Name;

        public Transform m_Transform;

        public MeshRenderer m_MeshRenderer;
        public MeshFilter m_MeshFilter;

        public SkinnedMeshRenderer m_SkinnedMeshRenderer;

        public Animator m_Animator;

        public Animation m_Animation;

        public override string Name => m_Name;

        public GameObject(ObjectReader reader) : base(reader)
        {
            int m_Component_size = reader.ReadInt32();

            m_Components = new List<PPtr<Component>>();
            for (int i = 0; i < m_Component_size; i++)
            {
                if ((version[0] == 5 && version[1] < 5) || version[0] < 5) // 5.5 down
                {
                    reader.ReadInt32();
                }

                m_Components.Add(new PPtr<Component>(reader));
            }

            _ = reader.ReadInt32(); // m_Layer
            m_Name = reader.ReadAlignedString();
        }

        public bool HasSkinnedMeshTessellationDataHolder()
        {
            if (m_Components == null)
            {
                return false;
            }

            foreach (var componentPtr in m_Components)
            {
                if (componentPtr == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(componentPtr.Name) &&
                    string.Equals(componentPtr.Name, "MonoSkinnedMeshTessellationDataHolder", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (componentPtr.TryGet(out var component) &&
                    component != null &&
                    string.Equals(component.Name, "MonoSkinnedMeshTessellationDataHolder", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasModel()
        {
            return HasMesh(m_Transform, new List<bool>());
        }

        private static bool HasMesh(Transform transform, List<bool> meshes)
        {
            try
            {
                transform.m_GameObject.TryGet(out var gameObject);

                if (gameObject.m_MeshRenderer != null)
                {
                    var mesh = GetMesh(gameObject.m_MeshRenderer);
                    meshes.Add(mesh != null);
                }

                if (gameObject.m_SkinnedMeshRenderer != null)
                {
                    var mesh = GetMesh(gameObject.m_SkinnedMeshRenderer);
                    meshes.Add(mesh != null);
                }

                foreach (var childPtr in transform.m_Children)
                {
                    if (childPtr.TryGet(out var childTransform))
                    {
                        HasMesh(childTransform, meshes);
                    }
                }
            }
            catch
            {
                // Ignore broken hierarchy nodes and continue scanning children.
            }

            return meshes.Any(x => x);
        }

        public static Mesh GetMesh(MeshRenderer meshRenderer)
        {
            if (meshRenderer == null)
            {
                return null;
            }

            if (!meshRenderer.m_GameObject.TryGet(out var gameObject) || gameObject == null)
            {
                return null;
            }

            if (gameObject.m_MeshFilter == null || gameObject.m_MeshFilter.m_Mesh == null)
            {
                return null;
            }

            if (gameObject.m_MeshFilter.m_Mesh.TryGet(out var mesh))
            {
                return mesh;
            }

            return null;
        }

        public static Mesh GetMesh(SkinnedMeshRenderer skinnedMeshRenderer)
        {
            if (skinnedMeshRenderer == null)
            {
                return null;
            }

            if (skinnedMeshRenderer.m_Mesh != null)
            {
                if (!skinnedMeshRenderer.m_Mesh.IsNull)
                {
                    if (skinnedMeshRenderer.m_Mesh.TryGet(out var directMesh) && directMesh != null)
                    {
                        return directMesh;
                    }

                    return null;
                }
            }

            if (skinnedMeshRenderer.m_GameObject != null &&
                skinnedMeshRenderer.m_GameObject.TryGet(out var gameObject) &&
                gameObject != null &&
                gameObject.HasSkinnedMeshTessellationDataHolder())
            {
                return TessellationMeshResolver.ResolveOriginalMesh(skinnedMeshRenderer);
            }

            return null;
        }
    }
}