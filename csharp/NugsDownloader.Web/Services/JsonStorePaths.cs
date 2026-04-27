namespace NugsDownloader.Web.Services;

public static class JsonStorePaths
{
    public static readonly string BaseDirectory = Path.Combine(AppContext.BaseDirectory, "state");
    public static readonly string Jobs = Path.Combine(BaseDirectory, "jobs.json");
    public static readonly string FileStates = Path.Combine(BaseDirectory, "file-states.json");
    public static readonly string Credentials = Path.Combine(BaseDirectory, "credentials.json");
    public static readonly string Secrets = Path.Combine(BaseDirectory, "secrets.json");
}
