class DtcReplyHandler(DtcTestContext context) :
    IHandleMessages<DtcReplyMessage>
{
    public async Task Handle(DtcReplyMessage message, HandlerContext handlerContext)
    {
        var incomingAttachment = handlerContext.Attachments();
        var bytes = await incomingAttachment.GetBytes(handlerContext.CancellationToken);
        Debug.WriteLine(bytes);
        context.HandlerEvent.Set();
    }
}
