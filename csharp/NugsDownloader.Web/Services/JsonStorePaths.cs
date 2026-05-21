using Microsoft.Extensions.Options;
using NugsDownloader.Web.Options;

namespace NugsDownloader.Web.Services;

public sealed class JsonStorePaths
{
    public JsonStorePaths(IOptions<NugsDownloaderStorageOptions> options)
    {
        BaseDirectory = options.Value.GetStateDirectory();
        Jobs = Path.Combine(BaseDirectory, "jobs.json");
        FileStates = Path.Combine(BaseDirectory, "file-states.json");
        Credentials = Path.Combine(BaseDirectory, "credentials.json");
        Secrets = Path.Combine(BaseDirectory, "secrets.json");
    }

    public string BaseDirectory { get; }
    public string Jobs { get; }
    public string FileStates { get; }
    public string Credentials { get; }
    public string Secrets { get; }
}
