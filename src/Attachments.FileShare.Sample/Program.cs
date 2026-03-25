using NServiceBus.Attachments.FileShare;

var configuration = new EndpointConfiguration("Attachments.FileShare.Sample");
configuration.EnableInstallers();
configuration.UsePersistence<LearningPersistence>();
configuration.UseTransport<LearningTransport>();
configuration.AuditProcessedMessagesTo("audit");
configuration.EnableAttachments("Attachments", TimeToKeep.Default);
configuration.UseSerialization<SystemJsonSerializer>();
var endpoint = await Endpoint.Start(configuration);
await SendMessage(endpoint);
Console.WriteLine("Press any key to stop program");
Console.ReadKey();
await endpoint.Stop();

static Task SendMessage(IEndpointInstance endpoint)
{
    var sendOptions = new SendOptions();
    sendOptions.RouteToThisEndpoint();
    var attachments = sendOptions.Attachments();
    attachments.AddString(name: "foo", value: "content");
    return endpoint.Send(new MyMessage(), sendOptions);
}