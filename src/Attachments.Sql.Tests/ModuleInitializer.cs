using System.Transactions;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        TransactionManager.ImplicitDistributedTransactions = true;
        VerifierSettings.InitializePlugins();
    }
}