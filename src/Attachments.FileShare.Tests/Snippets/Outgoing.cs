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
            attachments.Add(
                name: "attachment1",
                streamFactory: () => File.OpenRead("FilePath.txt"));
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
            attachments.Add(
                name: "attachment1",
                streamFactory: () => httpClient.GetStreamAsync("theUrl"));
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
            attachments.Add(
                name: "attachment1",
                streamFactory: () =>
                {
                    // The FileStream is passed directly to storage
                    // without being buffered in a MemoryStream or byte[]
                    return File.OpenRead("LargeFile.zip");
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
            string? tempFile = null;
            attachments.Add(
                name: "attachment1",
                streamFactory: async () =>
                {
                    var document = new Document();
                    tempFile = Path.GetTempFileName();
                    await using (var writeStream = File.Create(tempFile))
                    {
                        await document.SaveAsync(writeStream);
                    }

                    return File.OpenRead(tempFile);
                },
                cleanup: () =>
                {
                    if (tempFile != null)
                    {
                        File.Delete(tempFile);
                    }
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
            attachments.Add(
                name: "attachment1",
                stream: stream,
                cleanup: () => File.Delete("FilePath.txt"));
            return context.Send(new OtherMessage(), sendOptions);
        }
    }

    #endregion
}