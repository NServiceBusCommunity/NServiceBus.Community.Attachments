public class DbSetup
{
    static readonly object lockObj = new();
    static bool init;

    public static void Setup()
    {
        lock (lockObj)
        {
            if (init)
            {
                return;
            }

            init = true;
            if (!Connection.IsUsingEnvironmentVariable)
            {
                SqlHelper.EnsureDatabaseExists(Connection.ConnectionString);
            }

            using var connection = Connection.OpenConnection();
            Installer.CreateTable(connection, "MessageAttachments").Wait();
        }
    }
}