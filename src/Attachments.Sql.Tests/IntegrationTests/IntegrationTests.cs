using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Persistence.Sql;

public class IntegrationTests : IDisposable
{
    internal ManualResetEvent HandlerEvent = new(false);
    internal ManualResetEvent SagaEvent = new(false);
    internal bool shouldPerformNestedConnection;
    internal string connectionString = null!;

    [Test]
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
        // sql persistence connection spans the handler. so a nested connection will cause DTC
        if (useSqlTransport &&
            useSqlPersistence &&
            transactionMode == TransportTransactionMode.TransactionScope)
        {
            // this scenario is not supported. since useStorageSession=false means attachments
            // will open a nested connection rather than use reuse the storage session connection
            //TODO: should detect this a runtime and throw an better exception
            return;
        }

        if (useSqlTransport &&
            !useSqlPersistence &&
            transactionMode == TransportTransactionMode.TransactionScope)
        {
            // this scenario is not supported by netcore
            // will cause a "This platform does not support distributed transactions."
            //TODO: should detect this a runtime and throw a better exception
            return;
        }

        // so a nested connection will cause DTC
#pragma warning disable TUnit0018
        shouldPerformNestedConnection = !(useSqlPersistence &&
            transactionMode == TransportTransactionMode.TransactionScope);
#pragma warning restore TUnit0018

        await using var database = await Connection.SqlInstance.Build();
        connectionString = database.ConnectionString;

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

        configuration.RegisterComponents(
            registration: configureComponents =>
            {
                configureComponents.AddSingleton(this);
            });
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
            transport.Transactions(transactionMode);
        }

        var endpoint = await Endpoint.Start(configuration);
        var startMessageId = await SendStartMessage(endpoint);

        var timeout = TimeSpan.FromSeconds(10);
        if (!HandlerEvent.WaitOne(timeout))
        {
            throw new("TimedOut");
        }

        if (!SagaEvent.WaitOne(timeout))
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

    internal void PerformNestedConnection()
    {
        if (shouldPerformNestedConnection)
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
        }
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

    public void Dispose()
    {
        HandlerEvent.Dispose();
        SagaEvent.Dispose();
    }
}
