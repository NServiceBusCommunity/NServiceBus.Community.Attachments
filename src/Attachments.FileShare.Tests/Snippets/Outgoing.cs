// no regions since these are the same as the sql snippets
public class Outgoing
{
    class HandlerFactory :
        IHandleMessages<MyMessage>
    {
        public Task Handle(MyMessage message, HandlerContext context)
        {
            var sendOptions = new SendOptions();
            var attachments = sendOptions.Attachments();
            attachments.AddStream(
                name: "attachment1",
                writer: async stream =>
                {
                    await using var source = File.OpenRead("FilePath.txt");
                    await source.CopyToAsync(stream);
                });
            return context.Send(new OtherMessage(), sendOptions);
        }
    }

    class HandlerSyncFactory :
        IHandleMessages<MyMessage>
    {
        public Task Handle(MyMessage message, HandlerContext context)
        {
            var sendOptions = new SendOptions();
            var attachments = sendOptions.Attachments();
            attachments.AddStream(
                name: "attachment1",
                writer: stream =>
                {
                    using var source = File.OpenRead("FilePath.txt");
                    source.CopyTo(stream);
                });
            return context.Send(new OtherMessage(), sendOptions);
        }
    }

    class HandlerStreamWriter :
        IHandleMessages<MyMessage>
    {
        public Task Handle(MyMessage message, HandlerContext context)
        {
            var document = new Document();
            var sendOptions = new SendOptions();
            var attachments = sendOptions.Attachments();
            attachments.AddStream(
                name: "attachment1",
                writer: document.SaveAsync);
            return context.Send(new OtherMessage(), sendOptions);
        }
    }

    class HandlerInstance :
        IHandleMessages<MyMessage>
    {
        public Task Handle(MyMessage message, HandlerContext context)
        {
            var sendOptions = new SendOptions();
            var attachments = sendOptions.Attachments();
            var stream = File.OpenRead("FilePath.txt");
            attachments.Add(
                name: "attachment1",
                stream: stream,
                cleanup: () => File.Delete("FilePath.txt"));
            return context.Send(new OtherMessage(), sendOptions);
        }
    }

    class HandlerFromIncoming :
        IHandleMessages<MyMessage>
    {
        public Task Handle(MyMessage message, HandlerContext context)
        {
            var replyOptions = new ReplyOptions();
            var attachments = replyOptions.Attachments();
            attachments.AddFromIncoming(
                fromName: "input",
                toName: "output",
                transform: async (source, sink, cancel) =>
                {
                    using var reader = new StreamReader(source, leaveOpen: true);
                    var content = await reader.ReadToEndAsync(cancel);
                    await using var writer = new StreamWriter(sink, leaveOpen: true);
                    await writer.WriteAsync(content.ToUpperInvariant());
                });
            return context.Reply(new OtherMessage(), replyOptions);
        }
    }

    class HandlerImmediateWrite :
        IHandleMessages<MyMessage>
    {
        public async Task Handle(MyMessage message, HandlerContext context)
        {
            var replyOptions = new ReplyOptions();
            bool truncated;

            await using (var sink = await context.OpenOutgoingAttachment(replyOptions, "output"))
            {
                truncated = FileShareConverter.Convert(message.Source, sink);
            }

            await context.Reply(new OtherMessage { Truncated = truncated }, replyOptions);
        }
    }
}

static class FileShareConverter
{
    public static bool Convert(string source, Stream sink) => false;
}
