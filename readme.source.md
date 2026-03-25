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

 * BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.7058/22H2/2022Update)
 * Intel Core i9-9880H CPU 2.30GHz, 1 CPU, 16 logical and 8 physical cores
 * .NET SDK 10.0.200


### SQL Persister

Uses [LocalDb](https://github.com/SimonCropp/LocalDb) with a per-benchmark isolated database.

| Method | DataSize | Mean | Allocated |
|---|---|---:|---:|
| SaveStream | 1 KB | 14.46 ms | 24.59 KB |
| SaveBytes | 1 KB | 14.77 ms | 20.39 KB |
| SaveAndGetBytes | 1 KB | 23.99 ms | 38.22 KB |
| SaveAndCopyTo | 1 KB | 24.30 ms | 41.96 KB |
| SaveAndGetStream | 1 KB | 27.17 ms | 40.03 KB |
| SaveStream | 100 KB | 17.44 ms | 29.92 KB |
| SaveBytes | 100 KB | 16.74 ms | 25.06 KB |
| SaveAndGetBytes | 100 KB | 27.99 ms | 240.89 KB |
| SaveAndCopyTo | 100 KB | 28.76 ms | 57.79 KB |
| SaveAndGetStream | 100 KB | 28.00 ms | 45.36 KB |
| SaveStream | 1 MB | 106.83 ms | 44.37 KB |
| SaveBytes | 1 MB | 100.53 ms | 51.70 KB |
| SaveAndGetBytes | 1 MB | 123.98 ms | 2119.16 KB |
| SaveAndCopyTo | 1 MB | 125.54 ms | 143.88 KB |
| SaveAndGetStream | 1 MB | 129.32 ms | - |
| SaveStream | 10 MB | 931.08 ms | - |
| SaveBytes | 10 MB | 943.99 ms | - |
| SaveAndGetBytes | 10 MB | 1,105.97 ms | 20879.84 KB |
| SaveAndCopyTo | 10 MB | 1,157.36 ms | 1263.38 KB |
| SaveAndGetStream | 10 MB | 1,044.62 ms | 485.57 KB |


### FileShare Persister

| Method | DataSize | Mean | Allocated |
|---|---|---:|---:|
| SaveStream | 1 KB | 0.75 ms | 67.73 KB |
| SaveBytes | 1 KB | 0.63 ms | 67.67 KB |
| SaveAndGetBytes | 1 KB | 0.90 ms | 70.37 KB |
| SaveAndCopyTo | 1 KB | 0.88 ms | 69.61 KB |
| SaveAndGetStream | 1 KB | 0.86 ms | 69.11 KB |
| SaveStream | 100 KB | 0.68 ms | 3.45 KB |
| SaveBytes | 100 KB | 0.66 ms | 3.38 KB |
| SaveAndGetBytes | 100 KB | 0.93 ms | 105.08 KB |
| SaveAndCopyTo | 100 KB | 0.87 ms | 5.31 KB |
| SaveAndGetStream | 100 KB | 0.88 ms | 4.82 KB |
| SaveStream | 1 MB | 0.89 ms | 3.45 KB |
| SaveBytes | 1 MB | 0.88 ms | 3.38 KB |
| SaveAndGetBytes | 1 MB | 1.25 ms | 1029.07 KB |
| SaveAndCopyTo | 1 MB | 1.40 ms | 5.32 KB |
| SaveAndGetStream | 1 MB | 1.07 ms | 4.82 KB |
| SaveStream | 10 MB | 3.34 ms | 3.45 KB |
| SaveBytes | 10 MB | 3.42 ms | 3.38 KB |
| SaveAndGetBytes | 10 MB | 5.84 ms | 10245.08 KB |
| SaveAndCopyTo | 10 MB | 6.46 ms | 5.31 KB |
| SaveAndGetStream | 10 MB | 3.47 ms | 4.82 KB |


### Key Insights

 * **FileShare is ~20-270x faster than SQL** for raw operations, as expected for local file I/O vs database round-trips.
 * **Streaming keeps allocations flat**: At 10 MB, `SaveAndCopyTo` allocates 1.2 MB (SQL) / 5 KB (FileShare), while `SaveAndGetBytes` allocates 20.4 MB / 10 MB (the full data in a byte array).
 * **SQL save cost scales with data size**: ~15ms for 1 KB, ~17ms for 100 KB, ~104ms for 1 MB, ~937ms for 10 MB. FileShare stays under 4ms for all sizes up to 10 MB.
 * **`SaveStream` vs `SaveBytes`**: Nearly identical performance in both implementations. For SQL, the stream is passed directly to the `SqlParameter` with no intermediate copy.


## Icon

[Gecko](https://thenounproject.com/term/gecko/258949/) designed by [Alex Podolsky](https://thenounproject.com/alphatoster/) from [The Noun Project](https://thenounproject.com/).