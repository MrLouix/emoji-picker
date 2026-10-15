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
        Assert.Equal("Résultats pour « chat »", Localization.T("ResultsFor", "chat"));
    }

    [Fact]
    public void Apply_UnsupportedUiLanguage_FallsBackToEnglishButKeepsContentLanguage()
    {
        Localization.Apply("de");
        Assert.Equal("en", Localization.UiLanguage);
        Assert.Equal("de", Localization.ContentLanguage);
        Assert.Equal("Popular", Localization.T("Popular"));
    }

    [Fact]
    public void T_UnknownKey_ReturnsKey()
    {
        Assert.Equal("NoSuchKey", Localization.T("NoSuchKey"));
    }
}
