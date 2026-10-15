using System.Text.Json;
using EmojiPick.Models;
using EmojiPick.Services;
using Xunit;

namespace EmojiPick.Tests;

public class ConfigServiceTests
{
    [Fact]
    public void Normalize_ClampsValuesThatWouldBreakTheUi()
    {
        var config = new Config
        {
            Ui = new UiConfig { GridColumns = 0, GridRows = 100, FontSize = 2, WindowOpacity = 3 },
            Behavior = new BehaviorConfig { FuzzyThreshold = 60, MaxResults = 0, InjectMode = "" },
            Hotkey = new HotKeyConfig { Modifiers = Array.Empty<string>(), Key = " " },
        };

        ConfigService.Normalize(config);

        Assert.Equal(3, config.Ui.GridColumns);
        Assert.Equal(8, config.Ui.GridRows);
        Assert.Equal(14, config.Ui.FontSize);
        Assert.Equal(1.0, config.Ui.WindowOpacity);
        Assert.Equal(1.0, config.Behavior.FuzzyThreshold);
        Assert.Equal(1, config.Behavior.MaxResults);
        Assert.Equal("paste", config.Behavior.InjectMode);
        Assert.Equal(new[] { "Ctrl", "Alt" }, config.Hotkey.Modifiers);
        Assert.Equal("E", config.Hotkey.Key);
    }

    [Fact]
    public void Normalize_FillsMissingSections()
    {
        var config = JsonSerializer.Deserialize<Config>("""{ "Ui": null, "Llm": { "Providers": null } }""")!;

        ConfigService.Normalize(config);

        Assert.NotNull(config.Ui);
        Assert.Contains("ollama", config.Llm.Providers.Keys);
    }
}
