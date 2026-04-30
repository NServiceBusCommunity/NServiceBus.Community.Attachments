[NotInParallel]
public class AddFromIncomingTests :
    IDisposable
{
    static ManualResetEvent resetEvent = new(false);
    static byte[]? receivedBytes;

    [Test]
    public async Task TransformsIncomingAttachmentInOutgoingPipeline()
    {
        receivedBytes = null;
        resetEvent.Reset();

        var configuration = new EndpointConfiguration("FileShareAddFromIncomingTests");
        configuration.UsePersistence<LearningPersistence>();
        configuration.UseTransport<LearningTransport>();
        configuration.RegisterComponents(_ => _.AddSingleton(resetEvent));
        configuration.EnableAttachments(Path.GetFullPath("attachments/AddFromIncomingTests"), TimeToKeep.Default);
        configuration.UseSerialization<SystemJsonSerializer>();
        var endpoint = await Endpoint.Start(configuration);

        var sendOptions = new SendOptions();
        sendOptions.RouteToThisEndpoint();
        var attachments = sendOptions.Attachments();
        attachments.AddStream(
            "input",
            async stream =>
            {
                await using var writer = new StreamWriter(stream, leaveOpen: true);
                await writer.WriteAsync("hello");
            });
        await endpoint.Send(new InMessage(), sendOptions);

        resetEvent.WaitOne(TimeSpan.FromSeconds(20));
        await endpoint.Stop();

        await Assert.That(Encoding.UTF8.GetString(receivedBytes!)).IsEqualTo("HELLO");
    }

    [Test]
    public async Task BufferSourceMakesInputSeekable()
    {
        receivedBytes = null;
        resetEvent.Reset();

        var configuration = new EndpointConfiguration("FileShareAddFromIncomingBufferSourceTests");
        configuration.UsePersistence<LearningPersistence>();
        configuration.UseTransport<LearningTransport>();
        configuration.RegisterComponents(_ => _.AddSingleton(resetEvent));
        configuration.EnableAttachments(Path.GetFullPath("attachments/AddFromIncomingBufferSourceTests"), TimeToKeep.Default);
        configuration.UseSerialization<SystemJsonSerializer>();
        var endpoint = await Endpoint.Start(configuration);

        var sendOptions = new SendOptions();
        sendOptions.RouteToThisEndpoint();
        var attachments = sendOptions.Attachments();
        attachments.AddStream(
            "input",
            async stream =>
            {
                await using var writer = new StreamWriter(stream, leaveOpen: true);
                await writer.WriteAsync("seekme");
            });
        await endpoint.Send(new SeekMessage(), sendOptions);

        resetEvent.WaitOne(TimeSpan.FromSeconds(20));
        await endpoint.Stop();

        await Assert.That(Encoding.UTF8.GetString(receivedBytes!)).IsEqualTo("seekme(len=6)");
    }

    public void Dispose()
    {
    }

    class InMessage :
        IMessage;

    class OutMessage :
        IMessage;

    class SeekMessage :
        IMessage;

    class InHandler :
        IHandleMessages<InMessage>
    {
        public Task Handle(InMessage message, HandlerContext context)
        {
            var replyOptions = new ReplyOptions();
            var outgoing = replyOptions.Attachments();
            outgoing.AddFromIncoming(
                fromName: "input",
                toName: "output",
                transform: async (source, sink, cancel) =>
                {
                    using var reader = new StreamReader(source, leaveOpen: true);
                    var content = await reader.ReadToEndAsync(cancel);
                    await using var writer = new StreamWriter(sink, leaveOpen: true);
                    await writer.WriteAsync(content.ToUpperInvariant());
                });
            return context.Reply(new OutMessage(), replyOptions);
        }
    }

    class SeekHandler :
        IHandleMessages<SeekMessage>
    {
        public Task Handle(SeekMessage message, HandlerContext context)
        {
            var replyOptions = new ReplyOptions();
            var outgoing = replyOptions.Attachments();
            outgoing.AddFromIncoming(
                fromName: "input",
                toName: "output",
                bufferSource: true,
                transform: async (source, sink, cancel) =>
                {
                    var length = source.Length;
                    source.Position = 0;
                    using var reader = new StreamReader(source, leaveOpen: true);
                    var content = await reader.ReadToEndAsync(cancel);
                    await using var writer = new StreamWriter(sink, leaveOpen: true);
                    await writer.WriteAsync($"{content}(len={length})");
                });
            return context.Reply(new OutMessage(), replyOptions);
        }
    }

    class OutHandler :
        IHandleMessages<OutMessage>
    {
        public async Task Handle(OutMessage message, HandlerContext context)
        {
            await using var memoryStream = new MemoryStream();
            var incoming = context.Attachments();
            await incoming.CopyTo("output", memoryStream, context.CancellationToken);
            receivedBytes = memoryStream.ToArray();
            resetEvent.Set();
        }
    }
}
