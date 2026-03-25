using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Persistence.Sql;

public class DtcTests
{
    [Test]
    public async Task WithoutFix_DtcEscalationOccurs()
    {
        // Prove that opening two connections to different databases within a TransactionScope
        // causes DTC promotion or throws if MSDTC is unavailable.
        // Both DBs on the same LocalDB instance so 3-part names work.
        var nsbDb = await Connection.SqlInstance.Build("DtcEscalation_Nsb");
        var businessDb = await Connection.SqlInstance.Build("DtcEscalation_Biz");
        await Connection.CreateBusinessTable(businessDb.ConnectionString);

        try
        {
            bool dtcWasNeeded;
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

            await using var conn1 = new SqlConnection(nsbDb.ConnectionString);
            await conn1.OpenAsync();

            await using (var cmd = conn1.CreateCommand())
            {
                cmd.CommandText = "select count(*) from [dbo].[MessageAttachments]";
                await cmd.ExecuteScalarAsync();
            }

            try
            {
                // Second connection to Business DB — this triggers DTC promotion
                await using var conn2 = new SqlConnection(businessDb.ConnectionString);
                await conn2.OpenAsync();

                await using (var cmd = conn2.CreateCommand())
                {
                    cmd.CommandText = "select count(*) from [dbo].[BusinessEntities]";
                    await cmd.ExecuteScalarAsync();
                }

                // If we get here, DTC is available — verify promotion occurred
                var distributedId = Transaction.Current?.TransactionInformation.DistributedIdentifier;
                dtcWasNeeded = distributedId is not null && distributedId.Value != Guid.Empty;
                scope.Complete();
            }
            catch (Exception ex) when (
                ex is TransactionAbortedException or
                       TransactionManagerCommunicationException or
                       PlatformNotSupportedException)
            {
                // DTC is not available — the exception itself proves DTC was needed
                dtcWasNeeded = true;
            }

            await Assert.That(dtcWasNeeded).IsTrue()
                .Because("Two connections to different databases within a TransactionScope requires DTC");
        }
        finally
        {
            await nsbDb.DisposeAsync();
            await businessDb.DisposeAsync();
        }
    }

    [Test]
    public async Task WithFix_NoDtcEscalation_SingleConnectionWith3PartNames()
    {
        // Prove that using a single connection with 3-part names avoids DTC promotion.
        // Both DBs on the same LocalDB instance.
        var nsbDb = await Connection.SqlInstance.Build("DtcNoDtc_Nsb");
        var businessDb = await Connection.SqlInstance.Build("DtcNoDtc_Biz");
        await Connection.CreateBusinessTable(businessDb.ConnectionString);

        try
        {
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

            // Single connection to NSB DB
            await using var conn = new SqlConnection(nsbDb.ConnectionString);
            await conn.OpenAsync();

            // Query NSB DB directly
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "select count(*) from [dbo].[MessageAttachments]";
                await cmd.ExecuteScalarAsync();
            }

            // Query Business DB via 3-part name — same connection, no DTC
            var bizDbName = new SqlConnectionStringBuilder(businessDb.ConnectionString).InitialCatalog;
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"select count(*) from [{bizDbName}].[dbo].[BusinessEntities]";
                await cmd.ExecuteScalarAsync();
            }

            // Verify NO DTC promotion
            var distributedId = Transaction.Current?.TransactionInformation.DistributedIdentifier;
            var isNotPromoted = distributedId is null || distributedId.Value == Guid.Empty;
            await Assert.That(isNotPromoted).IsTrue()
                .Because("Using a single connection with 3-part names should not promote to DTC");

            scope.Complete();
        }
        finally
        {
            await nsbDb.DisposeAsync();
            await businessDb.DisposeAsync();
        }
    }

    [Test]
    public async Task WithFix_EndpointTest_NoDtcWithHandlerAndSaga()
    {
        await using var context = new DtcTestContext();

        context.NsbDatabase = await Connection.SqlInstance.Build("DtcEndpoint_Nsb");
        context.NsbConnectionString = context.NsbDatabase.ConnectionString;
        context.BusinessDatabase = await Connection.SqlInstance.Build("DtcEndpoint_Biz");
        context.BusinessConnectionString = context.BusinessDatabase.ConnectionString;
        await Connection.CreateBusinessTable(context.BusinessConnectionString);
        context.BusinessDatabaseName = new SqlConnectionStringBuilder(context.BusinessConnectionString).InitialCatalog;

        var connectionString = context.NsbConnectionString;

        // Create synonym in NSB DB for EF Core cross-database access
        await using (var conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"create synonym [dbo].[BusinessEntities] for [{context.BusinessDatabaseName}].[dbo].[BusinessEntities]";
            await cmd.ExecuteNonQueryAsync();
        }

        var endpointName = "DtcIntegrationTests";
        var configuration = new EndpointConfiguration(endpointName);

        SqlConnection NewConnection() => new(connectionString);

        // Attachments in the NSB DB, using synchronized storage session connectivity
        var nsbDatabaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        var attachments = configuration.EnableAttachments(NewConnection, TimeToKeep.Default, database: nsbDatabaseName, table: "Attachments");
        attachments.UseSynchronizedStorageSessionConnectivity();
        attachments.DisableCleanupTask();

        configuration.UseSerialization<SystemJsonSerializer>();
        configuration.RegisterComponents(_ => _.AddSingleton(context));

        // SQL Persistence on NSB DB
        var persistence = configuration.UsePersistence<SqlPersistence>();
        persistence.SqlDialect<SqlDialect.MsSqlServer>();
        persistence.DisableInstaller();
        persistence.ConnectionBuilder(NewConnection);
        var subscriptions = persistence.SubscriptionSettings();
        subscriptions.CacheFor(TimeSpan.FromMinutes(1));

        await RunSqlScripts(endpointName, NewConnection);

        configuration.DisableRetries();
        configuration.EnableInstallers();
        configuration.PurgeOnStartup(true);

        // SQL Transport on NSB DB with TransactionScope mode
        var transport = configuration.UseTransport<SqlServerTransport>();
        transport.ConnectionString(connectionString);
        transport.Transactions(TransportTransactionMode.TransactionScope);

        configuration.Recoverability().Failed(f => f.OnMessageSentToErrorQueue((msg, _) =>
        {
            context.HandlerError ??= msg.Exception;
            context.HandlerEvent.Set();
            return Task.CompletedTask;
        }));

        var endpoint = await Endpoint.Start(configuration);
        await SendStartMessage(endpoint);

        var timeout = TimeSpan.FromSeconds(30);
        if (!context.HandlerEvent.WaitOne(timeout))
        {
            if (context.HandlerError is not null)
            {
                throw new("Handler failed", context.HandlerError);
            }

            throw new("Handler timed out");
        }

        if (context.HandlerError is not null)
        {
            throw new("Handler failed", context.HandlerError);
        }

        if (!context.SagaEvent.WaitOne(timeout))
        {
            if (context.SagaError is not null)
            {
                throw new("Saga failed", context.SagaError);
            }

            throw new("Saga timed out");
        }

        await endpoint.Stop();

        // Assert handler operations
        await Assert.That(context.HandlerAdoWriteSucceeded).IsTrue();
        await Assert.That(context.HandlerAdoReadSucceeded).IsTrue();
        await Assert.That(context.HandlerEfWriteSucceeded).IsTrue();
        await Assert.That(context.HandlerEfReadSucceeded).IsTrue();
        await Assert.That(context.HandlerAttachmentReadSucceeded).IsTrue();

        // Assert saga operations
        await Assert.That(context.SagaAdoWriteSucceeded).IsTrue();
        await Assert.That(context.SagaAdoReadSucceeded).IsTrue();
        await Assert.That(context.SagaEfWriteSucceeded).IsTrue();
        await Assert.That(context.SagaEfReadSucceeded).IsTrue();
        await Assert.That(context.SagaAttachmentReadSucceeded).IsTrue();
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

    static Task SendStartMessage(IEndpointInstance endpoint)
    {
        var sendOptions = new SendOptions();
        sendOptions.RouteToThisEndpoint();
        var attachment = sendOptions.Attachments();
        attachment.AddStreamWriter(async stream => await GetStream().CopyToAsync(stream));
        attachment.AddStreamWriter(
            "withMetadata",
            async stream => await GetStream().CopyToAsync(stream),
            metadata: new Dictionary<string, string>
            {
                {
                    "key", "value"
                }
            });
        return endpoint.Send(new DtcSendMessage(), sendOptions);
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
