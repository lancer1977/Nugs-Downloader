using NugsDownloader.App.Abstractions;
using NugsDownloader.Domain.Entities;
using NugsDownloader.Domain.Providers;
using NugsDownloader.Domain.ValueObjects;

namespace NugsDownloader.App.UseCases;

public sealed class DownloadWorkflow : IDownloadWorkflow
{
    private readonly IProviderCatalog _providerCatalog;
    private readonly IJobRepository _jobRepository;
    private readonly IFileStateRepository _fileStateRepository;
    private readonly ICredentialStore _credentialStore;
    private readonly ISecretVault _secretVault;

    public DownloadWorkflow(
        IProviderCatalog providerCatalog,
        IJobRepository jobRepository,
        IFileStateRepository fileStateRepository,
        ICredentialStore credentialStore,
        ISecretVault secretVault)
    {
        _providerCatalog = providerCatalog;
        _jobRepository = jobRepository;
        _fileStateRepository = fileStateRepository;
        _credentialStore = credentialStore;
        _secretVault = secretVault;
    }

    public async Task<StartDownloadResult> StartAsync(
        StartDownloadRequest request,
        IProgress<DownloadProgress> progress,
        CancellationToken ct)
    {
        var provider = !string.IsNullOrWhiteSpace(request.ProviderId)
            ? _providerCatalog.FindById(request.ProviderId)
            : _providerCatalog.FindByUrl(request.SourceUrl);

        if (provider is null)
        {
            throw new InvalidOperationException($"No provider can handle {request.SourceUrl}");
        }

        var jobId = request.JobId ?? Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;

        var pendingJob = new DownloadJob(
            jobId,
            provider.Id,
            request.SourceUrl,
            request.SourceUrl.ToString(),
            DownloadJobStatus.Discovering,
            startedAt,
            startedAt,
            null,
            null,
            request.Preferences.OutputRoot);
        await _jobRepository.SaveAsync(pendingJob, ct);

        var auth = await provider.AuthenticateAsync(request.Credentials, ct);
        if (!auth.Success)
        {
            return new StartDownloadResult(
                jobId,
                provider.Id,
                "AuthFailed",
                auth.Message ?? "Authentication failed.");
        }

        var discovery = await provider.DiscoverAsync(request.SourceUrl, ct);

        var job = new DownloadJob(
            jobId,
            provider.Id,
            request.SourceUrl,
            discovery.Title,
            DownloadJobStatus.Running,
            startedAt,
            startedAt,
            null,
            null,
            request.Preferences.OutputRoot);

        await _jobRepository.SaveAsync(job, ct);

        if (auth.SecretRef is not null)
        {
            var account = new ProviderAccount(
                Guid.NewGuid(),
                provider.Id,
                auth.DisplayName ?? provider.DisplayName,
                request.Credentials.Username ?? string.Empty,
                auth.SecretRef,
                AuthenticationState.Valid,
                DateTimeOffset.UtcNow);

            await _credentialStore.SaveAsync(account, ct);
        }
        else if (!string.IsNullOrWhiteSpace(request.Credentials.Password) || !string.IsNullOrWhiteSpace(request.Credentials.Token))
        {
            var secretValue = request.Credentials.Token ?? request.Credentials.Password ?? string.Empty;
            var secretRef = await _secretVault.StoreAsync(provider.Id, request.Credentials.Label ?? request.Credentials.Username ?? "default", secretValue, ct);
            var account = new ProviderAccount(
                Guid.NewGuid(),
                provider.Id,
                auth.DisplayName ?? provider.DisplayName,
                request.Credentials.Username ?? string.Empty,
                secretRef,
                AuthenticationState.Valid,
                DateTimeOffset.UtcNow);

            await _credentialStore.SaveAsync(account, ct);
        }

        var plan = await provider.BuildDownloadPlanAsync(discovery, request.Preferences, ct);
        await provider.ExecuteDownloadAsync(plan, progress, ct);

        await _fileStateRepository.SaveAsync(
            new FileState(
                Guid.NewGuid(),
                jobId,
                Path.Combine(request.Preferences.OutputRoot, discovery.Title),
                FileKind.Metadata,
                FileStatus.Complete,
                0,
                0,
                null,
                DateTimeOffset.UtcNow),
            ct);

        await _jobRepository.SaveAsync(job with
        {
            Status = DownloadJobStatus.Completed,
            CompletedAt = DateTimeOffset.UtcNow
        }, ct);

        return new StartDownloadResult(jobId, provider.Id, "Completed", $"Processed {discovery.Title}");
    }
}
