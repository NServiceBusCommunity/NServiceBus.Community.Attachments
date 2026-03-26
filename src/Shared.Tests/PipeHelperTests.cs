using System.IO.Pipelines;

public class PipeHelperTests
{
    [Test]
    public async Task WriterException_PropagatedToReader()
    {
        var pipe = new Pipe();
        var writerTask = Task.Run(() => PipeHelper.WriteToPipe(
            _ => throw new InvalidOperationException("writer failed"),
            pipe));

        var readerStream = pipe.Reader.AsStream();
        await using (readerStream)
        {
            var readException = await Assert.ThrowsAsync<InvalidOperationException>(
                () => readerStream.CopyToAsync(Stream.Null));
            await Assert.That(readException!.Message).IsEqualTo("writer failed");
        }
    }
}
