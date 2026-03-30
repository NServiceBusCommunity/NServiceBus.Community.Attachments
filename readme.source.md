# <img src="/src/icon.png" height="25px"> NServiceBus.Community.Attachments

[![Build status](https://img.shields.io/appveyor/build/SimonCropp/nservicebus-community-attachments)](https://ci.appveyor.com/project/SimonCropp/nservicebus-community-attachments)
[![NuGet Status](https://img.shields.io/nuget/v/NServiceBus.Community.Attachments.FileShare.svg?label=Attachments.FileShare)](https://www.nuget.org/packages/NServiceBus.Community.Attachments.FileShare/)
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
| SaveStream | 1 KB | 15.83 ms | 25.47 KB |
| SaveBytes | 1 KB | 12.16 ms | 24.22 KB |
| SaveAndGetBytes | 1 KB | 15.73 ms | 42.42 KB |
| SaveString | 1 KB | 12.20 ms | 31.27 KB |
| SaveAndGetString | 1 KB | 17.12 ms | 54.88 KB |
| SaveAndCopyTo | 1 KB | 15.96 ms | 169.28 KB |
| SaveAndGetStream | 1 KB | 16.80 ms | 40.73 KB |
| SaveStream | 100 KB | 14.66 ms | 33.52 KB |
| SaveBytes | 100 KB | 13.10 ms | 31.50 KB |
| SaveAndGetBytes | 100 KB | 16.46 ms | 245.75 KB |
| SaveString | 100 KB | 12.32 ms | 42.34 KB |
| SaveAndGetString | 100 KB | 17.44 ms | 465.50 KB |
| SaveAndCopyTo | 100 KB | 18.85 ms | 52.74 KB |
| SaveAndGetStream | 100 KB | 16.63 ms | 45.36 KB |
| SaveStream | 1 MB | 34.87 ms | 83.88 KB |
| SaveBytes | 1 MB | 36.09 ms | 75.03 KB |
| SaveAndGetBytes | 1 MB | 39.83 ms | 2131.96 KB |
| SaveString | 1 MB | 33.46 ms | 94.23 KB |
| SaveAndGetString | 1 MB | 41.83 ms | 4242.59 KB |
| SaveAndCopyTo | 1 MB | 43.12 ms | 162.23 KB |
| SaveAndGetStream | 1 MB | 40.57 ms | 98.41 KB |
| SaveStream | 10 MB | 228.46 ms | 855.34 KB |
| SaveBytes | 10 MB | 233.89 ms | 681.26 KB |
| SaveAndGetBytes | 10 MB | 251.26 ms | 21172.63 KB |
| SaveString | 10 MB | 232.86 ms | 922.85 KB |
| SaveAndGetString | 10 MB | 263.46 ms | 42007.71 KB |
| SaveAndCopyTo | 10 MB | 258.50 ms | 1342.72 KB |
| SaveAndGetStream | 10 MB | 250.11 ms | 685.62 KB |


### FileShare Persister

| Method | DataSize | Mean | Allocated |
|---|---|---:|---:|
| SaveStream | 1 KB | 0.61 ms | 67.70 KB |
| SaveBytes | 1 KB | 0.64 ms | 67.70 KB |
| SaveAndGetBytes | 1 KB | 0.81 ms | 70.39 KB |
| SaveString | 1 KB | 1.09 ms | 138.86 KB |
| SaveAndGetString | 1 KB | 1.56 ms | 218.89 KB |
| SaveAndCopyTo | 1 KB | 1.07 ms | 69.57 KB |
| SaveAndGetStream | 1 KB | 0.74 ms | 69.07 KB |
| SaveStream | 100 KB | 0.63 ms | 3.41 KB |
| SaveBytes | 100 KB | 0.63 ms | 3.41 KB |
| SaveAndGetBytes | 100 KB | 0.87 ms | 105.10 KB |
| SaveString | 100 KB | 0.93 ms | 139.13 KB |
| SaveAndGetString | 100 KB | 1.80 ms | 627.05 KB |
| SaveAndCopyTo | 100 KB | 1.02 ms | 5.28 KB |
| SaveAndGetStream | 100 KB | 0.82 ms | 4.78 KB |
| SaveStream | 1 MB | 1.04 ms | 3.41 KB |
| SaveBytes | 1 MB | 0.91 ms | 3.41 KB |
| SaveAndGetBytes | 1 MB | 1.19 ms | 1029.10 KB |
| SaveString | 1 MB | 1.50 ms | 141.00 KB |
| SaveAndGetString | 1 MB | 6.00 ms | 4361.76 KB |
| SaveAndCopyTo | 1 MB | 1.34 ms | 5.28 KB |
| SaveAndGetStream | 1 MB | 1.13 ms | 4.81 KB |
| SaveStream | 10 MB | 2.86 ms | 3.41 KB |
| SaveBytes | 10 MB | 2.82 ms | 3.41 KB |
| SaveAndGetBytes | 10 MB | 5.90 ms | 10245.10 KB |
| SaveString | 10 MB | 7.82 ms | 160.12 KB |
| SaveAndGetString | 10 MB | 49.35 ms | 41705.30 KB |
| SaveAndCopyTo | 10 MB | 5.84 ms | 5.28 KB |
| SaveAndGetStream | 10 MB | 3.07 ms | 4.78 KB |


### Key Insights

 * **FileShare is ~15-80x faster than SQL** for raw operations, as expected for local file I/O vs database round-trips.
 * **Streaming avoids double allocation on read**: At 10 MB, `SaveAndGetStream` allocates ~686 KB (SQL) / ~5 KB (FileShare), while `SaveAndGetBytes` allocates ~21 MB / ~10 MB (data copied into a byte array on top of the original).
 * **`SaveStream` keeps memory bounded**: At 10 MB, `SaveStream` allocates ~855 KB (SQL) / ~3.4 KB (FileShare), because data is streamed incrementally via `System.IO.Pipelines` with backpressure — never buffering the full payload.
 * **String round-trips are expensive**: `SaveAndGetString` at 10 MB allocates ~42 MB (SQL) / ~41 MB (FileShare) due to encoding/decoding overhead.
 * **`SaveStream` vs `SaveBytes`**: Nearly identical wall-clock time, but `SaveStream` memory stays bounded regardless of payload size.


## Icon

[Gecko](https://thenounproject.com/term/gecko/258949/) designed by [Alex Podolsky](https://thenounproject.com/alphatoster/) from [The Noun Project](https://thenounproject.com/).