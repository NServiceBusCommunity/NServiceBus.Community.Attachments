#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace NServiceBus.Attachments
#if FileShare
.FileShare
#endif
#if Sql
.Sql
#endif
#if Raw
.Raw
#endif
;

/// <summary>
/// Wraps a <see cref="Stream"/> to provide extra information when reading.
/// </summary>
public class AttachmentStream :
    Stream,
    IAttachment
{
    // ReSharper disable once CollectionNeverUpdated.Local
    static Dictionary<string, string> emptyDictionary = [];

    /// <summary>
    /// An empty <see cref="AttachmentStream"/> that contains a "default" name and empty <see cref="MemoryStream"/> as content.
    /// </summary>
    public static AttachmentStream Empty() =>
        new("default", new MemoryStream(), 0, emptyDictionary);

    Stream inner;
    IEnumerable<Func<ValueTask>>? cleanups;
    long position;

    /// <summary>
    /// Initialises a new instance of <see cref="AttachmentStream"/>.
    /// </summary>
    /// <param name="name">The name of the attachment.</param>
    /// <param name="inner">The <see cref="Stream"/> to wrap.</param>
    /// <param name="length">The length of <paramref name="inner"/>.</param>
    /// <param name="metadata">The attachment metadata.</param>
    /// <param name="cleanups">Any extra cleanups.</param>
    public AttachmentStream(
        string name,
        Stream inner,
        long length,
        IReadOnlyDictionary<string, string> metadata,
        params Func<ValueTask>[] cleanups)
    {
        Guard.AgainstNullOrEmpty(name);
        this.inner = inner;
        this.cleanups = cleanups;
        Name = name;
        Length = length;
        Metadata = metadata;
    }

    /// <summary>
    /// Initialises a new instance of <see cref="AttachmentStream"/>.
    /// </summary>
    /// <param name="name">The name of the attachment.</param>
    /// <param name="inner">The <see cref="Stream"/> to wrap.</param>
    /// <param name="metadata">The attachment metadata.</param>
    public AttachmentStream(
        string name,
        Stream inner,
        IReadOnlyDictionary<string, string>? metadata = null) : this(name, inner, inner.Length, metadata ?? new Dictionary<string, string>(), inner.DisposeAsync)
    {
    }

    public override void EndWrite(IAsyncResult asyncResult) =>
        throw new NotImplementedException();

    public override void Flush() =>
        throw new NotImplementedException();

    public override Task FlushAsync(Cancel cancel) =>
        throw new NotImplementedException();

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, Cancel cancel)
    {
        var bytesRead = await inner.ReadAsync(buffer, offset, count, cancel);
        position += bytesRead;
        return bytesRead;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        inner.Dispose();
        if (cleanups is not null)
        {
            foreach (var cleanup in cleanups)
            {
                cleanup().GetAwaiter().GetResult();
            }
        }
    }

#if NET6_0_OR_GREATER

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await inner.DisposeAsync();
        if (cleanups is not null)
        {
            foreach (var cleanup in cleanups)
            {
                await cleanup();
            }
        }
    }

    public override void Write(ReadOnlySpan<byte> buffer) =>
        throw new NotImplementedException();

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, Cancel cancel = default) =>
        throw new NotImplementedException();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, Cancel cancel = default)
    {
        var bytesRead = await inner.ReadAsync(buffer,  cancel);
        position += bytesRead;
        return bytesRead;
    }

    public override int Read(Span<byte> buffer)
    {
        var bytesRead = inner.Read(buffer);
        position += bytesRead;
        return bytesRead;
    }

    public override void CopyTo(Stream destination, int bufferSize)
    {
        position = Length;
        inner.CopyTo(destination, bufferSize);
    }
#endif

    public override int ReadByte()
    {
        var readByte = inner.ReadByte();
        if (readByte != -1)
        {
            position++;
        }
        return readByte;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (offset == 0 && origin == SeekOrigin.Current)
        {
            return position;
        }

        var seek = inner.Seek(offset, origin);
        position = seek;
        return seek;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = inner.Read(buffer, offset, count);
        position += bytesRead;
        return bytesRead;
    }

    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanTimeout => inner.CanTimeout;
    public override bool CanWrite => false;

    /// <inheritdoc />
    public string Name { get; }
    public override long Length { get; }
    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Metadata { get; }
    public override int ReadTimeout => inner.ReadTimeout;

    public override long Position
    {
        get => position;
        set
        {
            if (position == value)
            {
                return;
            }

            throw new NotImplementedException();
        }
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
        inner.BeginRead(buffer, offset, count, callback, state);

    public override void Close()
    {
        inner.Close();
        base.Close();
    }

    public override Task CopyToAsync(Stream destination, int bufferSize, Cancel cancel)
    {
        position = Length;
        return inner.CopyToAsync(destination, bufferSize, cancel);
    }

    public override int EndRead(IAsyncResult asyncResult)
    {
        var readBytes = inner.EndRead(asyncResult);
        position += readBytes;
        return readBytes;
    }

    public override bool Equals(object? obj) =>
        inner.Equals(obj);

    public override int GetHashCode() =>
        inner.GetHashCode();

    public override string? ToString() =>
        inner.ToString();

    public override void SetLength(long value) =>
        throw new NotImplementedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotImplementedException();

    public override Task WriteAsync(byte[] buffer, int offset, int count, Cancel cancel) =>
        throw new NotImplementedException();

    public override void WriteByte(byte value) =>
        throw new NotImplementedException();

    public override int WriteTimeout => throw new NotImplementedException();

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
        throw new NotImplementedException();
}