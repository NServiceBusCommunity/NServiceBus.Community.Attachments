[NotInParallel]
public class AddFromIncomingExtraTests :
    IDisposable
{
    static readonly TestState state = new();

    [Test]
    public async Task BufferSinkLetsTransformSeekTheOutput()
    {
        await Run<BufferSinkMessage>("BufferSink", "abcd");
        await Assert.That(Encoding.UTF8.GetString(state.Bytes!)).IsEqualTo("abcd|len=4");
    }

    [Test]
    public async Task MetadataIsAttachedToProducedOutgoingAttachment()
    {
        await Run<MetadataMessage>("Metadata", "ignored");
        await Assert.That(state.Metadata).IsNotNull();
        await Assert.That(state.Metadata!["k1"]).IsEqualTo("v1");
        await Assert.That(state.Metadata["k2"]).IsEqualTo("v2");
    }

    [Test]
    public async Task TransformExceptionSurfacesAndPreventsReply()
    {
        await Run<TransformThrowsMessage>("TransformThrows", "ignored", expectReply: false);
        await Assert.That(state.HandlerException).IsNotNull();
        await Assert.That(state.HandlerException!.Message).Contains("transform boom");
    }

    [Test]
    public async Task CleanupCallbackIsInvokedAfterCommit()
    {
        await Run<CleanupMessage>("Cleanup", "ignored");
        await Assert.That(state.CleanupInvoked).IsTrue();
    }

    public void Dispose()
    {
    }

    class TestState
    {
        public byte[]? Bytes;
        public IReadOnlyDictionary<string, string>? Metadata;
        public Exception? HandlerException;
        public bool CleanupInvoked;
        public ManualResetEvent Reply = new(false);
    }

    static async Task Run<TMessage>(string suffix, string sourceContent, bool expectReply = true)
        where TMessage : IMessage, new()
    {
        state.Bytes = null;
        state.Metadata = null;
        state.HandlerException = null;
        state.CleanupInvoked = false;
        state.Reply.Reset();

        var attachmentsPath = Path.GetFullPath($"attachments/AddFromIncomingExtra_{suffix}");
        if (Directory.Exists(attachmentsPath))
        {
            Directory.Delete(attachmentsPath, recursive: true);
        }
        var transportPath = Path.GetFullPath($".learningtransport/AddFromIncomingExtra_{suffix}");
        if (Directory.Exists(transportPath))
        {
            Directory.Delete(transportPath, recursive: true);
        }

        var configuration = new EndpointConfiguration($"FileShareAddFromIncomingExtra_{suffix}");
        configuration.UsePersistence<LearningPersistence>();
        var transport = configuration.UseTransport<LearningTransport>();
        transport.StorageDirectory(transportPath);
        configuration.RegisterComponents(_ => _.AddSingleton(state));
        configuration.EnableAttachments(attachmentsPath, TimeToKeep.Default);
        configuration.UseSerialization<SystemJsonSerializer>();
        configuration.DisableRetries();
        var endpoint = await Endpoint.Start(configuration);

        var sendOptions = new SendOptions();
        sendOptions.RouteToThisEndpoint();
        var attachments = sendOptions.Attachments();
        attachments.AddStream("input", async stream =>
        {
            await using var writer = new StreamWriter(stream, leaveOpen: true);
            await writer.WriteAsync(sourceContent);
        });
        await endpoint.Send(new TMessage(), sendOptions);

        var fired = state.Reply.WaitOne(TimeSpan.FromSeconds(expectReply ? 10 : 5));
        await endpoint.Stop();

        if (expectReply && !fired)
        {
            throw new("TimedOut waiting for reply");
        }
    }

    class BufferSinkMessage :
        IMessage;

    class MetadataMessage :
        IMessage;

    class TransformThrowsMessage :
        IMessage;

    class CleanupMessage :
        IMessage;

    class OutMessage :
        IMessage;

    class BufferSinkHandler :
        IHandleMessages<BufferSinkMessage>
    {
        public Task Handle(BufferSinkMessage message, HandlerContext context)
        {
            var replyOptions = new ReplyOptions();
            var outgoing = replyOptions.Attachments();
            outgoing.AddFromIncoming(
                fromName: "input",
                toName: "output",
                bufferSink: true,
                transform: async (source, sink, cancel) =>
                {
                    if (!sink.CanSeek)
                    {
                        throw new("bufferSink: true expected the sink to be seekable.");
                    }

                    await source.CopyToAsync(sink, cancel);
                    // Reading sink.Length is only valid when bufferSink: true (a MemoryStream).
                    var trailer = Encoding.UTF8.GetBytes($"|len={sink.Length}");
                    await sink.WriteAsync(trailer, cancel);
                });
            return context.Reply(new OutMessage(), replyOptions);
        }
    }

    class MetadataHandler :
        IHandleMessages<MetadataMessage>
    {
        public Task Handle(MetadataMessage message, HandlerContext context)
        {
            var replyOptions = new ReplyOptions();
            var outgoing = replyOptions.Attachments();
            outgoing.AddFromIncoming(
                fromName: "input",
                toName: "output",
                metadata: new Dictionary<string, string>
                {
                    {"k1", "v1"},
                    {"k2", "v2"}
                },
                transform: (source, sink, cancel) => source.CopyToAsync(sink, cancel));
            return context.Reply(new OutMessage(), replyOptions);
        }
    }

    class TransformThrowsHandler :
        IHandleMessages<TransformThrowsMessage>
    {
        public async Task Handle(TransformThrowsMessage message, HandlerContext context)
        {
            try
            {
                var replyOptions = new ReplyOptions();
                var outgoing = replyOptions.Attachments();
                outgoing.AddFromIncoming(
                    fromName: "input",
                    toName: "output",
                    transform: (_, _, _) => throw new InvalidOperationException("transform boom"));
                await context.Reply(new OutMessage(), replyOptions);
            }
            catch (Exception ex)
            {
                state.HandlerException = ex;
                state.Reply.Set();
            }
        }
    }

    class CleanupHandler :
        IHandleMessages<CleanupMessage>
    {
        public Task Handle(CleanupMessage message, HandlerContext context)
        {
            var replyOptions = new ReplyOptions();
            var outgoing = replyOptions.Attachments();
            outgoing.AddFromIncoming(
                fromName: "input",
                toName: "output",
                cleanup: () => state.CleanupInvoked = true,
                transform: (source, sink, cancel) => source.CopyToAsync(sink, cancel));
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
            state.Bytes = memoryStream.ToArray();
            await foreach (var info in incoming.GetMetadata(context.CancellationToken))
            {
                if (info.Name == "output")
                {
                    state.Metadata = info.Metadata;
                }
            }
            state.Reply.Set();
        }
    }
}
