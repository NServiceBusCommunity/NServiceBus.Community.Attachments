using TUnit.Core.Interfaces;

public class ParallelLimit4 : IParallelLimit
{
    public int Limit => 4;
}

[ParallelLimiter<ParallelLimit4>]
public class IntegrationTests :
    IDisposable
{
    static ManualResetEvent resetEvent = new(false);

    [Test]
    public async Task Run()
    {
        var configuration = new EndpointConfiguration("FileShareIntegrationTests");
        configuration.UsePersistence<LearningPersistence>();
        configuration.UseTransport<LearningTransport>();
        configuration.RegisterComponents(_ => _.AddSingleton(resetEvent));
        configuration.EnableAttachments(Path.GetFullPath("attachments/IntegrationTests"), TimeToKeep.Default);
        configuration.UseSerialization<SystemJsonSerializer>();
        var endpoint = await Endpoint.Start(configuration);
        await SendStartMessage(endpoint);
        resetEvent.WaitOne();
        await endpoint.Stop();
    }

    public void Dispose() =>
        resetEvent.Dispose();

    static Task SendStartMessage(IEndpointInstance endpoint)
    {
        var sendOptions = new SendOptions();
        sendOptions.RouteToThisEndpoint();
        var attachment = sendOptions.Attachments();
        attachment.AddStreamWriter(async stream => await GetStream().CopyToAsync(stream));
        attachment.AddStreamWriter(
            "withMetadata",
            async stream => await GetStream().CopyToAsync(stream),
            metadata: new Dictionary<string, string>
            {
                {
                    "key", "value"
                }
            });
        attachment.Add(async appendAttachment =>
        {
            await appendAttachment("viaAttachmentFactory1", GetStream());
            await appendAttachment("viaAttachmentFactory2", GetStream());
        });
        return endpoint.Send(new SendMessage(), sendOptions);
    }

    static Stream GetStream()
    {
        var stream = new MemoryStream();
        var streamWriter = new StreamWriter(stream);
        streamWriter.Write("content");
        streamWriter.Flush();
        stream.Position = 0;
        return stream;
    }

    class SendHandler :
        IHandleMessages<SendMessage>
    {
        public async Task Handle(SendMessage message, HandlerContext context)
        {
            var incomingAttachments = context.Attachments();
            var withAttachment = await incomingAttachments.GetBytes("withMetadata", context.CancellationToken);
            await Assert.That(withAttachment.Metadata["key"]).IsEqualTo("value");
            var replyOptions = new ReplyOptions();
            var outgoingAttachment = replyOptions.Attachments();
            outgoingAttachment.AddStreamWriter(async stream => { await using var source = await incomingAttachments.GetStream(); await source.CopyToAsync(stream); });
            await context.Reply(new ReplyMessage(), replyOptions);
            var attachmentInfos = await incomingAttachments.GetMetadata(context.CancellationToken).ToAsyncList();
            await Assert.That(attachmentInfos.Count).IsEqualTo(4);
        }
    }

    class ReplyHandler :
        IHandleMessages<ReplyMessage>
    {
        public async Task Handle(ReplyMessage message, HandlerContext context)
        {
            await using var memoryStream = new MemoryStream();
            var incomingAttachment = context.Attachments();
            await incomingAttachment.CopyTo(memoryStream, context.CancellationToken);
            memoryStream.Position = 0;
            var buffer = memoryStream.GetBuffer();
            Debug.WriteLine(buffer);
            var attachmentInfos = await incomingAttachment.GetMetadata(context.CancellationToken).ToAsyncList();
            await Assert.That(attachmentInfos).HasSingleItem();
            resetEvent.Set();
        }
    }

    class SendMessage :
        IMessage;

    class ReplyMessage :
        IMessage;
}
