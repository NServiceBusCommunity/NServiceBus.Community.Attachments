class ReplyHandler(IntegrationTestContext context) :
    IHandleMessages<ReplyMessage>
{
    public async Task Handle(ReplyMessage message, HandlerContext handlerContext)
    {
        var incomingAttachment = handlerContext.Attachments();

        context.PerformNestedConnection();

        var buffer = await incomingAttachment.GetBytes(handlerContext.CancellationToken);
        Debug.WriteLine(buffer);
        await using var stream = await incomingAttachment.GetStream(handlerContext.CancellationToken);
        Debug.WriteLine(stream);
        var attachmentInfos = await incomingAttachment.GetMetadata(handlerContext.CancellationToken).ToAsyncList();
        await Assert.That(attachmentInfos).HasSingleItem();
        context.HandlerEvent.Set();
    }
}
