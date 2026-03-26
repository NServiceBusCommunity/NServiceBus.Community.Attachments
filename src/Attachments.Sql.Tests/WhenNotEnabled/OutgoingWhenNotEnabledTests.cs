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
        attachment.AddStream(async stream => await GetStream().CopyToAsync(stream));
        return endpoint.Send(new SendMessage(), sendOptions);
    }

    static Stream GetStream()
    {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write("sdflgkndkjfgn");
        writer.Flush();
        stream.Position = 0;
        return stream;
    }

    class SendMessage :
        IMessage;
}