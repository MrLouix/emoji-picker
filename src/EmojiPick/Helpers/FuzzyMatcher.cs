namespace EmojiPick.Helpers;

using System.Globalization;
using System.Text;
using EmojiPick.Models;

public static class FuzzyMatcher
{
    private const int MaxQueryTokens = 16;
    private const int MinReverseMatchLength = 3;

    /// <summary>
    /// Filler words ignored in queries and tags. Context text is often a whole sentence
    /// ("I love this pizza"), and words like "with"/"the" would otherwise match hundreds of emoji.
    /// Includes common French words since selections are frequently in French.
    /// </summary>
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "the", "of", "with", "in", "on", "at", "to", "for", "or", "is", "are",
        "it", "its", "my", "me", "i", "so", "be", "by", "this", "that", "was", "you", "your",
        "we", "he", "she", "they", "them", "but", "as", "from", "just", "very", "really",
        "le", "la", "les", "de", "des", "du", "un", "une", "et", "je", "tu", "il", "elle",
        "est", "pour", "avec", "sur", "dans", "pas", "que", "qui", "ce", "ça", "ca", "mon", "ma", "mes",
    };

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
        Span<int> prev = m < 128 ? stackalloc int[m + 1] : new int[m + 1];
        Span<int> curr = m < 128 ? stackalloc int[m + 1] : new int[m + 1];

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
            var swap = prev;
            prev = curr;
            curr = swap;
        }

        return prev[m];
    }

    /// <summary>
    /// Returns a relevance score (0–100) between a search text and a single tag.
    /// Cascade: exact (100) → prefix (90) → substring (75) → Levenshtein ratio.
    /// A tag is only matched *inside* the text when it has at least 3 characters,
    /// otherwise one-letter tags ("b", "o") would match every word.
    /// </summary>
    public static int ComputeScore(string text, string tag)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(tag)) return 0;

        return ScoreLowercase(text.ToLowerInvariant(), tag.ToLowerInvariant(), minScore: 0);
    }

    /// <summary>
    /// Same as <see cref="ComputeScore"/> for already-lowercased inputs. Returns 0 early when the
    /// length difference alone makes <paramref name="minScore"/> unreachable, which skips most
    /// Levenshtein computations when scanning the whole database.
    /// </summary>
    private static int ScoreLowercase(string t, string g, int minScore)
    {
        bool reverseAllowed = g.Length >= MinReverseMatchLength;

        if (t == g) return 100;
        if (g.StartsWith(t, StringComparison.Ordinal) || (reverseAllowed && t.StartsWith(g, StringComparison.Ordinal))) return 90;
        if (g.Contains(t, StringComparison.Ordinal) || (reverseAllowed && t.Contains(g, StringComparison.Ordinal))) return 75;

        int maxLen = Math.Max(t.Length, g.Length);
        int lengthGap = Math.Abs(t.Length - g.Length); // lower bound of the edit distance
        if (100 - lengthGap * 100 / maxLen < minScore) return 0;
        if (t[0] != g[0] && t[^1] != g[^1] && 100 - 2 * 100 / maxLen < minScore) return 0;

        int dist = DistanceLevenshtein(t, g);
        return Math.Max(0, 100 - dist * 100 / maxLen);
    }

    /// <summary>
    /// Lowercases and removes diacritics ("Fête" → "fete", "cœur" → "coeur"), matching how
    /// tags are stored in emojis.json.gzip (see tools/build_emoji_db.py).
    /// </summary>
    public static string Fold(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        var decomposed = text.ToLowerInvariant()
            .Replace("œ", "oe").Replace("æ", "ae")
            .Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Splits a query into lowercase, accent-free words, dropping punctuation and filler words.
    /// Keeps the last <see cref="MaxQueryTokens"/> words (closest to the cursor for context text).
    /// If everything is filtered out (e.g. the user typed "a"), the raw text is kept so
    /// type-ahead still works.
    /// </summary>
    public static List<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();

        var words = new List<string>();
        var current = new StringBuilder();
        foreach (var ch in Fold(text))
        {
            if (char.IsLetterOrDigit(ch))
            {
                current.Append(ch);
            }
            else if (current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
            }
        }
        if (current.Length > 0) words.Add(current.ToString());

        var tokens = words
            .Where(w => w.Length >= 2 && !StopWords.Contains(w))
            .Distinct()
            .TakeLast(MaxQueryTokens)
            .ToList();

        if (tokens.Count == 0 && words.Count > 0)
            tokens.Add(words[^1]);

        return tokens;
    }

    /// <summary>
    /// Returns the best-matching emojis for the given text.
    /// Each query word is scored against every tag; FuzzyScore is the best score found.
    /// Ties are broken by how many query words matched, then by an exact name match; remaining
    /// ties keep database order (Unicode order, which puts the most common emoji first).
    /// </summary>
    public static List<EmojiMatch> GetMatches(
        string text,
        List<EmojiEntry> emojis,
        int maxResults = 12,
        int threshold = 40)
    {
        if (string.IsNullOrWhiteSpace(text) || emojis is null)
            return new List<EmojiMatch>();

        var tokens = Tokenize(text);
        if (tokens.Count == 0) return new List<EmojiMatch>();

        var phrase = string.Join(' ', Fold(text).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        // OrderBy is stable, so equal scores keep the database order.
        return emojis
            .Select(e => Score(e, tokens, phrase, threshold))
            .Where(r => r.Match.FuzzyScore >= threshold)
            .OrderByDescending(r => r.Match.FuzzyScore)
            .ThenByDescending(r => r.MatchedTokens)
            .ThenByDescending(r => r.NameMatch)
            .Take(maxResults)
            .Select(r => r.Match)
            .ToList();
    }

    private static (EmojiMatch Match, int MatchedTokens, bool NameMatch) Score(
        EmojiEntry emoji, List<string> tokens, string phrase, int threshold)
    {
        int best = 0;
        int matched = 0;

        foreach (var token in tokens)
        {
            int tokenBest = 0;
            foreach (var tag in emoji.Tags)
            {
                if (tag.Length == 0 || StopWords.Contains(tag)) continue;
                var lowered = IsLowercase(tag) ? tag : tag.ToLowerInvariant();
                tokenBest = Math.Max(tokenBest, ScoreLowercase(token, lowered, Math.Max(tokenBest + 1, threshold)));
                if (tokenBest == 100) break;
            }

            if (tokenBest >= threshold) matched++;
            best = Math.Max(best, tokenBest);
        }

        // The name can only equal the query when its words matched, so only fold it then.
        bool nameMatch = best >= threshold && emoji.Name.Length == phrase.Length && Fold(emoji.Name) == phrase;
        if (nameMatch) best = 100;

        return (new EmojiMatch { Emoji = emoji, FuzzyScore = best, CombinedScore = best }, matched, nameMatch);
    }

    private static bool IsLowercase(string text)
    {
        foreach (var ch in text)
        {
            if (char.IsUpper(ch)) return false;
        }
        return true;
    }
}
