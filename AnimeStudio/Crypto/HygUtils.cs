using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace AnimeStudio
{
    internal class HygUtils
    {
        public static void Decrypt(Span<byte> buffer, ulong key1, ulong key2, bool skipScrambling=true)
        {
            Logger.Verbose($"Key1 : {key1:X16} | Key2 : {key2:X16}");
            ulong reversedKey1;
            ulong reversedKey2;

            if (skipScrambling)
            {
                reversedKey1 = key1;
                reversedKey2 = key2;
            } else
            {
                // rewriten from the assembly, this could def be optimized
                ulong descrambledKey1 = (((key1 << 16) | key1 & 0xFF00) << 8) | ((((key1 >> 16) & 0xFFFF) | key1 & 0xFF0000) >> 8);
                ulong hi = ((key2 & 0xFF00 | ((key2 & 0xFFFFFFFF) << 16)) << 8) | (((((key2 >> 16) & 0xFFFF) | key2 & 0xFF0000) & 0xFFFFFFFFFFFFFFFF) >> 8);
                ulong lo = ((((key2 >> 32) & 0xFFFF) & 0xFF00 | (((key2 >> 32) & 0xFFFFFFFF) << 16)) << 8) | ((((((key2 >> 32) & 0xFFFFFFFF) >> 16) & 0xFFFF) | ((key2 >> 32) & 0xFFFFFFFF) & 0xFF0000) >> 8);
                ulong descrambledKey2 = (lo & 0xFFFFFFFF) | ((hi & 0xFFFFFFFF) << 32);

                reversedKey1 = BitConverter.ToUInt64(BitConverter.GetBytes(descrambledKey1).Reverse().ToArray(), 0);
                reversedKey2 = BitConverter.ToUInt64(BitConverter.GetBytes(descrambledKey2).Reverse().ToArray(), 0);
            }

            var header = new byte[16];
            // keys in reverse order
            Buffer.BlockCopy(BitConverter.GetBytes(reversedKey2), 0, header, 0, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(reversedKey1), 0, header, 8, 8);
            Logger.Verbose($"Decrypt header : {Convert.ToHexString(header)}");

            BlbUtils.InitKeys(CryptoHelper.Blb3RC4Key, CryptoHelper.HygSBox, CryptoHelper.HygShiftRow, CryptoHelper.HygKey, CryptoHelper.HygMul);

            // same as hna
            for (int i = 0; i < header.Length; i++)
            {
                buffer[i % 0x10] ^= header[i];
            }

            BlbAES.Encrypt(buffer.Slice(0, 16).ToArray(), header.ToArray()).CopyTo(buffer);

            if (buffer.Length > 16)
            {
                BlbUtils.RC4(buffer);
            }

            BlbUtils.Descramble(buffer.Slice(0, 16));
        }
    }
}
