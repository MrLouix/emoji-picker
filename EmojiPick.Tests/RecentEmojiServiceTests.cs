using System.IO;
using EmojiPick.Services;
using Xunit;

namespace EmojiPick.Tests;

public class RecentEmojiServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"emojipick-recent-{Guid.NewGuid():N}.json");
    private readonly string _originalPath = RecentEmojiService.FilePath;

    public RecentEmojiServiceTests()
    {
        RecentEmojiService.FilePath = _path;
        RecentEmojiService.ResetForTesting();
    }

    public void Dispose()
    {
        RecentEmojiService.FilePath = _originalPath;
        RecentEmojiService.ResetForTesting();
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void Add_MovesEmojiToFrontWithoutDuplicates()
    {
        RecentEmojiService.Add("🔥");
        RecentEmojiService.Add("🍕");
        RecentEmojiService.Add("🔥");

        Assert.Equal(new[] { "🔥", "🍕" }, RecentEmojiService.GetRecent());
    }

    [Fact]
    public void Add_PersistsAcrossReloads()
    {
        RecentEmojiService.Add("🚀");
        RecentEmojiService.ResetForTesting();

        Assert.Equal(new[] { "🚀" }, RecentEmojiService.GetRecent());
    }

    [Fact]
    public void Add_KeepsAtMostMaxCount()
    {
        for (int i = 0; i < RecentEmojiService.MaxCount + 5; i++)
            RecentEmojiService.Add($"e{i}");

        Assert.Equal(RecentEmojiService.MaxCount, RecentEmojiService.GetRecent().Count);
        Assert.Equal($"e{RecentEmojiService.MaxCount + 4}", RecentEmojiService.GetRecent()[0]);
    }
}
