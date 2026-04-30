using NServiceBus.Attachments.FileShare;
using NServiceBus.Extensibility;

namespace NServiceBus;

public static partial class FileShareAttachmentsMessageContextExtensions
{
    /// <summary>
    /// Open a writable <see cref="Stream"/> for an outgoing attachment that is persisted immediately
    /// (during handler execution) rather than via the deferred outgoing pipeline. The handler writes
    /// to the returned stream and disposes it; on dispose the data is committed to the file share
    /// and the attachment is registered on <paramref name="options"/> so the outgoing message carries
    /// it in the Attachments header. Use when the handler needs the converter's output values
    /// (for example a "truncated" flag or encoding metadata) before composing the outgoing message —
    /// a case <c>AddFromIncoming</c> cannot serve.
    /// </summary>
    public static Task<Stream> OpenOutgoingAttachment(
        this HandlerContext context,
        SendOptions options,
        string name,
        GetTimeToKeep? timeToKeep = null,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        OpenForOptions(context, options, name, timeToKeep, metadata);

    /// <summary>
    /// See <see cref="OpenOutgoingAttachment(HandlerContext,SendOptions,string,GetTimeToKeep?,IReadOnlyDictionary{string,string}?)"/>.
    /// </summary>
    public static Task<Stream> OpenOutgoingAttachment(
        this HandlerContext context,
        ReplyOptions options,
        string name,
        GetTimeToKeep? timeToKeep = null,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        OpenForOptions(context, options, name, timeToKeep, metadata);

    /// <summary>
    /// See <see cref="OpenOutgoingAttachment(HandlerContext,SendOptions,string,GetTimeToKeep?,IReadOnlyDictionary{string,string}?)"/>.
    /// </summary>
    public static Task<Stream> OpenOutgoingAttachment(
        this HandlerContext context,
        PublishOptions options,
        string name,
        GetTimeToKeep? timeToKeep = null,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        OpenForOptions(context, options, name, timeToKeep, metadata);

    /// <summary>
    /// See <see cref="OpenOutgoingAttachment(HandlerContext,SendOptions,string,GetTimeToKeep?,IReadOnlyDictionary{string,string}?)"/>.
    /// The attachment is registered with the default name.
    /// </summary>
    public static Task<Stream> OpenOutgoingAttachment(
        this HandlerContext context,
        SendOptions options,
        GetTimeToKeep? timeToKeep = null,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        OpenForOptions(context, options, "default", timeToKeep, metadata);

    /// <summary>
    /// See <see cref="OpenOutgoingAttachment(HandlerContext,SendOptions,string,GetTimeToKeep?,IReadOnlyDictionary{string,string}?)"/>.
    /// The attachment is registered with the default name.
    /// </summary>
    public static Task<Stream> OpenOutgoingAttachment(
        this HandlerContext context,
        ReplyOptions options,
        GetTimeToKeep? timeToKeep = null,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        OpenForOptions(context, options, "default", timeToKeep, metadata);

    /// <summary>
    /// See <see cref="OpenOutgoingAttachment(HandlerContext,SendOptions,string,GetTimeToKeep?,IReadOnlyDictionary{string,string}?)"/>.
    /// The attachment is registered with the default name.
    /// </summary>
    public static Task<Stream> OpenOutgoingAttachment(
        this HandlerContext context,
        PublishOptions options,
        GetTimeToKeep? timeToKeep = null,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        OpenForOptions(context, options, "default", timeToKeep, metadata);

    static async Task<Stream> OpenForOptions(
        HandlerContext context,
        ExtendableOptions options,
        string name,
        GetTimeToKeep? timeToKeep,
        IReadOnlyDictionary<string, string>? metadata)
    {
        Guard.AgainstNullOrEmpty(name);

        if (!context.Extensions.TryGet<FileShareAttachmentState>(out var state))
        {
            throw new("OpenOutgoingAttachment used when attachments not enabled. Call EndpointConfiguration.EnableAttachments() first.");
        }

        var cancel = context.CancellationToken;
        var messageId = options.GetMessageId() ?? Guid.NewGuid().ToString();
        options.SetMessageId(messageId);

        var keep = (timeToKeep ?? TimeToKeep.Default)(null);
        var expiry = DateTime.UtcNow.Add(keep);

        var fileStream = await state.Persister.OpenSaveStream(messageId, name, expiry, metadata, cancel);
        var outgoing = (OutgoingAttachments) GetOutgoingAttachments(options);

        return new ImmediateAttachmentStream(
            inner: fileStream,
            commitAsync: () => new((Guid?) null),
            onCommitted: _ => outgoing.AddPreSaved(name, savedGuid: null, metadata));
    }

    static IOutgoingAttachments GetOutgoingAttachments(ExtendableOptions options)
    {
        var contextBag = options.GetExtensions();
        if (contextBag.TryGet<IOutgoingAttachments>(out var attachments))
        {
            return attachments;
        }

        attachments = new OutgoingAttachments();
        contextBag.Set(attachments);
        return attachments;
    }
}
