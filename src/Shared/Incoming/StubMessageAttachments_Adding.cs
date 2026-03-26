namespace NServiceBus.Attachments
#if FileShare
.FileShare.Testing
#endif
#if Sql
.Sql.Testing
#endif
;

public partial class StubMessageAttachments
{
    Dictionary<string, MockAttachment> currentAttachments = new(StringComparer.OrdinalIgnoreCase);
    Dictionary<string, Dictionary<string, MockAttachment>> attachments = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds a attachment that can then be used in a test.
    /// </summary>
    public void AddAttachment(string payload, Encoding? encoding, IDictionary<string, string>? metadata = null) =>
        AddAttachment("default", payload, encoding, metadata);

    /// <summary>
    /// Adds a attachment that can then be used in a test.
    /// </summary>
    public void AddAttachment(string name, string payload, Encoding? encoding, IDictionary<string, string>? metadata = null) =>
        AddAttachment(name, payload.ToBytes(encoding.Default()), metadata);

    /// <summary>
    /// Adds a attachment that can then be used in a test.
    /// </summary>
    public void AddAttachment(byte[] bytes, IDictionary<string, string>? metadata = null) =>
        AddAttachment("default", bytes, metadata);

    /// <summary>
    /// Adds a attachment that can then be used in a test.
    /// </summary>
    public void AddAttachment(string name, byte[] bytes, IDictionary<string, string>? metadata = null)
    {
        Guard.AgainstNullOrEmpty(name);
        var now = DateTime.UtcNow;
        currentAttachments.Add(name,
            new(
                name: name,
                created: now,
                expiry: now.AddDays(10),
                bytes: bytes,
                metadata: BuildMetadata(metadata)));
    }

    /// <summary>
    /// Adds a attachment that can then be used in a test.
    /// </summary>
    public void AddAttachmentForMessage(string messageId, byte[] bytes, IDictionary<string, string>? metadata = null) =>
        AddAttachmentForMessage(messageId, "default", bytes, metadata);

    /// <summary>
    /// Adds a attachment that can then be used in a test.
    /// </summary>
    public void AddAttachmentForMessage(string messageId, string name, byte[] bytes, IDictionary<string, string>? metadata = null)
    {
        Guard.AgainstNullOrEmpty(name);
        if (!attachments.TryGetValue(messageId, out var attachmentsForMessage))
        {
            attachments[messageId] = attachmentsForMessage = [];
        }

        var now = DateTime.UtcNow;
        attachmentsForMessage.Add(name,
            new(
                name: name,
                created: now,
                expiry: now.AddDays(10),
                bytes: bytes,
                metadata: BuildMetadata(metadata)));
    }

    static IReadOnlyDictionary<string, string> BuildMetadata(IDictionary<string, string>? metadata)
    {
        if (metadata is null)
        {
            return new Dictionary<string, string>();
        }

        return metadata.ToDictionary(_ => _.Key, _ => _.Value);
    }
}
