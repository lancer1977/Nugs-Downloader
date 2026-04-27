using NugsDownloader.Web.Services;
using Xunit;

namespace NugsDownloader.Tests;

public class JsonRepositoryTests
{
    [Fact]
    public async Task JsonFileRepository_RoundTripsLists()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "items.json");
        var repo = new TestJsonListRepository(path);
        var original = new List<TestItem>
        {
            new("1", "one"),
            new("2", "two")
        };

        await repo.SaveAsync(original, CancellationToken.None);

        var roundTrip = await repo.LoadAsync(CancellationToken.None);

        Assert.Equal(original, roundTrip);
    }

    [Fact]
    public async Task JsonFileRepository_RoundTripsDictionaries()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "items.json");
        var repo = new TestJsonDictionaryRepository(path);
        var original = new Dictionary<string, string>
        {
            ["a"] = "alpha",
            ["b"] = "bravo"
        };

        await repo.SaveAsync(original, CancellationToken.None);

        var roundTrip = await repo.LoadAsync(CancellationToken.None);

        Assert.Equal(original, roundTrip);
    }

    private sealed class TestJsonListRepository : JsonFileRepository
    {
        public TestJsonListRepository(string path) : base(path) { }

        public Task SaveAsync(List<TestItem> items, CancellationToken ct) => WriteAsync(items, ct);
        public Task<List<TestItem>> LoadAsync(CancellationToken ct) => ReadAsync(new List<TestItem>(), ct);
    }

    private sealed class TestJsonDictionaryRepository : JsonFileRepository
    {
        public TestJsonDictionaryRepository(string path) : base(path) { }

        public Task SaveAsync(Dictionary<string, string> items, CancellationToken ct) => WriteAsync(items, ct);
        public Task<Dictionary<string, string>> LoadAsync(CancellationToken ct) => ReadAsync(new Dictionary<string, string>(), ct);
    }

    private sealed record TestItem(string Id, string Name);
}
