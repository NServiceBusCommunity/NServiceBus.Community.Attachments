# <img src="/src/icon.png" height="25px"> NServiceBus.Community.Attachments

[![Build status](https://img.shields.io/appveyor/build/SimonCropp/nservicebus-community-attachments)](https://ci.appveyor.com/project/SimonCropp/nservicebus-community-attachments)
[![NuGet Status](https://img.shields.io/nuget/v/NServiceBus.Community.Attachments.FileShare.Raw.svg?label=Attachments.FileShare.Raw)](https://www.nuget.org/packages/NServiceBus.Community.Attachments.FileShare.Raw/)
[![NuGet Status](https://img.shields.io/nuget/v/NServiceBus.Community.Attachments.Sql.svg?label=Attachments.Sql)](https://www.nuget.org/packages/NServiceBus.Community.Attachments.Sql/)
[![NuGet Status](https://img.shields.io/nuget/v/NServiceBus.Community.Attachments.Sql.Raw.svg?label=Attachments.Sql.Raw)](https://www.nuget.org/packages/NServiceBus.Community.Attachments.Sql.Raw/)

Adds a streaming based attachment functionality to [NServiceBus](https://docs.particular.net/nservicebus/).

**See [Milestones](../../milestones?state=closed) for release notes.**

<!--- StartOpenCollectiveBackers -->

[Already a Patron? skip past this section](#endofbacking)


## Community backed

**It is expected that all developers [become a Patron](https://opencollective.com/nservicebuscommunity/contribute/patron-6976) to use NServiceBus Community Extensions. [Go to licensing FAQ](https://github.com/NServiceBusCommunity/Home/#licensingpatron-faq)**


### Sponsors

Support this project by [becoming a Sponsor](https://opencollective.com/nservicebuscommunity/contribute/sponsor-6972). The company avatar will show up here with a website link. The avatar will also be added to all GitHub repositories under the [NServiceBusCommunity organization](https://github.com/NServiceBusCommunity).


### Patrons

Thanks to all the backing developers. Support this project by [becoming a patron](https://opencollective.com/nservicebuscommunity/contribute/patron-6976).

<img src="https://opencollective.com/nservicebuscommunity/tiers/patron.svg?width=890&avatarHeight=60&button=false">

<a href="#" id="endofbacking"></a>

<!--- EndOpenCollectiveBackers -->


toc


## NuGet packages

 * https://www.nuget.org/packages/NServiceBus.Community.Attachments.FileShare
 * https://www.nuget.org/packages/NServiceBus.Community.Attachments.FileShare.Raw
 * https://www.nuget.org/packages/NServiceBus.Community.Attachments.Sql
 * https://www.nuget.org/packages/NServiceBus.Community.Attachments.Sql.Raw


## Compared to the DataBus

This project delivers similar functionality to the [DataBus](https://docs.particular.net/nservicebus/messaging/databus/). However it does have some different behavior:


### Read on demand

With the DataBus all binary data is read every message received. This is irrespective of if the receiving endpoint requires that data. With NServiceBus.Attachments data is explicitly read on demand, so if data is not required there is no performance impact. NServiceBus.Attachments also supports processing all data items via an `IAsyncEnumerable`.


### Memory usage

With the DataBus all data items are placed into byte arrays. This means that memory needs to be allocated to store those arrays on either reading or writing. With NServiceBus.Attachments data can be streamed and processed in an async manner. This can significantly decrease the memory pressure on an endpoint.


### Variety of data APIs

With the DataBus the only interaction is via byte arrays. NServiceBus.Attachments supports reading and writing using streams, byte arrays, or string.


## SQL

[Full Docs](/docs/sql.md)


## FileShare

[Full Docs](/docs/fileshare.md)


## Benchmarks

Benchmark results for attachment persister operations at different data sizes. Results collected using [BenchmarkDotNet](https://benchmarkdotnet.org/).


### Hardware

 * BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update)
 * 12th Gen Intel Core i5-12600
 * .NET 10.0.5


### SQL Persister

Uses [LocalDb](https://github.com/SimonCropp/LocalDb) with a per-benchmark isolated database.

| Method | DataSize | Mean | Allocated |
|---|---|---:|---:|
| SaveStream | 1 KB | 11.16 ms | 27.93 KB |
| SaveBytes | 1 KB | 10.65 ms | 20.08 KB |
| SaveAndGetBytes | 1 KB | 16.23 ms | 37.65 KB |
| SaveAndCopyTo | 1 KB | 15.95 ms | 41.39 KB |
| SaveAndGetStream | 1 KB | 16.08 ms | 40.13 KB |
| SaveStream | 100 KB | 12.45 ms | 29.76 KB |
| SaveBytes | 100 KB | 12.92 ms | 26.77 KB |
| SaveAndGetBytes | 100 KB | 17.92 ms | 240.32 KB |
| SaveAndCopyTo | 100 KB | 17.98 ms | 52.17 KB |
| SaveAndGetStream | 100 KB | 17.89 ms | 47.76 KB |
| SaveStream | 1 MB | 35.21 ms | 91.52 KB |
| SaveBytes | 1 MB | 32.59 ms | 54.73 KB |
| SaveAndGetBytes | 1 MB | 40.55 ms | 2127.00 KB |
| SaveAndCopyTo | 1 MB | 41.99 ms | 155.87 KB |
| SaveAndGetStream | 1 MB | 42.28 ms | 91.00 KB |
| SaveStream | 10 MB | 228.43 ms | 821.36 KB |
| SaveBytes | 10 MB | 227.29 ms | 517.84 KB |
| SaveAndGetBytes | 10 MB | 245.30 ms | 21044.09 KB |
| SaveAndCopyTo | 10 MB | 255.95 ms | 1330.73 KB |
| SaveAndGetStream | 10 MB | 250.83 ms | 711.31 KB |


### FileShare Persister

| Method | DataSize | Mean | Allocated |
|---|---|---:|---:|
| SaveStream | 1 KB | 0.62 ms | 67.73 KB |
| SaveBytes | 1 KB | 0.57 ms | 67.67 KB |
| SaveAndGetBytes | 1 KB | 0.90 ms | 70.37 KB |
| SaveAndCopyTo | 1 KB | 0.89 ms | 69.61 KB |
| SaveAndGetStream | 1 KB | 0.77 ms | 69.11 KB |
| SaveStream | 100 KB | 0.63 ms | 3.45 KB |
| SaveBytes | 100 KB | 0.58 ms | 4.30 KB |
| SaveAndGetBytes | 100 KB | 0.88 ms | 105.08 KB |
| SaveAndCopyTo | 100 KB | 0.89 ms | 5.32 KB |
| SaveAndGetStream | 100 KB | 0.80 ms | 4.82 KB |
| SaveStream | 1 MB | 0.78 ms | 3.45 KB |
| SaveBytes | 1 MB | 0.87 ms | 3.38 KB |
| SaveAndGetBytes | 1 MB | 1.16 ms | 1029.08 KB |
| SaveAndCopyTo | 1 MB | 1.28 ms | 5.32 KB |
| SaveAndGetStream | 1 MB | 1.00 ms | 4.82 KB |
| SaveStream | 10 MB | 3.03 ms | 3.45 KB |
| SaveBytes | 10 MB | 2.96 ms | 3.38 KB |
| SaveAndGetBytes | 10 MB | 5.57 ms | 10245.08 KB |
| SaveAndCopyTo | 10 MB | 5.75 ms | 5.32 KB |
| SaveAndGetStream | 10 MB | 3.29 ms | 4.82 KB |


### Key Insights

 * **FileShare is ~15-75x faster than SQL** for raw operations, as expected for local file I/O vs database round-trips.
 * **Streaming avoids double allocation on read**: At 10 MB, `SaveAndGetStream` allocates ~711 KB (SQL) / ~5 KB (FileShare), while `SaveAndGetBytes` allocates ~21 MB / ~10 MB (data copied into a byte array on top of the original).
 * **`SaveStream` keeps memory bounded**: At 10 MB, `SaveStream` allocates ~821 KB (SQL) / ~3.5 KB (FileShare), because data is streamed incrementally via `System.IO.Pipelines` with backpressure — never buffering the full payload.
 * **`SaveStream` vs `SaveBytes`**: Nearly identical wall-clock time, but `SaveBytes` allocates the full payload in memory while `SaveStream` stays bounded.


## Icon

[Gecko](https://thenounproject.com/term/gecko/258949/) designed by [Alex Podolsky](https://thenounproject.com/alphatoster/) from [The Noun Project](https://thenounproject.com/).