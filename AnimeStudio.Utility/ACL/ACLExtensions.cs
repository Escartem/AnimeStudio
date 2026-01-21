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
                    case MHYACLClip mhyaclClip:
                        if (game.Type.IsZZZ())
                        {
                            DBACL.DecompressTracks(mhyaclClip.m_ClipData, mhyaclClip.m_databaseData, out values, out times, true);
                        }
                        else
                        {
                            ACL.DecompressAll(mhyaclClip.m_ClipData, out values, out times);
                        }

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
