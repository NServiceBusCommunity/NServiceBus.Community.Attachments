using NServiceBus.Attachments.Sql;

namespace NServiceBus;

/// <summary>
/// Contextual extensions to manipulate attachments.
/// </summary>
public static partial class SqlAttachmentsMessageContextExtensions
{
    /// <summary>
    /// Provides an instance of <see cref="IMessageAttachments" /> for reading attachments.
    /// </summary>
    /// <remarks>
    /// Reads always run on a fresh connection from the configured factory rather than the receive
    /// transaction's connection. This lets a handler hold an <c>OpenOutgoingAttachment</c> sink
    /// open while reading incoming attachments without tripping MARS, at the cost of incoming
    /// reads no longer being enlisted in the receive transaction.
    /// </remarks>
    public static IMessageAttachments Attachments(this HandlerContext context)
    {
        var contextBag = context.Extensions;
        // check the context for a IMessageAttachments in case a mocked instance is injected for testing
        if (contextBag.TryGet<IMessageAttachments>(out var attachments))
        {
            return attachments;
        }

        if (!contextBag.TryGet<SqlAttachmentState>(out var state))
        {
            throw new($"Attachments used when not enabled. For example IMessageHandlerContext.{nameof(Attachments)}() was used but Attachments was not enabled via EndpointConfiguration.{nameof(SqlAttachmentsExtensions.EnableAttachments)}().");
        }

        return new MessageAttachmentsFromSqlFactory(state.GetConnection, context.MessageId, state.Persister);
    }
}
