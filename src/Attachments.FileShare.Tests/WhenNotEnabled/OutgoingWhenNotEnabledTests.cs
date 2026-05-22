public class OutgoingWhenNotEnabledTests
{
    [Test]
    public async Task Run()
    {
        var configuration = new EndpointConfiguration("FileShareOutgoingWhenNotEnabledTests");
        configuration.UsePersistence<LearningPersistence>();
        configuration.UseTransport<LearningTransport>();
        configuration.UseSerialization<SystemJsonSerializer>();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddNServiceBusEndpoint(configuration);
        using var host = builder.Build();
        await host.StartAsync();
        var session = host.Services.GetRequiredService<IMessageSession>();

        try
        {
            await SendStartMessageWithAttachment(session);
            throw new InvalidOperationException("Expected exception was not thrown");
        }
        catch (Exception exception)
        {
            await Verify(exception.Message);
        }

        await host.StopAsync();
    }

    static Task SendStartMessageWithAttachment(IMessageSession session)
    {
        var sendOptions = new SendOptions();
        sendOptions.RouteToThisEndpoint();
        var attachment = sendOptions.Attachments();
        attachment.AddStream(WriteContent);
        return session.Send(new SendMessage(), sendOptions);
    }

    static async Task WriteContent(Stream stream)
    {
        await using var writer = new StreamWriter(stream, leaveOpen: true);
        await writer.WriteAsync("content");
    }

    class SendMessage :
        IMessage;
}
