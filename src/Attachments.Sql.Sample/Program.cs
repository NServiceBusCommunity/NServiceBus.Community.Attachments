using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus.Attachments.Sql;

class Program
{
    static async Task Main()
    {
        await using var database = await Connection.SqlInstance.Build("sample");
        var connectionString = database.ConnectionString;

        var configuration = new EndpointConfiguration("Attachments.Sql.Sample");
        configuration.EnableInstallers();
        configuration.PurgeOnStartup(true);
        configuration.UsePersistence<LearningPersistence>();
        configuration.UseSerialization<SystemJsonSerializer>();
        var transport = configuration.UseTransport<SqlServerTransport>();
        transport.ConnectionString(connectionString);
        transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
        SqlConnection NewConnection() => new(connectionString);
        var attachments = configuration.EnableAttachments(NewConnection, TimeToKeep.Default);
        attachments.UseTransportConnectivity();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddNServiceBusEndpoint(configuration);
        using var host = builder.Build();
        await host.StartAsync();
        var session = host.Services.GetRequiredService<IMessageSession>();
        await SendMessage(session);
        Console.WriteLine("Press any key to stop program");
        Console.ReadKey();
        await host.StopAsync();
    }

    static Task SendMessage(IMessageSession session)
    {
        var sendOptions = new SendOptions();
        sendOptions.RouteToThisEndpoint();
        var attachments = sendOptions.Attachments();
        attachments.AddString(name: "foo", value: "content");
        return session.Send(new SendMessage(), sendOptions);
    }
}
