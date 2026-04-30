

## Data Cleanup

Attachment cleanup is enabled by default. It can be disabled using the following:


### FileShare

snippet: FileShareDisableCleanupTask


### Sql

snippet: SqlDisableCleanupTask


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


## Reading and writing attachments


### Choosing the right API for writing attachments

| API | Use when | Memory behavior |
|---|---|---|
| `AddStream` | Large payloads or data generated incrementally (recommended for large data) | Streams via `System.IO.Pipelines` with backpressure. Memory stays bounded regardless of payload size. |
| `Add(Stream)` | An existing `Stream` instance is available | Bridges to `AddStream` internally via `CopyToAsync`. |
| `AddFromIncoming` | The outgoing data is produced by transforming an incoming attachment of the current message | Reads from incoming and writes to the outgoing pipe at the same time. No intermediate buffer (unless `bufferSource`/`bufferSink` is set). |
| `AddBytes` / `AddString` | Small payloads already in memory (config, metadata, small documents) | Full payload allocated in memory. |
| `Add(AttachmentFactory)` | Number of attachments not known at compile time | Dynamic. Each attachment uses the memory model of its content. |
| `AddFile` | File on disk | Convenience wrapper over `AddStream`. |

```
AddStream (using System.IO.Pipelines):

┌──────────┐        ┌───────────┐        ┌──────────────┐        ┌─────────┐
│  Writer  │─write─>│   Pipe    │─read──>│  Attachments │─read──>│ Storage │
│  Code    │        │  (buffer) │        │   Library    │        │ (SQL/FS)│
└──────────┘        └───────────┘        └──────────────┘        └─────────┘

Writer and reader run concurrently. Pipe applies backpressure
so the writer pauses if the reader falls behind.
```


### Writing attachments to an outgoing message

While the below examples illustrate adding an attachment to `SendOptions`, equivalent operations can be performed on `PublishOptions` and `ReplyOptions`


#### AddStream (recommended)

Use `AddStream` to provide a delegate that writes to a stream. Internally the library uses `System.IO.Pipelines.Pipe` to bridge the writer with storage, enabling concurrent streaming with backpressure. No intermediate `MemoryStream`, `byte[]`, or temp file is needed.

snippet: OutgoingWithStreamInstance

For synchronous writers, an `Action<Stream>` overload is available. The async approach above is preferred as it avoids blocking the thread during I/O.

snippet: OutgoingWithSyncStreamInstance

snippet: OutgoingWithSavePattern

#### Add with an existing Stream

Use `Add` when a `Stream` instance is already available. Internally bridges to `AddStream` via `CopyToAsync`.

snippet: OutgoingInstance


#### Transform an incoming attachment

Use `AddFromIncoming` to produce an outgoing attachment by transforming an incoming attachment of the current message. The library opens the incoming read inside the outgoing pipeline so the source and sink streams are live at the same time. No intermediate `MemoryStream` or temp file is needed.

This is useful when the handler reads an incoming attachment, runs it through a converter, and forwards the converted bytes — without the handler needing to manage the lifetime of the incoming SQL/file stream across the deferred `AddStream` writer.

snippet: OutgoingFromIncoming

`bufferSource: true` buffers the incoming data to a seekable `MemoryStream` before the transform runs — use when the transform requires `Length`/`Position`/`Seek` on its input (e.g. email/MIME parsers).

`bufferSink: true` runs the transform against a seekable `MemoryStream` and drains it to storage afterwards — use when the transform requires seek operations on its output (e.g. some Aspose libraries).

NOTE: The transform runs *during* the outgoing pipeline (after `context.Reply` / `context.Send` is called). Any value the transform produces (e.g. a "truncated" flag, encoding metadata) cannot influence the outgoing message body, since the body has already been finalized by the caller. Use eager conversion if the handler needs such values in the outgoing message.


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
