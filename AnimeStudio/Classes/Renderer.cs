using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AnimeStudio
{
    public class StaticBatchInfo
    {
        public ushort firstSubMesh;
        public ushort subMeshCount;

        public StaticBatchInfo(ObjectReader reader)
        {
            firstSubMesh = reader.ReadUInt16();
            subMeshCount = reader.ReadUInt16();
        }
    }

    public abstract class Renderer : Component
    {
        public List<PPtr<Material>> m_Materials;
        public StaticBatchInfo m_StaticBatchInfo;
        public uint[] m_SubsetIndices;
        private bool isNewHeader = false;

        public static bool HasPrope(SerializedType type) => type.Match("F622BC5EE0E86D7BDF8C912DD94DCBF5") || type.Match("9255FA54269ADD294011FDA525B5FCAC");
        public static bool HasCullingDistance(SerializedType type) => type.Match("BFA28DBFE9993C2ABE21B3408666CFD3");
        public static bool HasStreamingMipmapBias(SerializedType type) => type.Match("3086DE02B7269C6DE7E840C57C244649");

        protected Renderer(ObjectReader reader) : base(reader)
        {
            if (version[0] < 5) //5.0 down
            {
                var m_Enabled = reader.ReadBoolean();
                var m_CastShadows = reader.ReadBoolean();
                var m_ReceiveShadows = reader.ReadBoolean();
                var m_LightmapIndex = reader.ReadByte();
            }
            else //5.0 and up
            {
                if (version[0] > 5 || (version[0] == 5 && version[1] >= 4)) //5.4 and up
                {
                    if (reader.Game.Type.IsGI())
                    {
                        CheckHeader(reader, 0x1A);
                    }
                    if (reader.Game.Type.IsBH3())
                    {
                        CheckHeader(reader, 0x12);
                    }
                    var m_Enabled = reader.ReadBoolean();
                    var m_CastShadows = reader.ReadByte();
                    var m_ReceiveShadows = reader.ReadByte();
                    if (version[0] > 2017 || (version[0] == 2017 && version[1] >= 2)) //2017.2 and up
                    {
                        var m_DynamicOccludee = reader.ReadByte();
                    }
                    if (reader.Game.Type.IsBH3Group())
                    {
                        var m_AllowHalfResolution = reader.ReadByte();
                        int m_EnableGpuQuery = isNewHeader ? reader.ReadByte() : 0;
                    }
                    if (reader.Game.Type.IsGIGroup())
                    {
                        var m_ReceiveDecals = reader.ReadByte();
                        var m_EnableShadowCulling = reader.ReadByte();
                        var m_EnableGpuQuery = reader.ReadByte();
                        var m_AllowHalfResolution = reader.ReadByte();
                        if (!reader.Game.Type.IsGICB1())
                        {
                            if (reader.Game.Type.IsGI())
                            {
                                var m_AllowPerMaterialProp = isNewHeader ? reader.ReadByte() : 0;
                            }
                            var m_IsRainOccluder = reader.ReadByte();
                            if (!reader.Game.Type.IsGICB2())
                            {
                                var m_IsDynamicAOOccluder = reader.ReadByte();
                                if (reader.Game.Type.IsGI())
                                {
                                    var m_IsHQDynamicAOOccluder = reader.ReadByte();
                                    var m_IsCloudObject = reader.ReadByte();
                                    var m_IsInteriorVolume = reader.ReadByte();
                                }
                            }
                            if (!reader.Game.Type.IsGIPack())
                            {
                                var m_IsDynamic = reader.ReadByte();
                            }
                            if (reader.Game.Type.IsGI())
                            {
                                var m_UseTessellation = reader.ReadByte();
                                var m_IsTerrainTessInfo = isNewHeader ? reader.ReadByte() : 0;
                                var m_UseVertexLightInForward = isNewHeader ? reader.ReadByte() : 0;
                                var m_CombineSubMeshInGeoPass = isNewHeader ? reader.ReadByte() : 0;
                            }
                        }
                    }
                    if (version[0] >= 2021) //2021.1 and up
                    {
                        var m_StaticShadowCaster = reader.ReadByte();
                        if (reader.Game.Type.IsArknightsEndfieldGroup())
                        {
                            var m_RealtimeShadowCaster = reader.ReadByte();
                            var m_SubMeshRenderMode = reader.ReadByte();
                            var m_CharacterIndex = reader.ReadByte();
                        }
                    }
                    var m_MotionVectors = reader.ReadByte();
                    var m_LightProbeUsage = reader.ReadByte();
                    var m_ReflectionProbeUsage = reader.ReadByte();
                    if (version[0] > 2019 || (version[0] == 2019 && version[1] >= 3)) //2019.3 and up
                    {
                        var m_RayTracingMode = reader.ReadByte();
                    }
                    if (version[0] >= 2020 || reader.Game.Type.IsZZZ()) //2020.1 and up
                    {
                        var m_RayTraceProcedural = reader.ReadByte();
                    }
                    if (reader.Game.Type.IsHYGCB1())
                    {
                        var m_UseOverrideAABBForCulling = reader.ReadByte();
                    }
                    if (reader.Game.Type.IsGI() || reader.Game.Type.IsGICB3() || reader.Game.Type.IsGICB3Pre())
                    {
                        var m_MeshShowQuality = reader.ReadByte();
                    }
                    if (reader.Game.Type.IsArknightsEndfieldCB3())
                    {
                        var m_RenderFoliageOccluder = reader.ReadByte();
                    }
                    reader.AlignStream();
                }
                else
                {
                    var m_Enabled = reader.ReadBoolean();
                    reader.AlignStream();
                    var m_CastShadows = reader.ReadByte();
                    var m_ReceiveShadows = reader.ReadBoolean();
                    reader.AlignStream();
                }

                if (reader.Game.Type.IsArknightsEndfieldCB3())
                {
                    var m_PlatformSpecificCastShadows = reader.ReadUInt32();
                }

                if (version[0] >= 2018 || (reader.Game.Type.IsBH3() && isNewHeader)) //2018 and up
                {
                    var m_RenderingLayerMask = reader.ReadUInt32();
                }

                if (version[0] > 2018 || (version[0] == 2018 && version[1] >= 3)) //2018.3 and up
                {
                    var m_RendererPriority = reader.ReadInt32();
                }

                var m_LightmapIndex = reader.ReadUInt16();
                var m_LightmapIndexDynamic = reader.ReadUInt16();
                if (reader.Game.Type.IsGIGroup() && (m_LightmapIndex != 0xFFFF || m_LightmapIndexDynamic != 0xFFFF))
                {
                    throw new Exception("Not Supported !! skipping....");
                }
            }

            if (version[0] >= 3) //3.0 and up
            {
                var m_LightmapTilingOffset = reader.ReadVector4();
            }

            if (version[0] >= 5) //5.0 and up
            {
                var m_LightmapTilingOffsetDynamic = reader.ReadVector4();
            }

            if (reader.Game.Type.IsGIGroup())
            {
                var m_ViewDistanceRatio = reader.ReadSingle();
                var m_ShaderLODDistanceRatio = reader.ReadSingle();
            }
            if (reader.Game.Type.IsHYGCB1())
            {
                var m_ViewDistanceRatio = reader.ReadSingle();
            }
            var m_MaterialsSize = reader.ReadInt32();
            m_Materials = new List<PPtr<Material>>();
            for (int i = 0; i < m_MaterialsSize; i++)
            {
                m_Materials.Add(new PPtr<Material>(reader));
            }

            if (version[0] < 3) //3.0 down
            {
                var m_LightmapTilingOffset = reader.ReadVector4();
            }
            else //3.0 and up
            {
                if (version[0] > 5 || (version[0] == 5 && version[1] >= 5)) //5.5 and up
                {
                    m_StaticBatchInfo = new StaticBatchInfo(reader);
                }
                else
                {
                    m_SubsetIndices = reader.ReadUInt32Array();
                }

                var m_StaticBatchRoot = new PPtr<Transform>(reader);
            }

            if (reader.Game.Type.IsGIGroup())
            {
                var m_MatLayers = reader.ReadInt32();
            }

            if (!reader.Game.Type.IsSR() || !HasPrope(reader.serializedType))
            {
                if (version[0] > 5 || (version[0] == 5 && version[1] >= 4)) //5.4 and up
                {
                    var m_ProbeAnchor = new PPtr<Transform>(reader);
                    var m_LightProbeVolumeOverride = new PPtr<GameObject>(reader);
                }
                else if (version[0] > 3 || (version[0] == 3 && version[1] >= 5)) //3.5 - 5.3
                {
                    var m_UseLightProbes = reader.ReadBoolean();
                    reader.AlignStream();

                    if (version[0] >= 5)//5.0 and up
                    {
                        var m_ReflectionProbeUsage = reader.ReadInt32();
                    }

                    var m_LightProbeAnchor = new PPtr<Transform>(reader); //5.0 and up m_ProbeAnchor
                }
            }

            if (reader.Game.Type.IsArknightsEndfieldCB3())
            {
                var m_ShadowProxyMesh = new PPtr<Mesh>(reader);
            }

            if (version[0] > 4 || (version[0] == 4 && version[1] >= 3)) //4.3 and up
            {
                if (version[0] == 4 && version[1] == 3) //4.3
                {
                    var m_SortingLayer = reader.ReadInt16();
                }
                else
                {
                    var m_SortingLayerID = reader.ReadUInt32();
                }

                //SInt16 m_SortingLayer 5.6 and up
                var m_SortingLayer2 = reader.ReadInt16();
                var m_SortingOrder = reader.ReadInt16();
                reader.AlignStream();
                if (reader.Game.Type.IsGIGroup() || reader.Game.Type.IsBH3())
                {
                    var m_UseHighestMip = reader.ReadBoolean();
                    reader.AlignStream();
                }
                if (reader.Game.Type.IsSR())
                {
                    var RenderFlag = reader.ReadUInt32();
                    if (HasStreamingMipmapBias(reader.serializedType))
                    {
                        var m_StreamingMipmapBias = reader.ReadSingle();
                    }
                    reader.AlignStream();
                }
                if (reader.Game.Type.IsZZZ())
                {
                    var m_NeedHizCulling = reader.ReadBoolean();
                    var m_HighShadingRate = reader.ReadBoolean();
                    var m_RayTracingLayerMask = reader.ReadBoolean();
                    reader.AlignStream();
                    if (HasCullingDistance(reader.serializedType))
                    {
                        var m_CullingDistance = reader.ReadSingle();
                    }
                }
                if (reader.Game.Type.IsArknightsEndfieldCB3())
                {
                    var m_EnableCharacterOutline = reader.ReadBoolean();
                    var m_EnablePerRendererLighting = reader.ReadBoolean();
                    reader.AlignStream();
                    var m_PerRendererLightingOffset = reader.ReadVector3();
                    var m_PerRendererLightingAnchor = new PPtr<Transform>(reader);
                    var m_LightModeMask = reader.ReadUInt32();
                    var m_RendererSortingFudge = reader.ReadSingle();
                    reader.AlignStream();
                }
            }
        }

        private void CheckHeader(ObjectReader reader, int offset)
        {
            short value = 0;
            var pos = reader.Position;
            while (value != -1 && reader.Position <= pos + offset)
            {
                value = reader.ReadInt16();
            }
            isNewHeader = (reader.Position - pos) == offset;
            reader.Position = pos;
        }
    }
}
