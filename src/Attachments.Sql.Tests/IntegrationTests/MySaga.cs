class MySaga(IntegrationTests tests) :
    Saga<MySaga.SagaData>,
    IAmStartedByMessages<SendMessage>
{
    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<SagaData> mapper) =>
        mapper.MapSaga(saga => saga.MyId)
            .ToMessage<SendMessage>(msg => msg.MyId);

    public async Task Handle(SendMessage message, HandlerContext context)
    {
        var incomingAttachment = context.Attachments();
        await using var stream = await incomingAttachment.GetStream(context.CancellationToken);
        tests.SagaEvent.Set();
    }

    public class SagaData :
        ContainSagaData
    {
        public Guid MyId { get; set; }
    }
}