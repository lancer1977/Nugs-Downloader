using NugsDownloader.App.Abstractions;
using NugsDownloader.App.UseCases;
using NugsDownloader.Infrastructure.Providers.Nugs;
using NugsDownloader.Web.Components;
using NugsDownloader.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<NugsDownloader.Domain.Providers.IMediaProvider, NugsMediaProvider>();
builder.Services.AddSingleton<IProviderCatalog, InMemoryProviderCatalog>(sp =>
    new InMemoryProviderCatalog(sp.GetServices<NugsDownloader.Domain.Providers.IMediaProvider>()));
builder.Services.AddSingleton<IJobRepository, JsonJobRepository>();
builder.Services.AddSingleton<IFileStateRepository, JsonFileStateRepository>();
builder.Services.AddSingleton<ICredentialStore, JsonCredentialStore>();
builder.Services.AddSingleton<ISecretVault, JsonSecretVault>();
builder.Services.AddScoped<IDownloadWorkflow, DownloadWorkflow>();

var app = builder.Build();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();
