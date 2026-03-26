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
}
