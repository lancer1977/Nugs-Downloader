using NugsDownloader.Domain.ValueObjects;
using Xunit;

namespace NugsDownloader.Tests;

public class MediaItemTests
{
    [Fact]
    public void MediaItem_StoresMetadata()
    {
        var item = new MediaItem(
            "m1",
            "Track One",
            "audio",
            1,
            new Dictionary<string, string>
            {
                ["artist"] = "Alice",
                ["label"] = "AL1",
            });

        Assert.Equal("m1", item.Id);
        Assert.Equal("Track One", item.DisplayName);
        Assert.Equal("audio", item.Kind);
        Assert.Equal(1, item.Index);
        Assert.Equal("Alice", item.Metadata["artist"]);
    }

    [Fact]
    public void MediaItem_WithReturnsUpdatedMetadata()
    {
        var item = new MediaItem("m1", "Track One", "audio", 1, new Dictionary<string, string>());
        var updated = item with { DisplayName = "Track One (Remaster)" };

        Assert.Equal("Track One (Remaster)", updated.DisplayName);
        Assert.Equal(item.Id, updated.Id);
        Assert.Equal(item.Index, updated.Index);
    }
}
