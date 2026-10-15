using System.Text.Json.Serialization;

namespace EmojiPick.Models;

/// <summary>
/// Represents a single emoji entry with metadata and searchable tags.
/// </summary>
public class EmojiEntry
{
    /// <summary>Emoji character (e.g. "😀"). Stored as "sequence" in emojis.json.</summary>
    [JsonPropertyName("sequence")]
    public string Char { get; set; } = "";

    /// <summary>Short English name (e.g. "grinning face")</summary>
    public string Name { get; set; } = "";

    /// <summary>Searchable tags for fuzzy matching (e.g. ["happy", "smile", "face"])</summary>
    public List<string> Tags { get; set; } = new List<string>();

    /// <summary>
    /// Category or Unicode qualification status. The bundled database stores the qualification
    /// status here ("fully-qualified", "minimally-qualified", "unqualified", "component").
    /// </summary>
    public string Category { get; set; } = "";

    /// <summary>Unicode codepoint string (e.g. "U+1F600")</summary>
    public string Unicode { get; set; } = "";

    /// <summary>Localized short names from Unicode CLDR, keyed by language (e.g. "fr" → "visage rieur").</summary>
    public Dictionary<string, string> Names { get; set; } = new();

    /// <summary>
    /// Single skin-tone variants, light → dark (5 entries), or null when the emoji has none.
    /// </summary>
    public List<string>? Tones { get; set; }

    public bool SupportsSkinTone => Tones is { Count: 5 };

    /// <summary>
    /// Character to display/insert for a skin tone: 0 = default (yellow), 1..5 = light → dark.
    /// </summary>
    public string WithSkinTone(int tone) =>
        tone is >= 1 and <= 5 && SupportsSkinTone ? Tones![tone - 1] : Char;

    /// <summary>Name in the requested language, falling back to the English name.</summary>
    public string GetDisplayName(string language) =>
        Names.TryGetValue(language, out var localized) && !string.IsNullOrWhiteSpace(localized) ? localized : Name;
}

/// <summary>
/// Match result with a relevance score (0-100).
/// </summary>
public class EmojiMatch
{
    public EmojiEntry Emoji { get; set; } = new();
    public int FuzzyScore { get; set; }
    public int LlmScore { get; set; }
    public int CombinedScore { get; set; }
}
