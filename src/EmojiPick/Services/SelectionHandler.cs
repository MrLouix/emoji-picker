using System.Text;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using EmojiPick.Helpers;
using EmojiPick.Models;

namespace EmojiPick.Services;

/// <summary>
/// Reads the text the user is working on in the foreground application, in order of reliability:
///   1. Selection exposed by UI Automation (no side effects).
///   2. Selection copied with a simulated Ctrl+C — the clipboard sequence number tells us whether
///      anything was actually copied, and the user's clipboard is restored afterwards.
///   3. Text just before the caret (UI Automation), so suggestions match what was just typed.
/// </summary>
public class SelectionHandler
{
    private const int UiAutomationTimeoutMs = 400;
    private const int CopyTimeoutMs = 300;
    private const int ContextChars = 60;

    /// <summary>Console windows: Ctrl+C there would interrupt the running program.</summary>
    private static readonly HashSet<string> ConsoleWindowClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ConsoleWindowClass", "CASCADIA_HOSTING_WINDOW_CLASS", "PseudoConsoleWindow", "mintty",
    };

    /// <param name="targetWindow">Foreground window when the hotkey was pressed.</param>
    /// <param name="allowCopy">False when modifiers are still held, so Ctrl+C would be misinterpreted.</param>
    public async Task<TextContext> CaptureAsync(IntPtr targetWindow, bool allowCopy = true)
    {
        var caret = CaretLocator.TryGetCaret(targetWindow);

        var uia = await QueryUiAutomationAsync();
        caret ??= uia.Caret;

        if (!string.IsNullOrWhiteSpace(uia.Selection))
        {
            LoggerService.Info($"SelectionHandler: selection via UI Automation ({uia.Selection.Length} chars)");
            return new TextContext
            {
                Text = uia.Selection,
                Source = TextSource.Selection,
                HasSelection = true,
                Caret = caret,
            };
        }

        // When UI Automation exposed the text, it already told us there is no selection:
        // skip the Ctrl+C fallback and its side effects.
        if (allowCopy && !uia.HasTextPattern && !IsConsoleWindow(targetWindow))
        {
            var copied = await CopySelectionAsync();
            if (!string.IsNullOrWhiteSpace(copied))
            {
                LoggerService.Info($"SelectionHandler: selection via clipboard ({copied.Length} chars)");
                return new TextContext
                {
                    Text = copied,
                    Source = TextSource.Selection,
                    HasSelection = true,
                    IsFromClipboard = true,
                    Caret = caret,
                };
            }
        }

        if (!string.IsNullOrWhiteSpace(uia.BeforeCaret))
        {
            LoggerService.Info("SelectionHandler: context before caret via UI Automation");
            return new TextContext
            {
                Text = uia.BeforeCaret,
                BeforeCursor = uia.BeforeCaret,
                CursorPosition = uia.BeforeCaret.Length,
                Source = TextSource.CursorContext,
                Caret = caret,
            };
        }

        LoggerService.Debug("SelectionHandler: no text available");
        return new TextContext { Source = TextSource.None, Caret = caret };
    }

    /// <summary>
    /// Sends Ctrl+C and returns the copied text only if the clipboard actually changed.
    /// Without this check, stale clipboard content from minutes ago was used as the query.
    /// </summary>
    private static async Task<string?> CopySelectionAsync()
    {
        try
        {
            var before = ClipboardService.SequenceNumber;
            var snapshot = ClipboardService.Capture();

            InputSimulator.SendCtrlC();

            var deadline = Environment.TickCount64 + CopyTimeoutMs;
            while (ClipboardService.SequenceNumber == before && Environment.TickCount64 < deadline)
                await Task.Delay(15);

            if (ClipboardService.SequenceNumber == before) return null;

            // Some apps write several formats in sequence; give them a moment to finish.
            await Task.Delay(30);
            var text = ClipboardService.GetText();
            ClipboardService.Restore(snapshot);
            return text;
        }
        catch (Exception ex)
        {
            LoggerService.Warn($"SelectionHandler: clipboard copy failed — {ex.Message}");
            return null;
        }
    }

    private static bool IsConsoleWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        var sb = new StringBuilder(256);
        return NativeMethods.GetClassName(hwnd, sb, sb.Capacity) > 0 && ConsoleWindowClasses.Contains(sb.ToString());
    }

    private readonly record struct UiaResult(string? Selection, string? BeforeCaret, ScreenRect? Caret, bool HasTextPattern = false);

    /// <summary>UI Automation can hang on unresponsive apps, so it runs off the UI thread with a timeout.</summary>
    private static async Task<UiaResult> QueryUiAutomationAsync()
    {
        var task = Task.Run(QueryUiAutomation);
        var finished = await Task.WhenAny(task, Task.Delay(UiAutomationTimeoutMs));
        if (finished == task) return task.Result;

        LoggerService.Debug("SelectionHandler: UI Automation timed out");
        return default;
    }

    private static UiaResult QueryUiAutomation()
    {
        try
        {
            var element = AutomationElement.FocusedElement;
            if (element is null) return default;

            if (element.TryGetCurrentPattern(TextPattern.Pattern, out var patternObj) &&
                patternObj is TextPattern textPattern)
            {
                var selections = textPattern.GetSelection();
                if (selections is not { Length: > 0 }) return default;

                var range = selections[0];
                var selected = range.GetText(-1);
                var caret = GetRangeBounds(range);

                if (!string.IsNullOrWhiteSpace(selected))
                    return new UiaResult(selected, null, caret, HasTextPattern: true);

                var before = range.Clone();
                before.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, -ContextChars);
                return new UiaResult(null, before.GetText(-1), caret, HasTextPattern: true);
            }

            // Simple edit fields without TextPattern: the text near the end is the best guess.
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valueObj) &&
                valueObj is ValuePattern valuePattern &&
                !element.Current.IsPassword)
            {
                var value = valuePattern.Current.Value ?? "";
                return new UiaResult(null, value.Length > ContextChars ? value[^ContextChars..] : value, null);
            }
        }
        catch (Exception ex)
        {
            LoggerService.Debug($"SelectionHandler: UI Automation failed — {ex.Message}");
        }

        return default;
    }

    private static ScreenRect? GetRangeBounds(TextPatternRange range)
    {
        try
        {
            var rects = range.GetBoundingRectangles();
            if (rects.Length == 0)
            {
                // An empty (caret) range has no bounds; measure the previous character instead.
                var previous = range.Clone();
                previous.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, -1);
                rects = previous.GetBoundingRectangles();
            }

            if (rects.Length == 0) return null;
            var r = rects[^1];
            return new ScreenRect((int)r.Right, (int)r.Top, 1, Math.Max(1, (int)r.Height));
        }
        catch
        {
            return null;
        }
    }
}
