using System;
using System.Runtime.InteropServices;
using AnimeStudio.PInvoke;

namespace ACLLibs
{
    public struct DecompressedClip
    {
        public IntPtr Values;
        public int ValuesCount;
        public IntPtr Times;
        public int TimesCount;
    }
    public static class ACL
    {
        private const string DLL_NAME = "acl";
        static ACL()
        {
            DllLoader.PreloadDll(DLL_NAME);
        }
        public static void DecompressAll(byte[] data, out float[] values, out float[] times)
        {
            var decompressedClip = new DecompressedClip();
            DecompressAll(data, ref decompressedClip);

            values = new float[decompressedClip.ValuesCount];
            Marshal.Copy(decompressedClip.Values, values, 0, decompressedClip.ValuesCount);

            times = new float[decompressedClip.TimesCount];
            Marshal.Copy(decompressedClip.Times, times, 0, decompressedClip.TimesCount);

            Dispose(ref decompressedClip);
        }

        #region importfunctions

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void DecompressAll(byte[] data, ref DecompressedClip decompressedClip);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Dispose(ref DecompressedClip decompressedClip);

        #endregion
    }

    public static class SRACL
    {
        private const string DLL_NAME = "AnimeStudio.ACL.SR";
        static SRACL()
        {
            // x64 only, so it lives in the application directory rather than in x86/x64.
            DllLoader.PreloadDll(DLL_NAME, archSpecific: false);
        }
        public static void DecompressAll(byte[] data, out float[] values, out float[] times)
        {
            var decompressedClip = new DecompressedClip();
            DecompressClip(data, ref decompressedClip);

            values = new float[decompressedClip.ValuesCount];
            Marshal.Copy(decompressedClip.Values, values, 0, decompressedClip.ValuesCount);

            times = new float[decompressedClip.TimesCount];
            Marshal.Copy(decompressedClip.Times, times, 0, decompressedClip.TimesCount);

            Dispose(ref decompressedClip);
        }

        #region importfunctions

        // This one is the acl 1.x uniformly-sampled decoder; its export is named DecompressClip.
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void DecompressClip(byte[] data, ref DecompressedClip decompressedClip);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Dispose(ref DecompressedClip decompressedClip);

        #endregion
    }

    public static class DBACL
    {
        private const string DLL_NAME = "AnimeStudio.ACL.DB";
        private const string DLL_NAME_ZZZ = "AnimeStudio.ACL.ZZZ";
        static DBACL()
        {
            // x64 only, so they live in the application directory rather than in x86/x64.
            DllLoader.PreloadDll(DLL_NAME, archSpecific: false);
            DllLoader.PreloadDll(DLL_NAME_ZZZ, archSpecific: false);
        }
        public static void DecompressTracks(byte[] data, byte[] db, out float[] values, out float[] times, bool isZZZ = false)
        {
            var decompressedClip = new DecompressedClip();

            var dataPtr = Marshal.AllocHGlobal(data.Length + 8);
            var dataAligned = new IntPtr(16 * (((long)dataPtr + 15) / 16));
            Marshal.Copy(data, 0, dataAligned, data.Length);

            var dbPtr = Marshal.AllocHGlobal(db.Length + 8);
            var dbAligned = new IntPtr(16 * (((long)dbPtr + 15) / 16));
            Marshal.Copy(db, 0, dbAligned, db.Length);

            // as long as m_ClipData is passed to the DB dll without the rest it should be fine
            // m_databaseData doesn't seem to be used. For now
            var streamer = IntPtr.Zero;
            if (isZZZ)
            {
                DecompressTracksZZZ(dataAligned, dbAligned, streamer, ref decompressedClip);
            }
            else
            {
                DecompressTracks(dataAligned, dbAligned, streamer, ref decompressedClip);
            }

            Marshal.FreeHGlobal(dataPtr);
            Marshal.FreeHGlobal(dbPtr);

            values = new float[decompressedClip.ValuesCount];
            Marshal.Copy(decompressedClip.Values, values, 0, decompressedClip.ValuesCount);

            times = new float[decompressedClip.TimesCount];
            Marshal.Copy(decompressedClip.Times, times, 0, decompressedClip.TimesCount);

            if (isZZZ)
            {
                DisposeZZZ(ref decompressedClip);
            }
            else
            {
                Dispose(ref decompressedClip);
            }
        }

        #region importfunctions

        // Both DLLs are built from the same dllmain.cpp, so they share this signature.
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void DecompressTracks(nint data, nint db, nint streamer, ref DecompressedClip decompressedClip);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Dispose(ref DecompressedClip decompressedClip);

        [DllImport(DLL_NAME_ZZZ, CallingConvention = CallingConvention.Cdecl, EntryPoint = "DecompressTracks")]
        private static extern void DecompressTracksZZZ(nint data, nint db, nint streamer, ref DecompressedClip decompressedClip);

        [DllImport(DLL_NAME_ZZZ, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Dispose")]
        private static extern void DisposeZZZ(ref DecompressedClip decompressedClip);

        #endregion
    }
}
