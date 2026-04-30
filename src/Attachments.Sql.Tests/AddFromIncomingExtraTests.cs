using Microsoft.Extensions.DependencyInjection;

[NotInParallel]
public class AddFromIncomingExtraTests
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
    }

    class TestState
    {
        public byte[]? Bytes;
        public IReadOnlyDictionary<string, string>? Metadata;
        public ManualResetEvent Reply = new(false);
    }

    static async Task Run<TMessage>(string suffix, string sourceContent)
        where TMessage : IMessage, new()
    {
        state.Bytes = null;
        state.Metadata = null;
        state.Reply.Reset();

        await using var database = await Connection.SqlInstance.Build($"AddFromIncomingExtra_{suffix}");
        var connectionString = database.ConnectionString;
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

        var configuration = new EndpointConfiguration($"SqlAddFromIncomingExtra_{suffix}");
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
        transport.StorageDirectory(Path.Combine(Path.GetTempPath(), $"AddFromIncomingExtra_{suffix}"));
        transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);

        var endpoint = await Endpoint.Start(configuration);

        var sendOptions = new SendOptions();
        sendOptions.RouteToThisEndpoint();
        var outgoing = sendOptions.Attachments();
        outgoing.AddStream("input", async stream =>
        {
            await using var writer = new StreamWriter(stream, leaveOpen: true);
            await writer.WriteAsync(sourceContent);
        });
        await endpoint.Send(new TMessage(), sendOptions);

        if (!state.Reply.WaitOne(TimeSpan.FromSeconds(20)))
        {
            await endpoint.Stop();
            throw new("TimedOut");
        }

        await endpoint.Stop();
    }

    class BufferSinkMessage :
        IMessage;

    class MetadataMessage :
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
                    {"k1", "v1"}
                },
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
