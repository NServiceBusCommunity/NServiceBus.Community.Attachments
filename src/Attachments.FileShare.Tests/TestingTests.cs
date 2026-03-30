public class TestingTests
{
    [Test]
    public async Task OutgoingAttachments()
    {
        var context = new RecordingHandlerContext();
        var handler = new OutgoingAttachmentsHandler();
        await handler.Handle(new(), context);
        var attachments = context.Sent
            .Single()
            .Options
            .Attachments();
        var attachment = attachments.Items.Single();
        await Assert.That(attachment.Name).Contains("theName");
        await Assert.That(attachments.HasPendingAttachments).IsTrue();
    }

    public class OutgoingAttachmentsHandler :
        IHandleMessages<AMessage>
    {
        public Task Handle(AMessage message, HandlerContext context)
        {
            var options = new SendOptions();
            var attachments = options.Attachments();
            attachments.AddStream("theName",
                async stream =>
                {
                    await using var source = File.OpenRead("");
                    await source.CopyToAsync(stream);
                });
            return context.Send(new AMessage(), options);
        }
    }

    [Test]
    public async Task OutgoingAttachmentsSync()
    {
        var context = new RecordingHandlerContext();
        var handler = new OutgoingAttachmentsSyncHandler();
        await handler.Handle(new(), context);
        var attachments = context.Sent
            .Single()
            .Options
            .Attachments();
        var attachment = attachments.Items.Single();
        await Assert.That(attachment.Name).Contains("theName");
        await Assert.That(attachments.HasPendingAttachments).IsTrue();
    }

    public class OutgoingAttachmentsSyncHandler :
        IHandleMessages<AMessage>
    {
        public Task Handle(AMessage message, HandlerContext context)
        {
            var options = new SendOptions();
            var attachments = options.Attachments();
            attachments.AddStream("theName",
                stream =>
                {
                    var writer = new StreamWriter(stream, leaveOpen: true);
                    writer.Write("content");
                    writer.Flush();
                });
            return context.Send(new AMessage(), options);
        }
    }

    [Test]
    public async Task IncomingAttachment()
    {
        var context = new RecordingHandlerContext();
        var handler = new IncomingAttachmentHandler();
        var mockMessageAttachments = new CustomMockMessageAttachments();
        context.InjectAttachmentsInstance(mockMessageAttachments);
        await handler.Handle(new(), context);
        await Assert.That(mockMessageAttachments.GetBytesWasCalled).IsTrue();
    }

    public class CustomMockMessageAttachments :
        MockMessageAttachments
    {
        public override Task<AttachmentBytes> GetBytes(Cancel cancel = default)
        {
            GetBytesWasCalled = true;
            return Task.FromResult(new AttachmentBytes("default", [5]));
        }

        public bool GetBytesWasCalled { get; private set; }
    }

    public class IncomingAttachmentHandler :
        IHandleMessages<AMessage>
    {
        public async Task Handle(AMessage message, HandlerContext context)
        {
            var attachment = context.Attachments();
            var bytes = await attachment.GetBytes(context.CancellationToken);
            Trace.WriteLine(bytes);
        }
    }

    public class AMessage;
}
