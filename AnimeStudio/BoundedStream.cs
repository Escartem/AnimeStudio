using System;
using System.IO;

namespace AnimeStudio
{
    public sealed class BoundedStream : Stream
    {
        private readonly Stream baseStream;
        private readonly long start;
        private readonly long length;
        private readonly bool leaveOpen;
        private long position;

        public BoundedStream(Stream baseStream, long start, long length, bool leaveOpen = false)
        {
            if (baseStream == null)
            {
                throw new ArgumentNullException(nameof(baseStream));
            }
            if (!baseStream.CanRead || !baseStream.CanSeek)
            {
                throw new ArgumentException("Base stream must be readable and seekable.", nameof(baseStream));
            }
            if (start < 0 || length < 0 || start > baseStream.Length || length > baseStream.Length - start)
            {
                throw new ArgumentOutOfRangeException(nameof(start), "Bounded stream range is outside the base stream.");
            }

            this.baseStream = baseStream;
            this.start = start;
            this.length = length;
            this.leaveOpen = leaveOpen;
        }

        public override bool CanRead => baseStream.CanRead;
        public override bool CanSeek => baseStream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => length;

        public override long Position
        {
            get => position;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            if (offset > buffer.Length || count > buffer.Length - offset)
            {
                throw new ArgumentException("Offset and count exceed buffer length.");
            }

            if (position >= length)
            {
                return 0;
            }

            count = (int)Math.Min(count, length - position);
            baseStream.Position = start + position;
            var read = baseStream.Read(buffer, offset, count);
            position += read;
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            if (position >= length)
            {
                return 0;
            }

            var count = (int)Math.Min(buffer.Length, length - position);
            baseStream.Position = start + position;
            var read = baseStream.Read(buffer[..count]);
            position += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var target = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => position + offset,
                SeekOrigin.End => length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };

            if (target < 0 || target > length)
            {
                throw new IOException("Unable to seek outside the bounded stream range.");
            }

            position = target;
            return position;
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !leaveOpen)
            {
                baseStream.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
