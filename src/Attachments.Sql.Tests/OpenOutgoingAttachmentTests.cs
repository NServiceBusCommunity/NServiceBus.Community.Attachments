using Microsoft.Extensions.DependencyInjection;

[NotInParallel]
public class OpenOutgoingAttachmentTests
{
    static ManualResetEvent resetEvent = new(false);
    static byte[]? receivedBytes;
    static bool? receivedTruncated;

    [Test]
    public async Task ImmediateWriteCarriesHandlerStateInReplyBody()
    {
        receivedBytes = null;
        receivedTruncated = null;
        resetEvent.Reset();

        await using var database = await Connection.SqlInstance.Build("OpenOutgoingAttachmentTests");
        var connectionString = database.ConnectionString;
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

        var configuration = new EndpointConfiguration("SqlOpenOutgoingAttachmentTests");
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
        transport.StorageDirectory(Path.Combine(Path.GetTempPath(), "OpenOutgoingAttachmentTests"));
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
        await endpoint.Send(new InMessage(), sendOptions);

        if (!resetEvent.WaitOne(TimeSpan.FromSeconds(20)))
        {
            throw new("TimedOut");
        }

        await endpoint.Stop();

        await Assert.That(Encoding.UTF8.GetString(receivedBytes!)).IsEqualTo("HELLO");
        await Assert.That(receivedTruncated).IsTrue();
    }

    class InMessage :
        IMessage;

    class OutMessage :
        IMessage
    {
        public bool Truncated { get; set; }
    }

    class InHandler :
        IHandleMessages<InMessage>
    {
        public async Task Handle(InMessage message, HandlerContext context)
        {
            var incoming = context.Attachments();
            using var sourceBuffer = new MemoryStream();
            await incoming.CopyTo("input", sourceBuffer, context.CancellationToken);
            sourceBuffer.Position = 0;

            var replyOptions = new ReplyOptions();
            bool truncated;

            await using (var sink = await context.OpenOutgoingAttachment(replyOptions, "output"))
            {
                using var reader = new StreamReader(sourceBuffer, leaveOpen: true);
                var content = await reader.ReadToEndAsync(context.CancellationToken);
                await using var writer = new StreamWriter(sink, leaveOpen: true);
                await writer.WriteAsync(content.ToUpperInvariant());
                truncated = content.Length > 3;
            }

            await context.Reply(new OutMessage { Truncated = truncated }, replyOptions);
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
            receivedTruncated = message.Truncated;
            resetEvent.Set();
        }
    }
}
