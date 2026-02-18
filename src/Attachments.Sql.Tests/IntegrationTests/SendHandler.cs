class SendHandler(IntegrationTests tests) :
    IHandleMessages<SendMessage>
{
    public async Task Handle(SendMessage message, HandlerContext context)
    {
        var replyOptions = new SendOptions();
        replyOptions.RouteToThisEndpoint();
        var incomingAttachments = context.Attachments();
        var attachment = await incomingAttachments.GetBytes("withMetadata", context.CancellationToken);
        await Assert.That(attachment).IsNotNull();
        await Assert.That(attachment.Metadata["key"]).IsEqualTo("value");

        using var directory = new TempDirectory();
        await incomingAttachments.CopyToDirectory(directory, cancel: context.CancellationToken);

        var outgoingAttachment = replyOptions.Attachments();
        outgoingAttachment.AddBytes(attachment);

        var attachmentInfos = await incomingAttachments.GetMetadata(context.CancellationToken).ToAsyncList();
        await Assert.That(attachmentInfos.Count).IsEqualTo(6);
        tests.PerformNestedConnection();

        await context.Send(new ReplyMessage(), replyOptions);
    }
}