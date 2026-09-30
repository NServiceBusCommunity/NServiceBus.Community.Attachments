// ReSharper disable UnusedVariable

// ReSharper disable UnusedType.Global
public class Incoming
{
    #region ProcessStream

    class HandlerProcessStream :
        IHandleMessages<MyMessage>
    {
        public Task Handle(MyMessage message, HandlerContext context)
        {
            var attachments = context.Attachments();
            return attachments.ProcessStream(
                name: "attachment1",
                action: async (stream, token) =>
                {
                    // Use the attachment stream. in this example copy to a file
                    await using var fileToCopyTo = File.Create("FilePath.txt");
                    await stream.CopyToAsync(fileToCopyTo, token);
                },
                context.CancellationToken);
        }
    }

    #endregion

    #region ProcessStreams

    class HandlerProcessStreams :
        IHandleMessages<MyMessage>
    {
        public Task Handle(MyMessage message, HandlerContext context)
        {
            var attachments = context.Attachments();
            return attachments.ProcessStreams(
                action: async (stream, cancel) =>
                {
                    // Use the attachment stream. in this example copy to a file
                    await using var file = File.Create($"{stream.Name}.txt");
                    await stream.CopyToAsync(file, cancel);
                },
                context.CancellationToken);
        }
    }

    #endregion

    #region ProcessStreamsForMessage

    class HandlerProcessStreamsForMessage :
        IHandleMessages<MyMessage>
    {
        public Task Handle(MyMessage message, HandlerContext context)
        {
            var attachments = context.Attachments();
            return attachments.ProcessStreamsForMessage(
                messageId: "theMessageId",
                action: async (stream, cancel) =>
                {
                    // Use the attachment stream. in this example copy to a file
                    await using var file = File.Create($"{stream.Name}.txt");
                    await stream.CopyToAsync(file, cancel);
                },
                context.CancellationToken);
        }
    }

    #endregion

    #region CopyTo

    class HandlerCopyTo :
        IHandleMessages<MyMessage>
    {
        public async Task Handle(MyMessage message, HandlerContext context)
        {
            var attachments = context.Attachments();
            await using var fileToCopyTo = File.Create("FilePath.txt");
            await attachments.CopyTo("attachment1", fileToCopyTo, context.CancellationToken);
        }
    }

    #endregion

    #region GetBytes

    class HandlerGetBytes :
        IHandleMessages<MyMessage>
    {
        public async Task Handle(MyMessage message, HandlerContext context)
        {
            var attachments = context.Attachments();
            var bytes = await attachments.GetBytes("attachment1", context.CancellationToken);
            // use the byte array
        }
    }

    #endregion

    #region GetStream

    class HandlerGetStream :
        IHandleMessages<MyMessage>
    {
        public async Task Handle(MyMessage message, HandlerContext context)
        {
            var attachments = context.Attachments();
            await using var attachment = await attachments.GetStream("attachment1", context.CancellationToken);
            // Use the attachment stream. in this example copy to a file
            await using var fileToCopyTo = File.Create("FilePath.txt");
            await attachment.CopyToAsync(fileToCopyTo, context.CancellationToken);
        }
    }

    #endregion

    #region TransferToSaga

    class ConvertSaga :
        Saga<ConvertSaga.SagaData>,
        IAmStartedByMessages<StartConvert>,
        IHandleMessages<ConvertCompleted>
    {
        public async Task Handle(ConvertCompleted message, HandlerContext context)
        {
            var cancel = context.CancellationToken;
            var attachments = context.Attachments();

            // Move the reply's attachment to this saga. The row is updated in place, so no data is copied,
            // and it is not deleted when the reply finishes processing.
            await attachments.TransferToSaga(Data, newName: message.Format, cancel: cancel);
            Data.Received.Add(message.Format);
            if (Data.Received.Count < 2)
            {
                return;
            }

            // Reads the attachments transferred by earlier replies, and the one transferred above.
            var pdf = await attachments.GetBytesForSaga(Data, "pdf", cancel);
            var word = await attachments.GetBytesForSaga(Data, "word", cancel);

            // Use the documents, then remove them.
            await attachments.DeleteForSaga(Data, cancel);
            MarkAsComplete();
        }

        #endregion

        public Task Handle(StartConvert message, HandlerContext context)
        {
            Data.DocumentId = message.DocumentId;
            return Task.CompletedTask;
        }

        protected override void ConfigureHowToFindSaga(SagaPropertyMapper<SagaData> mapper) =>
            mapper.MapSaga(_ => _.DocumentId)
                .ToMessage<StartConvert>(_ => _.DocumentId);

        public class SagaData :
            ContainSagaData
        {
            public Guid DocumentId { get; set; }
            public List<string> Received { get; set; } = [];
        }
    }

    class StartConvert :
        IMessage
    {
        public Guid DocumentId { get; set; }
    }

    class ConvertCompleted :
        IMessage
    {
        public string Format { get; set; } = null!;
    }
}