using System;
using System.Text;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;

namespace AnimeStudio
{
    public class ManjuuUtils
    {
        private const string Signature = "#$manjuuunity*!@";
        private const string Key = "7a346c32336268352333356826333231";
        private static ICryptoTransform Encryptor;

        public byte[] Index = new byte[0x10];
        public byte[] Sub = new byte[0x10];

        public ManjuuUtils(EndianBinaryReader reader)
        {
            reader.ReadUInt32();

            var infoBytes = reader.ReadBytes(0x10);
            var infoKey = reader.ReadBytes(0x10);
            reader.Position += 1;

            var signatureBytes = reader.ReadBytes(0x10);
            var signatureKey = reader.ReadBytes(0x10);
            reader.Position += 1;

            Logger.Verbose($"Initializing ManjuuUtils decryptor with key");
            try
            {
                using var aes = Aes.Create();
                aes.Mode = CipherMode.ECB;
                aes.Key = Convert.FromHexString(Key);

                Encryptor = aes.CreateEncryptor();
                Logger.Verbose($"Decryptor initialized !!");
            }
            catch (Exception e)
            {
                Logger.Error($"Invalid key !!\n{e.Message}");
            }

            DecryptKey(signatureKey, signatureBytes);

            var str = Encoding.UTF8.GetString(signatureBytes);
            Logger.Verbose($"Decrypted signature is {str}");
            if (str != Signature)
            {
                throw new Exception($"Invalid Signature, Expected {Signature} but found {str} instead");
            }

            DecryptKey(infoKey, infoBytes);

            infoBytes = infoBytes.ToUInt4Array();
            infoBytes.AsSpan(0, 0x10).CopyTo(Index);
            var subBytes = infoBytes.AsSpan(0x10, 0x10);
            for (var i = 0; i < subBytes.Length; i++)
            {
                var idx = (i % 4 * 4) + (i / 4);
                Sub[idx] = subBytes[i];
            }

        }

        public void DecryptBlock(Span<byte> bytes, int size, int index)
        {
            var offset = 0;
            while (offset < size)
            {
                offset += Decrypt(bytes.Slice(offset), index++, size - offset);
            }
        }

        private void DecryptKey(byte[] key, byte[] data)
        {
            if (Encryptor != null)
            {
                key = Encryptor.TransformFinalBlock(key, 0, key.Length);
                for (int i = 0; i < 0x10; i++)
                    data[i] ^= key[i];
            }
        }

        private int DecryptByte(Span<byte> bytes, ref int offset, ref int index)
        {
            var b = Sub[((index >> 2) & 3) + 4] + Sub[index & 3] + Sub[((index >> 4) & 3) + 8] + Sub[((byte)index >> 6) + 12];
            bytes[offset] = (byte)((Index[bytes[offset] & 0xF] - b) & 0xF | 0x10 * (Index[bytes[offset] >> 4] - b));
            b = bytes[offset];
            offset++;
            index++;
            return b;
        }

        private int Decrypt(Span<byte> bytes, int index, int remaining)
        {
            var offset = 0;

            var curByte = DecryptByte(bytes, ref offset, ref index);
            var byteHigh = curByte >> 4;
            var byteLow = curByte & 0xF;

            if (byteHigh == 0xF)
            {
                int b;
                do
                {
                    b = DecryptByte(bytes, ref offset, ref index);
                    byteHigh += b;
                } while (b == 0xFF);
            }

            offset += byteHigh;

            if (offset < remaining)
            {
                DecryptByte(bytes, ref offset, ref index);
                DecryptByte(bytes, ref offset, ref index);
                if (byteLow == 0xF)
                {
                    int b;
                    do
                    {
                        b = DecryptByte(bytes, ref offset, ref index);
                    } while (b == 0xFF);
                }
            }

            return offset;
        }
    }
}