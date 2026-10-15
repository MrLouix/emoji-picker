using EmojiPick.Models;
using EmojiPick.Services;
using Xunit;

namespace EmojiPick.Tests;

[Collection("EmojiDatabase")]
public class SkinToneTests : IDisposable
{
    public void Dispose() => EmojiMatcher.SetEmojisForTesting(new List<EmojiEntry>());

    private static EmojiEntry ThumbsUp() => new()
    {
        Char = "👍",
        Name = "thumbs up",
        Tones = new List<string> { "👍🏻", "👍🏼", "👍🏽", "👍🏾", "👍🏿" },
    };

    [Theory]
    [InlineData(0, "👍")]
    [InlineData(1, "👍🏻")]
    [InlineData(5, "👍🏿")]
    [InlineData(6, "👍")]
    [InlineData(-1, "👍")]
    public void WithSkinTone_ReturnsVariantOrBase(int tone, string expected)
    {
        Assert.Equal(expected, ThumbsUp().WithSkinTone(tone));
    }

    [Fact]
    public void WithSkinTone_EmojiWithoutTones_ReturnsBase()
    {
        var fire = new EmojiEntry { Char = "🔥", Name = "fire" };
        Assert.False(fire.SupportsSkinTone);
        Assert.Equal("🔥", fire.WithSkinTone(3));
    }

    [Fact]
    public void Database_ContainsToneVariants()
    {
        EmojiMatcher.SetEmojisForTesting(null);

        var thumbs = EmojiMatcher.FindByChar("👍");
        Assert.NotNull(thumbs);
        Assert.True(thumbs!.SupportsSkinTone);
        Assert.Equal("👍🏽", thumbs.WithSkinTone(3));

        // Multi-part sequences keep their structure (tone goes on the person, not the sign).
        Assert.Equal("👱🏿‍♀️", EmojiMatcher.FindByChar("👱‍♀️")?.WithSkinTone(5));
        Assert.False(EmojiMatcher.FindByChar("🔥")!.SupportsSkinTone);
    }

    [Fact]
    public void Database_HasFrenchNames()
    {
        EmojiMatcher.SetEmojisForTesting(null);
        Assert.Equal("cœur rouge", EmojiMatcher.FindByChar("❤️")?.GetDisplayName("fr"));
        Assert.Equal("red heart", EmojiMatcher.FindByChar("❤️")?.GetDisplayName("de"));
    }
}
