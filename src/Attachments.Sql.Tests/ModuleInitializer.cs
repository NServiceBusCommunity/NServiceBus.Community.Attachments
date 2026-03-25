using System.Transactions;
using NServiceBus.Logging;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        TransactionManager.ImplicitDistributedTransactions = true;
        VerifierSettings.InitializePlugins();
        LogManager.UseFactory(NullLogger.Instance);
    }
}