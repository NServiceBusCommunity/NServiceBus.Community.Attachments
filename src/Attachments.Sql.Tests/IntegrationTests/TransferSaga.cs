class TransferSaga(IntegrationTestContext context) :
    Saga<TransferSaga.SagaData>,
    IAmStartedByMessages<StartTransferSaga>,
    IHandleMessages<ContinueTransferSaga>
{
    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<SagaData> mapper) =>
        mapper.MapSaga(saga => saga.MyId)
            .ToMessage<StartTransferSaga>(msg => msg.MyId)
            .ToMessage<ContinueTransferSaga>(msg => msg.MyId);

    public async Task Handle(StartTransferSaga message, HandlerContext handlerContext)
    {
        var cancel = handlerContext.CancellationToken;
        await handlerContext.Attachments().TransferToSaga(Data, "first", cancel: cancel);

        // the continue message is only processed once this one has been committed,
        // at which point early cleanup has run for this message
        var sendOptions = new SendOptions();
        sendOptions.RouteToThisEndpoint();
        var outgoing = sendOptions.Attachments();
        outgoing.AddString("second content");
        outgoing.AddString("replacement", "replacement content");
        await handlerContext.Send(
            new ContinueTransferSaga
            {
                MyId = message.MyId
            },
            sendOptions);
    }

    public async Task Handle(ContinueTransferSaga message, HandlerContext handlerContext)
    {
        var cancel = handlerContext.CancellationToken;
        var attachments = handlerContext.Attachments();
        await attachments.TransferToSaga(Data, "second", cancel: cancel);

        // "first" was transferred by the start message, and so survived that message's early cleanup
        var original = await attachments.GetStringForSaga(Data, "first", cancel: cancel);
        await Assert.That(original.Value).IsEqualTo("first content");
        await attachments.TransferToSaga("replacement", Data, "first", replace: true, cancel: cancel);

        // both were transferred by this handler, so reading them proves the ForSaga reads run on the same
        // connection and transaction as the transfer
        var first = await attachments.GetStringForSaga(Data, "first", cancel: cancel);
        var second = await attachments.GetStringForSaga(Data, "second", cancel: cancel);
        await Assert.That(first.Value).IsEqualTo("replacement content");
        await Assert.That(second.Value).IsEqualTo("second content");

        var deleted = await attachments.DeleteForSaga(Data, cancel);
        await Assert.That(deleted).IsEqualTo(2);

        MarkAsComplete();
        context.TransferSagaEvent.Set();
    }

    public class SagaData :
        ContainSagaData
    {
        public Guid MyId { get; set; }
    }
}

class StartTransferSaga :
    IMessage
{
    public Guid MyId { get; set; } = Guid.NewGuid();
}

class ContinueTransferSaga :
    IMessage
{
    public Guid MyId { get; set; }
}
