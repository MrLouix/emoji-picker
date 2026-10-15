using System.IO;
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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    public static string ConfigDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EmojiPick");

    public static string ConfigFilePath => Path.Combine(ConfigDirectory, "config.json");

    public static string LogDirectory => Path.Combine(ConfigDirectory, "logs");
    public static string CacheDirectory => Path.Combine(ConfigDirectory, "cache");
    public static string ModelDirectory => Path.Combine(ConfigDirectory, "models");

    /// <summary>Current loaded configuration (thread-safe read access).</summary>
    public static Config Current => _config;

    /// <summary>True once <see cref="Load"/> has run; guards against saving untouched defaults.</summary>
    public static bool IsLoaded { get; private set; }

    /// <summary>Raised after <see cref="Update"/> replaced the configuration.</summary>
    public static event EventHandler? Changed;

    /// <summary>Deep copy, so a settings dialog can edit without touching the live config.</summary>
    public static Config Clone(Config config) =>
        JsonSerializer.Deserialize<Config>(JsonSerializer.Serialize(config, JsonOptions), JsonOptions)!;

    /// <summary>Validates, replaces and saves the configuration, then raises <see cref="Changed"/>.</summary>
    public static void Update(Config config)
    {
        lock (_lock)
        {
            Normalize(config);
            _config = config;
            IsLoaded = true;
            SaveInternal();
        }
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Load config from disk, or create defaults if missing.</summary>
    public static Config Load()
    {
        lock (_lock)
        {
            IsLoaded = true;

            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    var json = File.ReadAllText(ConfigFilePath);
                    var config = JsonSerializer.Deserialize<Config>(json, JsonOptions);
                    if (config != null)
                    {
                        Normalize(config);
                        _config = config;
                        return _config;
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.Warn($"Failed to parse config.json, using defaults: {ex.Message}");
                    BackupBrokenConfig();
                }
            }

            _config = CreateDefaultConfig();
            SaveInternal();
            return _config;
        }
    }

    internal static void Save()
    {
        lock (_lock)
        {
            if (!IsLoaded) return;
            SaveInternal();
        }
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
        File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(_config, JsonOptions));
    }

    /// <summary>Keep a copy of an unreadable config so the user's edits are not silently lost.</summary>
    private static void BackupBrokenConfig()
    {
        try
        {
            File.Copy(ConfigFilePath, ConfigFilePath + ".bak", overwrite: true);
            LoggerService.Warn($"Unreadable config backed up to {ConfigFilePath}.bak");
        }
        catch (Exception ex)
        {
            LoggerService.Warn($"Could not back up unreadable config: {ex.Message}");
        }
    }

    /// <summary>Fill missing sections and clamp values that would break the UI.</summary>
    internal static void Normalize(Config config)
    {
        var defaults = CreateDefaultConfig();
        config.App ??= defaults.App;
        config.Hotkey ??= defaults.Hotkey;
        config.Ui ??= defaults.Ui;
        config.Behavior ??= defaults.Behavior;
        config.Llm ??= defaults.Llm;
        config.Logging ??= defaults.Logging;
        config.Llm.Providers ??= defaults.Llm.Providers;

        if (config.Hotkey.Modifiers is not { Length: > 0 }) config.Hotkey.Modifiers = defaults.Hotkey.Modifiers;
        if (string.IsNullOrWhiteSpace(config.Hotkey.Key)) config.Hotkey.Key = defaults.Hotkey.Key;

        config.Ui.Theme = string.IsNullOrWhiteSpace(config.Ui.Theme) ? defaults.Ui.Theme : config.Ui.Theme;
        config.Ui.FontSize = Math.Clamp(config.Ui.FontSize, 14, 48);
        config.Ui.WindowOpacity = Math.Clamp(config.Ui.WindowOpacity, 0.5, 1.0);
        config.Ui.GridColumns = Math.Clamp(config.Ui.GridColumns, 3, 12);
        config.Ui.GridRows = Math.Clamp(config.Ui.GridRows, 1, 8);
        config.Ui.SkinTone = Math.Clamp(config.Ui.SkinTone, 0, 5);

        config.Behavior.FuzzyThreshold = Math.Clamp(config.Behavior.FuzzyThreshold, 0.0, 1.0);
        config.Behavior.MaxResults = Math.Clamp(config.Behavior.MaxResults, 1, 200);
        config.Behavior.InjectMode = string.IsNullOrWhiteSpace(config.Behavior.InjectMode)
            ? defaults.Behavior.InjectMode
            : config.Behavior.InjectMode;
    }

    private static Config CreateDefaultConfig() => new();
}
