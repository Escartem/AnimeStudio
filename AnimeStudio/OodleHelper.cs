using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AnimeStudio;


public static class OodleHelper
{
    public static bool use_old = false;

    public static int Decompress(Span<byte> compressed, Span<byte> decompressed)
    {
        if (use_old)
        {
            return OldOodleHelper.Decompress(compressed, decompressed);
        }
        else
        {
            return OozHelper.Decompress(compressed, decompressed);
        }
    }
}


public static class OldOodleHelper
{
    [DllImport(@"oodle.dll")]
    static extern int OodleLZ_Decompress(ref byte compressedBuffer, int compressedBufferSize, ref byte decompressedBuffer, int decompressedBufferSize, int fuzzSafe, int checkCRC, int verbosity, IntPtr rawBuffer, int rawBufferSize, IntPtr fpCallback, IntPtr callbackUserData, IntPtr decoderMemory, IntPtr decoderMemorySize, int threadPhase);

    public static int Decompress(Span<byte> compressed, Span<byte> decompressed)
    {
        int numWrite = -1;
        try
        {
            numWrite = OodleLZ_Decompress(ref compressed[0], compressed.Length, ref decompressed[0], decompressed.Length, 1, 0, 0, 0, 0, 0, 0, 0, 0, 3);
        }
        catch (Exception)
        {
            throw new IOException($"Oodle decompression error, write {numWrite} bytes but expected {decompressed.Length} bytes");
        }

        return numWrite;
    }
}

public static class OozHelper
{
    [DllImport(@"ooz.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int Ooz_Decompress(ref byte compressedBuffer, int compressedBufferSize, ref byte decompressedBuffer, int decompressedBufferSize, int fuzzSafe, int checkCRC, int verbosity, IntPtr rawBuffer, int rawBufferSize, IntPtr fpCallback, IntPtr callbackUserData, IntPtr decoderMemory, IntPtr decoderMemorySize, int threadPhase);

    public static int Decompress(Span<byte> compressed, Span<byte> decompressed)
    {
        int numWrite = -1;
        numWrite = Ooz_Decompress(ref compressed[0], compressed.Length, ref decompressed[0], decompressed.Length, 1, 0, 0, 0, 0, 0, 0, 0, 0, 3);

        if (numWrite <= 0)
        {
            throw new IOException($"Ooz decompression error, write {numWrite} bytes but expected {decompressed.Length} bytes");
        }

        return numWrite;
    }
}