public static class TestDataGenerator
{
    public static IEnumerable<Func<(bool useSqlTransport, bool useSqlTransportConnection, bool useSqlPersistence, bool useStorageSession, TransportTransactionMode mode, bool runEarlyCleanup)>> GetTestData()
    {
        List<TransportTransactionMode> transactionModes =
        [
            TransportTransactionMode.None,
            TransportTransactionMode.ReceiveOnly,
            TransportTransactionMode.SendsAtomicWithReceive,
            TransportTransactionMode.TransactionScope
        ];

        List<bool> boolValues = [true, false];

        foreach (var useSqlPersistence in boolValues)
        {
            foreach (var useSqlTransportConnection in boolValues)
            {
                foreach (var useStorageSession in boolValues)
                {
                    foreach (var useSqlTransport in boolValues)
                    {
                        foreach (var mode in transactionModes)
                        {
                            foreach (var runEarlyCleanup in boolValues)
                            {
                                if (!useSqlTransport && mode != TransportTransactionMode.SendsAtomicWithReceive)
                                {
                                    continue;
                                }

                                var capturedUseSqlTransport = useSqlTransport;
                                var capturedUseSqlTransportConnection = useSqlTransportConnection;
                                var capturedUseSqlPersistence = useSqlPersistence;
                                var capturedUseStorageSession = useStorageSession;
                                var capturedMode = mode;
                                var capturedRunEarlyCleanup = runEarlyCleanup;

                                yield return () => (capturedUseSqlTransport, capturedUseSqlTransportConnection, capturedUseSqlPersistence, capturedUseStorageSession, capturedMode, capturedRunEarlyCleanup);
                            }
                        }
                    }
                }
            }
        }
    }
}
