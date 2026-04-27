---
title: Architecture
status: active
owner: @codex
priority: high
complexity: 5
created: 2026-04-26
updated: 2026-04-26
tags: [roadmap, architecture, csharp, blazor, multisite]
---

# Architecture

## Target Solution Layout

```text
NugsDownloader.sln
src/
  NugsDownloader.Web/              Blazor UI and ASP.NET Core host
    NugsDownloader.Web.csproj
    Components/
    Pages/
    Services/
    appsettings*.json
  NugsDownloader.App/              Application services and use cases
    NugsDownloader.App.csproj
    Abstractions/
    UseCases/
    Workflows/
  NugsDownloader.Domain/           Shared entities, provider contracts, state models
    NugsDownloader.Domain.csproj
    Entities/
    Providers/
    ValueObjects/
    State/
  NugsDownloader.Infrastructure/   HTTP, storage, crypto, filesystem, provider implementations
    NugsDownloader.Infrastructure.csproj
    Auth/
    Downloads/
    Persistence/
    Providers/
    Security/
    Filesystem/
tests/
  NugsDownloader.Domain.Tests/
  NugsDownloader.App.Tests/
  NugsDownloader.Infrastructure.Tests/
```

## Responsibilities

### `NugsDownloader.Web`

- Hosts the Blazor UI and any thin HTTP endpoints
- Renders login, provider selection, queue, and job history pages
- Owns browser-facing state only
- Connects UI actions to application services through injected services or API clients

### `NugsDownloader.App`

- Coordinates use cases such as login, queue creation, download start/stop, and resume
- Translates UI requests into domain operations
- Owns workflow logic that is not tied to a specific provider
- Defines application-level ports for storage, providers, and secret handling

### `NugsDownloader.Domain`

- Defines core entities such as `DownloadJob`, `FileState`, `ProviderAccount`, and `MediaItem`
- Defines provider contracts and capability metadata
- Holds validation rules and shared enums/value objects
- Remains free of infrastructure and UI references

### `NugsDownloader.Infrastructure`

- Implements HTTP clients, storage, encryption, and filesystem access
- Implements provider adapters for Nugs, LivePhish, and future sites
- Persists job history, credentials, and resume state
- Implements application ports defined by `NugsDownloader.App`

## Concrete Namespace Map

- `NugsDownloader.Web`
- `NugsDownloader.Web.Components`
- `NugsDownloader.Web.Pages`
- `NugsDownloader.Web.Services`
- `NugsDownloader.App.UseCases`
- `NugsDownloader.App.Workflows`
- `NugsDownloader.App.Abstractions`
- `NugsDownloader.Domain.Entities`
- `NugsDownloader.Domain.Providers`
- `NugsDownloader.Domain.State`
- `NugsDownloader.Domain.ValueObjects`
- `NugsDownloader.Infrastructure.Providers.Nugs`
- `NugsDownloader.Infrastructure.Providers.LivePhish`
- `NugsDownloader.Infrastructure.Persistence`
- `NugsDownloader.Infrastructure.Security`
- `NugsDownloader.Infrastructure.Filesystem`
- `NugsDownloader.Infrastructure.Downloads`

## Provider Contract

```csharp
public interface IMediaProvider
{
    string Id { get; }
    string DisplayName { get; }

    bool CanHandle(Uri uri);
    ProviderCapabilities Capabilities { get; }

    Task<AuthResult> AuthenticateAsync(Credentials credentials, CancellationToken ct);
    Task<MediaDiscoveryResult> DiscoverAsync(Uri uri, CancellationToken ct);
    Task<DownloadPlan> BuildDownloadPlanAsync(MediaDiscoveryResult discovery, DownloadPreferences preferences, CancellationToken ct);
    Task ExecuteDownloadAsync(DownloadPlan plan, IProgress<DownloadProgress> progress, CancellationToken ct);
}
```

## State Model

- `DownloadJob` tracks one user-requested download or queue item.
- `FileState` tracks what exists on disk and whether it is complete, partial, or resumable.
- `ProviderAccount` stores credentials/tokens scoped to a provider.
- `DownloadSession` records the current run, progress, and restart metadata.
- `ProviderCapabilities` exposes what the provider supports so the UI can adapt.

## Domain Model Draft

### `DownloadJob`

- `Id`
- `ProviderId`
- `SourceUrl`
- `DisplayName`
- `Status`
- `RequestedAt`
- `StartedAt`
- `CompletedAt`
- `ErrorMessage`
- `OutputPath`

### `FileState`

- `Id`
- `JobId`
- `FilePath`
- `Kind` `audio|video|metadata|artwork`
- `Status`
- `ExpectedSize`
- `ActualSize`
- `Checksum`
- `LastVerifiedAt`

### `ProviderAccount`

- `Id`
- `ProviderId`
- `Label`
- `Username`
- `SecretRef`
- `AuthState`
- `LastVerifiedAt`

### `DownloadSession`

- `Id`
- `JobId`
- `ProviderId`
- `StartedAt`
- `LastProgressAt`
- `DownloadedBytes`
- `TotalBytes`
- `PercentComplete`
- `CurrentItem`
- `TotalItems`

### `ProviderCapabilities`

- `SupportsAudio`
- `SupportsVideo`
- `SupportsChapters`
- `SupportsResume`
- `SupportsTokens`
- `SupportsPasswordLogin`
- `SupportedFormats`
- `SupportedResolutions`

### `MediaDiscoveryResult`

- `ProviderId`
- `SourceUrl`
- `CanonicalUrl`
- `Title`
- `ArtistName`
- `Items`
- `HasVideo`
- `HasAudio`
- `Metadata`

### `DownloadPlan`

- `ProviderId`
- `JobId`
- `Items`
- `OutputRoot`
- `Preferences`
- `ExpectedFiles`
- `ResumeState`

### `DownloadPreferences`

- `PreferredAudioFormat`
- `PreferredVideoResolution`
- `SkipVideos`
- `SkipChapters`
- `ForceVideo`
- `OutputRoot`
- `WriteMetadata`
- `WriteArtwork`

### `AuthResult`

- `Success`
- `ProviderId`
- `SecretRef`
- `DisplayName`
- `ExpiresAt`
- `Message`

## Persistence

Start with SQLite unless there is a strong reason to stay file-based.

- Use SQLite for job history, file-state indexing, and resume metadata.
- Keep encrypted secret material separate from the job tables.
- Keep file system checks authoritative for whether a file actually exists.

## UI/Backend Split

- Blazor pages manage login, provider selection, queues, and job history.
- Application services own all provider calls and file-state updates.
- Background workers handle long-running download work.
- UI state should be derived from persisted job/session records instead of ephemeral component state.

## Initial Project Creation Commands

```bash
dotnet new sln -n NugsDownloader
dotnet new blazor -n NugsDownloader.Web -o src/NugsDownloader.Web
dotnet new classlib -n NugsDownloader.App -o src/NugsDownloader.App
dotnet new classlib -n NugsDownloader.Domain -o src/NugsDownloader.Domain
dotnet new classlib -n NugsDownloader.Infrastructure -o src/NugsDownloader.Infrastructure
dotnet new xunit -n NugsDownloader.Domain.Tests -o tests/NugsDownloader.Domain.Tests
dotnet new xunit -n NugsDownloader.App.Tests -o tests/NugsDownloader.App.Tests
dotnet new xunit -n NugsDownloader.Infrastructure.Tests -o tests/NugsDownloader.Infrastructure.Tests
```

## Wiring Order

1. Create `Domain` and define the provider/state contracts.
2. Add `App` ports and use cases.
3. Add `Infrastructure` implementations for SQLite, crypto, HTTP, and providers.
4. Build `Web` on top of the app services.
5. Port Nugs end to end before adding LivePhish.
