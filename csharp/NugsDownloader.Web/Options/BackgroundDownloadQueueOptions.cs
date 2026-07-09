namespace NugsDownloader.Web.Options;

public sealed class BackgroundDownloadQueueOptions
{
    public int Capacity { get; set; } = 2;
    public int Concurrency { get; set; } = 1;
}
