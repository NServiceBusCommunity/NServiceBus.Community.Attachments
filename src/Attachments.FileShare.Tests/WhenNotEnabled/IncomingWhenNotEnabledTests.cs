public class IncomingWhenNotEnabledTests : IDisposable
{
    public ManualResetEvent ResetEvent = new(false);
    public Exception? Exception;

    [Test]
    public async Task Run()
    {
        var configuration = new EndpointConfiguration("FileShareIncomingWhenNotEnabledTests");
        configuration.UsePersistence<LearningPersistence>();
        configuration.RegisterComponents(_ => _.AddSingleton(this));
        configuration.UseTransport<LearningTransport>();
        configuration.UseSerialization<SystemJsonSerializer>();
        var endpoint = await Endpoint.Start(configuration);
        await endpoint.SendLocal(new SendMessage());
        ResetEvent.WaitOne();
        await endpoint.Stop();
        await Assert.That(Exception).IsNotNull();
        await Verify(Exception!.Message);
    }

    class Handler(IncomingWhenNotEnabledTests whenNotEnabledTests) :
        IHandleMessages<SendMessage>
    {
        public Task Handle(SendMessage message, HandlerContext context)
        {
            try
            {
                context.Attachments();
            }
            catch (Exception e)
            {
                whenNotEnabledTests.Exception = e;
            }
            finally
            {
                whenNotEnabledTests.ResetEvent.Set();
            }

            return Task.CompletedTask;
        }
    }

    class SendMessage :
        IMessage;

    public void Dispose() =>
        ResetEvent.Dispose();
}