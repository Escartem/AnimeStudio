using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AnimeStudio
{
    public class HumanPoseMask
    {
        public uint word0;
        public uint word1;
        public uint word2;

        public HumanPoseMask(ObjectReader reader)
        {
            var version = reader.version;

            word0 = reader.ReadUInt32();
            word1 = reader.ReadUInt32();
            if (version[0] > 5 || (version[0] == 5 && version[1] >= 2)) //5.2 and up
            {
                word2 = reader.ReadUInt32();
            }
        }
    }

    public class SkeletonMaskElement
    {
        public uint m_PathHash;
        public float m_Weight;

        public SkeletonMaskElement(ObjectReader reader)
        {
            m_PathHash = reader.ReadUInt32();
            m_Weight = reader.ReadSingle();
        }
    }

    public class SkeletonMask
    {
        public List<SkeletonMaskElement> m_Data;

        public SkeletonMask(ObjectReader reader)
        {
            int numElements = reader.ReadInt32();
            m_Data = new List<SkeletonMaskElement>();
            for (int i = 0; i < numElements; i++)
            {
                m_Data.Add(new SkeletonMaskElement(reader));
            }
        }
    }

    public class LayerConstant
    {
        public uint m_StateMachineIndex;
        public uint m_StateMachineMotionSetIndex;
        public HumanPoseMask m_BodyMask;
        public SkeletonMask m_SkeletonMask;
        public uint m_Binding;
        public int m_LayerBlendingMode;
        public float m_DefaultWeight;
        public bool m_IKPass;
        public bool m_SyncedLayerAffectsTiming;

        public LayerConstant(ObjectReader reader)
        {
            var version = reader.version;

            m_StateMachineIndex = reader.ReadUInt32();
            m_StateMachineMotionSetIndex = reader.ReadUInt32();
            m_BodyMask = new HumanPoseMask(reader);
            m_SkeletonMask = new SkeletonMask(reader);
            if (reader.Game.Type.IsLoveAndDeepspace())
            {
                var m_GenericMask = new SkeletonMask(reader);
            }
            m_Binding = reader.ReadUInt32();
            m_LayerBlendingMode = reader.ReadInt32();
            if (version[0] > 4 || (version[0] == 4 && version[1] >= 2)) //4.2 and up
            {
                m_DefaultWeight = reader.ReadSingle();
            }
            m_IKPass = reader.ReadBoolean();
            if (version[0] > 4 || (version[0] == 4 && version[1] >= 2)) //4.2 and up
            {
                m_SyncedLayerAffectsTiming = reader.ReadBoolean();
            }
            if (reader.Game.Type.IsArknightsEndfieldCB3())
            {
                var m_MeshSpace = reader.ReadBoolean();
                var m_UseThreePoseBlender = reader.ReadBoolean();
                var m_curveMaskIndex = reader.ReadUInt32();
                var m_DisableRootMotionPass = reader.ReadBoolean();
                var m_ConvertSimpleBlend = reader.ReadBoolean();
                var m_PostProcessLayer = reader.ReadBoolean();
                reader.AlignStream();
                var m_layerOptMode = reader.ReadInt32();
                var m_LODThreshold = reader.ReadInt32();
                var m_AbilityThreshold = reader.ReadInt32();
                var m_UseIdentifyValues = reader.ReadBoolean();
            }
            reader.AlignStream();
        }
    }

    public class ConditionConstant
    {
        public uint m_ConditionMode;
        public uint m_EventID;
        public float m_EventThreshold;
        public float m_ExitTime;

        public ConditionConstant(ObjectReader reader)
        {
            m_ConditionMode = reader.ReadUInt32();
            m_EventID = reader.ReadUInt32();
            m_EventThreshold = reader.ReadSingle();
            m_ExitTime = reader.ReadSingle();
        }
    }

    public class TransitionBlockParamConstant
    {
        public int m_HashId;
        public float m_OutputValue;

        public TransitionBlockParamConstant(ObjectReader reader)
        {
            m_HashId = reader.ReadInt32();
            m_OutputValue = reader.ReadSingle();
        }
    }

    public class TransitionConstant
    {
        public List<ConditionConstant> m_ConditionConstantArray;
        public uint m_DestinationState;
        public uint m_FullPathID;
        public uint m_ID;
        public uint m_UserID;
        public float m_TransitionDuration;
        public float m_TransitionOffset;
        public float m_ExitTime;
        public bool m_HasExitTime;
        public bool m_HasFixedDuration;
        public int m_InterruptionSource;
        public bool m_OrderedInterruption;
        public bool m_Atomic;
        public bool m_CanTransitionToSelf;

        public TransitionConstant(ObjectReader reader)
        {
            var version = reader.version;

            int numConditions = reader.ReadInt32();
            m_ConditionConstantArray = new List<ConditionConstant>();
            for (int i = 0; i < numConditions; i++)
            {
                m_ConditionConstantArray.Add(new ConditionConstant(reader));
            }

            if (reader.Game.Type.IsArknightsEndfieldCB3())
            {
                var numTransitionBlockParam = reader.ReadInt32();
                var m_TransitionBlockParamConstantArray = new List<TransitionBlockParamConstant>();
                for (int i = 0; i < numTransitionBlockParam; i++)
                {
                    m_TransitionBlockParamConstantArray.Add(new TransitionBlockParamConstant(reader));
                }
            }

            m_DestinationState = reader.ReadUInt32();
            if (version[0] >= 5) //5.0 and up
            {
                m_FullPathID = reader.ReadUInt32();
            }

            m_ID = reader.ReadUInt32();
            m_UserID = reader.ReadUInt32();
            m_TransitionDuration = reader.ReadSingle();
            m_TransitionOffset = reader.ReadSingle();
            if (version[0] >= 5) //5.0 and up
            {
                if (reader.Game.Type.IsZZZCB2() || reader.Game.Type.IsZZZ())
                {
                    var m_AutoTransitionOffsetValue = reader.ReadSingle();
                    var m_AutoTransitionOffsetRatio = reader.ReadSingle();
                }
                m_ExitTime = reader.ReadSingle();
                if (reader.Game.Type.IsZZZCB2() || reader.Game.Type.IsZZZ())
                {
                    var m_FrameCount = reader.ReadInt32();
                    var m_TransitionOffsetCount = reader.ReadInt32();
                    var m_TotalFramesSrc = reader.ReadInt32();
                    var m_TotalFramesDest = reader.ReadInt32();
                }
                m_HasExitTime = reader.ReadBoolean();
                if (reader.Game.Type.IsZZZCB2() || reader.Game.Type.IsZZZ())
                {
                    var m_UseFrameCount = reader.ReadBoolean();
                }
                m_HasFixedDuration = reader.ReadBoolean();
                if (reader.Game.Type.IsZZZCB2() || reader.Game.Type.IsZZZ())
                {
                    var m_AutoTransitionOffset = reader.ReadBoolean();
                }
                reader.AlignStream();
                m_InterruptionSource = reader.ReadInt32();
                if (reader.Game.Type.IsArknightsEndfieldCB3())
                {
                    var m_BlendStyle = reader.ReadInt32();
                }
                m_OrderedInterruption = reader.ReadBoolean();
            }
            else
            {
                m_Atomic = reader.ReadBoolean();
            }

            if (version[0] > 4 || (version[0] == 4 && version[1] >= 5)) //4.5 and up
            {
                m_CanTransitionToSelf = reader.ReadBoolean();
            }

            if (reader.Game.Type.IsArknightsEndfieldCB3())
            {
                var m_EnableBlendRootMotion = reader.ReadBoolean();
            }

            reader.AlignStream();
        }
    }

    public class LeafInfoConstant
    {
        public uint[] m_IDArray;
        public uint m_IndexOffset;

        public LeafInfoConstant(ObjectReader reader)
        {
            m_IDArray = reader.ReadUInt32Array();
            m_IndexOffset = reader.ReadUInt32();
        }
    }

    public class MotionNeighborList
    {
        public uint[] m_NeighborArray;

        public MotionNeighborList(ObjectReader reader)
        {
            m_NeighborArray = reader.ReadUInt32Array();
        }
    }

    public class Blend2dDataConstant
    {
        public Vector2[] m_ChildPositionArray;
        public float[] m_ChildMagnitudeArray;
        public Vector2[] m_ChildPairVectorArray;
        public float[] m_ChildPairAvgMagInvArray;
        public List<MotionNeighborList> m_ChildNeighborListArray;

        public Blend2dDataConstant(ObjectReader reader)
        {
            m_ChildPositionArray = reader.ReadVector2Array();
            m_ChildMagnitudeArray = reader.ReadSingleArray();
            m_ChildPairVectorArray = reader.ReadVector2Array();
            m_ChildPairAvgMagInvArray = reader.ReadSingleArray();

            int numNeighbours = reader.ReadInt32();
            m_ChildNeighborListArray = new List<MotionNeighborList>();
            for (int i = 0; i < numNeighbours; i++)
            {
                m_ChildNeighborListArray.Add(new MotionNeighborList(reader));
            }
        }
    }

    public class Blend1dDataConstant // wrong labeled
    {
        public float[] m_ChildThresholdArray;

        public Blend1dDataConstant(ObjectReader reader)
        {
            m_ChildThresholdArray = reader.ReadSingleArray();
        }
    }

    public class BlendDirectDataConstant
    {
        public uint[] m_ChildBlendEventIDArray;
        public bool m_NormalizedBlendValues;

        public BlendDirectDataConstant(ObjectReader reader)
        {
            m_ChildBlendEventIDArray = reader.ReadUInt32Array();
            if (reader.Game.Type.IsArknightsEndfieldCB3())
            {
                var m_ChildPoseTimeEventIDArray = reader.ReadUInt32Array();
            }
            m_NormalizedBlendValues = reader.ReadBoolean();
            if (reader.Game.Type.IsArknightsEndfieldCB3())
            {
                var m_UsePoseTimeValues = reader.ReadBoolean();
            }
            reader.AlignStream();
        }
    }

    public class BlendSequenceDataConstant
    {
        public uint[] m_ChildBlendEventIDArray;
        public uint[] m_ChildPoseTimeEventIDArray;
        public bool m_NormalizedBlendValues;
        public bool m_UsePoseTimeValues;
        public HumanPoseMask m_BodyMask;
        public SkeletonMask m_SkeletonMask;
        public byte[] m_BlendingMode;
        public HumanPoseMask[] m_ChildBodyMask;
        public SkeletonMask[] m_ChildSkeletonMask;
        public float[] m_ChildSpeed;
        public int[] m_ChildLodThreshold;
        public int[] m_ChildAbilityThreshold;
        public byte[] m_ChildCullingMode;
        public bool m_UseBlendDuration;

        public BlendSequenceDataConstant(ObjectReader reader)
        {
            m_ChildBlendEventIDArray = reader.ReadUInt32Array();
            m_ChildPoseTimeEventIDArray = reader.ReadUInt32Array();
            m_NormalizedBlendValues= reader.ReadBoolean();
            m_UsePoseTimeValues = reader.ReadBoolean();
            reader.AlignStream();
            m_BodyMask = new HumanPoseMask(reader);
            m_SkeletonMask = new SkeletonMask(reader);
            m_BlendingMode = reader.ReadUInt8Array();
            reader.AlignStream();

            var numChildBodyMasks = reader.ReadInt32();
            m_ChildBodyMask = new HumanPoseMask[numChildBodyMasks];
            for (int i = 0; i < numChildBodyMasks; i++)
            {
                m_ChildBodyMask[i] = new HumanPoseMask(reader);
            }

            var numChildSkeletonMasks = reader.ReadInt32();
            m_ChildSkeletonMask = new SkeletonMask[numChildSkeletonMasks];
            for (int i = 0; i < numChildSkeletonMasks; i++)
            {
                m_ChildSkeletonMask[i] = new SkeletonMask(reader);
            }

            m_ChildSpeed = reader.ReadSingleArray();
            m_ChildLodThreshold = reader.ReadInt32Array();
            m_ChildAbilityThreshold = reader.ReadInt32Array();
            m_ChildCullingMode = reader.ReadUInt8Array();
            m_UseBlendDuration = reader.ReadBoolean();

            reader.AlignStream();
        }
    }

    public class BlendTreeNodeConstant
    {
        public uint m_BlendType;
        public uint m_BlendEventID;
        public uint m_BlendEventYID;
        public uint[] m_ChildIndices;
        public float[] m_ChildThresholdArray;
        public Blend1dDataConstant m_Blend1dData;
        public Blend2dDataConstant m_Blend2dData;
        public BlendDirectDataConstant m_BlendDirectData;
        public uint m_ClipID;
        public uint m_ClipIndex;
        public float m_Duration;
        public float m_CycleOffset;
        public bool m_Mirror;

        public BlendTreeNodeConstant(ObjectReader reader)
        {
            var version = reader.version;

            if (version[0] > 4 || (version[0] == 4 && version[1] >= 1)) //4.1 and up
            {
                m_BlendType = reader.ReadUInt32();
            }
            m_BlendEventID = reader.ReadUInt32();
            if (version[0] > 4 || (version[0] == 4 && version[1] >= 1)) //4.1 and up
            {
                m_BlendEventYID = reader.ReadUInt32();
            }
            m_ChildIndices = reader.ReadUInt32Array();
            if (version[0] < 4 || (version[0] == 4 && version[1] < 1)) //4.1 down
            {
                m_ChildThresholdArray = reader.ReadSingleArray();
            }

            if (version[0] > 4 || (version[0] == 4 && version[1] >= 1)) //4.1 and up
            {
                m_Blend1dData = new Blend1dDataConstant(reader);
                m_Blend2dData = new Blend2dDataConstant(reader);
            }

            if (version[0] >= 5) //5.0 and up
            {
                m_BlendDirectData = new BlendDirectDataConstant(reader);
            }

            if (reader.Game.Type.IsArknightsEndfieldCB3())
            {
                var m_BlendSequenceData = new BlendSequenceDataConstant(reader);
            }

            m_ClipID = reader.ReadUInt32();
            if (version[0] == 4 && version[1] >= 5) //4.5 - 5.0
            {
                m_ClipIndex = reader.ReadUInt32();
            }

            m_Duration = reader.ReadSingle();

            if (version[0] > 4
                || (version[0] == 4 && version[1] > 1)
                || (version[0] == 4 && version[1] == 1 && version[2] >= 3)) //4.1.3 and up
            {
                m_CycleOffset = reader.ReadSingle();
                if (reader.Game.Type.IsArknightsEndfieldGroup())
                {
                    var m_StateNameHash = reader.ReadUInt32();
                }
                m_Mirror = reader.ReadBoolean();
                reader.AlignStream();
            }
        }
    }

    public class BlendTreeConstant
    {
        public List<BlendTreeNodeConstant> m_NodeArray;
        public ValueArrayConstant m_BlendEventArrayConstant;

        public BlendTreeConstant(ObjectReader reader)
        {
            var version = reader.version;

            int numNodes = reader.ReadInt32();
            m_NodeArray = new List<BlendTreeNodeConstant>();
            for (int i = 0; i < numNodes; i++)
            {
                m_NodeArray.Add(new BlendTreeNodeConstant(reader));
            }

            if (version[0] < 4 || (version[0] == 4 && version[1] < 5)) //4.5 down
            {
                m_BlendEventArrayConstant = new ValueArrayConstant(reader);
            }
        }
    }


    public class StateConstant
    {
        public List<TransitionConstant> m_TransitionConstantArray;
        public int[] m_BlendTreeConstantIndexArray;
        public List<LeafInfoConstant> m_LeafInfoArray;
        public List<BlendTreeConstant> m_BlendTreeConstantArray;
        public uint m_NameID;
        public uint m_PathID;
        public uint m_FullPathID;
        public uint m_TagID;
        public uint m_SpeedParamID;
        public uint m_MirrorParamID;
        public uint m_CycleOffsetParamID;
        public float m_Speed;
        public float m_CycleOffset;
        public bool m_IKOnFeet;
        public bool m_WriteDefaultValues;
        public bool m_Loop;
        public bool m_Mirror;

        private static bool HasMDBBlendRate(SerializedType type) => type.Match("EC609A57E104C0459D2694035D42E771");

        public StateConstant(ObjectReader reader)
        {
            var version = reader.version;

            int numTransistions = reader.ReadInt32();
            m_TransitionConstantArray = new List<TransitionConstant>();
            for (int i = 0; i < numTransistions; i++)
            {
                m_TransitionConstantArray.Add(new TransitionConstant(reader));
            }

            m_BlendTreeConstantIndexArray = reader.ReadInt32Array();

            if (version[0] < 5 || (version[0] == 5 && version[1] < 2)) //5.2 down
            {
                int numInfos = reader.ReadInt32();
                m_LeafInfoArray = new List<LeafInfoConstant>();
                for (int i = 0; i < numInfos; i++)
                {
                    m_LeafInfoArray.Add(new LeafInfoConstant(reader));
                }
            }

            int numBlends = reader.ReadInt32();
            m_BlendTreeConstantArray = new List<BlendTreeConstant>();
            for (int i = 0; i < numBlends; i++)
            {
                m_BlendTreeConstantArray.Add(new BlendTreeConstant(reader));
            }

            m_NameID = reader.ReadUInt32();
            if (version[0] > 4 || (version[0] == 4 && version[1] >= 3)) //4.3 and up
            {
                m_PathID = reader.ReadUInt32();
            }
            if (version[0] >= 5) //5.0 and up
            {
                m_FullPathID = reader.ReadUInt32();
            }

            m_TagID = reader.ReadUInt32();
            if (version[0] > 5 || (version[0] == 5 && version[1] >= 1)) //5.1 and up
            {
                m_SpeedParamID = reader.ReadUInt32();
                m_MirrorParamID = reader.ReadUInt32();
                m_CycleOffsetParamID = reader.ReadUInt32();
            }

            if (version[0] > 2017 || (version[0] == 2017 && version[1] >= 2)) //2017.2 and up
            {
                var m_TimeParamID = reader.ReadUInt32();
            }

            m_Speed = reader.ReadSingle();
            if (version[0] > 4 || (version[0] == 4 && version[1] >= 1)) //4.1 and up
            {
                m_CycleOffset = reader.ReadSingle();
            }
            if (HasMDBBlendRate(reader.serializedType))
            {
                var m_MDBBlendRate = reader.ReadSingle();
            }
            m_IKOnFeet = reader.ReadBoolean();
            if (version[0] >= 5) //5.0 and up
            {
                m_WriteDefaultValues = reader.ReadBoolean();
            }

            m_Loop = reader.ReadBoolean();
            if (version[0] > 4 || (version[0] == 4 && version[1] >= 1)) //4.1 and up
            {
                m_Mirror = reader.ReadBoolean();
            }

            if (reader.Game.Type.IsArknightsEndfieldGroup())
            {
                var m_SyncGroupID = reader.ReadUInt32();
                var m_SyncGroupRole = reader.ReadUInt32();
            }
            if (reader.Game.Type.IsArknightsEndfieldCB3())
            {
                var m_CullingMode = reader.ReadUInt32();
                reader.AlignStream();
                var numStateParameterConstant = reader.ReadInt32();
                var m_StateParameterConstantArray = new StateParameterConstant[numStateParameterConstant];
                for (int i = 0; i < numStateParameterConstant; i++)
                {
                    m_StateParameterConstantArray[i] = new StateParameterConstant(reader);
                }
            }

            reader.AlignStream();
        }
    }

    public class StateParameterConstant
    {
        public int m_NameID;
        public float m_Value;

        public StateParameterConstant(ObjectReader reader)
        {
            m_NameID = reader.ReadInt32();
            m_Value = reader.ReadSingle();
        }
    }

    public class SelectorTransitionConstant
    {
        public uint m_Destination;
        public List<ConditionConstant> m_ConditionConstantArray;

        public SelectorTransitionConstant(ObjectReader reader)
        {
            m_Destination = reader.ReadUInt32();

            int numConditions = reader.ReadInt32();
            m_ConditionConstantArray = new List<ConditionConstant>();
            for (int i = 0; i < numConditions; i++)
            {
                m_ConditionConstantArray.Add(new ConditionConstant(reader));
            }

            if (reader.Game.Type.IsArknightsEndfieldCB3())
            {
                var numTransitionBlockParam = reader.ReadInt32();
                var m_TransitionBlockParamConstantArray = new List<TransitionBlockParamConstant>();
                for (int i = 0; i < numTransitionBlockParam; i++)
                {
                    m_TransitionBlockParamConstantArray.Add(new TransitionBlockParamConstant(reader));
                }
            }
        }
    }

    public class SelectorStateConstant
    {
        public List<SelectorTransitionConstant> m_TransitionConstantArray;
        public uint m_FullPathID;
        public bool m_isEntry;

        public SelectorStateConstant(ObjectReader reader)
        {
            int numTransitions = reader.ReadInt32();
            m_TransitionConstantArray = new List<SelectorTransitionConstant>();
            for (int i = 0; i < numTransitions; i++)
            {
                m_TransitionConstantArray.Add(new SelectorTransitionConstant(reader));
            }

            m_FullPathID = reader.ReadUInt32();
            m_isEntry = reader.ReadBoolean();
            reader.AlignStream();
        }
    }

    public class StateMachineConstant
    {
        public List<StateConstant> m_StateConstantArray;
        public List<TransitionConstant> m_AnyStateTransitionConstantArray;
        public List<SelectorStateConstant> m_SelectorStateConstantArray;
        public uint m_DefaultState;
        public uint m_MotionSetCount;

        public StateMachineConstant(ObjectReader reader)
        {
            var version = reader.version;

            int numStates = reader.ReadInt32();
            m_StateConstantArray = new List<StateConstant>();
            for (int i = 0; i < numStates; i++)
            {
                m_StateConstantArray.Add(new StateConstant(reader));
            }

            int numAnyStates = reader.ReadInt32();
            m_AnyStateTransitionConstantArray = new List<TransitionConstant>();
            for (int i = 0; i < numAnyStates; i++)
            {
                m_AnyStateTransitionConstantArray.Add(new TransitionConstant(reader));
            }

            if (version[0] >= 5) //5.0 and up
            {
                int numSelectors = reader.ReadInt32();
                m_SelectorStateConstantArray = new List<SelectorStateConstant>();
                for (int i = 0; i < numSelectors; i++)
                {
                    m_SelectorStateConstantArray.Add(new SelectorStateConstant(reader));
                }
            }

            m_DefaultState = reader.ReadUInt32();
            m_MotionSetCount = reader.ReadUInt32();
            if (reader.Game.Type.IsArknightsEndfieldCB3())
            {
                var m_ImmediateTransition = reader.ReadBoolean();
                var m_InterruptDynamicTransitions = reader.ReadBoolean();
                reader.AlignStream();
            }
        }
    }

    public class ValueArray
    {
        public bool[] m_BoolValues;
        public int[] m_IntValues;
        public float[] m_FloatValues;
        public Vector4[] m_VectorValues;
        public Vector3[] m_PositionValues;
        public Vector4[] m_QuaternionValues;
        public Vector3[] m_ScaleValues;

        public ValueArray(ObjectReader reader)
        {
            var version = reader.version;

            if (version[0] < 5 || (version[0] == 5 && version[1] < 5)) //5.5 down
            {
                m_BoolValues = reader.ReadBooleanArray();
                reader.AlignStream();
                m_IntValues = reader.ReadInt32Array();
                m_FloatValues = reader.ReadSingleArray();
            }

            if (version[0] < 4 || (version[0] == 4 && version[1] < 3)) //4.3 down
            {
                m_VectorValues = reader.ReadVector4Array();
            }
            else
            {
                m_PositionValues = reader.ReadVector3Array();

                m_QuaternionValues = reader.ReadVector4Array();

                m_ScaleValues = reader.ReadVector3Array();

                if (version[0] > 5 || (version[0] == 5 && version[1] >= 5)) //5.5 and up
                {
                    m_FloatValues = reader.ReadSingleArray();
                    m_IntValues = reader.ReadInt32Array();
                    m_BoolValues = reader.ReadBooleanArray();
                    reader.AlignStream();
                }
            }
        }
    }

    public class ControllerConstant
    {
        public List<LayerConstant> m_LayerArray;
        public List<StateMachineConstant> m_StateMachineArray;
        public ValueArrayConstant m_Values;
        public ValueArray m_DefaultValues;

        public ControllerConstant(ObjectReader reader)
        {
            int numLayers = reader.ReadInt32();
            m_LayerArray = new List<LayerConstant>();
            for (int i = 0; i < numLayers; i++)
            {
                m_LayerArray.Add(new LayerConstant(reader));
            }

            int numStates = reader.ReadInt32();
            m_StateMachineArray = new List<StateMachineConstant>();
            for (int i = 0; i < numStates; i++)
            {
                m_StateMachineArray.Add(new StateMachineConstant(reader));
            }

            m_Values = new ValueArrayConstant(reader);
            m_DefaultValues = new ValueArray(reader);
            if (reader.Game.Type.IsArknightsEndfieldCB3())
            {
                var m_ClothCalculatorType = reader.ReadInt32();
            }
        }
    }

    public class HGAnimationCurveMask : NamedObject
    {
        public uint[] m_Mask;
        public List<string> m_CurveNames;
        public GenericBinding[] m_GenericBindings;

        public HGAnimationCurveMask(ObjectReader reader) : base(reader)
        {
            m_Mask = reader.ReadUInt32Array();

            var curveNamesCount = reader.ReadInt32();
            m_CurveNames = new List<string>(curveNamesCount);
            for (int i = 0; i < curveNamesCount; i++)
            {
                m_CurveNames.Add(reader.ReadAlignedString());
            }
            reader.AlignStream();

            var genericBindingsCount = reader.ReadInt32();
            m_GenericBindings = new GenericBinding[genericBindingsCount];
            reader.AlignStream();
        }
    }

    public sealed class AnimatorController : RuntimeAnimatorController
    {
        public Dictionary<uint, string> m_TOS;
        public List<PPtr<AnimationClip>> m_AnimationClips;

        public AnimatorController(ObjectReader reader) : base(reader)
        {
            var m_ControllerSize = reader.ReadUInt32();
            var m_Controller = new ControllerConstant(reader);

            int tosSize = reader.ReadInt32();
            m_TOS = new Dictionary<uint, string>();
            for (int i = 0; i < tosSize; i++)
            {
                m_TOS.Add(reader.ReadUInt32(), reader.ReadAlignedString());
            }

            if (reader.Game.Type.IsArknightsEndfieldCB3())
            {
                int animationCurveMaskSize = reader.ReadInt32();
                var m_AnimationCurveMask = new PPtr<HGAnimationCurveMask>[animationCurveMaskSize];
                for (int i = 0; i < animationCurveMaskSize; i++)
                {
                    m_AnimationCurveMask[i] = new PPtr<HGAnimationCurveMask>(reader);
                }
            }

            int numClips = reader.ReadInt32();
            m_AnimationClips = new List<PPtr<AnimationClip>>();
            for (int i = 0; i < numClips; i++)
            {
                m_AnimationClips.Add(new PPtr<AnimationClip>(reader));
            }

            // TODO add some more stuff maybe?
        }
    }
}