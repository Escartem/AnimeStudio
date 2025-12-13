using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AnimeStudio
{
    public sealed class SkinnedMeshRenderer : Renderer
    {
        public PPtr<Mesh> m_Mesh;
        public List<PPtr<Transform>> m_Bones;
        public float[] m_BlendShapeWeights;
        public PPtr<Transform> m_RootBone;
        public AABB m_AABB;
        public bool m_DirtyAABB;

        public SkinnedMeshRenderer(ObjectReader reader) : base(reader)
        {
            int m_Quality = reader.ReadInt32();
            var m_UpdateWhenOffscreen = reader.ReadBoolean();
            if (!reader.Game.Type.IsHYGCB1())
            {
                var m_SkinNormals = reader.ReadBoolean(); //3.1.0 and below
            }
            if (reader.Game.Type.IsHYGCB1())
            {
                var m_UpdateBoundsWhenOffscreen = reader.ReadBoolean();
                var m_SkinnedMotionVectors = reader.ReadBoolean();
                var m_CanSkipSkinning = reader.ReadBoolean();
            }
            reader.AlignStream();

            if (version[0] == 2 && version[1] < 6) //2.6 down
            {
                var m_DisableAnimationWhenOffscreen = new PPtr<Animation>(reader);
            }

            m_Mesh = new PPtr<Mesh>(reader);

            var numBones = reader.ReadInt32();
            m_Bones = new List<PPtr<Transform>>();
            for (int b = 0; b < numBones; b++)
            {
                m_Bones.Add(new PPtr<Transform>(reader));
            }

            if (reader.Game.Type.IsZZZ())
            {
                var m_SortingFudge = reader.ReadSingle();
            }

            if (version[0] > 4 || (version[0] == 4 && version[1] >= 3)) //4.3 and up
            {
                m_BlendShapeWeights = reader.ReadSingleArray();
            }

            if (reader.Game.Type.IsGIGroup() || reader.Game.Type.IsZZZ() || reader.Game.Type.IsHYGCB1() || reader.Game.Type.IsArknightsEndfieldCB3())
            {
                m_RootBone = new PPtr<Transform>(reader);
                m_AABB = new AABB(reader);
                m_DirtyAABB = reader.ReadBoolean();
                reader.AlignStream();
            }

            if (reader.Game.Type.IsArknightsEndfieldCB3())
            {
                var m_SkinningRoot = new PPtr<Transform>(reader);
            }
        }
    }
}
