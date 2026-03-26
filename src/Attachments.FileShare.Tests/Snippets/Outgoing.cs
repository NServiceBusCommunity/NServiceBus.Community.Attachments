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
}
