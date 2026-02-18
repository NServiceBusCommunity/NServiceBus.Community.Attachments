using System.Threading.Tasks;

public class OutgoingWhenNotEnabledTests
{
    static OutgoingWhenNotEnabledTests() =>
        DbSetup.Setup();

    [Test]
    public async Task Run()
    {
        var configuration = new EndpointConfiguration("SqlOutgoingWhenNotEnabledTests");
        configuration.UsePersistence<LearningPersistence>();
        configuration.UseTransport<LearningTransport>();
        configuration.UseSerialization<SystemJsonSerializer>();
        var endpoint = await Endpoint.Start(configuration);

        var exception = await await Assert.ThrowsAsync<Exception>(() => SendStartMessageWithAttachment(endpoint));
        await Assert.That(exception).IsNotNull();
        await Verify(exception.Message);
        await endpoint.Stop();
    }

    static Task SendStartMessageWithAttachment(IEndpointInstance endpoint)
    {
        var sendOptions = new SendOptions();
        sendOptions.RouteToThisEndpoint();
        var attachment = sendOptions.Attachments();
        attachment.Add(GetStream);
        return endpoint.Send(new SendMessage(), sendOptions);
    }

    static Stream GetStream()
    {
        var stream = new MemoryStream();
        var streamWriter = new StreamWriter(stream);
        streamWriter.Write("sdflgkndkjfgn");
        streamWriter.Flush();
        stream.Position = 0;
        return stream;
    }

    class SendMessage :
        IMessage;
}