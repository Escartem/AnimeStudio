using Org.BouncyCastle.Crypto.Engines;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Text;
using Texture2DDecoder;
using ZstdSharp;

namespace AnimeStudio
{
    public readonly record struct AFKJourneyPkgEntry(
        long Offset,
        string Filename,
        int CompressedSize,
        int UncompressedSize,
        uint Crc32,
        ushort Method,
        long DataOffset);

    public static class AFKJourneyUtils
    {
        private static readonly byte[] JsoneMagic = { 0xFF, 0xFF, 0xFF, 0xFF };
        private static readonly byte[] ZipLocalHeader = { 0x50, 0x4B, 0x03, 0x04 };
        private static readonly byte[] BlowfishKey = Encoding.ASCII.GetBytes("b8c0aeff2944d1614d6bc63dc6b0a537");
        private const int ScanChunkSize = 64 * 1024 * 1024;
        private const int ScanOverlap = 256;
        private const byte FmtEtc1Rgb = 0x0A;
        private const byte FmtEtc1Rgba = 0x0B;
        private const byte FmtBc7 = 0x10;
        private const ushort PkgMethodStored = 0;
        private const ushort PkgMethodZstd = 20;

        public static bool HasDxtHeader(ReadOnlySpan<byte> data) => data.Length >= 3 && data[0] == (byte)'D' && data[1] == (byte)'X' && data[2] == (byte)'T';
        public static bool HasJsoneHeader(ReadOnlySpan<byte> data) => data.Length >= 4 && data[..4].SequenceEqual(JsoneMagic);

        public static bool IsSupportedSpecialFile(string path)
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[12];
            var read = stream.Read(header);
            var data = header[..read];
            return HasDxtHeader(data) || HasJsoneHeader(data) || IsPkgPath(path);
        }

        public static int TryExtractSpecialFile(string path, string outputDirectory)
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[12];
            var read = stream.Read(header);
            var data = header[..read];

            if (HasDxtHeader(data))
            {
                DecodeDxtFile(path, Path.Combine(outputDirectory, Path.GetFileName(path)));
                return 1;
            }

            if (HasJsoneHeader(data) && Path.GetExtension(path).Equals(".jsone", StringComparison.OrdinalIgnoreCase))
            {
                DecryptJsoneFile(path, outputDirectory);
                return 1;
            }

            if (IsPkgPath(path))
            {
                return ExtractPkgEntries(path, Path.Combine(outputDirectory, Path.GetFileName(path) + "_unpacked"));
            }

            return 0;
        }

        public static string DecryptJsoneToText(byte[] data)
        {
            if (!HasJsoneHeader(data))
            {
                throw new InvalidDataException("JSOne file does not start with the expected magic header.");
            }

            if (data.Length < 8)
            {
                throw new InvalidDataException("JSOne file is truncated.");
            }

            var totalSize = data.Length;
            var declaredSize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4, 4));
            var payload = data.AsSpan(8).ToArray();
            var engine = new BlowfishEngine();
            engine.Init(false, new Org.BouncyCastle.Crypto.Parameters.KeyParameter(BlowfishKey));

            var blockCount = ((totalSize - 4 - 5) >> 3) + 1;
            var tempIn = new byte[8];
            var tempOut = new byte[8];
            for (var i = 0; i < blockCount; i++)
            {
                var offset = i * 8;
                if (offset + 8 > payload.Length)
                {
                    break;
                }

                var block = payload.AsSpan(offset, 8);
                SwapHalves(block, tempIn);
                engine.ProcessBlock(tempIn, 0, tempOut, 0);
                SwapHalves(tempOut, block);
            }

            if (payload.Length < 4)
            {
                throw new InvalidDataException("JSOne decrypted payload is too small.");
            }

            var prefixLength = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, 4));
            var remainingSize = checked((int)declaredSize - (int)prefixLength - 4);
            var compressedOffset = checked((int)prefixLength + 4);
            if (remainingSize <= 4 || compressedOffset < 0 || compressedOffset + remainingSize > payload.Length)
            {
                throw new InvalidDataException("JSOne decrypted payload has invalid compressed data bounds.");
            }

            var compressed = payload.AsSpan(compressedOffset, remainingSize);
            var decompressedSize = BinaryPrimitives.ReadInt32LittleEndian(compressed[..4]);
            if (decompressedSize <= 0)
            {
                throw new InvalidDataException("JSOne LZ4 payload has invalid decompressed size.");
            }

            var decompressed = new byte[decompressedSize];
            var written = LZ4.Instance.Decompress(compressed[4..], decompressed);
            if (written != decompressedSize)
            {
                throw new InvalidDataException($"JSOne LZ4 decompression wrote {written} bytes, expected {decompressedSize}.");
            }

            return Encoding.UTF8.GetString(decompressed);
        }

        public static string DecryptJsoneFile(string path, string outputDirectory)
        {
            var text = DecryptJsoneToText(File.ReadAllBytes(path));
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(path) + ".json");
            File.WriteAllText(outputPath, text, Encoding.UTF8);
            return outputPath;
        }

        public static string DecodeDxtFile(string path, string outputPath)
        {
            var decoded = DecodeDxtToBitmapData(path);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            var finalOutputPath = Path.ChangeExtension(outputPath, ".png");
            using var image = Image.LoadPixelData<Bgra32>(decoded.Pixels, decoded.Width, decoded.Height);
            image.Mutate(static ctx => ctx.Flip(FlipMode.Vertical));
            image.SaveAsPng(finalOutputPath, new PngEncoder());
            return finalOutputPath;
        }

        public static (byte[] Pixels, int Width, int Height, string Format) DecodeDxtToBitmapData(string path)
        {
            var data = File.ReadAllBytes(path);
            if (!HasDxtHeader(data))
            {
                throw new InvalidDataException("DXT file does not start with the expected magic header.");
            }

            if (data.Length < 12)
            {
                throw new InvalidDataException("DXT file is truncated.");
            }

            var width = data[3] | (data[4] << 8);
            var height = data[5] | (data[6] << 8);
            var format = data[7];
            var decompressedSize = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(8, 4));
            if (width <= 0 || height <= 0 || decompressedSize <= 0)
            {
                throw new InvalidDataException("DXT file has invalid dimensions or payload size.");
            }

            var raw = new byte[decompressedSize];
            var written = LZ4.Instance.Decompress(data.AsSpan(12), raw);
            if (written != decompressedSize)
            {
                throw new InvalidDataException($"DXT LZ4 decompression wrote {written} bytes, expected {decompressedSize}.");
            }

            var pixelData = new byte[width * height * 4];
            var formatName = format switch
            {
                FmtBc7 => "BC7",
                FmtEtc1Rgb => "ETC1_RGB",
                FmtEtc1Rgba => "ETC1_RGBA",
                _ => $"0x{format:X2}",
            };
            bool success = format switch
            {
                FmtBc7 => TextureDecoder.DecodeBC7(raw, width, height, pixelData),
                FmtEtc1Rgb => TextureDecoder.DecodeETC1(raw, width, height, pixelData),
                FmtEtc1Rgba => TextureDecoder.DecodeETC1(raw, width, height, pixelData),
                _ => throw new InvalidDataException($"Unsupported AFK Journey DXT texture format 0x{format:X2}.")
            };

            if (!success)
            {
                throw new InvalidDataException("DXT texture decoder failed.");
            }

            return (pixelData, width, height, formatName);
        }

        public static List<AFKJourneyPkgEntry> ScanPkgEntries(string path, bool progress = true)
        {
            var entries = new List<AFKJourneyPkgEntry>();
            var seenOffsets = new HashSet<long>();
            var fileInfo = new FileInfo(path);
            long offset = 0;

            using var stream = File.OpenRead(path);
            while (offset < fileInfo.Length)
            {
                stream.Position = offset;
                var chunkSize = (int)Math.Min(ScanChunkSize, fileInfo.Length - offset);
                if (chunkSize <= 0)
                {
                    break;
                }

                var chunk = new byte[chunkSize];
                var read = stream.Read(chunk, 0, chunkSize);
                if (read <= 0)
                {
                    break;
                }

                var span = chunk.AsSpan(0, read);
                var searchOffset = 0;
                while (searchOffset < span.Length)
                {
                    var index = span[searchOffset..].IndexOf(ZipLocalHeader);
                    if (index < 0)
                    {
                        break;
                    }

                    index += searchOffset;
                    var absoluteOffset = offset + index;
                    searchOffset = index + 4;
                    if (seenOffsets.Contains(absoluteOffset) || index + 30 > span.Length)
                    {
                        continue;
                    }

                    var version = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(index + 4, 2));
                    var method = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(index + 8, 2));
                    var crc32 = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(index + 14, 4));
                    var compressedSize = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(index + 18, 4));
                    var uncompressedSize = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(index + 22, 4));
                    var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(index + 26, 2));
                    var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(index + 28, 2));

                    var valid =
                        version >= 10 && version <= 63 &&
                        nameLength > 0 && nameLength < 1024 &&
                        extraLength < 4096 &&
                        compressedSize >= 0 &&
                        index + 30 + nameLength <= span.Length;
                    if (!valid)
                    {
                        continue;
                    }

                    var nameBytes = span.Slice(index + 30, nameLength).ToArray();
                    var filename = Encoding.UTF8.GetString(nameBytes);
                    if (filename.EndsWith('/'))
                    {
                        continue;
                    }

                    var dataOffset = absoluteOffset + 30 + nameLength + extraLength;
                    entries.Add(new AFKJourneyPkgEntry(absoluteOffset, filename, compressedSize, uncompressedSize, crc32, method, dataOffset));
                    seenOffsets.Add(absoluteOffset);
                }

                offset += Math.Max(1, ScanChunkSize - ScanOverlap);
                if (progress && offset < fileInfo.Length)
                {
                    Progress.Report((int)Math.Min(offset, fileInfo.Length), (int)Math.Min(fileInfo.Length, int.MaxValue));
                }
            }

            return entries;
        }

        public static int ExtractPkgEntries(string path, string outputDirectory, string filterPattern = null)
        {
            var entries = ScanPkgEntries(path, progress: false);
            if (!string.IsNullOrEmpty(filterPattern))
            {
                entries = entries.Where(entry => FileSystemName.MatchesSimpleExpression(filterPattern, entry.Filename)).ToList();
            }

            Directory.CreateDirectory(outputDirectory);
            var extracted = 0;
            using var stream = File.OpenRead(path);
            using var decompressor = new Decompressor();
            foreach (var entry in entries)
            {
                stream.Position = entry.DataOffset;
                var outputPath = Path.Combine(outputDirectory, entry.Filename.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                switch (entry.Method)
                {
                    case PkgMethodStored:
                    {
                        using var output = File.Create(outputPath);
                        CopyBytes(stream, output, entry.CompressedSize);
                        extracted++;
                        break;
                    }
                    case PkgMethodZstd:
                    {
                        var compressed = new byte[entry.CompressedSize];
                        FillBuffer(stream, compressed);
                        var uncompressed = new byte[entry.UncompressedSize];
                        var written = decompressor.Unwrap(compressed, 0, compressed.Length, uncompressed, 0, uncompressed.Length);
                        if (written != entry.UncompressedSize)
                        {
                            throw new InvalidDataException($"Zstd decompression wrote {written} bytes, expected {entry.UncompressedSize}.");
                        }
                        File.WriteAllBytes(outputPath, uncompressed);
                        extracted++;
                        break;
                    }
                    default:
                        throw new InvalidDataException($"Unsupported AFK Journey pkg compression method {entry.Method} for {entry.Filename}.");
                }
            }

            return extracted;
        }

        private static bool IsPkgPath(string path) => Path.GetExtension(path).Equals(".pkg", StringComparison.OrdinalIgnoreCase);

        private static void SwapHalves(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            source[..4].CopyTo(destination[..4]);
            source[4..8].CopyTo(destination[4..8]);
            destination[..4].Reverse();
            destination[4..8].Reverse();
        }

        private static void CopyBytes(Stream input, Stream output, int count)
        {
            var buffer = new byte[Math.Min(count, 4 * 1024 * 1024)];
            var remaining = count;
            while (remaining > 0)
            {
                var read = input.Read(buffer, 0, Math.Min(buffer.Length, remaining));
                if (read <= 0)
                {
                    throw new EndOfStreamException("Unexpected end of stream while copying pkg entry data.");
                }
                output.Write(buffer, 0, read);
                remaining -= read;
            }
        }

        private static void FillBuffer(Stream input, byte[] buffer)
        {
            var totalRead = 0;
            while (totalRead < buffer.Length)
            {
                var read = input.Read(buffer, totalRead, buffer.Length - totalRead);
                if (read <= 0)
                {
                    throw new EndOfStreamException("Unexpected end of stream while reading pkg entry data.");
                }
                totalRead += read;
            }
        }
    }
}
