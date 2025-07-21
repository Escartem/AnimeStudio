using System;
using ACLLibs;

namespace AnimeStudio
{
    public static class ACLExtensions
    {
        public static void Process(this ACLClip m_ACLClip, Game game, out float[] values, out float[] times) 
        {
            if (game.Type.IsSRGroup())
            {
                var aclClip = m_ACLClip as MHYACLClip;
                SRACL.DecompressAll(aclClip.m_ClipData, out values, out times);
            }
            else
            {
                switch (m_ACLClip)
                {
                    case GIACLClip giaclClip:
                        DBACL.DecompressTracks(giaclClip.m_ClipData, giaclClip.m_DatabaseData, out values, out times);
                        break;
                    case ZZZACLClip zzzaclClip:
                        DBACL.DecompressTracks(zzzaclClip.m_TransformData, zzzaclClip.m_ScalarData, zzzaclClip.m_databaseData, zzzaclClip.m_DatabaseData, out values, out times);
                        break;
                    case MHYACLClip mhyaclClip:
                        ACL.DecompressAll(mhyaclClip.m_ClipData, out values, out times);

                        break;
                    default:
                        values = Array.Empty<float>();
                        times = Array.Empty<float>();
                        break;
                }
            }
        }
    }
}
