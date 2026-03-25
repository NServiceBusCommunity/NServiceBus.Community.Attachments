using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Persistence.Sql;

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
        var attachments = configuration.EnableAttachments(NewConnection, TimeToKeep.Default);
        configuration.UseSerialization<SystemJsonSerializer>();
        if (useStorageSession)
        {
            attachments.UseSynchronizedStorageSessionConnectivity();
        }

        if (!runEarlyCleanup)
        {
            attachments.DisableEarlyCleanup();
        }

        configuration.RegisterComponents(registration: _ => _.AddSingleton(context));
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

        attachments.UseTable("Attachments");
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

        var endpoint = await Endpoint.Start(configuration);
        var startMessageId = await SendStartMessage(endpoint);

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
            var persister = new Persister("Attachments");
            await foreach (var _ in persister.ReadAllMessageInfo(connection, null, startMessageId))
            {
                throw new("Expected attachments to be cleaned");
            }
        }

        await endpoint.Stop();
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

    static async Task<string> SendStartMessage(IEndpointInstance endpoint)
    {
        var sendOptions = new SendOptions();
        sendOptions.RouteToThisEndpoint();
        var messageId = Guid.NewGuid().ToString();
        sendOptions.SetMessageId(messageId);
        var attachment = sendOptions.Attachments();
        attachment.Add(GetStream);
        attachment.Add("second", GetStream);
        attachment.Add("dir/inDir", GetStream);
        attachment.Add(
            "withMetadata",
            GetStream,
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
        await endpoint.Send(new SendMessage(), sendOptions);
        return messageId;
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
}
