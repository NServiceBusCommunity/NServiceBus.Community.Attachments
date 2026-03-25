

## Data Cleanup

Attachment cleanup is enabled by default. It can be disabled using the following:

snippet: DisableCleanupTask


## Controlling attachment lifetime

When the cleanup task runs it uses the `Expiry` column to determine if a given attachment should be deleted. This column is populated when an attachment is written. When adding an attachment to an outgoing message, all methods accept an optional parameter `timeToKeep` of the type `GetTimeToKeep`. `GetTimeToKeep` is defined as:

```
public delegate TimeSpan GetTimeToKeep(TimeSpan? messageTimeToBeReceived);
```

Where `messageTimeToBeReceived` is value of [TimeToBeReceived](https://docs.particular.net/nservicebus/messaging/discard-old-messages.md). If no `timeToKeep` parameter for a specific attachment is defined then the endpoint level `timeToKeep` is used.

The result of `timeToKeep` is then added to the current date and persisted to the `Expiry` column.

The method `TimeToKeep.Default` provides a recommended default for for attachment lifetime calculation:

 * If [TimeToBeReceived](https://docs.particular.net/nservicebus/messaging/discard-old-messages.md) is defined then keep attachment for twice that time.
 * Else; keep for 10 days.


## Streaming without intermediate buffering

When using the Factory Approach or Instance Approach with a non-buffered stream (e.g. `FileStream`, `HttpClient.GetStreamAsync`), data is streamed directly to the underlying storage without being copied into a `MemoryStream` or `byte[]` first. This means attachments of any size can be sent without allocating equivalent memory.

For example, when using the SQL implementation, the `Stream` is passed directly to the `SqlParameter`, and ADO.NET reads from it in chunks during command execution. Similarly, the FileShare implementation copies directly from the source stream to the target file.

```
Pull-based streaming (Factory/Instance Approach):

┌──────────┐        ┌──────────────┐        ┌─────────┐
│  Source   │─read──>│  Attachments │─read──>│ Storage │
│ (Stream)  │       │   Library    │        │ (SQL/FS)│
└──────────┘        └──────────────┘        └─────────┘

Data flows directly from source to storage. No intermediate buffer.
```

To take advantage of this, use the Factory Approach with a stream that reads on demand:

snippet: OutgoingFactoryStream

To avoid buffering, do not call methods like `Stream.CopyTo(memoryStream)` or `stream.ToArray()` before passing the stream. Instead, pass the original stream directly and let the library handle the transfer.


### Push-based APIs (write-to-stream)

Some 3rd party APIs use a push-based pattern where they write to a stream provided by the caller, e.g. `document.SaveAsync(Stream)`. There are two approaches to handle this without buffering the full content in memory.


#### Stream Writer Approach (recommended)

Use `AddStreamWriter` to provide a delegate that writes to a stream. Internally the library uses `System.IO.Pipelines.Pipe` to bridge the push-based writer with the pull-based storage, enabling true concurrent streaming with backpressure. No intermediate `MemoryStream`, `byte[]`, or temp file is needed.

```
Stream Writer Approach (using System.IO.Pipelines):

┌──────────┐        ┌───────────┐        ┌──────────────┐        ┌─────────┐
│ 3rd Party│─write─>│   Pipe    │─read──>│  Attachments │─read──>│ Storage │
│   API    │        │  (buffer) │        │   Library    │        │ (SQL/FS)│
└──────────┘        └───────────┘        └──────────────┘        └─────────┘

Writer and reader run concurrently. Pipe applies backpressure
so the writer pauses if the reader falls behind.
```

snippet: OutgoingStreamWriter


#### Temp File Approach

Alternatively, use a temporary file combined with the async factory. This avoids memory buffering but requires disk I/O:

```
Temp File Approach:

Step 1: Write               Step 2: Read
┌──────────┐   ┌──────┐    ┌──────┐   ┌─────────┐
│ 3rd Party│──>│ Temp │    │ Temp │──>│ Storage │
│   API    │   │ File │    │ File │   │ (SQL/FS)│
└──────────┘   └──────┘    └──────┘   └─────────┘

Two-phase: write to disk first, then stream from disk to storage.
Temp file is deleted after persistence via the cleanup delegate.
```

snippet: OutgoingFactoryPushBased


## Reading and writing attachments


### Writing attachments to an outgoing message

Approaches to using attachments for an outgoing message.

Note: [Stream.Dispose](https://msdn.microsoft.com/en-us/library/ms227422.aspx) is called after the data has been persisted. As such it is not necessary for any code using attachments to perform this cleanup.

While the below examples illustrate adding an attachment to `SendOptions`, equivalent operations can be performed on `PublishOptions` and `ReplyOptions`


#### Factory Approach

The recommended approach for adding an attachment is by providing a delegate that constructs the stream. The execution of this delegate is then deferred until later in the outgoing pipeline, when the instance of the stream is required to be persisted.

There are both async and sync variants.

snippet: OutgoingFactory

snippet: OutgoingFactoryAsync


#### Instance Approach

In some cases an instance of a stream is already available in scope and as such it can be passed directly.

snippet: OutgoingInstance


### Reading attachments for an incoming message

Approaches to using attachments for the current incoming message.


#### Process named with a delegate

Processes an attachment with a specific name.

snippet: ProcessStream


#### Process all with a delegate

Processes all attachments.

snippet: ProcessStreams


#### Copy to a stream

Copy an attachment with a specific name to another stream.

snippet: CopyTo


#### Get an instance of a stream

Get a stream for an attachment with a specific name.

snippet: GetStream


#### Get data as bytes

Get a byte array for an attachment with a specific name.

WARNING: This should only be used the data size is know to be small as it causes the full size of the attachment to be allocated in memory.

snippet: GetBytes


### Reading attachments for a specific message

All of the above examples have companion methods that are suffixed with `ForMessage`. These methods allow a handler or saga to read any attachments as long as the message id for that attachment is known. For example processing all attachments for a specific message could be done as follows

snippet: ProcessStreamsForMessage

This can be helpful in a saga that is operating in a [Scatter-Gather](https://www.enterpriseintegrationpatterns.com/patterns/messaging/BroadcastAggregate.html) mode. So instead of storing all binaries inside the saga persister, the saga can instead store the message ids and then, at a latter point in time, access those attachments.


## Unit Testing

The below examples also use the [NServiceBus.Testing](https://docs.particular.net/nservicebus/testing/) extension.


### Testing outgoing attachments

snippet: TestOutgoingHandler

snippet: TestOutgoing


### Testing incoming attachments


#### Injecting a custom instance

To mock or verify incoming attachments is it necessary to inject a instance of `IMessageAttachments` into the current `IMessageHandlerContext`. This can be done using the `MockAttachmentHelper.InjectAttachmentsInstance()` extension method which exists in the `NServiceBus.Attachments.Testing` namespace.

snippet: InjectAttachmentsInstance

The implementation of `IMessageHandlerContext` can be a custom coded mock or constructed using any of the popular mocking/assertion frameworks.

There is a default implementation of `IMessageAttachments` named  `MockMessageAttachments`. This implementation stubs out all methods. All members are virtual so it can be used as simplified base class for custom mocks.

snippet: CustomMockMessageAttachments

Putting these parts together allows a handler, using incoming attachments, to be tested.

snippet: TestIncomingHandler

snippet: TestIncoming
