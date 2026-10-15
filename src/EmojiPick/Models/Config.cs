namespace EmojiPick.Models;

/// <summary>
/// Root configuration model for EmojiPick.
/// Backed by %APPDATA%\EmojiPick\config.json.
/// </summary>
public class Config
{
    public AppConfig App { get; set; } = new();
    public HotKeyConfig Hotkey { get; set; } = new();
    public UiConfig Ui { get; set; } = new();
    public BehaviorConfig Behavior { get; set; } = new();
    public LlmConfig Llm { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
    /// <summary>Language of emoji names ("en", "fr"…) or "auto" for the Windows display language.</summary>
    public string Language { get; set; } = "auto";
}

public class AppConfig
{
    public string Version { get; set; } = "1.0.0";
    public bool AutoStart { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
}

public class HotKeyConfig
{
    public string[] Modifiers { get; set; } = new[] { "Ctrl", "Alt" };
    public string Key { get; set; } = "E";
}

public class UiConfig
{
    /// <summary>"dark", "light" or "system" (follows the Windows app theme).</summary>
    public string Theme { get; set; } = "system";
    public int FontSize { get; set; } = 24;
    public double WindowOpacity { get; set; } = 0.97;
    public int GridColumns { get; set; } = 7;

    /// <summary>Number of visible rows; extra results are reachable by scrolling.</summary>
    public int GridRows { get; set; } = 4;

    /// <summary>"caret" (text cursor, falls back to mouse), "cursor" (mouse) or "center" (screen).</summary>
    public string PositionMode { get; set; } = "caret";

    /// <summary>Render emoji in color (Emoji.Wpf). False uses WPF's monochrome glyphs.</summary>
    public bool ColorEmoji { get; set; } = true;

    /// <summary>Default skin tone: 0 = none (yellow), 1..5 = light → dark.</summary>
    public int SkinTone { get; set; }
}

public class BehaviorConfig
{
    public bool AutoClose { get; set; } = true;

    /// <summary>"paste" (clipboard + Ctrl+V), "type" (Unicode keystrokes) or "clipboard" (copy only).</summary>
    public string InjectMode { get; set; } = "paste";

    /// <summary>Restore the previous clipboard content after pasting an emoji.</summary>
    public bool RestoreClipboard { get; set; } = true;

    /// <summary>Minimum fuzzy score (0..1) for a result to be shown.</summary>
    public double FuzzyThreshold { get; set; } = 0.6;
    public int MaxResults { get; set; } = 42;
}

public class LlmConfig
{
    public bool Enabled { get; set; } = true;
    public string Provider { get; set; } = "auto";
    public string[] FallbackChain { get; set; } = new[] { "ollama", "llamacpp", "fuzzy" };
    public bool CacheResults { get; set; } = true;
    public int CacheTtlMinutes { get; set; } = 5;
    public Dictionary<string, ProviderConfig> Providers { get; set; } = new()
    {
        ["ollama"] = new ProviderConfig
        {
            Enabled = true,
            Endpoint = "http://localhost:11434",
            Model = "mistral",
            TimeoutMs = 3000,
        },
        ["llamacpp"] = new ProviderConfig
        {
            Enabled = true,
            ModelPath = "%APPDATA%/EmojiPick/models/Mistral-7B-Q4_K_M.gguf",
            UseGpu = true,
            GpuType = "auto",
            GpuLayers = 20,
            ContextSize = 512,
            BatchSize = 64,
            TimeoutMs = 3000,
        },
    };
}

public class ProviderConfig
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "";
    public string Model { get; set; } = "";
    public string ModelPath { get; set; } = "";
    public bool UseGpu { get; set; }
    public string GpuType { get; set; } = "auto";
    public int GpuLayers { get; set; } = 20;
    public int ContextSize { get; set; } = 512;
    public int BatchSize { get; set; } = 64;
    public int TimeoutMs { get; set; } = 3000;
}

public class LoggingConfig
{
    public string Level { get; set; } = "info";
    public int MaxFileSizeMb { get; set; } = 10;
    public int RetentionDays { get; set; } = 7;
}
