public class TestingOutgoing
{
    #region TestOutgoingHandler

    public class Handler :
        IHandleMessages<MyMessage>
    {
        public Task Handle(MyMessage message, HandlerContext context)
        {
            var options = new SendOptions();
            var attachments = options.Attachments();
            attachments.AddStream(
                "theName",
                async stream =>
                {
                    await using var source = File.OpenRead("aFilePath");
                    await source.CopyToAsync(stream);
                });
            return context.Send(new OtherMessage(), options);
        }
    }

    #endregion

    #region TestOutgoing

    [Test]
    public async Task TestOutgoingAttachments()
    {
        //Arrange
        var context = new RecordingHandlerContext();
        var handler = new Handler();

        //Act
        await handler.Handle(new(), context);

        // Assert
        var sentMessage = context.Sent.Single();
        var attachments = sentMessage.Options.Attachments();
        var attachment = attachments.Items.Single();
        await Assert.That(attachment.Name).Contains("theName");
        await Assert.That(attachments.HasPendingAttachments).IsTrue();
    }

    #endregion
}
