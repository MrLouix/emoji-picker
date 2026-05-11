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
    public string Language { get; set; } = "en";
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
    public string Theme { get; set; } = "dark";
    public int FontSize { get; set; } = 24;
    public double WindowOpacity { get; set; } = 0.95;
    public int GridColumns { get; set; } = 4;
    public int GridRows { get; set; } = 3;
    public string PositionMode { get; set; } = "center";
}

public class BehaviorConfig
{
    public bool AutoClose { get; set; } = true;
    public string InjectMode { get; set; } = "paste";
    public double FuzzyThreshold { get; set; } = 0.6;
    public int MaxResults { get; set; } = 12;
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
