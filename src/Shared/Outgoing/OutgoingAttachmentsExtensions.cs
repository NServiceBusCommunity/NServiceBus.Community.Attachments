namespace NServiceBus.Attachments
#if FileShare
.FileShare
#endif
#if Sql
.Sql
#endif
#if Raw
.Raw
#endif
;

/// <summary>
/// Extensions for <see cref="IOutgoingAttachments"/>.
/// </summary>
public static class OutgoingAttachmentsExtensions
{
    /// <summary>
    /// Add a file to the <paramref name="attachments"/>.
    /// </summary>
    public static void AddFile(
        this IOutgoingAttachments attachments,
        string file,
        string? name = default)
    {
        Guard.FileExists(file);
        Guard.AgainstEmpty(name);

        attachments.AddStream(
            name ?? "default",
            async stream =>
            {
                await using var fileStream = File.OpenRead(file);
                await fileStream.CopyToAsync(stream);
            });
    }

    /// <summary>
    /// Add an outgoing attachment whose data is produced by transforming the incoming attachment of the same name
    /// for the current message. See <see cref="IOutgoingAttachments.AddFromIncoming"/>.
    /// </summary>
    public static void AddFromIncoming(
        this IOutgoingAttachments attachments,
        string name,
        Func<Stream, Stream, Cancel, Task> transform,
        bool bufferSource = false,
        bool bufferSink = false,
        GetTimeToKeep? timeToKeep = null,
        Action? cleanup = null,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        attachments.AddFromIncoming(name, name, transform, bufferSource, bufferSink, timeToKeep, cleanup, metadata);
}
