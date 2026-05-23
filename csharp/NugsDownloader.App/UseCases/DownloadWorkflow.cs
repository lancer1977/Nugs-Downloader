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
            request.Preferences.OutputRoot,
            request.Credentials.Label,
            request.Credentials.Username,
            request.Preferences);
        await _jobRepository.SaveAsync(pendingJob, ct);
        var currentJob = pendingJob;

        try
        {
            var auth = await provider.AuthenticateAsync(request.Credentials, ct);
            if (!auth.Success)
            {
                var message = auth.Message ?? "Authentication failed.";
                await _jobRepository.SaveAsync(currentJob with
                {
                    Status = DownloadJobStatus.Failed,
                    CompletedAt = DateTimeOffset.UtcNow,
                    ErrorMessage = message
                }, CancellationToken.None);

                return new StartDownloadResult(jobId, provider.Id, "AuthFailed", message);
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
                request.Preferences.OutputRoot,
                request.Credentials.Label,
                request.Credentials.Username,
                request.Preferences);

            await _jobRepository.SaveAsync(job, ct);
            currentJob = job;

            var secret = FirstNonEmpty(request.Credentials.Password, request.Credentials.Token, auth.SecretRef);
            if (!string.IsNullOrWhiteSpace(secret))
            {
                var label = FirstNonEmpty(request.Credentials.Label, auth.DisplayName, request.Credentials.Username, provider.DisplayName) ?? "default";
                var secretRef = await _secretVault.StoreAsync(provider.Id, label, secret, ct);
                var existing = await _credentialStore.GetAsync(provider.Id, label, ct);
                var account = new ProviderAccount(
                    existing?.Id ?? Guid.NewGuid(),
                    provider.Id,
                    label,
                    request.Credentials.Username ?? string.Empty,
                    secretRef,
                    AuthenticationState.Valid,
                    DateTimeOffset.UtcNow);

                await _credentialStore.SaveAsync(account, ct);
            }

            var plan = await provider.BuildDownloadPlanAsync(discovery, request.Preferences, ct);
            foreach (var expectedFile in plan.ExpectedFiles)
            {
                await _fileStateRepository.SaveAsync(expectedFile with
                {
                    JobId = jobId,
                    Status = FileStatus.Partial
                }, ct);
            }

            await provider.ExecuteDownloadAsync(plan, progress, ct);

            foreach (var expectedFile in plan.ExpectedFiles)
            {
                await _fileStateRepository.SaveAsync(expectedFile with
                {
                    JobId = jobId,
                    Status = FileStatus.Complete,
                    LastVerifiedAt = DateTimeOffset.UtcNow
                }, ct);
            }

            await _jobRepository.SaveAsync(job with
            {
                Status = DownloadJobStatus.Completed,
                CompletedAt = DateTimeOffset.UtcNow
            }, ct);

            return new StartDownloadResult(jobId, provider.Id, "Completed", $"Processed {discovery.Title}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await _jobRepository.SaveAsync(currentJob with
            {
                Status = DownloadJobStatus.Cancelled,
                CompletedAt = DateTimeOffset.UtcNow,
                ErrorMessage = "Download was cancelled."
            }, CancellationToken.None);

            return new StartDownloadResult(jobId, provider.Id, "Cancelled", "Download was cancelled.");
        }
        catch (Exception ex)
        {
            await _jobRepository.SaveAsync(currentJob with
            {
                Status = DownloadJobStatus.Failed,
                CompletedAt = DateTimeOffset.UtcNow,
                ErrorMessage = ex.Message
            }, CancellationToken.None);

            return new StartDownloadResult(jobId, provider.Id, "Failed", ex.Message);
        }
    }

    public async Task<StartDownloadResult> ResumeAsync(Guid jobId, CancellationToken ct)
    {
        var job = await _jobRepository.GetAsync(jobId, ct);
        if (job is null)
        {
            return new StartDownloadResult(jobId, string.Empty, "NotFound", "Job not found.");
        }

        var replayRequest = await BuildReplayRequestAsync(job, ct);
        if (replayRequest is not null)
        {
            return await StartAsync(replayRequest with { JobId = jobId }, new Progress<DownloadProgress>(), ct);
        }

        var fileStates = await _fileStateRepository.GetByJobAsync(jobId, ct);
        var partialStates = fileStates.Where(state => state.Status == FileStatus.Partial).ToArray();
        var resumableCount = 0;

        foreach (var state in partialStates)
        {
            var status = ResolveResumeStatus(state);
            if (status == FileStatus.Partial)
            {
                resumableCount++;
            }

            if (status != state.Status)
            {
                await _fileStateRepository.SaveAsync(state with
                {
                    Status = status,
                    LastVerifiedAt = DateTimeOffset.UtcNow
                }, ct);
            }
        }

        await _jobRepository.SaveAsync(job with
        {
            Status = resumableCount > 0 ? DownloadJobStatus.Running : DownloadJobStatus.Ready,
            ErrorMessage = null
        }, ct);

        var message = resumableCount > 0
            ? $"Resume prepared for {resumableCount} partial file(s)."
            : "No resumable partial files were found; job is ready to retry.";

        return new StartDownloadResult(jobId, job.ProviderId, "ResumePrepared", message);
    }

    public async Task<StartDownloadResult> RetryAsync(Guid jobId, CancellationToken ct)
    {
        var job = await _jobRepository.GetAsync(jobId, ct);
        if (job is null)
        {
            return new StartDownloadResult(jobId, string.Empty, "NotFound", "Job not found.");
        }

        var replayRequest = await BuildReplayRequestAsync(job, ct);
        if (replayRequest is not null)
        {
            return await StartAsync(replayRequest with { JobId = jobId }, new Progress<DownloadProgress>(), ct);
        }

        await _jobRepository.SaveAsync(job with
        {
            Status = DownloadJobStatus.Ready,
            CompletedAt = null,
            ErrorMessage = null
        }, ct);

        return new StartDownloadResult(jobId, job.ProviderId, "RetryQueued", "Job is ready to retry.");
    }

    private async Task<StartDownloadRequest?> BuildReplayRequestAsync(DownloadJob job, CancellationToken ct)
    {
        var provider = _providerCatalog.FindById(job.ProviderId);
        if (provider is null)
        {
            return null;
        }

        var account = await ResolveReplayAccountAsync(job.ProviderId, job.CredentialLabel, job.CredentialUsername, ct);
        if (account is null)
        {
            return null;
        }

        var secret = await _secretVault.GetAsync(account.SecretRef, ct);
        if (string.IsNullOrWhiteSpace(secret))
        {
            return null;
        }

        var preferences = job.Preferences ?? new DownloadPreferences(
            null,
            null,
            false,
            false,
            false,
            job.OutputPath,
            true,
            true);

        var credentials = new Credentials(
            account.Username,
            secret,
            null,
            account.Label);

        return new StartDownloadRequest(job.Id, provider.Id, job.SourceUrl, credentials, preferences);
    }

    private async Task<ProviderAccount?> ResolveReplayAccountAsync(string providerId, string? label, string? username, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(label))
        {
            var labeledAccount = await _credentialStore.GetAsync(providerId, label, ct);
            if (labeledAccount is not null)
            {
                return labeledAccount;
            }
        }

        var accounts = await _credentialStore.ListAsync(ct);
        return accounts
            .Where(account => account.ProviderId == providerId)
            .Where(account => string.IsNullOrWhiteSpace(username) || string.Equals(account.Username, username, StringComparison.OrdinalIgnoreCase))
            .Where(account => account.AuthState != AuthenticationState.Invalid)
            .OrderByDescending(account => account.LastVerifiedAt ?? DateTimeOffset.MinValue)
            .FirstOrDefault()
            ?? accounts
                .Where(account => account.ProviderId == providerId)
                .Where(account => account.AuthState != AuthenticationState.Invalid)
                .OrderByDescending(account => account.LastVerifiedAt ?? DateTimeOffset.MinValue)
                .FirstOrDefault();
    }

    private static FileStatus ResolveResumeStatus(FileState state)
    {
        if (!File.Exists(state.FilePath))
        {
            return FileStatus.Missing;
        }

        var actualSize = new FileInfo(state.FilePath).Length;
        if (state.ExpectedSize > 0 && actualSize != state.ActualSize)
        {
            return FileStatus.Stale;
        }

        if (state.LastVerifiedAt is not null && state.LastVerifiedAt < DateTimeOffset.UtcNow.AddHours(-24))
        {
            return FileStatus.Stale;
        }

        return FileStatus.Partial;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
