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

    Task ProcessOutgoing(IOutgoingLogicalMessageContext context)
    {
        var extensions = context.Extensions;
        if (!extensions.TryGet<IOutgoingAttachments>(out var attachments))
        {
            return Task.CompletedTask;
        }

        var outgoingAttachments = (OutgoingAttachments) attachments;
        if (!outgoingAttachments.HasPendingAttachments)
        {
            return Task.CompletedTask;
        }

        return ProcessOutgoingCore(context, outgoingAttachments);
    }

    async Task ProcessOutgoingCore(IOutgoingLogicalMessageContext context, OutgoingAttachments outgoingAttachments)
    {
        var attachmentNames = new List<string>();

        var timeToBeReceived = context.Extensions.GetTimeToBeReceivedFromConstraint();

        string? incomingMessageId = null;
        string GetIncomingMessageId() => incomingMessageId ??= context.IncomingMessageId();

        foreach (var (name, value) in outgoingAttachments.Inner)
        {
            await ProcessAttachment(timeToBeReceived, context.MessageId, GetIncomingMessageId, value, name);
            attachmentNames.Add(name);
        }

        foreach (var dynamic in outgoingAttachments.Dynamic)
        {
            await dynamic(async (name, stream, keep, cleanup, metadata) =>
            {
                var outgoing = new Outgoing
                {
                    Cleanup = cleanup,
                    StreamWriter = stream.CopyToAsync,
                    Metadata = metadata,
                    TimeToKeep = keep,
                };
                await ProcessAttachment(timeToBeReceived, context.MessageId, GetIncomingMessageId, outgoing, name);
                attachmentNames.Add(name);
            });
        }

        if (outgoingAttachments.DuplicateIncomingAttachments || outgoingAttachments.Duplicates.Count != 0)
        {
            if (outgoingAttachments.DuplicateIncomingAttachments)
            {
                var names = await persister.Duplicate(GetIncomingMessageId(), context.MessageId, context.CancellationToken);
                attachmentNames.AddRange(names);
            }

            foreach (var duplicate in outgoingAttachments.Duplicates)
            {
                attachmentNames.Add(duplicate.To);
                await persister.Duplicate(GetIncomingMessageId(), duplicate.From, context.MessageId, duplicate.To, context.CancellationToken);
            }
        }

        Guard.AgainstDuplicateNames(attachmentNames);

        context.Headers.Add("Attachments", string.Join(", ", attachmentNames));
    }

    async Task ProcessWriter(string messageId, string name, DateTime expiry, Func<Stream, Task> writer, IReadOnlyDictionary<string, string>? metadata, Cancel cancel)
    {
        var (writerTask, readerStream) = PipeHelper.StartWriter(writer, cancel);
        await using (readerStream)
        {
            await persister.SaveStream(messageId, name, expiry, readerStream, metadata, cancel);
            await writerTask;
        }
    }

    async Task ProcessAttachment(TimeSpan? timeToBeReceived, string messageId, Func<string> getIncomingMessageId, Outgoing outgoing, string name)
    {
        var outgoingStreamTimeToKeep = outgoing.TimeToKeep ?? endpointTimeToKeep;
        var timeToKeep = outgoingStreamTimeToKeep(timeToBeReceived);
        var expiry = DateTime.UtcNow.Add(timeToKeep);
        try
        {
            await Process(messageId, getIncomingMessageId, outgoing, name, expiry);
        }
        finally
        {
            outgoing.Cleanup?.Invoke();
        }
    }

    async Task Process(string messageId, Func<string> getIncomingMessageId, Outgoing outgoing, string name, DateTime expiry, Cancel cancel = default)
    {
        if (outgoing.IsPreSaved)
        {
            return;
        }

        if (outgoing.HasIncomingTransform)
        {
            await ProcessIncomingTransform(messageId, getIncomingMessageId(), outgoing, name, expiry, cancel);
            return;
        }

        if (outgoing.StreamWriter is not null)
        {
            await ProcessWriter(messageId, name, expiry, outgoing.StreamWriter, outgoing.Metadata, cancel);
            return;
        }

        if (outgoing.AsyncBytesFactory is not null)
        {
            var bytes = await outgoing.AsyncBytesFactory();
            await persister.SaveStream(messageId, name, expiry, new MemoryStream(bytes), outgoing.Metadata, cancel);
            return;
        }

        if (outgoing.BytesFactory is not null)
        {
            await persister.SaveStream(messageId, name, expiry, new MemoryStream(outgoing.BytesFactory()), outgoing.Metadata, cancel);
            return;
        }

        if (outgoing.BytesInstance is not null)
        {
            await persister.SaveStream(messageId, name, expiry, new MemoryStream(outgoing.BytesInstance), outgoing.Metadata, cancel);
            return;
        }

        if (outgoing.StringInstance is not null)
        {
            await persister.SaveString(messageId, name, expiry, outgoing.StringInstance, outgoing.Encoding, outgoing.Metadata, cancel);
            return;
        }

        throw new("No matching way to handle outgoing.");
    }

    Task ProcessIncomingTransform(string toMessageId, string fromMessageId, Outgoing outgoing, string name, DateTime expiry, Cancel cancel)
    {
        var transform = outgoing.IncomingTransform!;
        var fromName = outgoing.IncomingFromName!;
        var bufferSource = outgoing.BufferSource;
        var bufferSink = outgoing.BufferSink;

        Task Writer(Stream sink) =>
            persister.ProcessStream(
                fromMessageId,
                fromName,
                async (inStream, c) =>
                {
                    if (bufferSource)
                    {
                        using var bufferedSource = new MemoryStream();
                        await inStream.CopyToAsync(bufferedSource, c);
                        bufferedSource.Position = 0;
                        await RunTransform(transform, bufferedSource, sink, bufferSink, c);
                    }
                    else
                    {
                        await RunTransform(transform, inStream, sink, bufferSink, c);
                    }
                }, cancel);

        return ProcessWriter(toMessageId, name, expiry, Writer, outgoing.Metadata, cancel);
    }

    static async Task RunTransform(Func<Stream, Stream, Cancel, Task> transform, Stream source, Stream pipeSink, bool bufferSink, Cancel cancel)
    {
        if (bufferSink)
        {
            using var bufferedSink = new MemoryStream();
            await transform(source, bufferedSink, cancel);
            bufferedSink.Position = 0;
            await bufferedSink.CopyToAsync(pipeSink, cancel);
        }
        else
        {
            await transform(source, pipeSink, cancel);
        }
    }
}
