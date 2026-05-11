using System.Text.Json;
using EmojiPick.Models;

namespace EmojiPick.Services;

/// <summary>
/// Loads and saves user configuration from %APPDATA%\EmojiPick\config.json.
/// Thread-safe singleton.
/// </summary>
public static class ConfigService
{
    private static readonly object _lock = new();
    private static Config _config = new();

    public static string ConfigDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EmojiPick");

    public static string ConfigFilePath => Path.Combine(ConfigDirectory, "config.json");

    public static string LogDirectory => Path.Combine(ConfigDirectory, "logs");
    public static string CacheDirectory => Path.Combine(ConfigDirectory, "cache");
    public static string ModelDirectory => Path.Combine(ConfigDirectory, "models");

    /// <summary>Current loaded configuration (thread-safe read access).</summary>
    public static Config Current => _config;

    /// <summary>Load config from disk, or create defaults if missing.</summary>
    public static Config Load()
    {
        lock (_lock)
        {
            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    var json = File.ReadAllText(ConfigFilePath);
                    var config = JsonSerializer.Deserialize<Config>(json);
                    if (config != null)
                    {
                        var defaults = CreateDefaultConfig();
                        config.App ??= defaults.App;
                        config.Hotkey ??= defaults.Hotkey;
                        config.Ui ??= defaults.Ui;
                        config.Behavior ??= defaults.Behavior;
                        config.Llm ??= defaults.Llm;
                        config.Logging ??= defaults.Logging;
                        _config = config;
                        return _config;
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.Warn($"Failed to parse config.json, using defaults: {ex.Message}");
                }
            }

            _config = CreateDefaultConfig();
            SaveInternal();
            return _config;
        }
    }

    internal static void Save()
    {
        lock (_lock) { SaveInternal(); }
    }

    internal static void EnsureDirectories()
    {
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(ModelDirectory);
    }

    private static void SaveInternal()
    {
        EnsureDirectories();
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(_config, options));
    }

    private static Config CreateDefaultConfig() => new();
}
