using EmojiPick.Helpers;
using EmojiPick.Models;
using Xunit;

namespace EmojiPick.Tests;

public class FuzzyMatcherTests
{
    // ─── DistanceLevenshtein ──────────────────────────────────────────────────

    [Fact]
    public void Levenshtein_KittenSitting_Returns3()
    {
        Assert.Equal(3, FuzzyMatcher.DistanceLevenshtein("kitten", "sitting"));
    }

    [Fact]
    public void Levenshtein_EmptySourceAbcTarget_Returns3()
    {
        Assert.Equal(3, FuzzyMatcher.DistanceLevenshtein("", "abc"));
    }

    [Fact]
    public void Levenshtein_AbcSourceEmptyTarget_Returns3()
    {
        Assert.Equal(3, FuzzyMatcher.DistanceLevenshtein("abc", ""));
    }

    [Fact]
    public void Levenshtein_BothEmpty_Returns0()
    {
        Assert.Equal(0, FuzzyMatcher.DistanceLevenshtein("", ""));
    }

    [Fact]
    public void Levenshtein_IdenticalStrings_Returns0()
    {
        Assert.Equal(0, FuzzyMatcher.DistanceLevenshtein("abc", "abc"));
    }

    // ─── ComputeScore ─────────────────────────────────────────────────────────

    [Fact]
    public void ComputeScore_ExactMatch_Returns100()
    {
        Assert.Equal(100, FuzzyMatcher.ComputeScore("love", "love"));
    }

    [Fact]
    public void ComputeScore_PrefixOfTag_Returns90()
    {
        // "lov" est préfixe de "love" → branche prefix → 90
        Assert.Equal(90, FuzzyMatcher.ComputeScore("lov", "love"));
    }

    [Fact]
    public void ComputeScore_TextContainedInTag_AtLeast75()
    {
        // "happy face".StartsWith("happy") → branche prefix → 90 >= 75
        Assert.True(FuzzyMatcher.ComputeScore("happy", "happy face") >= 75);
    }

    // ─── GetMatches ───────────────────────────────────────────────────────────

    [Fact]
    public void GetMatches_BestMatchIsFirst()
    {
        var emojis = new List<EmojiEntry>
        {
            new() { Char = "😍", Tags = new List<string> { "love", "heart" } },
            new() { Char = "😊", Tags = new List<string> { "smile", "happy" } },
            new() { Char = "🎭", Tags = new List<string> { "theater", "drama" } },
            new() { Char = "🌹", Tags = new List<string> { "rose", "flower" } },
            new() { Char = "😢", Tags = new List<string> { "sad", "cry", "tear" } },
        };

        var results = FuzzyMatcher.GetMatches("love", emojis);

        Assert.NotEmpty(results);
        Assert.Equal("😍", results[0].Emoji.Char);
    }

    [Fact]
    public void GetMatches_ScoreBelowThreshold_NotIncluded()
    {
        // "xyz" contre des tags sans rapport → scores << 80
        var emojis = new List<EmojiEntry>
        {
            new() { Char = "🎲", Tags = new List<string> { "dice", "game", "luck" } },
        };

        var results = FuzzyMatcher.GetMatches("xyz", emojis, threshold: 80);

        Assert.Empty(results);
    }

    [Fact]
    public void GetMatches_EmptyText_ReturnsEmpty()
    {
        var emojis = new List<EmojiEntry>
        {
            new() { Char = "😀", Tags = new List<string> { "happy", "smile" } },
        };

        var results = FuzzyMatcher.GetMatches("", emojis);

        Assert.Empty(results);
    }

    [Fact]
    public void GetMatches_NullEmojis_ReturnsEmpty()
    {
        var results = FuzzyMatcher.GetMatches("love", null!);

        Assert.Empty(results);
    }
}
