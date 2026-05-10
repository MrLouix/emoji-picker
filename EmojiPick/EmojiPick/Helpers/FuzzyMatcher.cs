namespace EmojiPick.Helpers;

using EmojiPick.Models;

public static class FuzzyMatcher
{
    /// <summary>
    /// Optimized Levenshtein distance using a two-row rolling array — O(min(m,n)) memory.
    /// </summary>
    public static int DistanceLevenshtein(string s, string t)
    {
        if (s.Length == 0) return t.Length;
        if (t.Length == 0) return s.Length;

        // Keep s as the shorter string to minimize array allocation.
        if (s.Length > t.Length) (s, t) = (t, s);

        int m = s.Length, n = t.Length;
        int[] prev = new int[m + 1];
        int[] curr = new int[m + 1];

        for (int i = 0; i <= m; i++) prev[i] = i;

        for (int j = 1; j <= n; j++)
        {
            curr[0] = j;
            for (int i = 1; i <= m; i++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                curr[i] = Math.Min(
                    Math.Min(prev[i] + 1, curr[i - 1] + 1),
                    prev[i - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }

        return prev[m];
    }

    /// <summary>
    /// Returns a relevance score (0–100) between a search text and a single tag.
    /// Cascade: exact (100) → prefix (90) → substring (75) → Levenshtein ratio.
    /// </summary>
    public static int ComputeScore(string text, string tag)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(tag)) return 0;

        string t = text.ToLowerInvariant();
        string g = tag.ToLowerInvariant();

        if (t == g) return 100;
        if (g.StartsWith(t, StringComparison.Ordinal) || t.StartsWith(g, StringComparison.Ordinal)) return 90;
        if (g.Contains(t, StringComparison.Ordinal) || t.Contains(g, StringComparison.Ordinal)) return 75;

        int maxLen = Math.Max(t.Length, g.Length);
        int dist = DistanceLevenshtein(t, g);
        return Math.Max(0, 100 - dist * 100 / maxLen);
    }

    /// <summary>
    /// Returns the best-matching emojis for the given text, scored against all tags.
    /// FuzzyScore is the best ComputeScore across all tags of a given EmojiEntry.
    /// </summary>
    public static List<EmojiMatch> GetMatches(
        string text,
        List<EmojiEntry> emojis,
        int maxResults = 12,
        int threshold = 40)
    {
        if (string.IsNullOrWhiteSpace(text) || emojis is null)
            return new List<EmojiMatch>();

        return emojis
            .Select(e =>
            {
                int best = e.Tags.Count > 0
                    ? e.Tags.Max(tag => ComputeScore(text, tag))
                    : 0;
                return new EmojiMatch { Emoji = e, FuzzyScore = best };
            })
            .Where(m => m.FuzzyScore >= threshold)
            .OrderByDescending(m => m.FuzzyScore)
            .Take(maxResults)
            .ToList();
    }
}
