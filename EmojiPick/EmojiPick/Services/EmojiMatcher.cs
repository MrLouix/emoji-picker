[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("EmojiPick.Tests")]

namespace EmojiPick.Services;

using System.IO.Compression;
using System.Text.Json;
using EmojiPick.Helpers;
using EmojiPick.Models;

public static class EmojiMatcher
{
    private const string ResourceName = "EmojiPick.Data.emojis.json.gzip";
    private const int DefaultMaxResults = 12;
    private const int DefaultThreshold = 40;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

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
            if (bytes is null || bytes.Length == 0) return [];

            using var compressed = new MemoryStream(bytes);
            using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
            using var decompressed = new MemoryStream();
            gzip.CopyTo(decompressed);

            var json = System.Text.Encoding.UTF8.GetString(decompressed.ToArray());
            if (string.IsNullOrWhiteSpace(json)) return [];

            return JsonSerializer.Deserialize<List<EmojiEntry>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static List<EmojiMatch> GetMatches(string text, int maxResults = DefaultMaxResults, int threshold = DefaultThreshold)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var key = $"{text.ToLowerInvariant()}|{maxResults}|{threshold}";

        lock (_cacheLock)
        {
            if (_cache.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTime.UtcNow)
                return entry.Results;
        }

        var results = FuzzyMatcher.GetMatches(text, Emojis, maxResults, threshold);

        lock (_cacheLock)
        {
            _cache[key] = (results, DateTime.UtcNow.Add(CacheTtl));
        }

        return results;
    }

    public static List<EmojiEntry> GetPopularEmoji()
    {
        var emojis = Emojis;
        if (emojis.Count == 0) return GetHardcodedFallback();

        var popular = emojis
            .Where(e => string.Equals(e.Category, "smileys", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(e.Category, "people", StringComparison.OrdinalIgnoreCase))
            .Take(DefaultMaxResults)
            .ToList();

        if (popular.Count < DefaultMaxResults)
            popular.AddRange(emojis.Where(e => !popular.Contains(e)).Take(DefaultMaxResults - popular.Count));

        return popular.Count > 0 ? popular : GetHardcodedFallback();
    }

    private static List<EmojiEntry> GetHardcodedFallback() =>
    [
        new() { Char = "😀", Name = "grinning face",               Tags = ["happy", "smile"] },
        new() { Char = "😂", Name = "face with tears of joy",      Tags = ["laugh", "lol"] },
        new() { Char = "❤️", Name = "red heart",                   Tags = ["love", "heart"] },
        new() { Char = "👍", Name = "thumbs up",                   Tags = ["good", "ok", "approve"] },
        new() { Char = "🙏", Name = "folded hands",                Tags = ["thank", "please", "pray"] },
        new() { Char = "😊", Name = "smiling face",                Tags = ["happy", "blush"] },
        new() { Char = "🎉", Name = "party popper",                Tags = ["party", "celebrate"] },
        new() { Char = "🔥", Name = "fire",                        Tags = ["fire", "hot", "lit"] },
        new() { Char = "✅", Name = "check mark",                  Tags = ["check", "done", "ok"] },
        new() { Char = "💪", Name = "flexed biceps",               Tags = ["strong", "muscle"] },
        new() { Char = "😎", Name = "smiling face with sunglasses",Tags = ["cool", "sunglasses"] },
        new() { Char = "🚀", Name = "rocket",                      Tags = ["rocket", "launch", "space"] },
    ];

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
