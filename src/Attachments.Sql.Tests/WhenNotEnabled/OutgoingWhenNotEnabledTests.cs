public class OutgoingWhenNotEnabledTests
{
    [Test]
    public async Task Run()
    {
        var configuration = new EndpointConfiguration("SqlOutgoingWhenNotEnabledTests");
        configuration.UsePersistence<LearningPersistence>();
        configuration.UseTransport<LearningTransport>();
        configuration.UseSerialization<SystemJsonSerializer>();
        var endpoint = await Endpoint.Start(configuration);

        try
        {
            await SendStartMessageWithAttachment(endpoint);
            throw new InvalidOperationException("Expected exception was not thrown");
        }
        catch (Exception exception)
        {
            await Verify(exception.Message);
        }

        await endpoint.Stop();
    }

    static Task SendStartMessageWithAttachment(IEndpointInstance endpoint)
    {
        var sendOptions = new SendOptions();
        sendOptions.RouteToThisEndpoint();
        var attachment = sendOptions.Attachments();
        attachment.AddStream(WriteContent);
        return endpoint.Send(new SendMessage(), sendOptions);
    }

    static async Task WriteContent(Stream stream)
    {
        await using var writer = new StreamWriter(stream, leaveOpen: true);
        await writer.WriteAsync("sdflgkndkjfgn");
    }

    class SendMessage :
        IMessage;
}