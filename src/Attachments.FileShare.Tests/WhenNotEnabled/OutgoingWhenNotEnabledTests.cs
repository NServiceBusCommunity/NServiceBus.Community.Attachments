using System.Threading.Tasks;

public class OutgoingWhenNotEnabledTests
{
    [Test]
    public async Task Run()
    {
        var configuration = new EndpointConfiguration("FileShareOutgoingWhenNotEnabledTests");
        configuration.UsePersistence<LearningPersistence>();
        configuration.UseTransport<LearningTransport>();
        configuration.UseSerialization<SystemJsonSerializer>();
        configuration.AssemblyScanner().ExcludeAssemblies("xunit.runner.utility.netcoreapp10.dll");
        var endpoint = await Endpoint.Start(configuration);

        var exception = await await Assert.ThrowsAsync<Exception>(() => SendStartMessageWithAttachment(endpoint));
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
        streamWriter.Write("content");
        streamWriter.Flush();
        stream.Position = 0;
        return stream;
    }

    class SendMessage :
        IMessage;
}