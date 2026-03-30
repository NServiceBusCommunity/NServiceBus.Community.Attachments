public class PositionTrackingStreamTests
{
    [Test]
    public async Task Position_StartsAtZero()
    {
        using var inner = new MemoryStream();
        using var stream = new PositionTrackingStream(inner);
        await Assert.That(stream.Position).IsEqualTo(0);
    }

    [Test]
    public async Task Write_TracksPosition()
    {
        using var inner = new MemoryStream();
        using var stream = new PositionTrackingStream(inner);
        stream.Write([1, 2, 3], 0, 3);
        await Assert.That(stream.Position).IsEqualTo(3);
        stream.Write([4, 5], 0, 2);
        await Assert.That(stream.Position).IsEqualTo(5);
    }

    [Test]
    public async Task WriteAsync_TracksPosition()
    {
        using var inner = new MemoryStream();
        using var stream = new PositionTrackingStream(inner);
        await stream.WriteAsync(new byte[] { 1, 2, 3 }, 0, 3);
        await Assert.That(stream.Position).IsEqualTo(3);
    }

    [Test]
    public async Task WriteAsyncMemory_TracksPosition()
    {
        using var inner = new MemoryStream();
        using var stream = new PositionTrackingStream(inner);
        await stream.WriteAsync(new byte[] { 1, 2, 3, 4 }.AsMemory());
        await Assert.That(stream.Position).IsEqualTo(4);
    }

    [Test]
    public async Task WriteByte_TracksPosition()
    {
        using var inner = new MemoryStream();
        using var stream = new PositionTrackingStream(inner);
        stream.WriteByte(42);
        await Assert.That(stream.Position).IsEqualTo(1);
        stream.WriteByte(43);
        await Assert.That(stream.Position).IsEqualTo(2);
    }

    [Test]
    public void SetPosition_Throws()
    {
        using var inner = new MemoryStream();
        using var stream = new PositionTrackingStream(inner);
        Assert.Throws<NotSupportedException>(() => stream.Position = 5);
    }

    [Test]
    public async Task WrittenData_PassedToInnerStream()
    {
        using var inner = new MemoryStream();
        using var stream = new PositionTrackingStream(inner);
        stream.Write([10, 20, 30], 0, 3);
        await Assert.That(inner.ToArray()).IsEquivalentTo(new byte[] { 10, 20, 30 });
    }

    [Test]
    public async Task Dispose_DoesNotDisposeInner()
    {
        var inner = new MemoryStream();
        var stream = new PositionTrackingStream(inner);
        stream.Dispose();
        // inner should still be usable
        inner.Write([1, 2, 3], 0, 3);
        await Assert.That(inner.ToArray()).IsEquivalentTo(new byte[] { 1, 2, 3 });
    }

    [Test]
    public async Task DisposeAsync_DoesNotDisposeInner()
    {
        var inner = new MemoryStream();
        var stream = new PositionTrackingStream(inner);
        await stream.DisposeAsync();
        // inner should still be usable
        inner.Write([1, 2, 3], 0, 3);
        await Assert.That(inner.ToArray()).IsEquivalentTo(new byte[] { 1, 2, 3 });
    }

    [Test]
    public async Task PipeHelper_WriterCanReadPosition()
    {
        long capturedPosition = -1;
        var (writerTask, readerStream) = PipeHelper.StartWriter(
            stream =>
            {
                stream.Write([1, 2, 3, 4, 5], 0, 5);
                capturedPosition = stream.Position;
                return Task.CompletedTask;
            });

        await using (readerStream)
        {
            await readerStream.CopyToAsync(Stream.Null);
        }

        await writerTask;
        await Assert.That(capturedPosition).IsEqualTo(5);
    }
}
