using Microsoft.Data.SqlClient;

class IntegrationTestContext : IAsyncDisposable
{
    public ManualResetEvent HandlerEvent = new(false);
    public ManualResetEvent SagaEvent = new(false);
    public bool ShouldPerformNestedConnection;
    public string? ConnectionString;
    public SqlDatabase? Database;

    public void PerformNestedConnection()
    {
        if (ShouldPerformNestedConnection)
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Database != null)
        {
            await Database.DisposeAsync();
        }

        HandlerEvent.Dispose();
        SagaEvent.Dispose();
    }
}
