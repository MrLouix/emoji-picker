using EmojiPick.Helpers;
using Xunit;

namespace EmojiPick.Tests;

public class LocalizationTests : IDisposable
{
    public void Dispose() => Localization.Apply("en");

    [Fact]
    public void FrenchTable_CoversEveryEnglishKey()
    {
        Assert.Empty(Localization.MissingFrenchKeys());
    }

    [Fact]
    public void Apply_French_TranslatesAndFormats()
    {
        Localization.Apply("fr");
        Assert.Equal("fr", Localization.UiLanguage);
        Assert.Equal("Aucun emoji pour « chat »", Localization.T("NoResultsTitle", "chat"));
    }

    [Fact]
    public void Apply_UnsupportedUiLanguage_FallsBackToEnglishButKeepsContentLanguage()
    {
        Localization.Apply("de");
        Assert.Equal("en", Localization.UiLanguage);
        Assert.Equal("de", Localization.ContentLanguage);
        Assert.Equal("POPULAR", Localization.T("SectionPopular"));
    }

    [Fact]
    public void T_UnknownKey_ReturnsKey()
    {
        Assert.Equal("NoSuchKey", Localization.T("NoSuchKey"));
    }
}
