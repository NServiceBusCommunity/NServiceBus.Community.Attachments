namespace NServiceBus.Attachments
#if FileShare
    .FileShare
#elif Sql
    .Sql
#endif
;

// Wraps a writable Stream that the handler writes to. Disposal:
//   1. Disposes the underlying writer (signals end-of-input to the persister).
//   2. Awaits the persister's commit task so any storage error surfaces here.
//   3. Registers the saved attachment in OutgoingAttachments so SendBehavior emits the
//      Attachments header but skips the deferred save.
sealed class ImmediateAttachmentStream :
    Stream
{
    readonly Stream inner;
    readonly Func<ValueTask<Guid?>> commitAsync;
    readonly Action<Guid?> onCommitted;
    bool committed;

    public ImmediateAttachmentStream(Stream inner, Func<ValueTask<Guid?>> commitAsync, Action<Guid?> onCommitted)
    {
        this.inner = inner;
        this.commitAsync = commitAsync;
        this.onCommitted = onCommitted;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => inner.Length;
    public override long Position
    {
        get => inner.Position;
        set => throw new NotSupportedException();
    }

    public override void Flush() => inner.Flush();
    public override Task FlushAsync(Cancel cancel) => inner.FlushAsync(cancel);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
    public override void Write(ReadOnlySpan<byte> buffer) => inner.Write(buffer);
    public override void WriteByte(byte value) => inner.WriteByte(value);

    public override Task WriteAsync(byte[] buffer, int offset, int count, Cancel cancel) =>
        inner.WriteAsync(buffer, offset, count, cancel);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, Cancel cancel = default) =>
        inner.WriteAsync(buffer, cancel);

    public override async ValueTask DisposeAsync()
    {
        if (committed)
        {
            return;
        }

        committed = true;
        await inner.DisposeAsync();
        var guid = await commitAsync();
        onCommitted(guid);
        await base.DisposeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (committed)
        {
            base.Dispose(disposing);
            return;
        }

        committed = true;
        if (disposing)
        {
            inner.Dispose();
            var guid = commitAsync().AsTask().GetAwaiter().GetResult();
            onCommitted(guid);
        }
        base.Dispose(disposing);
    }
}
