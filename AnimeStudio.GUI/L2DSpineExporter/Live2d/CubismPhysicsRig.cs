using System;
using System.Collections.Generic;
using System.IO;
using AnimeStudio;

namespace UnityLive2DExtractor
{
    public class CubismPhysicsNormalizationTuplet
    {
        public float Maximum;
        public float Minimum;
        public float Default;

        public CubismPhysicsNormalizationTuplet(BinaryReader reader)
        {
            Maximum = reader.ReadSingle();
            Minimum = reader.ReadSingle();
            Default = reader.ReadSingle();
        }
    }

    public class CubismPhysicsNormalization
    {
        public CubismPhysicsNormalizationTuplet Position;
        public CubismPhysicsNormalizationTuplet Angle;

        public CubismPhysicsNormalization(BinaryReader reader)
        {
            Position = new CubismPhysicsNormalizationTuplet(reader);
            Angle = new CubismPhysicsNormalizationTuplet(reader);
        }
    }

    public class CubismPhysicsParticle
    {
        public Vector2 InitialPosition;
        public float Mobility;
        public float Delay;
        public float Acceleration;
        public float Radius;

        public CubismPhysicsParticle(EndianBinaryReader reader)
        {
            InitialPosition = reader.ReadVector2();
            Mobility = reader.ReadSingle();
            Delay = reader.ReadSingle();
            Acceleration = reader.ReadSingle();
            Radius = reader.ReadSingle();
        }
    }

    public class CubismPhysicsOutput
    {
        public string DestinationId;
        public int ParticleIndex;
        public Vector2 TranslationScale;
        public float AngleScale;
        public float Weight;
        public CubismPhysicsSourceComponent SourceComponent;
        public bool IsInverted;

        public CubismPhysicsOutput(EndianBinaryReader reader)
        {
            DestinationId = reader.ReadAlignedString();
            ParticleIndex = reader.ReadInt32();
            TranslationScale = reader.ReadVector2();
            AngleScale = reader.ReadSingle();
            Weight = reader.ReadSingle();
            SourceComponent = (CubismPhysicsSourceComponent)reader.ReadInt32();
            IsInverted = reader.ReadBoolean();
            reader.AlignStream();
        }
    }


    public enum CubismPhysicsSourceComponent
    {
        X,
        Y,
        Angle,
    }

    public class CubismPhysicsInput
    {
        public string SourceId;
        public Vector2 ScaleOfTranslation;
        public float AngleScale;
        public float Weight;
        public CubismPhysicsSourceComponent SourceComponent;
        public bool IsInverted;

        public CubismPhysicsInput(EndianBinaryReader reader)
        {
            SourceId = reader.ReadAlignedString();
            ScaleOfTranslation = reader.ReadVector2();
            AngleScale = reader.ReadSingle();
            Weight = reader.ReadSingle();
            SourceComponent = (CubismPhysicsSourceComponent)reader.ReadInt32();
            IsInverted = reader.ReadBoolean();
            reader.AlignStream();
        }
    }

    public class CubismPhysicsSubRig
    {
        public CubismPhysicsInput[] Input;
        public CubismPhysicsOutput[] Output;
        public CubismPhysicsParticle[] Particles;
        public CubismPhysicsNormalization Normalization;

        public CubismPhysicsSubRig(EndianBinaryReader reader)
        {
            var numInput = reader.ReadInt32();
            Input = new CubismPhysicsInput[numInput];
            for (int i = 0; i < numInput; i++)
            {
                Input[i] = new CubismPhysicsInput(reader);
            }
            var numOutput = reader.ReadInt32();
            Output = new CubismPhysicsOutput[numOutput];
            for (int i = 0; i < numOutput; i++)
            {
                Output[i] = new CubismPhysicsOutput(reader);
            }
            var numParticles = reader.ReadInt32();
            Particles = new CubismPhysicsParticle[numParticles];
            for (int i = 0; i < numParticles; i++)
            {
                Particles[i] = new CubismPhysicsParticle(reader);
            }
            Normalization = new CubismPhysicsNormalization(reader);
        }
    }

    public class CubismPhysicsRig
    {
        public CubismPhysicsSubRig[] SubRigs;

        public CubismPhysicsRig(EndianBinaryReader reader)
        {
            var numSubRigs = reader.ReadInt32();
            SubRigs = new CubismPhysicsSubRig[numSubRigs];
            for (int i = 0; i < numSubRigs; i++)
            {
                SubRigs[i] = new CubismPhysicsSubRig(reader);
            }
        }
    }
}
