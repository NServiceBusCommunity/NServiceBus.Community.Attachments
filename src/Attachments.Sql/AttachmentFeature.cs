using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Attachments.Sql;
using NServiceBus.Features;
using NServiceBus.Logging;

class AttachmentFeature :
    Feature
{
    static ILog log = LogManager.GetLogger("AttachmentFeature");

    protected override void Setup(FeatureConfigurationContext context)
    {
        var settings = context.Settings.Get<AttachmentSettings>();

        var connectionFactory = settings.ConnectionFactory;
        var pipeline = context.Pipeline;
        var persister = new Persister(settings.Database, settings.Schema, settings.TableName);
        pipeline.Register(new ReceiveRegistration(connectionFactory, persister, settings.UseTransport, settings.UseSynchronizedStorage));

        if (settings.RunEarlyCleanup)
        {
            log.Debug("Did not register DeleteBehaviorRegistration since RunEarlyCleanup is not enabled.");
            if (settings.UseTransport)
            {
                pipeline.Register(new DeleteBehaviorRegistration(connectionFactory, persister));
            }
        }

        pipeline.Register(new SendRegistration(connectionFactory, persister, settings.TimeToKeep));
        if (context.Settings.PurgeOnStartup())
        {
            context.RegisterStartupTask(_ => new PurgeTask(persister, settings.ConnectionFactory));
        }

        if (settings.RunCleanTask)
        {
            context.RegisterStartupTask(services => CreateCleaner(settings, persister, services));
        }
    }

    static Cleaner CreateCleaner(AttachmentSettings settings, IPersister persister, IServiceProvider services) =>
        new(
            async token =>
            {
                await using var connection = await settings.ConnectionFactory(token);
                var count = await persister.CleanupItemsOlderThan(connection, null, DateTime.UtcNow, token);
                if (count != 0)
                {
                    log.Debug($"Deleted {count} attachments during cleanup");
                }
            },
            criticalError: services.GetRequiredService<CriticalError>().Raise,
            frequencyToRunCleanup: TimeSpan.FromHours(1),
            timer: new AsyncTimer());
}