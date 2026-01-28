using System;
using System.Runtime.InteropServices;

namespace AnimeStudio
{
    //Special thanks to LukeFZ#4035.
    public static class FairGuardUtils
    {
        private static class CB2Constants
        {
            public const uint SEED_PART0_XOR = 0x226a61b9;
            public const uint SEED_PART1_XOR = 0x7a39d018;
            public const uint SEED_PART2_XOR = 0x18f6d8aa;
            public const uint SEED_PART3_XOR = 0xaa255fb1;
            public const uint SEED_PART4_XOR = 0xf78dd8eb;

            public const uint KEY_PART0_SUB = 0x1C26B82D;
            public const uint KEY_PART1_ADD = 0x3F72EAF3;
            public const uint KEY_PART2_XOR = 0x82C57E3C;
            public const uint KEY_PART3_ADD = 0x6F2A7347;
        }

        private static class CB1Constants
        {
            public const uint SEED_PART0_XOR = 0x1274CBEC ^ 0x3F72EAF3;
            public const uint SEED_PART1_XOR = 0xBE482704;
            public const uint SEED_PART2_XOR = 0x753BDCAA;
            public const uint SEED_PART3_XOR = 0x82C57E3C ^ 0xE3D947D3;
            public const uint SEED_PART4_XOR = 0x6F2A7347 ^ 0x4736C714;

            public const uint KEY_PART0_SUB = 0x1C26B82D;
            public const uint KEY_PART1_ADD = 0x3F72EAF3;
            public const uint KEY_PART2_XOR = 0x82C57E3C;
            public const uint KEY_PART3_ADD = 0x6F2A7347;
        }

        public static void Decrypt(Span<byte> bytes, GameType gameType)
        {
            Logger.Verbose($"Attempting to decrypt block with FairGuard encryption...");

            var encryptedOffset = 0;
            var encryptedSize = Math.Min(0x500, bytes.Length);

            if (encryptedSize < 0x20)
            {
                Logger.Verbose("block size is less that minimum, skipping...");
                return;
            }

            var encrypted = bytes.Slice(encryptedOffset, encryptedSize);
            var encryptedInts = MemoryMarshal.Cast<byte, int>(encrypted);

            for (int i = 0; i < 0x20; i++)
            {
                encrypted[i] ^= 0xA6;
            }

            uint seedPart0, seedPart1, seedPart2, seedPart3, seedPart4;

            if (gameType.IsArknightsEndfieldCB2())
            {
                seedPart0 = (uint)(encryptedInts[2] ^ encryptedInts[6] ^ CB2Constants.SEED_PART0_XOR);
                seedPart1 = (uint)(encryptedInts[3] ^ encryptedInts[0] ^ CB2Constants.SEED_PART1_XOR ^ encryptedSize);
                seedPart2 = (uint)(encryptedInts[1] ^ encryptedInts[5] ^ CB2Constants.SEED_PART2_XOR ^ encryptedSize);
                seedPart3 = (uint)(encryptedInts[0] ^ encryptedInts[7] ^ CB2Constants.SEED_PART3_XOR);
                seedPart4 = (uint)(encryptedInts[4] ^ encryptedInts[7] ^ CB2Constants.SEED_PART4_XOR);
            }
            else if (gameType.IsArknightsEndfieldCB1())
            {
                seedPart0 = (uint)(encryptedInts[2] ^ encryptedInts[6] ^ CB1Constants.SEED_PART0_XOR);
                seedPart1 = (uint)(encryptedInts[3] ^ encryptedInts[0] ^ CB1Constants.SEED_PART1_XOR ^ encryptedSize);
                seedPart2 = (uint)(encryptedInts[1] ^ encryptedInts[5] ^ CB1Constants.SEED_PART2_XOR ^ encryptedSize);
                seedPart3 = (uint)(encryptedInts[0] ^ encryptedInts[7] ^ CB1Constants.SEED_PART3_XOR);
                seedPart4 = (uint)(encryptedInts[4] ^ encryptedInts[7] ^ CB1Constants.SEED_PART4_XOR);
            }
            else
            {
                throw new InvalidOperationException("Unsupported game type for FairGuard decryption");
            }

            var seedInts = new uint[] { seedPart0, seedPart1, seedPart2, seedPart3, seedPart4 };
            var seedBytes = MemoryMarshal.AsBytes<uint>(seedInts);

            var seed = GenerateSeed(seedBytes);
            var seedBuffer = BitConverter.GetBytes(seed);
            seed = CRC.CalculateDigest(seedBuffer, 0, (uint)seedBuffer.Length);

            var key = seedInts[0] ^ seedInts[1] ^ seedInts[2] ^ seedInts[3] ^ seedInts[4] ^ (uint)encryptedSize;

            RC4(seedBytes, key);
            var keySeed = CRC.CalculateDigest(seedBytes.ToArray(), 0, (uint)seedBytes.Length);
            var keySeedBytes = BitConverter.GetBytes(keySeed);
            keySeed = GenerateSeed(keySeedBytes);

            uint keyPart0, keyPart1, keyPart2, keyPart3;

            if (gameType.IsArknightsEndfieldCB2())
            {
                keyPart0 = (seedInts[3] - CB2Constants.KEY_PART0_SUB) ^ keySeed;
                keyPart1 = (seedInts[2] + CB2Constants.KEY_PART1_ADD) ^ seed;
                keyPart2 = seedInts[0] ^ CB2Constants.KEY_PART2_XOR ^ keySeed;
                keyPart3 = (seedInts[1] + CB2Constants.KEY_PART3_ADD) ^ seed;
            }
            else // CB1
            {
                keyPart0 = (seedInts[3] - CB1Constants.KEY_PART0_SUB) ^ keySeed;
                keyPart1 = (seedInts[2] + CB1Constants.KEY_PART1_ADD) ^ seed;
                keyPart2 = seedInts[0] ^ CB1Constants.KEY_PART2_XOR ^ keySeed;
                keyPart3 = (seedInts[1] + CB1Constants.KEY_PART3_ADD) ^ seed;
            }

            var keyVector = new uint[] { keyPart0, keyPart1, keyPart2, keyPart3 };

            var block = encrypted[0x20..];
            if (block.Length >= 0x80)
            {
                RC4(block[..0x60], seed);
                for (int i = 0; i < 0x60; i++)
                {
                    block[i] ^= (byte)(seed ^ 0x6E);
                }

                block = block[0x60..];
                var blockSize = (encryptedSize - 0x80) / 4;
                for (int i = 0; i < 4; i++)
                {
                    var blockOffset = i * blockSize;
                    var blockKey = i switch
                    {
                        0 => 0x6142756Eu,
                        1 => 0x62496E66u,
                        2 => 0x1304B000u,
                        3 => 0x6E8E30ECu,
                        _ => throw new NotImplementedException()
                    };
                    RC4(block.Slice(blockOffset, blockSize), seed);
                    var blockInts = MemoryMarshal.Cast<byte, uint>(block[blockOffset..]);
                    for (int j = 0; j < blockSize / 4; j++)
                    {
                        blockInts[j] ^= seed ^ keyVector[i] ^ blockKey;
                    }
                }
            }
            else
            {
                RC4(block, seed);
            }
        }

        private static uint GenerateSeed(Span<byte> bytes)
        {
            var state = new uint[] { 0xC1646153, 0x78DA0550, 0x2947E56B };
            for (int i = 0; i < bytes.Length; i++)
            {
                state[0] = 0x21 * state[0] + bytes[i];
                if ((state[0] & 0xF) >= 0xB)
                {
                    state[0] = (state[0] ^ RotateIsSet(state[2], 6)) - 0x2CD86315;
                }
                else if ((state[0] & 0xF0) >> 4 > 0xE)
                {
                    state[0] = (state[1] ^ 0xAB4A010B) + (state[0] ^ RotateIsSet(state[2], 9));
                }
                else if ((state[0] & 0xF00) >> 8 < 2)
                {
                    state[1] = ((state[2] >> 3) - 0x55EEAB7B) ^ state[0];
                }
                else if (state[1] + 0x567A >= 0xAB5489E4)
                {
                    state[1] = (state[1] >> 16) ^ state[0];
                }
                else if ((state[1] ^ 0x738766FA) <= state[2])
                {
                    state[1] = (state[1] >> 8) ^ state[2];
                }
                else if (state[1] == 0x68F53AA6)
                {
                    if ((state[1] ^ (state[0] + state[2])) > 0x594AF86E)
                    {
                        state[1] -= 0x8CA292E;
                    }
                    else
                    {
                        state[2] -= 0x760A1649;
                    }
                }
                else
                {
                    if (state[0] > 0x865703AF)
                    {
                        state[1] = state[2] ^ (state[0] - 0x564389D7);
                    }
                    else
                    {
                        state[1] = (state[1] - 0x12B9DD92) ^ state[0];
                    }

                    state[0] ^= RotateIsSet(state[1], 8);
                }
            }

            return state[0];
        }

        private static uint RotateIsSet(uint value, int count) => (((value >> count) != 0) || ((value << (32 - count))) != 0) ? 1u : 0u;

        public class CRC
        {
            private static readonly uint[] Table;

            static CRC()
            {
                Table = new uint[256];
                const uint kPoly = 0xD35E417E;
                for (uint i = 0; i < 256; i++)
                {
                    uint r = i;
                    for (int j = 0; j < 8; j++)
                    {
                        if ((r & 1) != 0)
                            r = (r >> 1) ^ kPoly;
                        else
                            r >>= 1;
                    }
                    Table[i] = r;
                }
            }

            uint _value = 0xFFFFFFFF;

            public void Update(byte[] data, uint offset, uint size)
            {
                for (uint i = 0; i < size; i++)
                    _value = (Table[(byte)_value ^ data[offset + i]] ^ (_value >> 9)) + 0x5B;
            }

            public uint GetDigest() { return ~_value - 0x41607A3D; }

            public static uint CalculateDigest(byte[] data, uint offset, uint size)
            {
                var crc = new CRC();
                crc.Update(data, offset, size);
                return crc.GetDigest();
            }
        }

        public static void RC4(Span<byte> data, uint key) => RC4(data, BitConverter.GetBytes(key));

        public static void RC4(Span<byte> data, byte[] key)
        {
            int[] S = new int[0x100];
            for (int _ = 0; _ < 0x100; _++)
            {
                S[_] = _;
            }

            int[] T = new int[0x100];

            if (key.Length == 0x100)
            {
                Buffer.BlockCopy(key, 0, T, 0, key.Length);
            }
            else
            {
                for (int _ = 0; _ < 0x100; _++)
                {
                    T[_] = key[_ % key.Length];
                }
            }

            int i = 0;
            int j = 0;
            for (i = 0; i < 0x100; i++)
            {
                j = (j + S[i] + T[i]) % 0x100;

                (S[j], S[i]) = (S[i], S[j]);
            }

            i = j = 0;
            for (int iteration = 0; iteration < data.Length; iteration++)
            {
                i = (i + 1) % 0x100;
                j = (j + S[i]) % 0x100;

                (S[j], S[i]) = (S[i], S[j]);
                var K = (uint)S[(S[j] + S[i]) % 0x100];

                var k = (byte)(K << 1) | (K >> 7);
                data[iteration] ^= (byte)(k - 0x61);
            }
        }
    }
}
