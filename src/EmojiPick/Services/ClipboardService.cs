using System.Windows.Forms;

namespace EmojiPick.Services;

/// <summary>
/// Thread-safe clipboard operations with save/restore capability.
/// Phase 4 placeholder — implemented later.
/// </summary>
public static class ClipboardService
{
    public static string? GetText()
    {
        try
        {
            return Clipboard.GetText(TextDataFormat.UnicodeText);
        }
        catch
        {
            return null;
        }
    }

    public static void SetText(string text)
    {
        try
        {
            Clipboard.SetText(text, TextDataFormat.UnicodeText);
        }
        catch
        {
            // Silently fail — clipboard may be locked by another process
        }
    }

    public static bool HasText()
    {
        try
        {
            return Clipboard.ContainsText(TextDataFormat.UnicodeText);
        }
        catch
        {
            return false;
        }
    }
}
