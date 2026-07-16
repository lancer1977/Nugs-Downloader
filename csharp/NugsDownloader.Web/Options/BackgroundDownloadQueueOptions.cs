namespace NugsDownloader.Web.Options;

public sealed class BackgroundDownloadQueueOptions
{
    public int Capacity { get; set; } = 2;
    public int Concurrency { get; set; } = 1;
    public int LeaseSeconds { get; set; } = 300;
    public int RetryDelaySeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
}
