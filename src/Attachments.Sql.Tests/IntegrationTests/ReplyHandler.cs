class ReplyHandler(IntegrationTests tests) :
    IHandleMessages<ReplyMessage>
{
    public async Task Handle(ReplyMessage message, HandlerContext context)
    {
        var incomingAttachment = context.Attachments();

        tests.PerformNestedConnection();

        var buffer = await incomingAttachment.GetBytes(context.CancellationToken);
        Debug.WriteLine(buffer);
        await using var stream = await incomingAttachment.GetStream(context.CancellationToken);
        Debug.WriteLine(stream);
        var attachmentInfos = await incomingAttachment.GetMetadata(context.CancellationToken).ToAsyncList();
        await Assert.That(attachmentInfos).HasSingleItem();
        tests.HandlerEvent.Set();
    }
}