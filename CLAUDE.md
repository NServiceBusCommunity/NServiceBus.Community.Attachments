# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

NServiceBus.Community.Attachments provides streaming-based attachment functionality for NServiceBus messaging. It offers two implementations:
- **FileShare**: Stores attachments on a network file share
- **SQL**: Stores attachments in SQL Server using `varbinary` columns

This is a community-backed extension requiring OpenCollective patronage.

## Build Commands

```bash
# Build the solution
dotnet build src --configuration Release

# Run all tests (use --project, not positional arg)
dotnet test --project src/Attachments.Sql.Tests --configuration Release
dotnet test --project src/Attachments.FileShare.Tests --configuration Release

# Run a single test by name
dotnet test --project src/Attachments.Sql.Tests --configuration Release --filter "FullyQualifiedName~TestClassName.TestMethodName"
```

**Prerequisites**:
- .NET SDK 10.0.102 (preview) - specified in `src/global.json`
- SQL Server LocalDB for SQL tests (via [LocalDb](https://github.com/SimonCropp/LocalDb))

## Project Structure

```
src/
├── Attachments.FileShare/      # FileShare implementation
├── Attachments.FileShare.Raw/  # Low-level FileShare APIs (no NServiceBus dependency)
├── Attachments.Sql/            # SQL Server implementation
├── Attachments.Sql.Raw/        # Low-level SQL APIs (no NServiceBus dependency)
├── Shared/                     # Shared code (as a shared project, not compiled separately)
├── Helpers/                    # Utility library
├── *.Tests/                    # Test projects
└── *.Sample/                   # Sample/example projects
```

## Architecture

### Pipeline Integration
Both implementations integrate with NServiceBus via pipeline behaviors:
- `AttachmentFeature` - Feature registration with NServiceBus
- `SendBehavior` / `ReceiveBehavior` - Pipeline behaviors for outgoing/incoming messages
- Extension methods on `EndpointConfiguration` for setup (`EnableAttachments`)
- Extension methods on `IMessageHandlerContext` for handler access (`context.Attachments()`)

### Key Patterns
- **Partial classes**: Persister logic is split across files (e.g., `Persister_Save.cs`, `Persister_Get.cs`)
- **Pipe-based streaming**: `AddStream` uses `System.IO.Pipelines` to stream data to storage with backpressure. Writer runs on a background thread via `Task.Run`; shared setup is in `PipeHelper.StartWriter` (`Shared/PipeHelper.cs`)
- **Automatic cleanup**: `AsyncTimer` runs periodic cleanup based on `Expiry` column/metadata

### Internal Organization
Each implementation follows this structure:
- `Incoming/` - Reading attachments in handlers
- `Outgoing/` - Writing attachments when sending messages
- `Persister/` - Storage implementation
- SQL also has `Install/` for database schema scripts

## Testing

- Framework: **TUnit** with **Verify** for snapshot testing
- Integration tests (in `IntegrationTests/`) are excluded from Release builds
- Documentation snippets use `// snippet:` comments and are extracted by MarkdownSnippets

## Code Style

- C# preview language features enabled
- 4-space indentation, LF line endings
- Warnings as errors (`TreatWarningsAsErrors=true`)
- Central package management via `Directory.Packages.props`
- Uses `Cancel` as alias for `CancellationToken` (via global using)
- Uses `HandlerContext` as alias for `IMessageHandlerContext`

## Documentation

- `readme.md` is auto-generated from `readme.source.md` by MarkdownSnippets
- Documentation in `docs/` is similarly generated from `docs/mdsource/`
- Code snippets from tests are embedded using `<!-- snippet: SnippetName -->` markers
