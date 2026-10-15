using EmojiPick.Services;
using Xunit;

namespace EmojiPick.Tests;

public class HotKeyManagerTests
{
    [Theory]
    [InlineData("E", 0x45)]
    [InlineData("e", 0x45)]
    [InlineData("1", 0x31)]   // used to parse as Keys.LButton (1)
    [InlineData("F12", 0x7B)]
    [InlineData("Space", 0x20)]
    [InlineData("42", 0)]
    [InlineData("NotAKey", 0)]
    public void ResolveVirtualKey_MapsKeyNames(string key, uint expected)
    {
        Assert.Equal(expected, HotKeyManager.ResolveVirtualKey(key));
    }
}
