class SendHandler(IntegrationTestContext context) :
    IHandleMessages<SendMessage>
{
    public async Task Handle(SendMessage message, HandlerContext handlerContext)
    {
        var replyOptions = new SendOptions();
        replyOptions.RouteToThisEndpoint();
        var incomingAttachments = handlerContext.Attachments();
        var attachment = await incomingAttachments.GetBytes("withMetadata", handlerContext.CancellationToken);
        await Assert.That(attachment).IsNotNull();
        await Assert.That(attachment.Metadata["key"]).IsEqualTo("value");

        using var directory = new TempDirectory();
        await incomingAttachments.CopyToDirectory(directory, cancel: handlerContext.CancellationToken);

        var outgoingAttachment = replyOptions.Attachments();
        outgoingAttachment.AddBytes(attachment);

        var attachmentInfos = await incomingAttachments.GetMetadata(handlerContext.CancellationToken).ToAsyncList();
        await Assert.That(attachmentInfos.Count).IsEqualTo(6);
        context.PerformNestedConnection();

        await handlerContext.Send(new ReplyMessage(), replyOptions);
    }
}
