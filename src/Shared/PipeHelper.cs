using System.IO.Pipelines;

static class PipeHelper
{
    /// <summary>
    /// Starts the writer on a background thread and returns the reader stream.
    /// Task.Run ensures the writer runs on a separate thread so the
    /// reader can start immediately, even if the writer does synchronous
    /// work before its first await.
    /// </summary>
    public static (Task writerTask, Stream readerStream) StartWriter(Func<Stream, Task> writer, Cancel cancel = default)
    {
        var pipe = new Pipe();
        var writerTask = Task.Run(() => WriteToPipe(writer, pipe), cancel);
        return (writerTask, pipe.Reader.AsStream());
    }

    static async Task WriteToPipe(Func<Stream, Task> writer, Pipe pipe)
    {
        Exception? error = null;
        try
        {
            await writer(new PositionTrackingStream(pipe.Writer.AsStream()));
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
