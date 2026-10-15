using System.Runtime.InteropServices;
using System.Windows.Forms;
using EmojiPick.Helpers;

namespace EmojiPick.Services;

/// <summary>
/// Clipboard operations with retries (the clipboard is often briefly locked by other
/// processes) and snapshot/restore so EmojiPick does not destroy the user's clipboard.
/// Must be called from an STA thread (the WPF UI thread).
/// </summary>
public static class ClipboardService
{
    private const int RetryCount = 5;
    private const int RetryDelayMs = 20;

    /// <summary>Changes every time any application writes to the clipboard.</summary>
    public static uint SequenceNumber => NativeMethods.GetClipboardSequenceNumber();

    public static string? GetText()
    {
        return Retry(() => Clipboard.ContainsText(TextDataFormat.UnicodeText)
            ? Clipboard.GetText(TextDataFormat.UnicodeText)
            : null);
    }

    public static bool SetText(string text)
    {
        try
        {
            Clipboard.SetDataObject(new DataObject(DataFormats.UnicodeText, text), copy: true, RetryCount, RetryDelayMs);
            return true;
        }
        catch (Exception ex)
        {
            LoggerService.Warn($"ClipboardService: SetText failed — {ex.Message}");
            return false;
        }
    }

    public static bool HasText()
    {
        return Retry(() => Clipboard.ContainsText(TextDataFormat.UnicodeText));
    }

    /// <summary>Copies every readable format currently on the clipboard.</summary>
    public static IDataObject? Capture()
    {
        return Retry<IDataObject?>(() =>
        {
            var source = Clipboard.GetDataObject();
            if (source is null) return null;

            var copy = new DataObject();
            foreach (var format in source.GetFormats(autoConvert: false))
            {
                try
                {
                    var data = source.GetData(format, autoConvert: false);
                    if (data is not null) copy.SetData(format, autoConvert: false, data);
                }
                catch
                {
                    // Some private/delayed-render formats cannot be read — skip them.
                }
            }
            return copy;
        });
    }

    /// <summary>Puts a snapshot from <see cref="Capture"/> back on the clipboard.</summary>
    public static void Restore(IDataObject? snapshot)
    {
        try
        {
            if (snapshot is null || snapshot.GetFormats(autoConvert: false).Length == 0)
                Clipboard.Clear();
            else
                Clipboard.SetDataObject(snapshot, copy: true, RetryCount, RetryDelayMs);
        }
        catch (Exception ex)
        {
            LoggerService.Warn($"ClipboardService: restore failed — {ex.Message}");
        }
    }

    private static T? Retry<T>(Func<T> action)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return action();
            }
            catch (ExternalException) when (attempt < RetryCount)
            {
                Thread.Sleep(RetryDelayMs);
            }
            catch
            {
                return default;
            }
        }
    }
}
