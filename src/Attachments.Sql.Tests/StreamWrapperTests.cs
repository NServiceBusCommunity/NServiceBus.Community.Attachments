// ReSharper disable MustUseReturnValue
// ReSharper disable StreamReadReturnValueIgnored
public class StreamWrapperTests
{
    static byte[] buffer = "content"u8.ToArray();

    [Test]
    public Task ReadBytesAsync() =>
        Run(_ => _.ReadAsync(new byte[2], 0, 2));

    [Test]
    public Task ReadBytes() =>
        Run(_ => _.Read(new byte[2], 0, 2));

    [Test]
    public Task ReadSpan() =>
        Run(_ => _.Read(new(new byte[2])));

    [Test]
    public Task ReadMemory() =>
        Run(async _ => await _.ReadAsync(new(new byte[2])));

    [Test]
    public Task ReadByte() =>
        Run(_ => _.ReadByte());

    [Test]
    public Task CopyTo() =>
        Run(_ => _.CopyTo(new MemoryStream()));

    [Test]
    public Task CopyToAsync() =>
        Run(_ => _.CopyToAsync(new MemoryStream()));

    static async Task Run(Action<AttachmentStream> action)
    {
        using var stream = new MemoryStream(buffer);
        var wrapper = new AttachmentStream("name", stream,buffer.Length, new Dictionary<string, string>());
        action(wrapper);
        await Assert.That(wrapper.Position).IsEqualTo(stream.Position);
    }

    static async Task Run(Func<AttachmentStream, Task> action)
    {
        using var stream = new MemoryStream(buffer);
        var wrapper = new AttachmentStream("name", stream,buffer.Length, new Dictionary<string, string>());
        await action(wrapper);
        await Assert.That(wrapper.Position).IsEqualTo(stream.Position);
    }
}
