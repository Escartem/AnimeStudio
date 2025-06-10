using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AnimeStudio;
public static class OozHelper
{
    [DllImport(@"ooz.dll")]
    static extern int Ooz_Decompress(ref byte compressedBuffer, int compressedBufferSize, ref byte decompressedBuffer, int decompressedBufferSize, int fuzzSafe, int checkCRC, int verbosity, IntPtr rawBuffer, int rawBufferSize, IntPtr fpCallback, IntPtr callbackUserData, IntPtr decoderMemory, IntPtr decoderMemorySize, int threadPhase);

    public static int Decompress(Span<byte> compressed, Span<byte> decompressed)
    {
        int numWrite = -1;
        try
        {
            numWrite = Ooz_Decompress(ref compressed[0], compressed.Length, ref decompressed[0], decompressed.Length, 1, 0, 0, 0, 0, 0, 0, 0, 0, 3);
        }
        catch (Exception)
        {
            throw new IOException($"Oodle decompression error, write {numWrite} bytes but expected {decompressed.Length} bytes");
        }

        return numWrite;
    }
}