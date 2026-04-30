using NServiceBus.Attachments;

public class OutgoingAttachmentsTests
{
    [Test]
    public async Task AddFromIncoming_RegistersTransform()
    {
        var attachments = new OutgoingAttachments();
        static Task Transform(Stream source, Stream sink, Cancel cancel) => Task.CompletedTask;

        attachments.AddFromIncoming("fromName", "toName", Transform);

        await Assert.That(attachments.HasPendingAttachments).IsTrue();
        var entry = attachments.Inner["toName"];
        await Assert.That(entry.HasIncomingTransform).IsTrue();
        await Assert.That(entry.IncomingFromName).IsEqualTo("fromName");
        await Assert.That(entry.IncomingTransform).IsNotNull();
        await Assert.That(entry.BufferSource).IsFalse();
        await Assert.That(entry.BufferSink).IsFalse();
    }

    [Test]
    public async Task AddFromIncoming_PropagatesBufferingFlags()
    {
        var attachments = new OutgoingAttachments();
        attachments.AddFromIncoming(
            fromName: "fromName",
            toName: "toName",
            transform: (_, _, _) => Task.CompletedTask,
            bufferSource: true,
            bufferSink: true);

        var entry = attachments.Inner["toName"];
        await Assert.That(entry.BufferSource).IsTrue();
        await Assert.That(entry.BufferSink).IsTrue();
    }

    [Test]
    public async Task AddFromIncoming_PropagatesMetadataAndTimeToKeep()
    {
        var attachments = new OutgoingAttachments();
        var metadata = new Dictionary<string, string> {{"key", "value"}};
        GetTimeToKeep timeToKeep = _ => TimeSpan.FromHours(1);

        attachments.AddFromIncoming(
            fromName: "fromName",
            toName: "toName",
            transform: (_, _, _) => Task.CompletedTask,
            timeToKeep: timeToKeep,
            metadata: metadata);

        var entry = attachments.Inner["toName"];
        await Assert.That(entry.Metadata).IsEqualTo(metadata);
        await Assert.That(entry.TimeToKeep).IsEqualTo(timeToKeep);
    }

    [Test]
    public async Task AddFromIncoming_SingleNameExtension_UsesNameForBoth()
    {
        IOutgoingAttachments attachments = new OutgoingAttachments();
        attachments.AddFromIncoming("name", (_, _, _) => Task.CompletedTask);

        var inner = ((OutgoingAttachments) attachments).Inner;
        await Assert.That(inner.ContainsKey("name")).IsTrue();
        await Assert.That(inner["name"].IncomingFromName).IsEqualTo("name");
    }

    [Test]
    public async Task AddPreSaved_RegistersAsSkippableInSendBehavior()
    {
        var attachments = new OutgoingAttachments();
        var guid = Guid.NewGuid();
        attachments.AddPreSaved("name", guid, metadata: null);

        var entry = attachments.Inner["name"];
        await Assert.That(entry.IsPreSaved).IsTrue();
        await Assert.That(entry.PreSavedGuid).IsEqualTo(guid);
        await Assert.That(entry.HasStreamWriter).IsFalse();
        await Assert.That(entry.HasIncomingTransform).IsFalse();
    }

    [Test]
    public async Task AddPreSaved_WithoutGuid_StillFlagsPreSaved()
    {
        var attachments = new OutgoingAttachments();
        attachments.AddPreSaved("name", savedGuid: null, metadata: null);

        var entry = attachments.Inner["name"];
        await Assert.That(entry.IsPreSaved).IsTrue();
        await Assert.That(entry.PreSavedGuid).IsNull();
    }
}
