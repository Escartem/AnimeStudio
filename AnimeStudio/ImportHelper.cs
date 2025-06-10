using Org.Brotli.Dec;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using static AnimeStudio.BundleFile;
using static AnimeStudio.CryptoHelper;

namespace AnimeStudio
{
    public static class ImportHelper
    {
        public static void MergeSplitAssets(string path, bool allDirectories = false)
        {
            Logger.Verbose($"Processing split assets (.splitX) prior to loading files...");
            var splitFiles = Directory.GetFiles(path, "*.split0", allDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            Logger.Verbose($"Found {splitFiles.Length} split files, attempting to merge...");
            foreach (var splitFile in splitFiles)
            {
                var destFile = Path.GetFileNameWithoutExtension(splitFile);
                var destPath = Path.GetDirectoryName(splitFile);
                var destFull = Path.Combine(destPath, destFile);
                if (!File.Exists(destFull))
                {
                    var splitParts = Directory.GetFiles(destPath, destFile + ".split*");
                    Logger.Verbose($"Creating {destFull} where split files will be combined");
                    using (var destStream = File.Create(destFull))
                    {
                        for (int i = 0; i < splitParts.Length; i++)
                        {
                            var splitPart = destFull + ".split" + i;
                            using (var sourceStream = File.OpenRead(splitPart))
                            {
                                sourceStream.CopyTo(destStream);
                                Logger.Verbose($"{splitPart} has been combined into {destFull}");
                            }
                        }
                    }
                }
            }
        }

        public static string[] ProcessingSplitFiles(List<string> selectFile)
        {
            Logger.Verbose("Filter out paths that has .split and has the same name");
            var splitFiles = selectFile.Where(x => x.Contains(".split"))
                .Select(x => Path.Combine(Path.GetDirectoryName(x), Path.GetFileNameWithoutExtension(x)))
                .Distinct()
                .ToList();
            selectFile.RemoveAll(x => x.Contains(".split"));
            foreach (var file in splitFiles)
            {
                if (File.Exists(file))
                {
                    selectFile.Add(file);
                }
            }
            return selectFile.Distinct().ToArray();
        }

        public static FileReader DecompressGZip(FileReader reader)
        {
            Logger.Verbose($"Decompressing GZip file {reader.FileName} into memory");
            using (reader)
            {
                var stream = new MemoryStream();
                using (var gs = new GZipStream(reader.BaseStream, CompressionMode.Decompress))
                {
                    gs.CopyTo(stream);
                }
                stream.Position = 0;
                return new FileReader(reader.FullPath, stream);
            }
        }

        public static FileReader DecompressBrotli(FileReader reader)
        {
            Logger.Verbose($"Decompressing Brotli file {reader.FileName} into memory");
            using (reader)
            {
                var stream = new MemoryStream();
                using (var brotliStream = new BrotliInputStream(reader.BaseStream))
                {
                    brotliStream.CopyTo(stream);
                }
                stream.Position = 0;
                return new FileReader(reader.FullPath, stream);
            }
        }

        public static FileReader DecryptPack(FileReader reader, Game game)
        {
            Logger.Verbose($"Attempting to decrypt file {reader.FileName} with Pack encryption");

            const int PackSize = 0x880;
            const string PackSignature = "pack";
            const string UnityFSSignature = "UnityFS";
           
            var data = reader.ReadBytes((int)reader.Length);
            var packIdx = data.Search(PackSignature);
            if (packIdx == -1)
            {
                Logger.Verbose($"Signature {PackSignature} was not found, aborting...");
                reader.Position = 0;
                return reader;
            }
            Logger.Verbose($"Found signature {PackSignature} at offset 0x{packIdx:X8}");
            var mr0kIdx = data.Search("mr0k", packIdx);
            if (mr0kIdx == -1)
            {
                Logger.Verbose("Signature mr0k was not found, aborting...");
                reader.Position = 0;
                return reader;
            }
            Logger.Verbose($"Found signature mr0k signature at offset 0x{mr0kIdx:X8}");

            Logger.Verbose("Attempting to process pack chunks...");
            var ms = new MemoryStream();
            try
            {
                var mr0k = (Mr0k)game;

                long readSize = 0;
                long bundleSize = 0;
                reader.Position = 0;
                while (reader.Remaining > 0)
                {
                    var pos = reader.Position;
                    var signature = reader.ReadStringToNull(4);
                    if (signature == PackSignature)
                    {
                        Logger.Verbose($"Found {PackSignature} chunk at position {reader.Position - PackSignature.Length}");
                        var isMr0k = reader.ReadBoolean();
                        Logger.Verbose("Chunk is mr0k encrypted");
                        var blockSize = BinaryPrimitives.ReadInt32LittleEndian(reader.ReadBytes(4));

                        Logger.Verbose($"Chunk size is 0x{blockSize:X8}");
                        Span<byte> buffer = new byte[blockSize];
                        reader.Read(buffer);
                        if (isMr0k)
                        {
                            buffer = Mr0kUtils.Decrypt(buffer, mr0k);
                        }
                        ms.Write(buffer);

                        if (bundleSize == 0)
                        {
                            Logger.Verbose("This is header chunk !! attempting to read the bundle size");
                            using var blockReader = new EndianBinaryReader(new MemoryStream(buffer.ToArray()));
                            var header = new Header()
                            {
                                signature = blockReader.ReadStringToNull(),
                                version = blockReader.ReadUInt32(),
                                unityVersion = blockReader.ReadStringToNull(),
                                unityRevision = blockReader.ReadStringToNull(),
                                size = blockReader.ReadInt64()
                            };
                            bundleSize = header.size;
                            Logger.Verbose($"Bundle size is 0x{bundleSize:X8}");
                        }

                        readSize += buffer.Length;

                        if (readSize % (PackSize - 0x80) == 0)
                        {
                            var padding = PackSize - 9 - blockSize;
                            reader.Position += padding;
                            Logger.Verbose($"Skip 0x{padding:X8} padding");
                        }

                        if (readSize == bundleSize)
                        {
                            Logger.Verbose($"Bundle has been read entirely !!");
                            readSize = 0;
                            bundleSize = 0;
                        }

                        continue;
                    }

                    reader.Position = pos;
                    signature = reader.ReadStringToNull();
                    if (signature == UnityFSSignature)
                    {
                        Logger.Verbose($"Found {UnityFSSignature} chunk at position {reader.Position - (UnityFSSignature.Length + 1)}");
                        var header = new Header()
                        {
                            signature = reader.ReadStringToNull(),
                            version = reader.ReadUInt32(),
                            unityVersion = reader.ReadStringToNull(),
                            unityRevision = reader.ReadStringToNull(),
                            size = reader.ReadInt64()
                        };

                        Logger.Verbose($"Bundle size is 0x{header.size:X8}");
                        reader.Position = pos;
                        reader.BaseStream.CopyTo(ms, header.size);
                        continue;
                    }
                    
                    throw new InvalidOperationException($"Expected signature {PackSignature} or {UnityFSSignature}, got {signature} instead !!");
                }
            }
            catch (InvalidCastException)
            {
                Logger.Error($"Game type mismatch, Expected {nameof(GameType.GI_Pack)} ({nameof(Mr0k)}) but got {game.Name} ({game.GetType().Name}) !!");
            }
            catch (Exception e)
            {
                Logger.Error($"Error while reading pack file {reader.FullPath}", e);
            }
            finally
            {
                reader.Dispose();
            }

            Logger.Verbose("Decrypted pack file successfully !!");
            ms.Position = 0;
            return new FileReader(reader.FullPath, ms);
        }

        public static FileReader DecryptMark(FileReader reader)
        {
            Logger.Verbose($"Attempting to decrypt file {reader.FileName} with Mark encryption");

            var signature = reader.ReadStringToNull(4);
            if (signature != "mark")
            {
                Logger.Verbose($"Expected signature mark, found {signature} instead, aborting...");
                reader.Position = 0;
                return reader;
            }

            const int BlockSize = 0xA00;
            const int ChunkSize = 0x264;
            const int ChunkPadding = 4;

            var blockPadding = ((BlockSize / ChunkSize) + 1) * ChunkPadding;
            var chunkSizeWithPadding = ChunkSize + ChunkPadding;
            var blockSizeWithPadding = BlockSize + blockPadding;

            var index = 0;
            var block = new byte[blockSizeWithPadding];
            var chunk = new byte[chunkSizeWithPadding];
            var dataStream = new MemoryStream();
            while (reader.BaseStream.Length != reader.BaseStream.Position)
            {
                var readBlockBytes = reader.Read(block);
                using var blockStream = new MemoryStream(block, 0, readBlockBytes);
                while (blockStream.Length != blockStream.Position)
                {
                    var readChunkBytes = blockStream.Read(chunk);
                    if (readBlockBytes == blockSizeWithPadding || readChunkBytes == chunkSizeWithPadding)
                    {
                        readChunkBytes -= ChunkPadding;
                    }
                    for (int i = 0; i < readChunkBytes; i++)
                    {
                        chunk[i] ^= MarkKey[index++ % MarkKey.Length];
                    }
                    dataStream.Write(chunk, 0, readChunkBytes);
                }
            }

            Logger.Verbose("Decrypted mark file successfully !!");
            reader.Dispose();
            dataStream.Position = 0;
            return new FileReader(reader.FullPath, dataStream);
        }

        public static FileReader DecryptEnsembleStar(FileReader reader)
        {
            Logger.Verbose($"Attempting to decrypt file {reader.FileName} with Ensemble Star encryption");
            using (reader)
            {
                var data = reader.ReadBytes((int)reader.Length);
                var count = data.Length;

                var stride = count % 3 + 1;
                var remaining = count % 7;
                var size = remaining + ~(count % 3) + EnsembleStarKey2.Length;
                for (int i = 0; i < count; i += stride)
                {
                    var offset = i / stride;
                    var k1 = offset % EnsembleStarKey1.Length;
                    var k2 = offset % EnsembleStarKey2.Length;
                    var k3 = offset % EnsembleStarKey3.Length;

                    data[i] = (byte)(EnsembleStarKey1[k1] ^ ((size ^ EnsembleStarKey3[k3] ^ data[i] ^ EnsembleStarKey2[k2]) + remaining));
                }

                Logger.Verbose("Decrypted Ensemble Star file successfully !!");
                return new FileReader(reader.FullPath, new MemoryStream(data));
            }
        }

        public static FileReader ParseFakeHeader(FileReader reader)
        {
            Logger.Verbose($"Attempting to parse file {reader.FileName} with fake header");

            var stream = reader.BaseStream;
            var data = reader.ReadBytes(0x1000);
            var idx = data.Search("UnityFS");
            if (idx != -1)
            {
                Logger.Verbose($"Found fake header at offset 0x{idx:X8}");
                var idx2 = data[(idx + 1)..].Search("UnityFS");
                if (idx2 != -1)
                {
                    Logger.Verbose($"Found real header at offset 0x{idx + idx2 + 1:X8}");
                    stream = new OffsetStream(stream, idx + idx2 + 1);
                }
                else
                {
                    Logger.Verbose("Real header was not found, assuming fake header is the real one");
                    stream = new OffsetStream(stream, idx);
                }
            }

            Logger.Verbose("Parsed fake header file successfully !!");
            return new FileReader(reader.FullPath, stream);
        }
        
        public static FileReader DecryptFantasyOfWind(FileReader reader)
        {
            Logger.Verbose($"Attempting to decrypt file {reader.FileName} with Fantasy of Wind encryption");

            byte[] encryptKeyName = Encoding.UTF8.GetBytes("28856");
            const int MinLength = 0xC8;
            const int KeyLength = 8;
            const int EnLength = 0x32;
            const int StartEnd = 0x14;
            const int HeadLength = 5;

            var signature = reader.ReadStringToNull(HeadLength);
            if (string.Compare(signature, "K9999") > 0 || reader.Length <= MinLength)
            {
                Logger.Verbose($"Signature version {signature} is higher than K9999 or stream length {reader.Length} is less than minimum length {MinLength}, aborting...");
                reader.Position = 0;
                return reader;
            }

            reader.Position = reader.Length + ~StartEnd;
            var keyLength = reader.ReadByte();
            reader.Position = reader.Length - StartEnd - 2;
            var enLength = reader.ReadByte();

            var enKeyPos = (byte)((keyLength % KeyLength) + KeyLength);
            var encryptedLength = (byte)((enLength % EnLength) + EnLength);

            reader.Position = reader.Length - StartEnd - enKeyPos;
            var encryptKey = reader.ReadBytes(KeyLength);

            var subByte = (byte)(reader.Length - StartEnd - KeyLength - (keyLength % KeyLength));
            for (var i = 0; i < KeyLength; i++)
            {
                if (encryptKey[i] == 0)
                {
                    encryptKey[i] = (byte)(subByte + i);
                }
            }

            var key = new byte[encryptKeyName.Length + KeyLength];
            encryptKeyName.CopyTo(key.AsMemory(0));
            encryptKey.CopyTo(key.AsMemory(encryptKeyName.Length));

            reader.Position = HeadLength;
            var data = reader.ReadBytes(encryptedLength);
            for (int i = 0; i < encryptedLength; i++)
            {
                data[i] ^= key[i % key.Length]; 
            }

            MemoryStream ms = new();
            ms.Write(Encoding.UTF8.GetBytes("Unity"));
            ms.Write(data);
            reader.BaseStream.CopyTo(ms);
            ms.Position = 0;

            Logger.Verbose("Decrypted Fantasy of Wind file successfully !!");
            return new FileReader(reader.FullPath, ms);
        }
        public static FileReader ParseHelixWaltz2(FileReader reader)
        {
            Logger.Verbose($"Attempting to decrypt file {reader.FileName} with Helix Waltz 2 encryption");

            var originalHeader = new byte[] { 0x55, 0x6E, 0x69, 0x74, 0x79, 0x46, 0x53, 0x00, 0x00, 0x00, 0x00, 0x07, 0x35, 0x2E, 0x78, 0x2E };

            var signature = reader.ReadStringToNull();
            reader.AlignStream();

            if (signature != "SzxFS")
            {
                Logger.Verbose($"Expected signature SzxFS, found {signature} instead, aborting...");
                reader.Position = 0;
                return reader;
            }

            var seed = reader.ReadInt32();
            reader.Position = 0x10;
            var data = reader.ReadBytes((int)reader.Remaining);

            var sbox = new byte[0x100];
            for (int i = 0; i < sbox.Length; i++)
            {
                sbox[i] = (byte)i;
            }

            var key = new byte[0x100];
            var random = new Random(seed);
            for (int i = 0; i < key.Length; i++)
            {
                var idx = random.Next(i, 0x100);
                var b = sbox[idx];
                sbox[idx] = sbox[i];
                sbox[i] = b;
                key[b] = (byte)i;
            }

            for (int i = 0; i < data.Length; i++)
            {
                var idx = data[i];
                data[i] = key[idx]; 
            }

            Logger.Verbose("Decrypted Helix Waltz 2 file successfully !!");
            MemoryStream ms = new();
            ms.Write(originalHeader);
            ms.Write(data);
            ms.Position = 0;

            return new FileReader(reader.FullPath, ms);
        }
        public static FileReader DecryptAnchorPanic(FileReader reader)
        {
            Logger.Verbose($"Attempting to decrypt file {reader.FileName} with Anchor Panic encryption");

            const int BlockSize = 0x800;

            var data = reader.ReadBytes(0x1000);
            reader.Position = 0;

            var idx = data.Search("UnityFS");
            if (idx != -1)
            {
                Logger.Verbose("Found UnityFS signature, file might not be encrypted");
                return ParseFakeHeader(reader);
            }

            var key = GetKey(Path.GetFileNameWithoutExtension(reader.FileName));
            Logger.Verbose($"Calculated key is {key}");

            var chunkIndex = 0;
            MemoryStream ms = new();
            while (reader.Remaining > 0)
            {
                var chunkSize = Math.Min((int)reader.Remaining, BlockSize);
                Logger.Verbose($"Chunk {chunkIndex} size is {chunkSize}");
                var chunk = reader.ReadBytes(chunkSize);
                if (IsEncrypt((int)reader.Length, chunkIndex++))
                {
                    Logger.Verbose($"Chunk {chunkIndex} is encrypted, decrypting...");
                    RC4(chunk, key);
                }

                ms.Write(chunk);
            }

            Logger.Verbose("Decrypted Anchor Panic file successfully !!");
            ms.Position = 0;
            return new FileReader(reader.FullPath, ms);

            bool IsEncrypt(int fileSize, int chunkIndex)
            {
                const int MaxEncryptChunkIndex = 4;

                if (chunkIndex == 0)
                    return true;

                if (fileSize / BlockSize == chunkIndex)
                    return true;

                if (MaxEncryptChunkIndex < chunkIndex)
                    return false;

                return fileSize % 2 == chunkIndex % 2;
            }
            
            byte[] GetKey(string fileName)
            {
                const string Key = "KxZKZolAT3QXvsUU";

                string keyHash = CalculateMD5(Key);
                string nameHash = CalculateMD5(fileName);
                var key = $"{keyHash[..5]}leiyan{nameHash[Math.Max(0, nameHash.Length - 5)..]}";
                return Encoding.UTF8.GetBytes(key);

                string CalculateMD5(string str)
                {
                    var bytes = Encoding.UTF8.GetBytes(str);
                    bytes = MD5.HashData(bytes);
                    return Convert.ToHexString(bytes).ToLowerInvariant();
                }
            }

            void RC4(Span<byte> data, byte[] key)
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

                    data[iteration] ^= Convert.ToByte(K);
                }
            }
        }

        public static FileReader DecryptDreamscapeAlbireo(FileReader reader)
        {
            Logger.Verbose($"Attempting to decrypt file {reader.FileName} with Dreamscape Albireo encryption");

            var key = new byte[] { 0x1E, 0x1E, 0x01, 0x01, 0xFC };

            var signature = reader.ReadStringToNull(4);
            if (signature != "MJJ")
            {
                Logger.Verbose($"Expected signature MJJ, found {signature} instead, aborting...");
                reader.Position = 0;
                return reader;
            }

            reader.Endian = EndianType.BigEndian;

            var u1 = reader.ReadUInt32();
            var u2 = reader.ReadUInt32();
            var u3 = reader.ReadUInt32();
            var u4 = reader.ReadUInt32();
            var u5 = reader.ReadUInt32();
            var u6 = reader.ReadUInt32();

            var flag = Scrample(u4) ^ 0x70020017;
            var compressedBlocksInfoSize = Scrample(u1) ^ u4;
            var uncompressedBlocksInfoSize = Scrample(u6) ^ u1;

            var sizeHigh = (u5 & 0xFFFFFF00 | u2 >> 24) ^ u4;
            var sizeLow = (u5 >> 24 | (u2 << 8)) ^ u1;
            var size = (long)(sizeHigh << 32 | sizeLow);

            Logger.Verbose($"Decrypted File info: Flag 0x{flag:X8} | Compressed blockInfo size 0x{compressedBlocksInfoSize:X8} | Decompressed blockInfo size 0x{uncompressedBlocksInfoSize:X8} | Bundle size 0x{size:X8}");

            var blocksInfo = reader.ReadBytes((int)compressedBlocksInfoSize);
            for(int i = 0; i < blocksInfo.Length; i++)
            {
                blocksInfo[i] ^= key[i % key.Length];
            }

            var data = reader.ReadBytes((int)reader.Remaining);

            var buffer = (stackalloc byte[8]);
            MemoryStream ms = new();
            ms.Write(Encoding.UTF8.GetBytes("UnityFS\x00"));
            BinaryPrimitives.WriteUInt32BigEndian(buffer, 6);
            ms.Write(buffer[..4]);
            ms.Write(Encoding.UTF8.GetBytes("5.x.x\x00"));
            ms.Write(Encoding.UTF8.GetBytes("2018.4.2f1\x00"));
            BinaryPrimitives.WriteInt64BigEndian(buffer, size);
            ms.Write(buffer);
            BinaryPrimitives.WriteUInt32BigEndian(buffer, compressedBlocksInfoSize);
            ms.Write(buffer[..4]);
            BinaryPrimitives.WriteUInt32BigEndian(buffer, uncompressedBlocksInfoSize);
            ms.Write(buffer[..4]);
            BinaryPrimitives.WriteUInt32BigEndian(buffer, flag);
            ms.Write(buffer[..4]);
            ms.Write(blocksInfo);
            ms.Write(data);
            reader.BaseStream.CopyTo(ms);
            ms.Position = 0;

            Logger.Verbose("Decrypted Dreamscape Albireo file successfully !!");
            return new FileReader(reader.FullPath, ms);

            static uint Scrample(uint value) => (value >> 5) & 0xFFE000 | (value >> 29) | (value << 14) & 0xFF000000 | (8 * value) & 0x1FF8;
        }

        public static FileReader DecryptImaginaryFest(FileReader reader)
        {
            Logger.Verbose($"Attempting to decrypt file {reader.FileName} with Imaginary Fest encryption");

            const string dataRoot = "data";
            var key = new byte[] { 0xBD, 0x65, 0xF2, 0x4F, 0xBE, 0xD1, 0x36, 0xD4, 0x95, 0xFE, 0x64, 0x94, 0xCB, 0xD3, 0x7E, 0x91, 0x57, 0xB7, 0x94, 0xB7, 0x9F, 0x70, 0xB2, 0xA9, 0xA0, 0xD5, 0x4E, 0x36, 0xC6, 0x40, 0xE0, 0x2F, 0x4E, 0x6E, 0x76, 0x6D, 0xCD, 0xAE, 0xEA, 0x05, 0x13, 0x6B, 0xA7, 0x84, 0xFF, 0xED, 0x90, 0x91, 0x15, 0x7E, 0xF1, 0xF8, 0xA5, 0x9C, 0xB6, 0xDE, 0xF9, 0x56, 0x57, 0x18, 0xBF, 0x94, 0x63, 0x6F, 0x1B, 0xE2, 0x92, 0xD2, 0x7E, 0x25, 0x95, 0x23, 0x24, 0xCB, 0x93, 0xD3, 0x36, 0xD9, 0x18, 0x11, 0xF5, 0x50, 0x18, 0xE4, 0x22, 0x28, 0xD8, 0xE2, 0x1A, 0x57, 0x1E, 0x04, 0x88, 0xA5, 0x84, 0xC0, 0x6C, 0x3B, 0x46, 0x62, 0xCE, 0x85, 0x10, 0x2E, 0xA0, 0xDC, 0xD3, 0x09, 0xB2, 0xB6, 0xA4, 0x8D, 0xAF, 0x74, 0x36, 0xF7, 0x9A, 0x3F, 0x98, 0xDA, 0x62, 0x57, 0x71, 0x75, 0x92, 0x05, 0xA3, 0xB2, 0x7C, 0xCA, 0xFB, 0x1E, 0xBE, 0xC9, 0x24, 0xC1, 0xD2, 0xB9, 0xDE, 0xE4, 0x7E, 0xF3, 0x0F, 0xB4, 0xFB, 0xA2, 0xC1, 0xC2, 0x14, 0x5C, 0x78, 0x13, 0x74, 0x41, 0x8D, 0x79, 0xF4, 0x3C, 0x49, 0x92, 0x98, 0xF2, 0xCD, 0x8C, 0x09, 0xA6, 0x40, 0x34, 0x51, 0x1C, 0x11, 0x2B, 0xE0, 0x6B, 0x42, 0x9C, 0x86, 0x41, 0x06, 0xF6, 0xD2, 0x87, 0xF1, 0x10, 0x26, 0x89, 0xC2, 0x7B, 0x2A, 0x5D, 0x1C, 0xDA, 0x92, 0xC8, 0x93, 0x59, 0xF9, 0x60, 0xD0, 0xB5, 0x1E, 0xD5, 0x75, 0x56, 0xA0, 0x05, 0x83, 0x90, 0xAC, 0x72, 0xC8, 0x10, 0x09, 0xED, 0x1A, 0x46, 0xD9, 0x39, 0x6B, 0x9E, 0x19, 0x5E, 0x51, 0x44, 0x09, 0x0D, 0x74, 0xAB, 0xA8, 0xF9, 0x32, 0x43, 0xBC, 0xD2, 0xED, 0x7B, 0x6C, 0x75, 0x32, 0x24, 0x14, 0x43, 0x5D, 0x98, 0xB2, 0xFC, 0xFB, 0xF5, 0x9A, 0x19, 0x03, 0xB0, 0xB7, 0xAC, 0xAE, 0x8B };

            var signatureBytes = reader.ReadBytes(8);
            var signature = Encoding.UTF8.GetString(signatureBytes[..7]);
            if (signature == "UnityFS")
            {
                Logger.Verbose("Found UnityFS signature, file might not be encrypted");
                reader.Position = 0;
                return reader;
            }

            if (signatureBytes[7] != 0)
            {
                Logger.Verbose($"File might be encrypted with a byte xorkey 0x{signatureBytes[7]:X8}, attemping to decrypting...");
                var xorKey = signatureBytes[7];
                for (int i = 0; i < signatureBytes.Length; i++)
                {
                    signatureBytes[i] ^= xorKey;
                }
                signature = Encoding.UTF8.GetString(signatureBytes[..7]);
                if (signature == "UnityFS")
                {
                    Logger.Verbose("Found UnityFS signature, key is valid, decrypting the rest of the stream");
                    var remaining = reader.ReadBytes((int)reader.Remaining);
                    for (int i = 0; i < remaining.Length; i++)
                    {
                        remaining[i] ^= xorKey;
                    }

                    Logger.Verbose("Decrypted Imaginary Fest file successfully !!");
                    var stream = new MemoryStream();
                    stream.Write(signatureBytes);
                    stream.Write(remaining);
                    stream.Position = 0;
                    return new FileReader(reader.FullPath, stream);
                }
            }

            reader.Position = 0;

            var paths = reader.FullPath.Split(Path.DirectorySeparatorChar);
            var startIdx = Array.FindIndex(paths, x => x == dataRoot);
            if (startIdx != -1 && startIdx != paths.Length - 1)
            {
                Logger.Verbose("File is in the data folder !!");
                var path = string.Join(Path.AltDirectorySeparatorChar, paths[(startIdx+1)..]);
                var offset = GetLoadAssetBundleOffset(path);
                if (offset > 0 && offset < reader.Length)
                {
                    Logger.Verbose($"Calculated offset is 0x{offset:X8}, attempting to read signature...");
                    reader.Position = offset;
                    signature = reader.ReadStringToNull(7);
                    if (signature == "UnityFS")
                    {
                        Logger.Verbose($"Found UnityFS signature, file starts at 0x{offset:X8} !!");
                        Logger.Verbose("Parsed Imaginary Fest file successfully !!");
                        reader.Position = offset;
                        return new FileReader(reader.FullPath, new MemoryStream(reader.ReadBytes((int)reader.Remaining)));
                    }
                }
                Logger.Verbose($"Invalid offset, attempting to generate key...");
                reader.Position = 0;
                var data = reader.ReadBytes((int)reader.Remaining);
                var key_value = GetHashCode(path);
                Logger.Verbose($"Generated key is 0x{key_value:X8}, decrypting...");
                Decrypt(data, key_value);
                Logger.Verbose("Decrypted Imaginary Fest file successfully !!");
                return new FileReader(reader.FullPath, new MemoryStream(data));
            }

            Logger.Verbose("File doesn't match any of the encryption types");
            reader.Position = 0;
            return reader;

            int GetLoadAssetBundleOffset(string str)
            {
                var hashCode = GetHashCode(str);
                var offset = 1;
                var index = -4;
                do
                {
                    var s = hashCode >> (index + 8);
                    index += 4;
                    offset += s % 0x80 | 0x80;
                }
                while (4 * (hashCode & 3) != index);
                return offset;
            }

            int GetHashCode(string str, int pattern = 0)
            {
                var table = new int[4];

                var len = str.Length - 1;
                for (int i = 0; i < table.Length; i++)
                {
                    var c = str[len & ~(len >> 0x1F)];
                    table[i] = GetJammingInt(pattern + c);
                    pattern += table.Length;
                    len--;
                }

                var shift = 0;
                for (int i = str.Length - 1; i >= 0; i--)
                {
                    var c = str[i];
                    shift = (shift + i) ^ c;
                    table[i % table.Length] += c << shift;
                }
                return table[0] ^ table[1] ^ table[2] ^ table[3];
            }

            int GetJammingInt(int top_index)
            {
                return BinaryPrimitives.TryReadInt32LittleEndian(key.AsSpan(top_index), out var value) ? value : -1;
            }

            void Decrypt(byte[] bytes, int key_value)
            {
                var step = (key_value >> 8) % 3 + 1;
                for (int i = 0; i < bytes.Length; i++)
                {
                    var index = (byte)key_value;
                    bytes[i] ^= key[index];
                    key_value += step;
                }
            }
        }
        public static FileReader DecryptAliceGearAegis(FileReader reader)
        {
            Logger.Verbose($"Attempting to decrypt file {reader.FileName} with Alice Gear Aegis encryption");

            var key = new byte[] { 0x1B, 0x59, 0x62, 0x33, 0x78, 0x76, 0x45, 0xB3, 0x5B, 0x48, 0x39, 0xD7, 0x9C, 0x21, 0x89, 0x94 };

            var header = new Header()
            {
                signature = reader.ReadStringToNull(),
                version = reader.ReadUInt32(),
                unityVersion = reader.ReadStringToNull(),
                unityRevision = reader.ReadStringToNull(),
                size = reader.ReadInt64()
            };
            if (header.signature == "UnityFS" && header.size == reader.Length)
            {
                reader.Position = 0;
                return reader;
            }

            reader.Position = 8;
            var seed = (reader.Length - reader.Position) % key.Length;

            var encryptedBlock = reader.ReadBytes(0x80);
            var data = reader.ReadBytes((int)reader.Remaining);
            for (int i = 0; i < encryptedBlock.Length; i++)
            {
                encryptedBlock[i] ^= key[seed++ % key.Length];
            }

            Logger.Verbose("Decrypted Alice Gear Aegis file successfully !!");
            MemoryStream ms = new();
            ms.Write(Encoding.UTF8.GetBytes("UnityFS\x00"));
            ms.Write(encryptedBlock);
            ms.Write(data);
            ms.Position = 0;

            return new FileReader(reader.FullPath, ms);
        }
        
        public static FileReader DecryptProjectSekai(FileReader reader)
        {
            Logger.Verbose($"Attempting to decrypt file {reader.FileName} with Project Sekai encryption");

            var key = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00 };

            reader.Endian = EndianType.LittleEndian;
            var version = reader.ReadUInt32();

            if (version != 0x10 && version != 0x20)
            {
                reader.Endian = EndianType.BigEndian;
                reader.Position = 0;
                return reader;
            }

            MemoryStream ms = new();
            if (version == 0x10)
            {
                var buffer = (stackalloc byte[8]);
                for (int i = 0; i < 0x10; i++)
                {
                    var read = reader.Read(buffer);
                    for (int j = 0; j < key.Length; j++)
                    {
                        buffer[j] ^= key[j];
                    }
                    ms.Write(buffer[..read]);
                }
            }

            ms.Write(reader.ReadBytes((int)reader.Remaining));

            Logger.Verbose("Decrypted Project Sekai file successfully !!");
            ms.Position = 0;
            return new FileReader(reader.FullPath, ms);
        }
        
        public static FileReader DecryptCodenameJump(FileReader reader)
        {
            Logger.Verbose($"Attempting to decrypt file {reader.FileName} with Codename Jump encryption");

            var key = new byte[] { 0x6B, 0xC9, 0xAC, 0x0E, 0xE7, 0xD2, 0xB1, 0x99, 0x39, 0x59, 0x26, 0x56, 0x1B, 0x6C, 0xBB, 0xA4, 0x83, 0xC8, 0x79, 0x2E, 0x4B, 0xB2, 0x9D, 0x69, 0x35, 0xB8, 0x9A, 0xD6, 0xD5, 0x63, 0x95, 0x20, 0x14, 0x82, 0x1C, 0x7C, 0xD4, 0xA9, 0x15, 0x56, 0xC3, 0xC5, 0xD7, 0x21, 0x03, 0x4E, 0x4A, 0x34, 0x6B, 0x05, 0x2D, 0x0B, 0xE2, 0x7D, 0x7D, 0xD7, 0xB2, 0xAE, 0x9E, 0x56, 0x91, 0xBA, 0x81, 0x81, 0x0E, 0x08, 0x4D, 0xA0, 0x09, 0xB5, 0x60, 0x74, 0x58, 0x36, 0x89, 0x09, 0x19, 0x2C, 0x10, 0xB1, 0xD0, 0xA3, 0x4C, 0x36, 0xAA, 0x95, 0xBC, 0x10, 0x39, 0x30, 0x93, 0xE8, 0xAD, 0x38, 0x51, 0xAA, 0xCA, 0x08, 0x67, 0x03, 0x08, 0xD1, 0x20, 0x05, 0x27, 0x0B, 0x9D, 0xB1, 0x4B, 0x42, 0x98, 0x03, 0x5A, 0x49, 0x97, 0xB0, 0x2A, 0xB6, 0x3A, 0x2C, 0x33, 0xA3, 0x65, 0xC7, 0x7D, 0xB9, 0x41, 0xAD, 0xE7, 0x70, 0x59, 0x61, 0x82, 0x59, 0xC9, 0x5A, 0x0B, 0x13, 0x6D, 0x95, 0x31, 0x31, 0x23, 0x22, 0xD0, 0x51, 0x45, 0x59, 0x09, 0x57, 0xA2, 0x60, 0x3B, 0xCE, 0x9B, 0x6E, 0x22, 0x9E, 0x87, 0xBD, 0x83, 0x88, 0x73, 0xD0, 0x79, 0xD0, 0xAC, 0xDC, 0xE1, 0x6C, 0xB3, 0xA4, 0xCC, 0x98, 0x04, 0xE8, 0xB6, 0xBB, 0xAC, 0x21, 0xB9, 0x2A, 0x6E, 0x78, 0x01, 0xED, 0xC1, 0xA6, 0x79, 0xE0, 0x9B, 0x68, 0x7B, 0x8A, 0x25, 0xE4, 0x47, 0xBB, 0x5D, 0x2A, 0xC0, 0x5A, 0xDE, 0x31, 0xEC, 0x5C, 0xCE, 0x6D, 0xBE, 0x68, 0x1E, 0x93, 0x44, 0x89, 0x56, 0x68, 0x4C, 0x6E, 0xD0, 0x46, 0xB0, 0x97, 0xE4, 0x72, 0x23, 0xB5, 0x87, 0x18, 0xD5, 0x2D, 0xA9, 0x0E, 0x63, 0xAE, 0xCE, 0x4A, 0x69, 0xD0, 0xD1, 0x6B, 0xB0, 0x0C, 0x1A, 0xBD, 0xE3, 0x01, 0x45, 0x8B, 0x93, 0xD5, 0x83, 0x9C, 0xB7, 0x12, 0x6C, 0xD5 };

            var signatureBytes = reader.ReadBytes(8);
            reader.Position = 0;

            for (int i = 0; i < signatureBytes.Length; i++)
            {
                signatureBytes[i] ^= key[i % key.Length];
            }
            var signature = Encoding.UTF8.GetString(signatureBytes[..7]);
            if (signature != "UnityFS")
            {
                Logger.Verbose($"Unknown signature, exepcted UnityFS but got {signature} instead !!");
                return reader;
            }

            var data = reader.ReadBytes((int)reader.Remaining);
            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= key[i % key.Length];
            }

            Logger.Verbose("Decrypted Codename Jump file successfully !!");
            MemoryStream ms = new();
            ms.Write(data);
            ms.Position = 0;
            return new FileReader(reader.FullPath, ms);
        }
        
        public static FileReader DecryptGirlsFrontline(FileReader reader)
        {
            Logger.Verbose($"Attempting to decrypt file {reader.FileName} with Girls Frontline encryption");

            var originalHeader = new byte[] { 0x55, 0x6E, 0x69, 0x74, 0x79, 0x46, 0x53, 0x00, 0x00, 0x00, 0x00, 0x07, 0x35, 0x2E, 0x78, 0x2E };

            var key = reader.ReadBytes(0x10);
            for (int i = 0; i < key.Length; i++)
            {
                var b = (byte)(key[i] ^ originalHeader[i]);
                key[i] = b != originalHeader[i] ? b : originalHeader[i];
            }

            reader.Position = 0;
            var data = reader.ReadBytes((int)reader.Remaining);
            var size = Math.Min(data.Length, 0x8000);
            for (int i = 0; i < size; i++)
            {
                data[i] ^= key[i % key.Length];
            }

            Logger.Verbose("Decrypted Girls Frontline file successfully !!");

            MemoryStream ms = new();
            ms.Write(data);
            ms.Position = 0;
            return new FileReader(reader.FullPath, ms);
        }
        
        public static FileReader DecryptReverse1999(FileReader reader)
        {
            Logger.Verbose($"Attempting to decrypt file {reader.FileName} with Reverse: 1999 encryption");

            var signatureBytes = reader.ReadBytes(8);
            var signature = Encoding.UTF8.GetString(signatureBytes[..7]);
            if (signature == "UnityFS")
            {
                Logger.Verbose("Found UnityFS signature, file might not be encrypted");
                reader.Position = 0;
                return reader;
            }

            var key = GetAbEncryptKey(Path.GetFileNameWithoutExtension(reader.FileName));
            for (int i = 0; i < signatureBytes.Length; i++)
            {
                signatureBytes[i] ^= key;
            }

            signature = Encoding.UTF8.GetString(signatureBytes[..7]);
            if (signature == "UnityFS")
            {
                Logger.Verbose($"Found UnityFS signature, key 0x{key:X2} is valid, decrypting the rest of the stream");
                var remaining = reader.ReadBytes((int)reader.Remaining);
                for (int i = 0; i < remaining.Length; i++)
                {
                    remaining[i] ^= key;
                }

                Logger.Verbose("Decrypted Reverse: 1999 file successfully !!");
                MemoryStream stream = new();
                stream.Write(signatureBytes);
                stream.Write(remaining);
                stream.Position = 0;
                return new FileReader(reader.FullPath, stream);
            }

            Logger.Verbose("File doesn't match any of the encryption types");
            reader.Position = 0;
            return reader;

            static byte GetAbEncryptKey(string md5Name)
            {
                byte key = 0;
                foreach (var c in md5Name)
                {
                    key += (byte)c;
                }
                return (byte)(key + (byte)(2 * ((key & 1) + 1)));
            }
        }

        public static FileReader DecryptJJKPhantomParade(FileReader reader)
        {
            Logger.Verbose($"Attempting to decrypt file {reader.FileName} with Jujutsu Kaisen: Phantom Parade encryption");

            var key = reader.ReadBytes(2);
            var signatureBytes = reader.ReadBytes(13);
            var generation = reader.ReadByte();

            for (int i = 0; i < 13; i++)
            {
                signatureBytes[i] ^= key[i % key.Length];
            }

            var signature = Encoding.UTF8.GetString(signatureBytes);
            if (signature != "_GhostAssets_")
            {
                throw new Exception("Invalid signature");
            }

            generation ^= (byte)(key[0] ^ key[1]);

            if (generation != 1)
            {
                throw new Exception("Invalid generation");
            }

            long value = 0;
            var data = reader.ReadBytes((int)reader.Remaining);
            var blockCount = data.Length / 0x10;

            using var writerMS = new MemoryStream();
            using var writer = new BinaryWriter(writerMS);
            for (int i = 0; i <= blockCount; i++)
            {
                if (i % 0x40 == 0)
                {
                    value = 0x64 * ((i / 0x40) + 1);
                }
                writer.Write(value);
                writer.Write((long)0);
                value += 1;
            }

            using var aes = Aes.Create();
            aes.Key = new byte[] { 0x36, 0x31, 0x35, 0x34, 0x65, 0x30, 0x30, 0x66, 0x39, 0x45, 0x39, 0x63, 0x65, 0x34, 0x36, 0x64, 0x63, 0x39, 0x30, 0x35, 0x34, 0x45, 0x30, 0x37, 0x31, 0x37, 0x33, 0x41, 0x61, 0x35, 0x34, 0x36 };
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            var encryptor = aes.CreateEncryptor();

            var keyBytes = writerMS.ToArray();
            keyBytes = encryptor.TransformFinalBlock(keyBytes, 0, keyBytes.Length);

            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= keyBytes[i];
            }

            Logger.Verbose("Decrypted Jujutsu Kaisen: Phantom Parade file successfully !!");

            MemoryStream ms = new();
            ms.Write(data);
            ms.Position = 0;
            return new FileReader(reader.FullPath, ms);
        }

        public static FileReader DecryptMuvLuvDimensions(FileReader reader)
        {
            Logger.Verbose($"Attempting to decrypt file {reader.FileName} with Muv Luv Dimensions encryption");

            var key = new byte[] { 0xFD, 0x13, 0x7B, 0xEE, 0xC5, 0xFE, 0x50, 0x12, 0x4D, 0x38 };

            var data = reader.ReadBytes((int)reader.Remaining);
            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= key[i % key.Length];
            }

            Logger.Verbose("Decrypted Muv Luv Dimensions file successfully !!");

            MemoryStream ms = new();
            ms.Write(data);
            ms.Position = 0;
            return new FileReader(reader.FullPath, ms);
        }

        public static FileReader DecryptPartyAnimals(FileReader reader)
        {
            Logger.Verbose($"Attempting to decrypt file {reader.FileName} with Party Animals encryption");

            var table = new int[] { 0x8C, 0xE8, 0x93, 0xEB, 0xD1, 0xF0, 0x82, 0xCF, 0x9A, 0xBB, 0xEF, 0xB8, 0xC7, 0xA8, 0x8E, 0xDB, 0x96, 0x80, 0xAD, 0xC2, 0x86, 0xD8, 0x81, 0xFA, 0xE6, 0xAF, 0xD0, 0x9E, 0x95, 0xFE, 0xF6, 0x88, 0xF8, 0x85, 0xE4, 0xBC, 0xB6, 0xA4, 0xCB, 0xE3, 0xE0, 0x9F, 0xD3, 0xA7, 0xA3, 0xFF, 0x9C, 0x9D, 0xEE, 0xDE, 0xC9, 0xB0, 0xD5, 0xBE, 0x89, 0xF4, 0xBF, 0xED, 0xD9, 0xBA, 0xA5, 0xCE, 0x94, 0xC5, 0xCC, 0x90, 0xC8, 0xBD, 0x92, 0xB7, 0xF7, 0x97, 0x9B, 0xAB, 0xB4, 0xE9, 0xA6, 0xAC, 0xA9, 0xB2, 0xC1, 0xE5, 0xA1, 0xA0, 0xC4, 0xDC, 0xEC, 0xFD, 0xC0, 0xF3, 0xD2, 0xB3, 0x98, 0x8B, 0xD6, 0xB5, 0xE7, 0xAE, 0xC3, 0xE1, 0xB1, 0xF5, 0xA2, 0xE2, 0xF2, 0xAA, 0xF9, 0x99, 0xD4, 0x84, 0xFC, 0x8D, 0xF1, 0xDF, 0xB9, 0xD7, 0xDA, 0x91, 0xCA, 0x83, 0xEA, 0x8F, 0xCD, 0xDD, 0xC6, 0x87, 0xFB, 0x8A };

            var name = Path.GetFileNameWithoutExtension(reader.FileName);
            var nameBytes = Encoding.UTF8.GetBytes(name);

            var key = (byte)(0x7C ^ nameBytes.Aggregate((a, b) => (byte)(a ^ b)));
            var pos = table[nameBytes.Aggregate((a, b) => (byte)(a + b)) % table.Length];

            var data = reader.ReadBytes((int)reader.Remaining);

            for (int i = pos; i < data.Length; i++)
            {
                data[i] ^= (byte)(key ^ (i / 8) + 1);
            }

            Logger.Verbose("Decrypted Party Animals file successfully !!");

            MemoryStream ms = new();
            ms.Write(data);
            ms.Position = 0;
            return new FileReader(reader.FullPath, ms);
        }

        public static FileReader DecryptLoveAndDeepspace(FileReader reader)
        {
            Logger.Verbose($"Attempting to decrypt file {reader.FileName} with Love And Deepspace encryption");

            var vector = new byte[] { 0x35, 0x6B, 0x05, 0x00 };
            var originalHeader = new byte[] { 0x55, 0x6E, 0x69, 0x74, 0x79, 0x46, 0x53, 0x00, 0x00, 0x00, 0x00, 0x07, 0x35, 0x2E, 0x78, 0x2E };

            var seed = reader.ReadBytes(0x10);
            for (int i = 0; i < seed.Length; i++)
            {
                var b = (byte)(seed[i] ^ originalHeader[i] ^ vector[0]);
                seed[i] = b != originalHeader[i] ? b : originalHeader[i];
            }

            var key = new byte[0x40];
            for (int i = 0; i < vector.Length; i++)
            {
                for (int j = 0; j < seed.Length; j++)
                {
                    var offset = i * 0x10;
                    key[offset + j] = (byte)(seed[j] ^ vector[i]);
                }
            }

            reader.Position = 0;
            var data = reader.ReadBytes((int)reader.Remaining);
            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= key[i % key.Length];
            }

            Logger.Verbose("Decrypted Love And Deepspace file successfully !!");

            MemoryStream ms = new();
            ms.Write(data);
            ms.Position = 0;
            return new FileReader(reader.FullPath, ms);
        }

        public static FileReader DecryptSchoolGirlStrikers(FileReader reader)
        {
            Logger.Verbose($"Attempting to decrypt file {reader.FileName} with School Girl Strikers encryption");

            var data = reader.ReadBytes((int)reader.Remaining);

            byte key = 0xFF;
            var stride = data.Length % 7 + 3;
            for (int i = 1; i < data.Length; i++)
            {
                if (i % stride != 0)
                {
                    data[i] ^= key;
                }
                else
                {
                    key = (byte)~key;
                }
            }

            Logger.Verbose("Decrypted School Girl Strikers file successfully !!");

            MemoryStream ms = new();
            ms.Write(data);
            ms.Position = 0;
            return new FileReader(reader.FullPath, ms);
        }
    }
}
