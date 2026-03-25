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

 * BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037)
 * 12th Gen Intel Core i5-12600 3.30GHz, 1 CPU, 12 logical and 6 physical cores
 * .NET SDK 10.0.201


### SQL Persister

Uses [LocalDb](https://github.com/SimonCropp/LocalDb) with a per-benchmark isolated database.

| Method | DataSize | Mean | Allocated |
|---|---|---:|---:|
| SaveStream | 1 KB | 11.23 ms | 25.14 KB |
| SaveBytes | 1 KB | 11.07 ms | 20.94 KB |
| SaveAndGetBytes | 1 KB | 16.11 ms | 38.37 KB |
| SaveAndCopyTo | 1 KB | 17.09 ms | 42.11 KB |
| SaveAndGetStream | 1 KB | 17.14 ms | 40.85 KB |
| SaveStream | 100 KB | 13.67 ms | 129.47 KB |
| SaveBytes | 100 KB | 12.93 ms | 124.61 KB |
| SaveAndGetBytes | 100 KB | 18.00 ms | 340.04 KB |
| SaveAndCopyTo | 100 KB | 18.40 ms | 151.89 KB |
| SaveAndGetStream | 100 KB | 17.71 ms | 145.18 KB |
| SaveStream | 1 MB | 34.24 ms | 1099.01 KB |
| SaveBytes | 1 MB | 38.82 ms | 1081.27 KB |
| SaveAndGetBytes | 1 MB | 45.65 ms | 3145.98 KB |
| SaveAndCopyTo | 1 MB | 43.54 ms | 1187.44 KB |
| SaveAndGetStream | 1 MB | 42.48 ms | 1113.15 KB |
| SaveStream | 10 MB | 247.39 ms | 10903.38 KB |
| SaveBytes | 10 MB | 273.41 ms | 10729.63 KB |
| SaveAndGetBytes | 10 MB | 283.44 ms | 31193.08 KB |
| SaveAndCopyTo | 10 MB | 296.23 ms | 11559.48 KB |
| SaveAndGetStream | 10 MB | 282.45 ms | 10902.93 KB |


### FileShare Persister

| Method | DataSize | Mean | Allocated |
|---|---|---:|---:|
| SaveStream | 1 KB | 0.75 ms | 68.76 KB |
| SaveBytes | 1 KB | 0.75 ms | 68.70 KB |
| SaveAndGetBytes | 1 KB | 0.98 ms | 71.39 KB |
| SaveAndCopyTo | 1 KB | 1.07 ms | 70.63 KB |
| SaveAndGetStream | 1 KB | 0.87 ms | 70.13 KB |
| SaveStream | 100 KB | 0.71 ms | 103.47 KB |
| SaveBytes | 100 KB | 0.67 ms | 103.41 KB |
| SaveAndGetBytes | 100 KB | 0.95 ms | 205.10 KB |
| SaveAndCopyTo | 100 KB | 1.09 ms | 105.34 KB |
| SaveAndGetStream | 100 KB | 0.94 ms | 104.84 KB |
| SaveStream | 1 MB | 1.23 ms | 1027.47 KB |
| SaveBytes | 1 MB | 1.19 ms | 1027.41 KB |
| SaveAndGetBytes | 1 MB | 1.73 ms | 2053.10 KB |
| SaveAndCopyTo | 1 MB | 1.86 ms | 1029.34 KB |
| SaveAndGetStream | 1 MB | 1.46 ms | 1028.84 KB |
| SaveStream | 10 MB | 5.47 ms | 10243.47 KB |
| SaveBytes | 10 MB | 5.70 ms | 10243.41 KB |
| SaveAndGetBytes | 10 MB | 9.15 ms | 20485.10 KB |
| SaveAndCopyTo | 10 MB | 9.03 ms | 10245.34 KB |
| SaveAndGetStream | 10 MB | 5.87 ms | 10244.84 KB |


### Key Insights

 * **FileShare is ~15-50x faster than SQL** for raw operations, as expected for local file I/O vs database round-trips.
 * **Streaming avoids double allocation on read**: At 10 MB, `SaveAndGetStream` allocates ~10 MB (the data itself), while `SaveAndGetBytes` allocates ~20-31 MB (data copied into a byte array on top of the original).
 * **SQL save cost scales with data size**: ~11ms for 1 KB, ~13ms for 100 KB, ~36ms for 1 MB, ~260ms for 10 MB. FileShare stays under 6ms for all sizes up to 10 MB.
 * **`SaveStream` vs `SaveBytes`**: Nearly identical performance in both implementations.


## Icon

[Gecko](https://thenounproject.com/term/gecko/258949/) designed by [Alex Podolsky](https://thenounproject.com/alphatoster/) from [The Noun Project](https://thenounproject.com/).