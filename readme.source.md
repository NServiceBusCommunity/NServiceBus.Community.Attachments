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
| SaveStream | 1 KB | 14.70 ms | 25.16 KB |
| SaveBytes | 1 KB | 11.51 ms | 21.10 KB |
| SaveAndGetBytes | 1 KB | 16.30 ms | 38.67 KB |
| SaveAndCopyTo | 1 KB | 16.85 ms | 42.41 KB |
| SaveAndGetStream | 1 KB | 15.56 ms | 41.16 KB |
| SaveStream | 100 KB | 14.42 ms | 33.16 KB |
| SaveBytes | 100 KB | 12.64 ms | 124.77 KB |
| SaveAndGetBytes | 100 KB | 18.01 ms | 340.34 KB |
| SaveAndCopyTo | 100 KB | 17.79 ms | 152.20 KB |
| SaveAndGetStream | 100 KB | 17.68 ms | 145.48 KB |
| SaveStream | 1 MB | 41.25 ms | 94.20 KB |
| SaveBytes | 1 MB | 34.59 ms | 1100.77 KB |
| SaveAndGetBytes | 1 MB | 42.32 ms | 3164.34 KB |
| SaveAndCopyTo | 1 MB | 43.21 ms | 1180.41 KB |
| SaveAndGetStream | 1 MB | 42.30 ms | 1132.30 KB |
| SaveStream | 10 MB | 261.72 ms | 803.57 KB |
| SaveBytes | 10 MB | 228.35 ms | 10753.71 KB |
| SaveAndGetBytes | 10 MB | 247.43 ms | 31268.21 KB |
| SaveAndCopyTo | 10 MB | 256.46 ms | 11574.94 KB |
| SaveAndGetStream | 10 MB | 250.81 ms | 10903.18 KB |


### FileShare Persister

| Method | DataSize | Mean | Allocated |
|---|---|---:|---:|
| SaveStream | 1 KB | 0.64 ms | 68.76 KB |
| SaveBytes | 1 KB | 0.64 ms | 68.70 KB |
| SaveAndGetBytes | 1 KB | 0.83 ms | 71.39 KB |
| SaveAndCopyTo | 1 KB | 0.82 ms | 70.63 KB |
| SaveAndGetStream | 1 KB | 0.84 ms | 70.13 KB |
| SaveStream | 100 KB | 0.66 ms | 103.47 KB |
| SaveBytes | 100 KB | 0.67 ms | 103.41 KB |
| SaveAndGetBytes | 100 KB | 0.96 ms | 205.10 KB |
| SaveAndCopyTo | 100 KB | 0.93 ms | 105.34 KB |
| SaveAndGetStream | 100 KB | 0.84 ms | 104.84 KB |
| SaveStream | 1 MB | 1.07 ms | 1027.47 KB |
| SaveBytes | 1 MB | 0.96 ms | 1027.41 KB |
| SaveAndGetBytes | 1 MB | 1.53 ms | 2053.10 KB |
| SaveAndCopyTo | 1 MB | 1.43 ms | 1029.34 KB |
| SaveAndGetStream | 1 MB | 1.08 ms | 1028.84 KB |
| SaveStream | 10 MB | 4.44 ms | 10243.46 KB |
| SaveBytes | 10 MB | 4.72 ms | 10243.41 KB |
| SaveAndGetBytes | 10 MB | 7.57 ms | 20485.09 KB |
| SaveAndCopyTo | 10 MB | 7.45 ms | 10245.34 KB |
| SaveAndGetStream | 10 MB | 4.65 ms | 10244.84 KB |


### Key Insights

 * **FileShare is ~15-50x faster than SQL** for raw operations, as expected for local file I/O vs database round-trips.
 * **Streaming avoids double allocation on read**: At 10 MB, `SaveAndGetStream` allocates ~10 MB (the data itself), while `SaveAndGetBytes` allocates ~20-31 MB (data copied into a byte array on top of the original).
 * **`SaveStream` keeps memory bounded**: At 10 MB, `SaveStream` allocates only ~804 KB vs ~10.8 MB for `SaveBytes`, because data is streamed incrementally via `System.IO.Pipelines` with backpressure — never buffering the full payload.
 * **`SaveStream` vs `SaveBytes`**: Nearly identical performance in both implementations.


## Icon

[Gecko](https://thenounproject.com/term/gecko/258949/) designed by [Alex Podolsky](https://thenounproject.com/alphatoster/) from [The Noun Project](https://thenounproject.com/).