using Microsoft.Extensions.DependencyInjection;

[NotInParallel]
public class OpenOutgoingAttachmentInterleavedTests
{
    static readonly TestState state = new();

    // Reads a chunk from the incoming SQL stream and writes a transformed chunk to the outgoing
    // SQL sink in a tight loop, so both streams are live at the same time. With reads going via
    // the connection factory and writes against the receive transport's connection, the two
    // streams cannot collide on the same connection.
    [Test]
    [Arguments(TransportTransactionMode.SendsAtomicWithReceive)]
    [Arguments(TransportTransactionMode.ReceiveOnly)]
    public async Task InterleavesReadAndWrite(TransportTransactionMode transactionMode)
    {
        state.Bytes = null;
        state.Reply.Reset();

        var dbName = $"OpenInterleaved_{transactionMode}";
        await using var database = await Connection.SqlInstance.Build(dbName);
        var connectionString = database.ConnectionString;
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

        var configuration = new EndpointConfiguration($"SqlOpenInterleaved_{transactionMode}");
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

        // Use a payload long enough to span many read iterations against the 16-byte buffer.
        var payload = string.Concat(Enumerable.Repeat("abcdefghij", 1000));

        var sendOptions = new SendOptions();
        sendOptions.RouteToThisEndpoint();
        var outgoing = sendOptions.Attachments();
        outgoing.AddStream("input", async stream =>
        {
            await using var writer = new StreamWriter(stream, leaveOpen: true);
            await writer.WriteAsync(payload);
        });
        await endpoint.Send(new InMessage(), sendOptions);

        if (!state.Reply.WaitOne(TimeSpan.FromSeconds(30)))
        {
            await endpoint.Stop();
            throw new($"TimedOut for mode {transactionMode}");
        }

        await endpoint.Stop();

        await Assert.That(Encoding.UTF8.GetString(state.Bytes!)).IsEqualTo(payload.ToUpperInvariant());
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
            await using (var source = await incoming.GetStream("input", context.CancellationToken))
            {
                var buffer = new byte[16];
                int read;
                while ((read = await source.ReadAsync(buffer, context.CancellationToken)) > 0)
                {
                    for (var i = 0; i < read; i++)
                    {
                        if (buffer[i] is >= (byte)'a' and <= (byte)'z')
                        {
                            buffer[i] -= 32;
                        }
                    }

                    await sink.WriteAsync(buffer.AsMemory(0, read), context.CancellationToken);
                }
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
