using EmojiPick.Models;
using EmojiPick.Services;
using Xunit;

namespace EmojiPick.Tests;

public class EmojiMatcherTests : IDisposable
{
    public EmojiMatcherTests()
    {
        EmojiMatcher.SetEmojisForTesting(new List<EmojiEntry>());
    }

    public void Dispose()
    {
        EmojiMatcher.SetEmojisForTesting(new List<EmojiEntry>());
    }

    private static List<EmojiEntry> MakeTestEmojis() =>
        new List<EmojiEntry>
        {
            new() { Char = "😀", Name = "grinning face",          Tags = new List<string> { "happy", "smile", "grin" },              Category = "smileys" },
            new() { Char = "😂", Name = "face with tears of joy", Tags = new List<string> { "laugh", "lol", "funny" },               Category = "smileys" },
            new() { Char = "❤️", Name = "red heart",              Tags = new List<string> { "love", "heart", "red" },                Category = "symbols" },
            new() { Char = "👍", Name = "thumbs up",              Tags = new List<string> { "good", "ok", "approve", "yes" },        Category = "people" },
            new() { Char = "🔥", Name = "fire",                   Tags = new List<string> { "fire", "hot", "lit", "flame" },         Category = "objects" },
            new() { Char = "🎉", Name = "party popper",           Tags = new List<string> { "party", "celebrate", "confetti" },      Category = "activities" },
            new() { Char = "😢", Name = "crying face",            Tags = new List<string> { "sad", "cry", "tear", "unhappy" },       Category = "smileys" },
            new() { Char = "🌹", Name = "rose",                   Tags = new List<string> { "rose", "flower", "love", "romantic" },  Category = "nature" },
            new() { Char = "😎", Name = "smiling with sunglasses",Tags = new List<string> { "cool", "sunglasses", "awesome" },       Category = "smileys" },
            new() { Char = "🚀", Name = "rocket",                 Tags = new List<string> { "rocket", "space", "launch" },           Category = "travel" },
            new() { Char = "👏", Name = "clapping hands",         Tags = new List<string> { "clap", "applause", "well done" },       Category = "people" },
            new() { Char = "🐱", Name = "cat face",               Tags = new List<string> { "cat", "kitten", "meow" },               Category = "animals" },
            new() { Char = "🍕", Name = "pizza",                  Tags = new List<string> { "pizza", "food", "italian" },            Category = "food" },
        };

    // ── Embedded resource — graceful degradation ────────────────────────────

    [Fact]
    public void LoadFromEmbeddedResource_PlaceholderData_DoesNotThrow()
    {
        EmojiMatcher.SetEmojisForTesting(null);  // force real resource load
        var ex = Record.Exception(() => EmojiMatcher.GetMatches("happy"));
        Assert.Null(ex);
    }

    [Fact]
    public void LoadFromEmbeddedResource_PlaceholderData_ReturnsEmptyMatches()
    {
        EmojiMatcher.SetEmojisForTesting(null);
        Assert.Empty(EmojiMatcher.GetMatches("happy"));
    }

    // ── Empty / whitespace text ─────────────────────────────────────────────

    [Fact]
    public void GetMatches_EmptyText_ReturnsEmpty()
    {
        EmojiMatcher.SetEmojisForTesting(MakeTestEmojis());
        Assert.Empty(EmojiMatcher.GetMatches(""));
    }

    [Fact]
    public void GetMatches_WhitespaceText_ReturnsEmpty()
    {
        EmojiMatcher.SetEmojisForTesting(MakeTestEmojis());
        Assert.Empty(EmojiMatcher.GetMatches("   "));
    }

    // ── Matching quality ────────────────────────────────────────────────────

    [Fact]
    public void GetMatches_BestMatchIsFirst()
    {
        EmojiMatcher.SetEmojisForTesting(MakeTestEmojis());
        var results = EmojiMatcher.GetMatches("love");
        Assert.NotEmpty(results);
        Assert.True(results[0].FuzzyScore >= results[^1].FuzzyScore);
    }

    [Fact]
    public void GetMatches_ResultsSortedByScoreDescending()
    {
        EmojiMatcher.SetEmojisForTesting(MakeTestEmojis());
        var results = EmojiMatcher.GetMatches("happy");
        Assert.NotEmpty(results);
        for (int i = 1; i < results.Count; i++)
            Assert.True(results[i].FuzzyScore <= results[i - 1].FuzzyScore);
    }

    // ── maxResults / threshold ──────────────────────────────────────────────

    [Fact]
    public void GetMatches_RespectsMaxResults()
    {
        EmojiMatcher.SetEmojisForTesting(MakeTestEmojis());
        // "love" is an exact tag on ❤️ and 🌹 — without cap we'd get 2+
        var results = EmojiMatcher.GetMatches("love", maxResults: 1);
        Assert.Equal(1, results.Count);
    }

    [Fact]
    public void GetMatches_ThresholdFiltersLowScores()
    {
        EmojiMatcher.SetEmojisForTesting(MakeTestEmojis());
        var results = EmojiMatcher.GetMatches("happy", threshold: 95);
        Assert.All(results, m => Assert.True(m.FuzzyScore >= 95));
    }

    // ── GetPopularEmoji ─────────────────────────────────────────────────────

    [Fact]
    public void GetPopularEmoji_EmptyDatabase_ReturnsFallback()
    {
        // Constructor already injected an empty list
        var popular = EmojiMatcher.GetPopularEmoji();
        Assert.NotEmpty(popular);
    }

    [Fact]
    public void GetPopularEmoji_WithData_PrefersSmileysAndPeople()
    {
        EmojiMatcher.SetEmojisForTesting(MakeTestEmojis());
        var popular = EmojiMatcher.GetPopularEmoji();
        Assert.NotEmpty(popular);
        // First entries must come from smileys or people categories
        var first = popular[0];
        Assert.True(
            string.Equals(first.Category, "smileys", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(first.Category, "people", StringComparison.OrdinalIgnoreCase));
    }

    // ── Cache ───────────────────────────────────────────────────────────────

    [Fact]
    public void GetMatches_SameQuery_ReturnsCachedReference()
    {
        EmojiMatcher.SetEmojisForTesting(MakeTestEmojis());
        var first  = EmojiMatcher.GetMatches("happy");
        var second = EmojiMatcher.GetMatches("happy");
        Assert.Same(first, second);
    }
}
