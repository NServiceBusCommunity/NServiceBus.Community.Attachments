public class IncomingWhenNotEnabledTests : IDisposable
{
    public ManualResetEvent ResetEvent = new(false);
    public Exception? Exception;

    [Test]
    public async Task Run()
    {
        var configuration = new EndpointConfiguration("SqlIncomingWhenNotEnabledTests");
        configuration.UsePersistence<LearningPersistence>();
        configuration.UseTransport<LearningTransport>();
        configuration.UseSerialization<SystemJsonSerializer>();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(this);
        builder.Services.AddNServiceBusEndpoint(configuration);
        using var host = builder.Build();
        await host.StartAsync();
        var session = host.Services.GetRequiredService<IMessageSession>();
        await session.SendLocal(new SendMessage());
        ResetEvent.WaitOne();
        await host.StopAsync();

        await Assert.That(Exception).IsNotNull();
        await Verify(Exception!.Message);
    }

    class Handler(IncomingWhenNotEnabledTests incomingWhenNotEnabledTests) :
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
                incomingWhenNotEnabledTests.Exception = e;
            }
            finally
            {
                incomingWhenNotEnabledTests.ResetEvent.Set();
            }

            return Task.CompletedTask;
        }
    }

    class SendMessage :
        IMessage;

    public void Dispose() =>
        ResetEvent.Dispose();
}
