using Microsoft.Extensions.DependencyInjection;

[NotInParallel]
public class AddFromIncomingDefaultNameTests
{
    static ManualResetEvent resetEvent = new(false);
    static byte[]? receivedBytes;

    [Test]
    public async Task RoundTripsWithDefaultName()
    {
        receivedBytes = null;
        resetEvent.Reset();

        await using var database = await Connection.SqlInstance.Build("AddFromIncomingDefaultName");
        var connectionString = database.ConnectionString;
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

        var configuration = new EndpointConfiguration("SqlAddFromIncomingDefaultName");
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
        transport.StorageDirectory(Path.Combine(Path.GetTempPath(), "AddFromIncomingDefaultName"));
        transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);

        var endpoint = await Endpoint.Start(configuration);

        // Sender writes the incoming attachment under the default name.
        var sendOptions = new SendOptions();
        sendOptions.RouteToThisEndpoint();
        var outgoing = sendOptions.Attachments();
        outgoing.Add(BuildStream("hello"));
        await endpoint.Send(new InMessage(), sendOptions);

        if (!resetEvent.WaitOne(TimeSpan.FromSeconds(20)))
        {
            await endpoint.Stop();
            throw new("TimedOut");
        }

        await endpoint.Stop();

        await Assert.That(Encoding.UTF8.GetString(receivedBytes!)).IsEqualTo("HELLO");
    }

    static MemoryStream BuildStream(string content) =>
        new(Encoding.UTF8.GetBytes(content));

    class InMessage :
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
            // No-name overload reads the default-named incoming and registers the result under the default name.
            outgoing.AddFromIncoming(transform: async (source, sink, cancel) =>
            {
                using var reader = new StreamReader(source, leaveOpen: true);
                var content = await reader.ReadToEndAsync(cancel);
                await using var writer = new StreamWriter(sink, leaveOpen: true);
                await writer.WriteAsync(content.ToUpperInvariant());
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
            // No-name CopyTo reads the default-named attachment.
            await incoming.CopyTo(memoryStream, context.CancellationToken);
            receivedBytes = memoryStream.ToArray();
            resetEvent.Set();
        }
    }
}
