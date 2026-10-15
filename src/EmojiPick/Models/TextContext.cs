namespace EmojiPick.Models;

/// <summary>
/// Text context extracted from the active application.
/// Can come from a clipboard selection or from UI Automation (cursor context).
/// </summary>
public class TextContext
{
    /// <summary>Main text content (selection or context around cursor)</summary>
    public string Text { get; set; } = "";

    /// <summary>Source of the text (Selection, CursorContext, or None)</summary>
    public TextSource Source { get; set; } = TextSource.None;

    /// <summary>For cursor context: position of the caret within the extracted text</summary>
    public int CursorPosition { get; set; }

    /// <summary>Characters before the cursor position</summary>
    public string BeforeCursor { get; set; } = "";

    /// <summary>Characters after the cursor position</summary>
    public string AfterCursor { get; set; } = "";

    /// <summary>Whether there was an active text selection</summary>
    public bool HasSelection { get; set; }

    /// <summary>Whether the text came from clipboard (selection) vs Accessibility API (context)</summary>
    public bool IsFromClipboard { get; set; }

    /// <summary>Caret bounds in physical screen pixels, when the target application exposes them.</summary>
    public ScreenRect? Caret { get; set; }

    public static TextContext Empty => new();
}

/// <summary>Rectangle in physical screen pixels.</summary>
public readonly record struct ScreenRect(int Left, int Top, int Width, int Height)
{
    public int Bottom => Top + Height;
}

public enum TextSource
{
    /// <summary>Explicitly selected text (via Ctrl+C)</summary>
    Selection,

    /// <summary>Context around the cursor (20 chars before/after)</summary>
    CursorContext,

    /// <summary>No text available</summary>
    None,
}
