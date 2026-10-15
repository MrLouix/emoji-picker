using System.IO;
using System.Text.Json;

namespace EmojiPick.Services;

/// <summary>
/// Most-recently-used emoji, persisted in %APPDATA%\EmojiPick\recent.json.
/// Shown in the picker when there is no search text or context.
/// </summary>
public static class RecentEmojiService
{
    public const int MaxCount = 32;

    private static readonly object _lock = new();
    private static List<string>? _recent;

    /// <summary>Overridable for unit tests.</summary>
    internal static string FilePath { get; set; } = Path.Combine(ConfigService.ConfigDirectory, "recent.json");

    public static IReadOnlyList<string> GetRecent()
    {
        lock (_lock)
        {
            return Load().ToList();
        }
    }

    public static void Add(string emoji)
    {
        if (string.IsNullOrEmpty(emoji)) return;

        lock (_lock)
        {
            var list = Load();
            list.Remove(emoji);
            list.Insert(0, emoji);
            if (list.Count > MaxCount) list.RemoveRange(MaxCount, list.Count - MaxCount);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(list));
            }
            catch (Exception ex)
            {
                LoggerService.Warn($"RecentEmojiService: could not save — {ex.Message}");
            }
        }
    }

    internal static void ResetForTesting()
    {
        lock (_lock) { _recent = null; }
    }

    private static List<string> Load()
    {
        if (_recent is not null) return _recent;

        try
        {
            _recent = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(FilePath)) ?? new List<string>()
                : new List<string>();
        }
        catch (Exception ex)
        {
            LoggerService.Warn($"RecentEmojiService: could not read {FilePath} — {ex.Message}");
            _recent = new List<string>();
        }

        _recent.RemoveAll(string.IsNullOrEmpty);
        return _recent;
    }
}
