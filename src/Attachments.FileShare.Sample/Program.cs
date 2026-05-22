using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus.Attachments.FileShare;

var configuration = new EndpointConfiguration("Attachments.FileShare.Sample");
configuration.EnableInstallers();
configuration.UsePersistence<LearningPersistence>();
configuration.UseTransport<LearningTransport>();
configuration.AuditProcessedMessagesTo("audit");
configuration.EnableAttachments("Attachments", TimeToKeep.Default);
configuration.UseSerialization<SystemJsonSerializer>();
var builder = Host.CreateApplicationBuilder();
builder.Services.AddNServiceBusEndpoint(configuration);
using var host = builder.Build();
await host.StartAsync();
var session = host.Services.GetRequiredService<IMessageSession>();
await SendMessage(session);
Console.WriteLine("Press any key to stop program");
Console.ReadKey();
await host.StopAsync();

static Task SendMessage(IMessageSession session)
{
    var sendOptions = new SendOptions();
    sendOptions.RouteToThisEndpoint();
    var attachments = sendOptions.Attachments();
    attachments.AddString(name: "foo", value: "content");
    return session.Send(new MyMessage(), sendOptions);
}
