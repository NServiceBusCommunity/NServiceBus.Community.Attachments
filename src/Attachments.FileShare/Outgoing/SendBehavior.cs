using System.IO.Pipelines;
using NServiceBus.Attachments.FileShare;
using NServiceBus.Pipeline;

class SendBehavior(IPersister persister, GetTimeToKeep endpointTimeToKeep) :
    Behavior<IOutgoingLogicalMessageContext>
{
    public override async Task Invoke(IOutgoingLogicalMessageContext context, Func<Task> next)
    {
        await ProcessOutgoing(context);
        await next();
    }

    async Task ProcessOutgoing(IOutgoingLogicalMessageContext context)
    {
        var extensions = context.Extensions;
        if (!extensions.TryGet<IOutgoingAttachments>(out var attachments))
        {
            return;
        }

        var outgoingAttachments = (OutgoingAttachments) attachments;
        if (!outgoingAttachments.HasPendingAttachments)
        {
            return;
        }

        var attachmentNames = new List<string>();

        var timeToBeReceived = extensions.GetTimeToBeReceivedFromConstraint();

        foreach (var (name, value) in outgoingAttachments.Inner)
        {
            await ProcessAttachment(timeToBeReceived, context.MessageId, value, name);
            attachmentNames.Add(name);
        }

        foreach (var dynamic in outgoingAttachments.Dynamic)
        {
            await dynamic(async (name, stream, keep, cleanup, metadata) =>
            {
                var capturedStream = stream;
                var outgoing = new Outgoing
                {
                    Cleanup = cleanup,
                    StreamWriter = async target => await capturedStream.CopyToAsync(target),
                    Metadata = metadata,
                    TimeToKeep = keep,
                };
                await ProcessAttachment(timeToBeReceived, context.MessageId, outgoing, name);
                attachmentNames.Add(name);
            });
        }

        if (outgoingAttachments.DuplicateIncomingAttachments || outgoingAttachments.Duplicates.Count != 0)
        {
            var incomingMessageId = context.IncomingMessageId();
            if (outgoingAttachments.DuplicateIncomingAttachments)
            {
                var names = await persister.Duplicate(incomingMessageId, context.MessageId, context.CancellationToken);
                attachmentNames.AddRange(names);
            }

            foreach (var duplicate in outgoingAttachments.Duplicates)
            {
                attachmentNames.Add(duplicate.To);
                await persister.Duplicate(incomingMessageId, duplicate.From, context.MessageId, duplicate.To, context.CancellationToken);
            }
        }

        Guard.AgainstDuplicateNames(attachmentNames);

        context.Headers.Add("Attachments", string.Join(", ", attachmentNames));
    }

    async Task ProcessStreamWriter(string messageId, string name, DateTime expiry, Func<Stream, Task> streamWriter, IReadOnlyDictionary<string, string>? metadata, Cancel cancel)
    {
        var pipe = new Pipe();
        var writerTask = Task.Run(async () =>
        {
            try
            {
                await streamWriter(pipe.Writer.AsStream());
            }
            finally
            {
                await pipe.Writer.CompleteAsync();
            }
        });

        var readerStream = pipe.Reader.AsStream();
        await using (readerStream)
        {
            await persister.SaveStream(messageId, name, expiry, readerStream, metadata, cancel);
            await writerTask;
        }
    }

    async Task ProcessAttachment(TimeSpan? timeToBeReceived, string messageId, Outgoing outgoing, string name)
    {
        var outgoingStreamTimeToKeep = outgoing.TimeToKeep ?? endpointTimeToKeep;
        var timeToKeep = outgoingStreamTimeToKeep(timeToBeReceived);
        var expiry = DateTime.UtcNow.Add(timeToKeep);
        try
        {
            await Process(messageId, outgoing, name, expiry);
        }
        finally
        {
            outgoing.Cleanup?.Invoke();
        }
    }

    async Task Process(string messageId, Outgoing outgoing, string name, DateTime expiry, Cancel cancel = default)
    {
        if (outgoing.StreamWriter is not null)
        {
            await ProcessStreamWriter(messageId, name, expiry, outgoing.StreamWriter, outgoing.Metadata, cancel);
            return;
        }

        if (outgoing.AsyncBytesFactory is not null)
        {
            var bytes = await outgoing.AsyncBytesFactory();
            await persister.SaveBytes(messageId, name, expiry, bytes, outgoing.Metadata, cancel);
            return;
        }

        if (outgoing.BytesFactory is not null)
        {
            await persister.SaveBytes(messageId, name, expiry, outgoing.BytesFactory(), outgoing.Metadata, cancel);
            return;
        }

        if (outgoing.BytesInstance is not null)
        {
            await persister.SaveBytes(messageId, name, expiry, outgoing.BytesInstance, outgoing.Metadata, cancel);
            return;
        }

        if (outgoing.StringInstance is not null)
        {
            await persister.SaveString(messageId, name, expiry, outgoing.StringInstance, outgoing.Encoding, outgoing.Metadata, cancel);
            return;
        }

        throw new("No matching way to handle outgoing.");
    }
}