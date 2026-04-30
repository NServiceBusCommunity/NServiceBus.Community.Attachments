[NotInParallel]
public class OpenOutgoingAttachmentExtraTests :
    IDisposable
{
    static readonly TestState state = new();

    [Test]
    public async Task SendOptionsOverloadProducesAttachment()
    {
        await Run<SendMessage>("SendOptions");
        await Assert.That(Encoding.UTF8.GetString(state.Bytes!)).IsEqualTo("HELLO");
    }

    [Test]
    public async Task HonorsCallerProvidedMessageId()
    {
        var explicitId = Guid.NewGuid().ToString();
        state.ExplicitMessageId = explicitId;
        await Run<HonorIdMessage>("HonorMessageId");
        await Assert.That(state.ReceivedMessageId).IsEqualTo(explicitId);
    }

    [Test]
    public async Task MultipleImmediateWritesSurviveTogether()
    {
        await Run<MultipleMessage>("Multiple");
        await Assert.That(Encoding.UTF8.GetString(state.Bytes!)).IsEqualTo("FIRST");
        await Assert.That(Encoding.UTF8.GetString(state.SecondBytes!)).IsEqualTo("SECOND");
    }

    [Test]
    public async Task ImmediateAndAddStreamCoexistOnSameMessage()
    {
        await Run<MixedMessage>("Mixed");
        await Assert.That(Encoding.UTF8.GetString(state.Bytes!)).IsEqualTo("IMMEDIATE");
        await Assert.That(Encoding.UTF8.GetString(state.SecondBytes!)).IsEqualTo("DEFERRED");
    }

    [Test]
    public async Task SyncDisposeCommitsTheAttachment()
    {
        await Run<SyncDisposeMessage>("SyncDispose");
        await Assert.That(Encoding.UTF8.GetString(state.Bytes!)).IsEqualTo("sync");
    }

    public void Dispose()
    {
    }

    class TestState
    {
        public byte[]? Bytes;
        public byte[]? SecondBytes;
        public string? ExplicitMessageId;
        public string? ReceivedMessageId;
        public ManualResetEvent Reply = new(false);
    }

    static async Task Run<TMessage>(string suffix)
        where TMessage : IMessage, new()
    {
        state.Bytes = null;
        state.SecondBytes = null;
        state.ReceivedMessageId = null;
        state.Reply.Reset();

        var attachmentsPath = Path.GetFullPath($"attachments/OpenOutgoingExtra_{suffix}");
        if (Directory.Exists(attachmentsPath))
        {
            Directory.Delete(attachmentsPath, recursive: true);
        }
        var transportPath = Path.GetFullPath($".learningtransport/OpenOutgoingExtra_{suffix}");
        if (Directory.Exists(transportPath))
        {
            Directory.Delete(transportPath, recursive: true);
        }

        var configuration = new EndpointConfiguration($"FileShareOpenOutgoingExtra_{suffix}");
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
            await writer.WriteAsync("hello");
        });
        await endpoint.Send(new TMessage(), sendOptions);

        if (!state.Reply.WaitOne(TimeSpan.FromSeconds(10)))
        {
            await endpoint.Stop();
            throw new("TimedOut");
        }

        await endpoint.Stop();
    }

    class SendMessage :
        IMessage;

    class HonorIdMessage :
        IMessage;

    class MultipleMessage :
        IMessage;

    class MixedMessage :
        IMessage;

    class SyncDisposeMessage :
        IMessage;

    class OutMessage :
        IMessage;

    class SendHandler :
        IHandleMessages<SendMessage>
    {
        public async Task Handle(SendMessage message, HandlerContext context)
        {
            var sendOptions = new SendOptions();
            sendOptions.RouteToThisEndpoint();

            await using (var sink = await context.OpenOutgoingAttachment(sendOptions, "output"))
            {
                await using var writer = new StreamWriter(sink, leaveOpen: true);
                await writer.WriteAsync("HELLO");
            }

            await context.Send(new OutMessage(), sendOptions);
        }
    }

    class HonorIdHandler :
        IHandleMessages<HonorIdMessage>
    {
        public async Task Handle(HonorIdMessage message, HandlerContext context)
        {
            var replyOptions = new ReplyOptions();
            replyOptions.SetMessageId(state.ExplicitMessageId!);

            await using (var sink = await context.OpenOutgoingAttachment(replyOptions, "output"))
            {
                await using var writer = new StreamWriter(sink, leaveOpen: true);
                await writer.WriteAsync("payload");
            }

            await context.Reply(new OutMessage(), replyOptions);
        }
    }

    class MultipleHandler :
        IHandleMessages<MultipleMessage>
    {
        public async Task Handle(MultipleMessage message, HandlerContext context)
        {
            var replyOptions = new ReplyOptions();

            await using (var first = await context.OpenOutgoingAttachment(replyOptions, "output"))
            {
                await using var w = new StreamWriter(first, leaveOpen: true);
                await w.WriteAsync("FIRST");
            }

            await using (var second = await context.OpenOutgoingAttachment(replyOptions, "second"))
            {
                await using var w = new StreamWriter(second, leaveOpen: true);
                await w.WriteAsync("SECOND");
            }

            await context.Reply(new OutMessage(), replyOptions);
        }
    }

    class MixedHandler :
        IHandleMessages<MixedMessage>
    {
        public async Task Handle(MixedMessage message, HandlerContext context)
        {
            var replyOptions = new ReplyOptions();

            await using (var sink = await context.OpenOutgoingAttachment(replyOptions, "output"))
            {
                await using var w = new StreamWriter(sink, leaveOpen: true);
                await w.WriteAsync("IMMEDIATE");
            }

            replyOptions.Attachments().AddStream("second", async stream =>
            {
                await using var w = new StreamWriter(stream, leaveOpen: true);
                await w.WriteAsync("DEFERRED");
            });

            await context.Reply(new OutMessage(), replyOptions);
        }
    }

    class SyncDisposeHandler :
        IHandleMessages<SyncDisposeMessage>
    {
        public async Task Handle(SyncDisposeMessage message, HandlerContext context)
        {
            var replyOptions = new ReplyOptions();
            var sink = await context.OpenOutgoingAttachment(replyOptions, "output");
            using (sink)
            {
                using var w = new StreamWriter(sink, leaveOpen: true);
                w.Write("sync");
            }

            await context.Reply(new OutMessage(), replyOptions);
        }
    }

    class OutHandler :
        IHandleMessages<OutMessage>
    {
        public async Task Handle(OutMessage message, HandlerContext context)
        {
            state.ReceivedMessageId = context.MessageId;
            var incoming = context.Attachments();
            await using var firstStream = new MemoryStream();
            await incoming.CopyTo("output", firstStream, context.CancellationToken);
            state.Bytes = firstStream.ToArray();

            var infos = await incoming.GetMetadata(context.CancellationToken).ToAsyncList();
            if (infos.Any(_ => _.Name == "second"))
            {
                await using var secondStream = new MemoryStream();
                await incoming.CopyTo("second", secondStream, context.CancellationToken);
                state.SecondBytes = secondStream.ToArray();
            }

            state.Reply.Set();
        }
    }
}
