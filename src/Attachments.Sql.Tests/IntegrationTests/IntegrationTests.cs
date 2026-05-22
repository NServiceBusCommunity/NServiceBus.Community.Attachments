using NServiceBus.Persistence.Sql;
using TUnit.Core.Interfaces;

public class ParallelLimit4 : IParallelLimit
{
    public int Limit => 4;
}

[ParallelLimiter<ParallelLimit4>]
public class IntegrationTests
{
    [Test]
    [Explicit]
    public Task AdHoc() =>
        RunSql(
            useSqlTransport: false,
            useSqlTransportConnection: false,
            useSqlPersistence: false,
            useStorageSession: false,
            transactionMode: TransportTransactionMode.SendsAtomicWithReceive,
            runEarlyCleanup: true);

    [Test]
    [MethodDataSource(typeof(TestDataGenerator), nameof(TestDataGenerator.GetTestData))]
    public async Task RunSql(
        bool useSqlTransport,
        bool useSqlTransportConnection,
        bool useSqlPersistence,
        bool useStorageSession,
        TransportTransactionMode transactionMode,
        bool runEarlyCleanup)
    {
        await using var context = new IntegrationTestContext();
        // so a nested connection will cause DTC
        context.ShouldPerformNestedConnection = !(useSqlPersistence &&
            transactionMode == TransportTransactionMode.TransactionScope);

        var dbName = $"Int_{useSqlTransport}_{useSqlTransportConnection}_{useSqlPersistence}_{useStorageSession}_{transactionMode}_{runEarlyCleanup}";
        context.Database = await Connection.SqlInstance.Build(dbName);
        context.ConnectionString = context.Database.ConnectionString;
        var connectionString = context.ConnectionString;

        var endpointName = "SqlIntegrationTests";
        var configuration = new EndpointConfiguration(endpointName);
        SqlConnection NewConnection() => new(connectionString);
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        var attachments = configuration.EnableAttachments(NewConnection, TimeToKeep.Default, database: databaseName, table: "Attachments");
        configuration.UseSerialization<SystemJsonSerializer>();
        if (useStorageSession)
        {
            attachments.UseSynchronizedStorageSessionConnectivity();
        }

        if (!runEarlyCleanup)
        {
            attachments.DisableEarlyCleanup();
        }

        if (useSqlPersistence)
        {
            var persistence = configuration.UsePersistence<SqlPersistence>();

            SqlConnection ConnectionBuilder() =>
                new(connectionString);

            await RunSqlScripts(endpointName, ConnectionBuilder);
            persistence.SqlDialect<SqlDialect.MsSqlServer>();
            persistence.DisableInstaller();
            persistence.ConnectionBuilder(ConnectionBuilder);
            var subscriptions = persistence.SubscriptionSettings();
            subscriptions.CacheFor(TimeSpan.FromMinutes(1));
        }
        else
        {
            configuration.UsePersistence<LearningPersistence>();
        }

        configuration.DisableRetries();
        configuration.EnableInstallers();
        configuration.PurgeOnStartup(true);
        attachments.DisableCleanupTask();

        if (useSqlTransportConnection)
        {
            attachments.UseTransportConnectivity();
        }

        if (useSqlTransport)
        {
            var transport = configuration.UseTransport<SqlServerTransport>();
            transport.ConnectionString(connectionString);
            transport.Transactions(transactionMode);
        }
        else
        {
            var transport = configuration.UseTransport<LearningTransport>();
            transport.StorageDirectory($"Int_{useSqlTransportConnection}_{useSqlPersistence}_{useStorageSession}_{transactionMode}_{runEarlyCleanup}");
            transport.Transactions(transactionMode);
        }

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(context);
        builder.Services.AddNServiceBusEndpoint(configuration);
        using var host = builder.Build();
        await host.StartAsync();
        var session = host.Services.GetRequiredService<IMessageSession>();
        var startMessageId = await SendStartMessage(session);

        var timeout = TimeSpan.FromSeconds(20);
        if (!context.HandlerEvent.WaitOne(timeout))
        {
            throw new("TimedOut");
        }

        if (!context.SagaEvent.WaitOne(timeout))
        {
            throw new("TimedOut");
        }

        if (useSqlTransportConnection &&
            useSqlTransport &&
            transactionMode != TransportTransactionMode.None &&
            runEarlyCleanup)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            var persister = new Persister(databaseName, table: "Attachments");
            await foreach (var _ in persister.ReadAllMessageInfo(connection, null, startMessageId))
            {
                throw new("Expected attachments to be cleaned");
            }
        }

        await host.StopAsync();
    }

    static Task RunSqlScripts(string endpointName, Func<SqlConnection> connectionBuilder)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var scriptDir = Path.Combine(baseDir, "NServiceBus.Persistence.Sql", "MsSqlServer");

#pragma warning disable CS0618
        return ScriptRunner.Install(
#pragma warning restore CS0618
            sqlDialect: new SqlDialect.MsSqlServer(),
            tablePrefix: endpointName + "_",
            connectionBuilder: connectionBuilder,
            scriptDirectory: scriptDir,
            shouldInstallOutbox: false,
            shouldInstallSagas: true,
            shouldInstallSubscriptions: false,
            cancellationToken: default);
    }

    static async Task<string> SendStartMessage(IMessageSession session)
    {
        var sendOptions = new SendOptions();
        sendOptions.RouteToThisEndpoint();
        var messageId = Guid.NewGuid().ToString();
        sendOptions.SetMessageId(messageId);
        var attachment = sendOptions.Attachments();
        attachment.AddStream(WriteContent);
        attachment.AddStream("second", WriteContent);
        attachment.AddStream("dir/inDir", WriteContent);
        attachment.AddStream(
            "withMetadata",
            WriteContent,
            metadata: new Dictionary<string, string>
            {
                {
                    "key", "value"
                }
            });
        attachment.Add(async appendAttachment =>
        {
            await appendAttachment("viaAttachmentFactory1", GetStream());
            await appendAttachment("viaAttachmentFactory2", GetStream());
        });
        await session.Send(new SendMessage(), sendOptions);
        return messageId;
    }

    static async Task WriteContent(Stream stream)
    {
        await using var writer = new StreamWriter(stream, leaveOpen: true);
        await writer.WriteAsync("content");
    }

    static Stream GetStream()
    {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write("content");
        writer.Flush();
        stream.Position = 0;
        return stream;
    }
}
