using Microsoft.Extensions.DependencyInjection;

[NotInParallel]
public class AddFromIncomingTests
{
    static ManualResetEvent resetEvent = new(false);
    static byte[]? receivedBytes;

    [Test]
    public Task TransformsIncomingAttachmentInOutgoingPipeline() =>
        RunRoundTrip(
            endpointSuffix: "Streamed",
            sendStartMessage: endpoint => endpoint.Send(new InMessage(), BuildSendOptions("hello")),
            expected: "HELLO");

    [Test]
    public Task BufferSourceMakesInputSeekable() =>
        RunRoundTrip(
            endpointSuffix: "Buffered",
            sendStartMessage: endpoint => endpoint.Send(new SeekMessage(), BuildSendOptions("seekme")),
            expected: "seekme(len=6)");

    static async Task RunRoundTrip(string endpointSuffix, Func<IEndpointInstance, Task> sendStartMessage, string expected)
    {
        receivedBytes = null;
        resetEvent.Reset();

        await using var database = await Connection.SqlInstance.Build($"AddFromIncoming_{endpointSuffix}");
        var connectionString = database.ConnectionString;
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

        var configuration = new EndpointConfiguration($"SqlAddFromIncoming_{endpointSuffix}");
        SqlConnection NewConnection() => new(connectionString);
        var attachments = configuration.EnableAttachments(NewConnection, TimeToKeep.Default, database: databaseName, table: "Attachments");
        configuration.UseSerialization<SystemJsonSerializer>();
        configuration.UsePersistence<LearningPersistence>();
        configuration.DisableRetries();
        configuration.EnableInstallers();
        configuration.PurgeOnStartup(true);
        attachments.DisableCleanupTask();
        configuration.RegisterComponents(_ => _.AddSingleton(resetEvent));
        var transport = configuration.UseTransport<LearningTransport>();
        transport.StorageDirectory(Path.Combine(Path.GetTempPath(), $"AddFromIncoming_{endpointSuffix}"));
        transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);

        var endpoint = await Endpoint.Start(configuration);
        await sendStartMessage(endpoint);

        if (!resetEvent.WaitOne(TimeSpan.FromSeconds(20)))
        {
            throw new("TimedOut");
        }

        await endpoint.Stop();

        await Assert.That(Encoding.UTF8.GetString(receivedBytes!)).IsEqualTo(expected);
    }

    static SendOptions BuildSendOptions(string content)
    {
        var sendOptions = new SendOptions();
        sendOptions.RouteToThisEndpoint();
        var outgoing = sendOptions.Attachments();
        outgoing.AddStream(
            "input",
            async stream =>
            {
                await using var writer = new StreamWriter(stream, leaveOpen: true);
                await writer.WriteAsync(content);
            });
        return sendOptions;
    }

    class InMessage :
        IMessage;

    class SeekMessage :
        IMessage;

    class OutMessage :
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
