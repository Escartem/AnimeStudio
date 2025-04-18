﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AnimeStudio
{
    public class AnimationClipOverride
    {
        public PPtr<AnimationClip> m_OriginalClip;
        public PPtr<AnimationClip> m_OverrideClip;

        public AnimationClipOverride(ObjectReader reader)
        {
            m_OriginalClip = new PPtr<AnimationClip>(reader);
            m_OverrideClip = new PPtr<AnimationClip>(reader);
        }
    }

    public sealed class AnimatorOverrideController : RuntimeAnimatorController
    {
        public PPtr<RuntimeAnimatorController> m_Controller;
        public List<AnimationClipOverride> m_Clips;

        public AnimatorOverrideController(ObjectReader reader) : base(reader)
        {
            m_Controller = new PPtr<RuntimeAnimatorController>(reader);

            int numOverrides = reader.ReadInt32();
            m_Clips = new List<AnimationClipOverride>();
            for (int i = 0; i < numOverrides; i++)
            {
                m_Clips.Add(new AnimationClipOverride(reader));
            }
        }
    }
}
