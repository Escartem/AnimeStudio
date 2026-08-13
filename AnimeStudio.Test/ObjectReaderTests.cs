using System;
using System.IO;
using System.Runtime.CompilerServices;
using AnimeStudio;

namespace AnimeStudio.Test;

public class ObjectReaderTests
{
    [Fact]
    public void Read_is_limited_to_the_declared_object_window()
    {
        var reader = CreateReader(new byte[] { 0, 0, 0, 0, 10, 20, 30, 40 }, byteStart: 4, byteSize: 4);
        reader.Reset();

        var buffer = new byte[4];
        var read = reader.Read(buffer, 0, buffer.Length);

        Assert.Equal(4, read);
        Assert.Equal(new byte[] { 10, 20, 30, 40 }, buffer);
        Assert.Equal(0, reader.BytesLeft());
        Assert.Throws<EndOfStreamException>(() => reader.Read(new byte[1], 0, 1));
    }

    [Fact]
    public void Reset_restores_the_object_start_and_remaining_byte_count()
    {
        var reader = CreateReader(new byte[] { 0, 0, 0, 0, 10, 20, 30, 40 }, byteStart: 4, byteSize: 4);
        reader.Reset();
        reader.ReadByte();

        reader.Reset();

        Assert.Equal(4, reader.Position);
        Assert.Equal(4, reader.BytesLeft());
    }

    [Fact]
    public void ReadVector3_uses_three_components_for_unity_5_4_and_newer()
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(1.5f).CopyTo(bytes, 4);
        BitConverter.GetBytes(2.5f).CopyTo(bytes, 8);
        BitConverter.GetBytes(3.5f).CopyTo(bytes, 12);
        var reader = CreateReader(bytes, byteStart: 4, byteSize: 12, version: new[] { 5, 4, 0, 0 });
        reader.Reset();

        var value = reader.ReadVector3();

        Assert.Equal(1.5f, value.X);
        Assert.Equal(2.5f, value.Y);
        Assert.Equal(3.5f, value.Z);
    }

    [Fact]
    public void ReadXForm_reads_translation_rotation_and_scale_from_the_object_window()
    {
        var bytes = new byte[44];
        for (var i = 0; i < 10; i++)
        {
            BitConverter.GetBytes(i + 1f).CopyTo(bytes, 4 + i * sizeof(float));
        }
        var reader = CreateReader(bytes, byteStart: 4, byteSize: 40);
        reader.Reset();

        var value = reader.ReadXForm();

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(i + 1f, value[i]);
        }
        Assert.Equal(0, reader.BytesLeft());
    }

    private static ObjectReader CreateReader(byte[] bytes, long byteStart, uint byteSize, int[]? version = null)
    {
        var serializedFile = (SerializedFile)RuntimeHelpers.GetUninitializedObject(typeof(SerializedFile));
        serializedFile.fileName = "memory.assets";
        serializedFile.version = version ?? new[] { 5, 4, 0, 0 };
        serializedFile.header = new SerializedFileHeader { m_Version = SerializedFileFormatVersion.LargeFilesSupport };

        var objectInfo = new ObjectInfo
        {
            m_PathID = 1,
            byteStart = byteStart,
            byteSize = byteSize,
            classID = (int)ClassIDType.Texture2D,
        };

        return new ObjectReader(
            new EndianBinaryReader(new MemoryStream(bytes), EndianType.LittleEndian),
            serializedFile,
            objectInfo,
            game: null);
    }
}
