[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("EmojiPick.Tests")]

namespace EmojiPick.Services;

using System.IO;
using System.IO.Compression;
using System.Text.Json;
using EmojiPick.Helpers;
using EmojiPick.Models;

public static class EmojiMatcher
{
    private const string ResourceName = "EmojiPick.Data.emojis.json.gzip";
    private const int DefaultMaxResults = 12;
    private const int DefaultThreshold = 40;
    private const int MaxCacheEntries = 256;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Qualification statuses that duplicate a fully-qualified entry (e.g. "☺" vs "☺️")
    /// or are not standalone emoji (skin tone / hair components).
    /// </summary>
    private static readonly HashSet<string> ExcludedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "minimally-qualified", "unqualified", "component",
    };

    private static volatile List<EmojiEntry>? _emojis;
    private static readonly object _emojisLock = new();

    private static readonly Dictionary<string, (List<EmojiMatch> Results, DateTime ExpiresAt)> _cache = new();
    private static readonly object _cacheLock = new();

    private static List<EmojiEntry> Emojis
    {
        get
        {
            if (_emojis is { } cached) return cached;
            lock (_emojisLock)
            {
                _emojis ??= LoadEmojis();
                return _emojis;
            }
        }
    }

    private static List<EmojiEntry> LoadEmojis()
    {
        try
        {
            var bytes = ResourceLoader.LoadEmbeddedResource(ResourceName);
            if (bytes is null || bytes.Length == 0) return new List<EmojiEntry>();

            using var compressed = new MemoryStream(bytes);
            using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
            using var decompressed = new MemoryStream();
            gzip.CopyTo(decompressed);

            var json = System.Text.Encoding.UTF8.GetString(decompressed.ToArray());
            if (string.IsNullOrWhiteSpace(json)) return new List<EmojiEntry>();

            var all = JsonSerializer.Deserialize<List<EmojiEntry>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<EmojiEntry>();

            var filtered = FilterDisplayable(all);
            LoggerService.Info($"EmojiMatcher: loaded {filtered.Count} emoji ({all.Count} entries in database)");
            return filtered;
        }
        catch (Exception ex)
        {
            LoggerService.Error("EmojiMatcher: failed to load emoji database", ex);
            return new List<EmojiEntry>();
        }
    }

    /// <summary>
    /// Keeps one entry per emoji: drops empty characters, non fully-qualified duplicates
    /// and skin-tone variants (the base emoji stays searchable).
    /// </summary>
    internal static List<EmojiEntry> FilterDisplayable(IEnumerable<EmojiEntry> entries)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return entries
            .Where(e => !string.IsNullOrEmpty(e.Char))
            .Where(e => !ExcludedStatuses.Contains(e.Category))
            .Where(e => !e.Name.Contains("skin tone", StringComparison.OrdinalIgnoreCase))
            .Where(e => seen.Add(e.Char))
            .ToList();
    }

    public static List<EmojiMatch> GetMatches(string text, int maxResults = DefaultMaxResults, int threshold = DefaultThreshold)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<EmojiMatch>();

        var key = $"{text.Trim().ToLowerInvariant()}|{maxResults}|{threshold}";

        lock (_cacheLock)
        {
            if (_cache.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTime.UtcNow)
                return entry.Results;
        }

        var results = FuzzyMatcher.GetMatches(text, Emojis, maxResults, threshold);

        lock (_cacheLock)
        {
            if (_cache.Count >= MaxCacheEntries) PruneCache();
            _cache[key] = (results, DateTime.UtcNow.Add(CacheTtl));
        }

        return results;
    }

    /// <summary>Looks up an emoji by its character (used to rebuild the recent list).</summary>
    public static EmojiEntry? FindByChar(string emoji)
    {
        if (string.IsNullOrEmpty(emoji)) return null;
        return Emojis.FirstOrDefault(e => e.Char == emoji)
            ?? GetHardcodedFallback().FirstOrDefault(e => e.Char == emoji);
    }

    public static List<EmojiEntry> GetPopularEmoji(int count = DefaultMaxResults)
    {
        var emojis = Emojis;
        if (emojis.Count == 0) return GetHardcodedFallback().Take(count).ToList();

        var popular = emojis
            .Where(e => string.Equals(e.Category, "smileys", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(e.Category, "people", StringComparison.OrdinalIgnoreCase))
            .Take(count)
            .ToList();

        if (popular.Count < count)
            popular.AddRange(emojis.Where(e => !popular.Contains(e)).Take(count - popular.Count));

        return popular.Count > 0 ? popular : GetHardcodedFallback().Take(count).ToList();
    }

    private static void PruneCache()
    {
        var now = DateTime.UtcNow;
        foreach (var expired in _cache.Where(kv => kv.Value.ExpiresAt <= now).Select(kv => kv.Key).ToList())
            _cache.Remove(expired);

        if (_cache.Count >= MaxCacheEntries) _cache.Clear();
    }

    private static List<EmojiEntry> GetHardcodedFallback() => new List<EmojiEntry>
    {
        new EmojiEntry { Char = "😀", Name = "grinning face",               Tags = new List<string> { "happy", "smile" } },
        new EmojiEntry { Char = "😂", Name = "face with tears of joy",      Tags = new List<string> { "laugh", "lol" } },
        new EmojiEntry { Char = "❤️", Name = "red heart",                   Tags = new List<string> { "love", "heart" } },
        new EmojiEntry { Char = "👍", Name = "thumbs up",                   Tags = new List<string> { "good", "ok", "approve" } },
        new EmojiEntry { Char = "🙏", Name = "folded hands",                Tags = new List<string> { "thank", "please", "pray" } },
        new EmojiEntry { Char = "😊", Name = "smiling face",                Tags = new List<string> { "happy", "blush" } },
        new EmojiEntry { Char = "🎉", Name = "party popper",                Tags = new List<string> { "party", "celebrate" } },
        new EmojiEntry { Char = "🔥", Name = "fire",                        Tags = new List<string> { "fire", "hot", "lit" } },
        new EmojiEntry { Char = "✅", Name = "check mark",                  Tags = new List<string> { "check", "done", "ok" } },
        new EmojiEntry { Char = "💪", Name = "flexed biceps",               Tags = new List<string> { "strong", "muscle" } },
        new EmojiEntry { Char = "😎", Name = "smiling face with sunglasses",Tags = new List<string> { "cool", "sunglasses" } },
        new EmojiEntry { Char = "🚀", Name = "rocket",                      Tags = new List<string> { "rocket", "launch", "space" } },
    };

    /// <summary>
    /// Injects a fixed emoji list for unit tests and clears the query cache.
    /// Pass null to force a real reload from the embedded resource on next access.
    /// </summary>
    internal static void SetEmojisForTesting(List<EmojiEntry>? emojis)
    {
        lock (_emojisLock) { _emojis = emojis; }
        lock (_cacheLock) { _cache.Clear(); }
    }
}
