using Microsoft.Extensions.DependencyInjection;

[NotInParallel]
public class OpenOutgoingAttachmentExtraTests
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

        await using var database = await Connection.SqlInstance.Build($"OpenOutgoingExtra_{suffix}");
        var connectionString = database.ConnectionString;
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

        var configuration = new EndpointConfiguration($"SqlOpenOutgoingExtra_{suffix}");
        SqlConnection NewConnection() => new(connectionString);
        var attachments = configuration.EnableAttachments(NewConnection, TimeToKeep.Default, database: databaseName, table: "Attachments");
        configuration.UseSerialization<SystemJsonSerializer>();
        configuration.UsePersistence<LearningPersistence>();
        configuration.DisableRetries();
        configuration.EnableInstallers();
        configuration.PurgeOnStartup(true);
        attachments.DisableCleanupTask();
        configuration.RegisterComponents(_ => _.AddSingleton(state));
        var transport = configuration.UseTransport<LearningTransport>();
        transport.StorageDirectory(Path.Combine(Path.GetTempPath(), $"OpenOutgoingExtra_{suffix}"));
        transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);

        var endpoint = await Endpoint.Start(configuration);

        var sendOptions = new SendOptions();
        sendOptions.RouteToThisEndpoint();
        var outgoing = sendOptions.Attachments();
        outgoing.AddStream("input", async stream =>
        {
            await using var writer = new StreamWriter(stream, leaveOpen: true);
            await writer.WriteAsync("hello");
        });
        await endpoint.Send(new TMessage(), sendOptions);

        if (!state.Reply.WaitOne(TimeSpan.FromSeconds(20)))
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
