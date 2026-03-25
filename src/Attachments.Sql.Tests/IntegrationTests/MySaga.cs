class MySaga(IntegrationTestContext context) :
    Saga<MySaga.SagaData>,
    IAmStartedByMessages<SendMessage>
{
    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<SagaData> mapper) =>
        mapper.MapSaga(saga => saga.MyId)
            .ToMessage<SendMessage>(msg => msg.MyId);

    public async Task Handle(SendMessage message, HandlerContext handlerContext)
    {
        var incomingAttachment = handlerContext.Attachments();
        await using var stream = await incomingAttachment.GetStream(handlerContext.CancellationToken);
        context.SagaEvent.Set();
    }

    public class SagaData :
        ContainSagaData
    {
        public Guid MyId { get; set; }
    }
}
