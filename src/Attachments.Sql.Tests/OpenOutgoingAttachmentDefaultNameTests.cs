using Microsoft.Extensions.DependencyInjection;

[NotInParallel]
public class OpenOutgoingAttachmentDefaultNameTests
{
    static readonly TestState state = new();

    [Test]
    public async Task RoundTripsWithDefaultName()
    {
        state.Bytes = null;
        state.Reply.Reset();

        await using var database = await Connection.SqlInstance.Build("OpenDefaultName");
        var connectionString = database.ConnectionString;
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

        var configuration = new EndpointConfiguration("SqlOpenDefaultName");
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
        transport.StorageDirectory(Path.Combine(Path.GetTempPath(), "OpenDefaultName"));
        transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);

        var endpoint = await Endpoint.Start(configuration);

        await endpoint.SendLocal(new InMessage());

        if (!state.Reply.WaitOne(TimeSpan.FromSeconds(20)))
        {
            await endpoint.Stop();
            throw new("TimedOut");
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
            var sendOptions = new SendOptions();
            sendOptions.RouteToThisEndpoint();

            // No-name overload registers the attachment under the default name.
            await using (var sink = await context.OpenOutgoingAttachment(sendOptions))
            {
                await using var writer = new StreamWriter(sink, leaveOpen: true);
                await writer.WriteAsync("HELLO");
            }

            await context.Send(new OutMessage(), sendOptions);
        }
    }

    class OutHandler :
        IHandleMessages<OutMessage>
    {
        public async Task Handle(OutMessage message, HandlerContext context)
        {
            // No-name CopyTo reads the default-named attachment, proving the round-trip.
            var incoming = context.Attachments();
            await using var memoryStream = new MemoryStream();
            await incoming.CopyTo(memoryStream, context.CancellationToken);
            state.Bytes = memoryStream.ToArray();
            state.Reply.Set();
        }
    }
}
