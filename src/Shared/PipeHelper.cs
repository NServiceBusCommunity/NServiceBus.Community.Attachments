using System.IO.Pipelines;

static class PipeHelper
{
    public static async Task WriteToPipe(Func<Stream, Task> writer, Pipe pipe)
    {
        Exception? error = null;
        try
        {
            await writer(pipe.Writer.AsStream());
        }
        catch (Exception ex)
        {
            error = ex;
            throw;
        }
        finally
        {
            await pipe.Writer.CompleteAsync(error);
        }
    }
}
