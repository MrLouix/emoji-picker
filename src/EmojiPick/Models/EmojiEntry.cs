namespace EmojiPick.Models;

/// <summary>
/// Represents a single emoji entry with metadata and searchable tags.
/// </summary>
public class EmojiEntry
{
    /// <summary>Emoji character (e.g. "😀")</summary>
    public string Char { get; set; } = "";

    /// <summary>Short English name (e.g. "grinning face")</summary>
    public string Name { get; set; } = "";

    /// <summary>Searchable tags for fuzzy matching (e.g. ["happy", "smile", "face"])</summary>
    public List<string> Tags { get; set; } = new List<string>();

    /// <summary>Unicode category group (e.g. "smileys", "hands", "objects")</summary>
    public string Category { get; set; } = "";

    /// <summary>Unicode codepoint string (e.g. "U+1F600")</summary>
    public string Unicode { get; set; } = "";
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
