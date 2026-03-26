# Upgrade from v18 to v19


## Stream factory overloads removed

The following `Add` overloads on `IOutgoingAttachments` have been removed:

 * `Add(Func<Stream> streamFactory, ...)`
 * `Add(string name, Func<Stream> streamFactory, ...)`
 * `Add<T>(Func<Task<T>> streamFactory, ...)` where T : Stream
 * `Add<T>(string name, Func<Task<T>> streamFactory, ...)` where T : Stream

These methods buffered the entire stream content through `CopyToAsync`, providing no memory advantage over `AddBytes` for small payloads and no streaming advantage over `AddStream` for large payloads.


### Migration paths


#### Existing Stream instance available

Use `Add(Stream)`:

```cs
// Before
attachments.Add("name", () => myStream);

// After
attachments.Add("name", myStream);
```


#### Async stream source (file, HTTP, database)

Use `AddStream` to stream data directly to storage with bounded memory:

```cs
// Before
attachments.Add<FileStream>("name", () => Task.FromResult(File.OpenRead("path.txt")));

// After
attachments.AddStream("name", async stream =>
{
    await using var source = File.OpenRead("path.txt");
    await source.CopyToAsync(stream);
});
```

`AddStream` uses `System.IO.Pipelines` internally so the full payload is never buffered in memory.


#### Small data already in memory

Use `AddBytes` or `AddString` instead:

```cs
// Before
attachments.Add("name", () => new MemoryStream(data));

// After
attachments.AddBytes("name", data);
```


## SQL EnableAttachments: database parameter

The `database` parameter on `EnableAttachments` is now derived from the connection for the `string connection` and `Func<SqlConnection>` overloads. It defaults to `null` on those overloads and is resolved automatically from the connection string.

The `Func<Cancel, Task<SqlConnection>>` overload still requires an explicit `database` parameter (defaults to `"nservicebus"`) since the database name cannot be derived without opening a connection.

```cs
// Before: database was always required
configuration.EnableAttachments(connectionString, database: "mydb");

// After: database is derived from the connection string
configuration.EnableAttachments(connectionString);
```
