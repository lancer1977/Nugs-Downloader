using Microsoft.AspNetCore.DataProtection;
using System.Reflection;
using Microsoft.Extensions.FileProviders;
using NugsDownloader.App.Abstractions;
using NugsDownloader.App.UseCases;
using NugsDownloader.Web.Health;
using NugsDownloader.Infrastructure.Providers.LivePhish;
using NugsDownloader.Infrastructure.Providers.Nugs;
using NugsDownloader.Web.Components;
using NugsDownloader.Web.Options;
using NugsDownloader.Web.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<NugsDownloaderStorageOptions>(builder.Configuration.GetSection("NugsDownloader"));
var storageOptions = new NugsDownloaderStorageOptions();
builder.Configuration.GetSection("NugsDownloader").Bind(storageOptions);
var dataProtectionKeysDirectory = Path.Combine(storageOptions.GetStateDirectory(), "data-protection-keys");
Directory.CreateDirectory(dataProtectionKeysDirectory);
builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysDirectory))
    .SetApplicationName("NugsDownloader");

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddSingleton<NugsDownloader.Domain.Providers.IMediaProvider, NugsMediaProvider>();
builder.Services.AddSingleton<NugsDownloader.Domain.Providers.IMediaProvider, LivePhishMediaProvider>();
builder.Services.AddSingleton<IProviderCatalog, InMemoryProviderCatalog>(sp =>
    new InMemoryProviderCatalog(sp.GetServices<NugsDownloader.Domain.Providers.IMediaProvider>()));
builder.Services.AddSingleton<SqliteStorePaths>();
builder.Services.AddSingleton<SqliteStateStore>();
builder.Services.AddSingleton<IJobRepository, SqliteJobRepository>();
builder.Services.AddSingleton<IFileStateRepository, SqliteFileStateRepository>();
builder.Services.AddSingleton<ICredentialStore, SqliteCredentialStore>();
builder.Services.AddSingleton<ISecretVault, SqliteSecretVault>();
builder.Services.AddScoped<IDownloadWorkflow, DownloadWorkflow>();
builder.Services
    .AddHealthChecks()
    .AddCheck<StorageHealthCheck>("storage", tags: new[] { "ready" });

var app = builder.Build();

var mudBlazorStaticAssets = ResolveMudBlazorStaticAssetsPath();
if (mudBlazorStaticAssets is not null)
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(mudBlazorStaticAssets),
        RequestPath = "/_content/MudBlazor"
    });
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();

static string? ResolveMudBlazorStaticAssetsPath()
{
    var version = typeof(MudBlazor.MudComponentBase)
        .Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion
        .Split('+', StringSplitOptions.RemoveEmptyEntries)
        .FirstOrDefault();

    if (string.IsNullOrWhiteSpace(version))
    {
        return null;
    }

    var packageRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".nuget",
        "packages",
        "mudblazor",
        version,
        "staticwebassets");

    return Directory.Exists(packageRoot) ? packageRoot : null;
}

public partial class Program;
