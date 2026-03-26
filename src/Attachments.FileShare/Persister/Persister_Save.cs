namespace NServiceBus.Attachments.FileShare
#if Raw
    .Raw
#endif
    ;

public partial class Persister
{
    /// <inheritdoc />
    public virtual Task SaveStream(string messageId, string name, DateTime expiry, Stream stream, IReadOnlyDictionary<string, string>? metadata = null, Cancel cancel = default)
    {
        Guard.AgainstNullOrEmpty(messageId);
        Guard.AgainstNullOrEmpty(name);
        stream.MoveToStart();
        return Save(messageId, name, expiry, metadata, (fileStream, cancel) => stream.CopyToAsync(fileStream, 4096, cancel), cancel);
    }

    /// <inheritdoc />
    public virtual Task SaveString(string messageId, string name, DateTime expiry, string value, Encoding? encoding = null, IReadOnlyDictionary<string, string>? metadata = null, Cancel cancel = default)
    {
        Guard.AgainstNullOrEmpty(messageId);
        Guard.AgainstNullOrEmpty(name);
        encoding = encoding.Default();
        var dictionary = MetadataSerializer.AppendEncoding(encoding, metadata);
        return Save(
            messageId,
            name,
            expiry,
            dictionary,
            async (fileStream, _)  =>
            {
                await using var writer = fileStream.BuildLeaveOpenWriter(encoding);
                await writer.WriteAsync(value);
            },
            cancel);
    }

    async Task Save(
        string messageId,
        string? name,
        DateTime expiry,
        IReadOnlyDictionary<string, string>? metadata,
        Func<FileStream, Cancel, Task> action,
        Cancel cancel = default)
    {
        name ??= "default";

        var attachmentDirectory = GetAttachmentDirectory(messageId, name);
        ThrowIfDirectoryExists(attachmentDirectory, messageId, name);

        Directory.CreateDirectory(attachmentDirectory);
        var dataFile = Path.Combine(attachmentDirectory, "data");
        expiry = expiry.ToUniversalTime();
        var expiryFile = Path.Combine(attachmentDirectory, $"{expiry:yyyy-MM-ddTHHmm}.expiry");
        await using (File.Create(expiryFile))
        {
        }

        await WriteMetadata(attachmentDirectory, metadata, cancel);

        await using var fileStream = FileHelpers.OpenWrite(dataFile);
        await action(fileStream, cancel);
    }
}