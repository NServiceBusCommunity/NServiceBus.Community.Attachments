/// <summary>
/// Wraps a write-only stream to track Position by counting bytes written.
/// PipeWriterStream throws NotSupportedException for Position,
/// but some libraries (e.g. Aspose.Words) read stream.Position.
/// </summary>
class PositionTrackingStream(Stream inner) :
    Stream
{
    long position;

    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => inner.Length;

    public override long Position
    {
        get => position;
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        inner.Write(buffer, offset, count);
        position += count;
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancel)
    {
        await inner.WriteAsync(buffer, offset, count, cancel);
        position += count;
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancel = default)
    {
        await inner.WriteAsync(buffer, cancel);
        position += buffer.Length;
    }

    public override void WriteByte(byte value)
    {
        inner.WriteByte(value);
        position++;
    }

    public override void Flush() => inner.Flush();

    public override Task FlushAsync(CancellationToken cancel) => inner.FlushAsync(cancel);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}
