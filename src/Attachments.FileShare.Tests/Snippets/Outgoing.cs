public class Outgoing
{
    #region OutgoingFactory

    class HandlerFactory :
        IHandleMessages<MyMessage>
    {
        public Task Handle(MyMessage message, HandlerContext context)
        {
            var sendOptions = new SendOptions();
            var attachments = sendOptions.Attachments();
            attachments.AddStreamWriter(
                name: "attachment1",
                streamWriter: async stream =>
                {
                    await using var source = File.OpenRead("FilePath.txt");
                    await source.CopyToAsync(stream);
                });
            return context.Send(new OtherMessage(), sendOptions);
        }
    }

    #endregion

    #region OutgoingFactoryAsync

    class HandlerFactoryAsync :
        IHandleMessages<MyMessage>
    {
        static HttpClient httpClient = new();

        public Task Handle(MyMessage message, HandlerContext context)
        {
            var sendOptions = new SendOptions();
            var attachments = sendOptions.Attachments();
            attachments.AddStreamWriter(
                name: "attachment1",
                streamWriter: async stream =>
                {
                    await using var source =
                        await httpClient.GetStreamAsync("theUrl");
                    await source.CopyToAsync(stream);
                });
            return context.Send(new OtherMessage(), sendOptions);
        }
    }

    #endregion

    #region OutgoingFactoryStream

    class HandlerFactoryStream :
        IHandleMessages<MyMessage>
    {
        public Task Handle(MyMessage message, HandlerContext context)
        {
            var sendOptions = new SendOptions();
            var attachments = sendOptions.Attachments();
            attachments.AddStreamWriter(
                name: "attachment1",
                streamWriter: async stream =>
                {
                    // The FileStream is passed directly to storage
                    // without being buffered in a MemoryStream or byte[]
                    await using var source = File.OpenRead("LargeFile.zip");
                    await source.CopyToAsync(stream);
                });
            return context.Send(new OtherMessage(), sendOptions);
        }
    }

    #endregion

    #region OutgoingFactoryPushBased

    class HandlerFactoryPushBased :
        IHandleMessages<MyMessage>
    {
        public Task Handle(MyMessage message, HandlerContext context)
        {
            var sendOptions = new SendOptions();
            var attachments = sendOptions.Attachments();
            var document = new Document();
            attachments.AddStreamWriter(
                name: "attachment1",
                streamWriter: async stream =>
                {
                    await document.SaveAsync(stream);
                });
            return context.Send(new OtherMessage(), sendOptions);
        }
    }

    #endregion

    #region OutgoingStreamWriter

    class HandlerStreamWriter :
        IHandleMessages<MyMessage>
    {
        public Task Handle(MyMessage message, HandlerContext context)
        {
            var document = new Document();
            var sendOptions = new SendOptions();
            var attachments = sendOptions.Attachments();
            attachments.AddStreamWriter(
                name: "attachment1",
                streamWriter: stream => document.SaveAsync(stream));
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
            attachments.AddStreamWriter(
                name: "attachment1",
                streamWriter: async target => await stream.CopyToAsync(target),
                cleanup: () => File.Delete("FilePath.txt"));
            return context.Send(new OtherMessage(), sendOptions);
        }
    }

    #endregion
}