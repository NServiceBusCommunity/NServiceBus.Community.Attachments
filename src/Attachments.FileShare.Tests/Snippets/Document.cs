class Document
{
    byte[] data = [1, 2, 3];

    public Task SaveAsync(Stream stream) =>
        stream.WriteAsync(data).AsTask();
}