public class TestingTests
{
    [Test]
    public async Task OutgoingAttachments()
    {
        var context = new RecordingHandlerContext();
        var handler = new OutgoingAttachmentsHandler();
        await handler.Handle(new(), context);
        await Verify(context);
    }

    public class OutgoingAttachmentsHandler :
        IHandleMessages<AMessage>
    {
        public Task Handle(AMessage message, HandlerContext context)
        {
            var options = new SendOptions();
            var attachments = options.Attachments();
            attachments.AddStream(
                "theName",
                writer: async stream =>
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
        await Verify(context);
    }

    public class OutgoingAttachmentsSyncHandler :
        IHandleMessages<AMessage>
    {
        public Task Handle(AMessage message, HandlerContext context)
        {
            var options = new SendOptions();
            var attachments = options.Attachments();
            attachments.AddStream(
                "theName",
                writer: stream =>
                {
                    var streamWriter = new StreamWriter(stream, leaveOpen: true);
                    streamWriter.Write("content");
                    streamWriter.Flush();
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

    [Test]
    public async Task StubTransferToSaga()
    {
        var sagaData = new ASagaData
        {
            Id = Guid.NewGuid()
        };
        var attachments = new StubMessageAttachments();
        attachments.AddAttachment([5]);
        attachments.AddAttachment("second", [6]);

        await attachments.TransferToSaga(sagaData, "first");
        await attachments.TransferToSaga("second", sagaData, timeToKeep: TimeSpan.FromDays(1));

        await Assert.That(await attachments.GetMetadata().CountAsync()).IsEqualTo(0);
        byte[] first = await attachments.GetBytesForSaga(sagaData, "first");
        await Assert.That((int) first[0]).IsEqualTo(5);
        byte[] second = await attachments.GetBytesForSaga(sagaData, "second");
        await Assert.That((int) second[0]).IsEqualTo(6);
        await Assert.That(await attachments.DeleteForSaga(sagaData)).IsEqualTo(2);
        await Assert.That(await attachments.DeleteForSaga(sagaData)).IsEqualTo(0);
    }

    [Test]
    public async Task StubTransferToSagaReplace()
    {
        var sagaData = new ASagaData
        {
            Id = Guid.NewGuid()
        };
        var attachments = new StubMessageAttachments();
        attachments.AddAttachment("older", [5]);
        attachments.AddAttachment("newer", [6]);
        await attachments.TransferToSaga("older", sagaData, "doc");

        await Assert.ThrowsAsync<Exception>(() => attachments.TransferToSaga("newer", sagaData, "doc"));
        await attachments.TransferToSaga("newer", sagaData, "doc", replace: true);

        byte[] doc = await attachments.GetBytesForSaga(sagaData, "doc");
        await Assert.That((int) doc[0]).IsEqualTo(6);
        await Assert.That(await attachments.DeleteForSaga(sagaData)).IsEqualTo(1);
    }

    [Test]
    public async Task StubProcessByteArrayForMessageDefaultName()
    {
        var attachments = new StubMessageAttachments();
        attachments.AddAttachmentForMessage("theMessageId", [5]);
        byte[]? received = null;

        await attachments.ProcessByteArrayForMessage(
            "theMessageId",
            (bytes, _) =>
            {
                received = bytes;
                return Task.CompletedTask;
            });

        await Assert.That((int) received![0]).IsEqualTo(5);
    }

    [Test]
    public async Task StubProcessStreamForMessageDefaultName()
    {
        var attachments = new StubMessageAttachments();
        attachments.AddAttachmentForMessage("theMessageId", [5]);
        var received = -1;

        await attachments.ProcessStreamForMessage(
            "theMessageId",
            (stream, _) =>
            {
                received = stream.ReadByte();
                return Task.CompletedTask;
            });

        await Assert.That(received).IsEqualTo(5);
    }

    public class ASagaData :
        ContainSagaData;

    public class AMessage;
}
