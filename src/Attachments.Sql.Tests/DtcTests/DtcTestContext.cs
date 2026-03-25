class DtcTestContext : IAsyncDisposable
{
    public ManualResetEvent HandlerEvent = new(false);
    public ManualResetEvent SagaEvent = new(false);

    public SqlDatabase? NsbDatabase;
    public string? NsbConnectionString;

    public SqlDatabase? BusinessDatabase;
    public string? BusinessConnectionString;
    public string? BusinessDatabaseName;

    public bool HandlerAdoReadSucceeded;
    public bool HandlerAdoWriteSucceeded;
    public bool HandlerEfReadSucceeded;
    public bool HandlerEfWriteSucceeded;
    public bool HandlerAttachmentReadSucceeded;

    public bool SagaAdoReadSucceeded;
    public bool SagaAdoWriteSucceeded;
    public bool SagaEfReadSucceeded;
    public bool SagaEfWriteSucceeded;
    public bool SagaAttachmentReadSucceeded;

    public Exception? HandlerError;
    public Exception? SagaError;

    public async ValueTask DisposeAsync()
    {
        if (NsbDatabase != null)
        {
            await NsbDatabase.DisposeAsync();
        }

        if (BusinessDatabase != null)
        {
            await BusinessDatabase.DisposeAsync();
        }

        HandlerEvent.Dispose();
        SagaEvent.Dispose();
    }
}
