public class Outgoing
{
    #region OutgoingWithStreamInstance

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

    #endregion

    #region OutgoingWithSyncStreamInstance

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

    #endregion

    #region OutgoingWithSavePattern

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

    #endregion

    #region OutgoingInstance

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

    #endregion

    #region OutgoingFromIncoming

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

    #endregion
}
