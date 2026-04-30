using Microsoft.Extensions.DependencyInjection;

[NotInParallel]
public class OpenOutgoingAttachmentConcurrentReadTests
{
    static readonly TestState state = new();

    // Reads run on a fresh connection (MessageContextExtensions.Attachments goes through the
    // connection factory). This test holds an OpenOutgoingAttachment sink open on the receive
    // transport's connection while reading the incoming attachment — the previous design would
    // have failed with "MultipleActiveResultSets".
    [Test]
    [Arguments(TransportTransactionMode.SendsAtomicWithReceive)]
    [Arguments(TransportTransactionMode.ReceiveOnly)]
    public async Task ReadsIncomingWhileSinkIsOpen(TransportTransactionMode transactionMode)
    {
        state.Bytes = null;
        state.Reply.Reset();

        var dbName = $"OpenConcurrent_{transactionMode}";
        await using var database = await Connection.SqlInstance.Build(dbName);
        var connectionString = database.ConnectionString;
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

        var configuration = new EndpointConfiguration($"SqlOpenConcurrent_{transactionMode}");
        SqlConnection NewConnection() => new(connectionString);
        var attachments = configuration.EnableAttachments(NewConnection, TimeToKeep.Default, database: databaseName, table: "Attachments");
        attachments.UseTransportConnectivity();
        configuration.UseSerialization<SystemJsonSerializer>();
        configuration.UsePersistence<LearningPersistence>();
        configuration.DisableRetries();
        configuration.EnableInstallers();
        configuration.PurgeOnStartup(true);
        attachments.DisableCleanupTask();
        configuration.RegisterComponents(_ => _.AddSingleton(state));

        var transport = configuration.UseTransport<SqlServerTransport>();
        transport.ConnectionString(connectionString);
        transport.Transactions(transactionMode);

        var endpoint = await Endpoint.Start(configuration);

        var sendOptions = new SendOptions();
        sendOptions.RouteToThisEndpoint();
        var outgoing = sendOptions.Attachments();
        outgoing.AddStream("input", async stream =>
        {
            await using var writer = new StreamWriter(stream, leaveOpen: true);
            await writer.WriteAsync("hello");
        });
        await endpoint.Send(new InMessage(), sendOptions);

        if (!state.Reply.WaitOne(TimeSpan.FromSeconds(30)))
        {
            await endpoint.Stop();
            throw new($"TimedOut for mode {transactionMode}");
        }

        await endpoint.Stop();

        await Assert.That(Encoding.UTF8.GetString(state.Bytes!)).IsEqualTo("HELLO");
    }

    class TestState
    {
        public byte[]? Bytes;
        public ManualResetEvent Reply = new(false);
    }

    class InMessage :
        IMessage;

    class OutMessage :
        IMessage;

    class InHandler :
        IHandleMessages<InMessage>
    {
        public async Task Handle(InMessage message, HandlerContext context)
        {
            var incoming = context.Attachments();
            var replyOptions = new ReplyOptions();

            await using (var sink = await context.OpenOutgoingAttachment(replyOptions, "output"))
            {
                // Read the incoming attachment while the outgoing sink is held open.
                using var sourceBuffer = new MemoryStream();
                await incoming.CopyTo("input", sourceBuffer, context.CancellationToken);
                sourceBuffer.Position = 0;
                using var reader = new StreamReader(sourceBuffer, leaveOpen: true);
                var content = await reader.ReadToEndAsync(context.CancellationToken);
                await using var writer = new StreamWriter(sink, leaveOpen: true);
                await writer.WriteAsync(content.ToUpperInvariant());
            }

            await context.Reply(new OutMessage(), replyOptions);
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
            state.Reply.Set();
        }
    }
}
