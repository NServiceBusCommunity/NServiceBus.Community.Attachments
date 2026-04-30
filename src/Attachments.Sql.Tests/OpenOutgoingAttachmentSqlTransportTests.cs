using Microsoft.Extensions.DependencyInjection;

[NotInParallel]
public class OpenOutgoingAttachmentSqlTransportTests
{
    static readonly TestState state = new();

    [Test]
    [Arguments(TransportTransactionMode.SendsAtomicWithReceive)]
    [Arguments(TransportTransactionMode.ReceiveOnly)]
    public Task ImmediateWriteCommitsUnderEachTransportConnectionMode(TransportTransactionMode transactionMode) =>
        RunForMode(transactionMode);

    // TransactionScope opens a second connection inside the ambient transaction (the
    // SqlAttachmentState.Transaction branch), which promotes to DTC. Marked Explicit so the
    // suite stays green on machines without MSDTC; run it on infrastructure that has DTC enabled.
    [Test]
    [Explicit]
    public Task ImmediateWriteCommitsUnderTransactionScope() =>
        RunForMode(TransportTransactionMode.TransactionScope);

    static async Task RunForMode(TransportTransactionMode transactionMode)
    {
        state.Bytes = null;
        state.Reply.Reset();

        // Each mode pushes a different artifact into TransportTransaction (System.Transactions.Transaction
        // for TransactionScope, SqlTransaction for SendsAtomicWithReceive, SqlConnection for ReceiveOnly),
        // exercising the three non-factory branches of ResolveConnection.
        var dbName = $"OpenImmediate_{transactionMode}";
        await using var database = await Connection.SqlInstance.Build(dbName);
        var connectionString = database.ConnectionString;
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

        var configuration = new EndpointConfiguration($"SqlOpenImmediate_{transactionMode}");
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
            var replyOptions = new ReplyOptions();
            await using (var sink = await context.OpenOutgoingAttachment(replyOptions, "output"))
            {
                await using var writer = new StreamWriter(sink, leaveOpen: true);
                await writer.WriteAsync("HELLO");
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
