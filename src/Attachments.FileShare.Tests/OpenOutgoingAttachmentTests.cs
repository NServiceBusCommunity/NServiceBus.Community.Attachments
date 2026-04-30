[NotInParallel]
public class OpenOutgoingAttachmentTests :
    IDisposable
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

        var configuration = new EndpointConfiguration("FileShareOpenOutgoingAttachmentTests");
        configuration.UsePersistence<LearningPersistence>();
        configuration.UseTransport<LearningTransport>();
        configuration.RegisterComponents(_ => _.AddSingleton(resetEvent));
        configuration.EnableAttachments(Path.GetFullPath("attachments/OpenOutgoingAttachmentTests"), TimeToKeep.Default);
        configuration.UseSerialization<SystemJsonSerializer>();
        var endpoint = await Endpoint.Start(configuration);

        var sendOptions = new SendOptions();
        sendOptions.RouteToThisEndpoint();
        var attachments = sendOptions.Attachments();
        attachments.AddStream(
            "input",
            async stream =>
            {
                await using var writer = new StreamWriter(stream, leaveOpen: true);
                await writer.WriteAsync("hello");
            });
        await endpoint.Send(new InMessage(), sendOptions);

        resetEvent.WaitOne(TimeSpan.FromSeconds(20));
        await endpoint.Stop();

        await Assert.That(Encoding.UTF8.GetString(receivedBytes!)).IsEqualTo("HELLO");
        await Assert.That(receivedTruncated).IsTrue();
    }

    public void Dispose()
    {
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

            await context.Reply(
                new OutMessage
                {
                    Truncated = truncated
                },
                replyOptions);
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
