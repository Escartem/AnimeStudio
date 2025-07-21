using System;
using System.Runtime.InteropServices;
using AnimeStudio;
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
        private const string DLL_NAME = "sracl";
        static SRACL()
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

    public static class DBACL
    {
        private const string DLL_NAME = "acldb";
        private const string DLL_NAME_ZZZ = "AnimeStudio.ACLNative2";
        static DBACL()
        {
            DllLoader.PreloadDll(DLL_NAME);
            DllLoader.PreloadDll(DLL_NAME_ZZZ);
        }

        private static IntPtr AlignAndCopyData(byte[] data, out IntPtr base_ptr)
        {
            base_ptr = IntPtr.Zero;
            if (data == null)
            {
                return IntPtr.Zero;
            }
            base_ptr = Marshal.AllocHGlobal(data.Length + 8);
            var dataAligned = new IntPtr(16 * (((long)base_ptr + 15) / 16));
            Marshal.Copy(data, 0, dataAligned, data.Length);
            return dataAligned;
        }

        public static void DecompressTracks(byte[] transform_data, byte[] scalar_data, byte[] db, byte[] db_bulk_data, out float[] values, out float[] times)
        {
            var decompressedClip = new DecompressedClip();

            var transform_data_ptr = AlignAndCopyData(transform_data, out var transform_data_base_ptr);
            var scalar_data_ptr = AlignAndCopyData(scalar_data, out var scalar_data_base_ptr);
            var db_ptr = AlignAndCopyData(db, out var db_base_ptr);
            var db_bulk_data_ptr = AlignAndCopyData(db_bulk_data, out var db_bulk_data_base_ptr);

            DecompressTracksZZZ(transform_data_ptr, scalar_data_ptr, db_ptr, db_bulk_data_ptr, ref decompressedClip);

            Marshal.FreeHGlobal(transform_data_base_ptr);
            Marshal.FreeHGlobal(scalar_data_base_ptr);
            Marshal.FreeHGlobal(db_base_ptr);
            Marshal.FreeHGlobal(db_bulk_data_base_ptr);

            values = new float[decompressedClip.ValuesCount];
            Marshal.Copy(decompressedClip.Values, values, 0, decompressedClip.ValuesCount);

            times = new float[decompressedClip.TimesCount];
            Marshal.Copy(decompressedClip.Times, times, 0, decompressedClip.TimesCount);

            DisposeZZZ(ref decompressedClip);
        }
        public static void DecompressTracks(byte[] data, byte[] db, out float[] values, out float[] times, bool isZZZ = false)
        {
            var decompressedClip = new DecompressedClip();

            var dataPtr = Marshal.AllocHGlobal(data.Length + 8);
            var dataAligned = new IntPtr(16 * (((long)dataPtr + 15) / 16));
            Marshal.Copy(data, 0, dataPtr, data.Length);

            var dbPtr = Marshal.AllocHGlobal(db.Length + 8);
            var dbAligned = new IntPtr(16 * (((long)dbPtr + 15) / 16));
            Marshal.Copy(db, 0, dbAligned, db.Length);

            DecompressTracks(dataAligned, dbAligned, ref decompressedClip);

            Marshal.FreeHGlobal(dataPtr);
            Marshal.FreeHGlobal(dbPtr);

            values = new float[decompressedClip.ValuesCount];
            Marshal.Copy(decompressedClip.Values, values, 0, decompressedClip.ValuesCount);

            times = new float[decompressedClip.TimesCount];
            Marshal.Copy(decompressedClip.Times, times, 0, decompressedClip.TimesCount);

            Dispose(ref decompressedClip);
        }

        #region importfunctions

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void DecompressTracks(nint data, nint db, ref DecompressedClip decompressedClip);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Dispose(ref DecompressedClip decompressedClip);

        [DllImport(DLL_NAME_ZZZ, CallingConvention = CallingConvention.Cdecl, EntryPoint = "DecompressTracksZZZ")]
        private static extern void DecompressTracksZZZ(nint transform_tracks, nint scalar_tracks, nint database, nint bulk_data, ref DecompressedClip decompressedClip);

        [DllImport(DLL_NAME_ZZZ, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Dispose")]
        private static extern void DisposeZZZ(ref DecompressedClip decompressedClip);

        #endregion
    }
}
