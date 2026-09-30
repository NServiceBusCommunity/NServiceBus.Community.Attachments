# SQL Attachments

Uses a SQL Server [varbinary](https://docs.microsoft.com/en-us/sql/t-sql/data-types/binary-and-varbinary-transact-sql) to store attachments for messages.


## Usage

Two settings are required as part of the default usage:

 * A connection factory that returns an open instance of a [SqlConnection](https://msdn.microsoft.com/en-us/library/system.data.sqlclient.sqlconnection.aspx). Note that any Exception that occurs during opening the connection should be handled by the factory.
 * A default time to keep for attachments.

snippet: SqlEnableAttachments


### Recommended Usage

Extract out the connection factory to a helper method

snippet: OpenConnection

Also uses the `NServiceBus.Attachments.Sql.TimeToKeep.Default` method for attachment cleanup.

This usage results in the following:

snippet: SqlEnableAttachmentsRecommended


### Using ambient connectivity

Attachments can leverage the ambient SQL connectivity from either the [transport](https://docs.particular.net/transports/) and/or the [persister](https://docs.particular.net/persistence/).

If both `UseSynchronizedStorageSessionConnectivity` and `UseTransportConnectivity` are defined, the `SynchronizedStorageSession` will be used first, followed by the `TransportTransaction`.

Ambient connectivity applies to attachment writes only — attachment saves run on the ambient connection/transaction so the save is atomic with the receive (under `SendsAtomicWithReceive`) or the persister's storage session. Attachment reads always run on a fresh connection from the `connectionFactory` and are not enlisted in the receive transaction. This lets a handler hold an `OpenOutgoingAttachment` sink open while reading incoming attachments without colliding with the write on a non-MARS connection. Each read call (`GetStream`, `CopyTo`, `GetBytes`, `ProcessStream`, etc.) opens its own short-lived connection; SQL connection pooling makes this cheap.


#### Use SynchronizedStorageSession connectivity

To use the ambient [SynchronizedStorageSession persister](https://docs.particular.net/nservicebus/handlers/accessing-data.md#using-nservicebus-persistence):

snippet: UseSynchronizedStorageSessionConnectivity

This approach attempts to use the SynchronizedStorageSession using the following steps:

 * For the current context attempt to retrieve an instance of `SynchronizedStorageSession`. If no `SynchronizedStorageSession` exists, don't continue and fall back to the [SqlConnection](https://docs.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlconnection) retrieved by the `connectionFactory`.
 * Attempt to retrieve a property named 'Transaction' that is a [SqlTransaction](https://docs.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqltransaction) from the `SynchronizedStorageSession`. If it exists, use it for outgoing attachment operations in the current pipeline.
 * Attempt to retrieve a property named 'Connection' that is a [SqlConnection](https://docs.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlconnection) from the `SynchronizedStorageSession`. If it exists, use it for outgoing attachment operations in the current pipeline.

The properties are retrieved using [reflection](https://docs.microsoft.com/en-us/dotnet/framework/reflection-and-codedom/reflection) since there is no API in NServiceBus to access SynchronizedStorageSession data via type.


#### Use transport connectivity

To use the ambient [transport transaction](https://docs.particular.net/transports/transactions):

snippet: UseTransportConnectivity

This approach attempts to use the transport transaction using the following steps:

 * For the current context, attempt to retrieve an instance of `TransportTransaction`. If no `TransportTransaction` exists, don't continue and fall back to using the [SqlConnection](https://docs.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlconnection) retrieved by the `connectionFactory`.
 * Attempt to retrieve an instance of [Transaction](https://docs.microsoft.com/en-us/dotnet/api/system.transactions.transaction) from the `TransportTransaction`. If it exists, use it in [SqlConnection.EnlistTransaction](https://docs.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlconnection.enlisttransaction) with an instance of [SqlConnection](https://docs.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlconnection) retrieved by the `connectionFactory`. Then use that [SqlConnection](https://docs.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlconnection) for outgoing attachment operations in the current pipeline.
 * Attempt to retrieve an instance of [SqlTransaction](https://docs.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqltransaction) from the `TransportTransaction`. If it exists, use it for outgoing attachment operations in the current pipeline.
 * Attempt to retrieve an instance of [SqlConnection](https://docs.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlconnection) from the `TransportTransaction`. If it exists, use it for outgoing attachment operations in the current pipeline.
 * Any attachments associated with a message send will be deleted after message processing.


## Transferring attachments to a saga

A saga that gathers attachments from several messages (for example, replies in a scatter-gather) can take ownership of each incoming attachment instead of copying its data into saga state. `TransferToSaga` updates the attachment's row in place, so the `varbinary` data is not copied or rewritten.

snippet: TransferToSaga

 * Early cleanup deletes attachments by the incoming message id. After a transfer that id no longer matches, so the attachment survives after the message finishes processing.
 * Pass `newName` when several messages carry an attachment with the same name, since a saga can only own one attachment of each name. Transferring to a name the saga already owns throws, unless `replace: true` is passed, in which case the existing attachment is deleted first. This suits sagas that can receive a newer reply for the same item, where the latest should win. If the attachment being transferred does not exist, nothing is deleted.
 * The attachment keeps its existing expiry unless `timeToKeep` is passed, and the [cleanup task](#data-cleanup) removes it once it expires. Call `DeleteForSaga` to remove the saga's attachments as soon as they are no longer needed.
 * `TransferToSaga`, `GetBytesForSaga`, `GetMemoryStreamForSaga`, `GetStringForSaga` and `DeleteForSaga` run on the ambient connection and transaction, so they are atomic with the rest of the handler and see transfers made earlier in the same handler. This requires `UseSynchronizedStorageSessionConnectivity` or `UseTransportConnectivity`. Without either, each call commits on its own connection.
 * The other read members use a separate connection. Read any attachments of the current message before transferring them, since a read on a separate connection can wait on the transfer's lock until the handler's transaction commits.


## Installation


### Script execution runs by default at endpoint startup

To streamline development the attachment installer is, by default, executed at endpoint startup, in the same manner as all other [installers](https://docs.particular.net/nservicebus/operations/installers).

snippet: ExecuteAtStartup

NOTE: Note that this is also a valid approach for higher level environments.


### Optionally take control of script execution

However in higher level environment scenarios, where standard installers are being run, but the SQL attachment installation has been executed as part of a deployment, it may be necessary to explicitly disable the attachment installer executing while leaving standard installers enabled.

snippet: DisableInstaller


## Table Name

The default table name and schema is `dbo.MessageAttachments`. It can be changed with the following:

snippet: UseTableName


include: attachments
