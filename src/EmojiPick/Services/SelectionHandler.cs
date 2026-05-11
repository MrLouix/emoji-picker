using System.Windows.Automation;
using EmojiPick.Helpers;
using EmojiPick.Models;

namespace EmojiPick.Services;

public class SelectionHandler
{
    public TextContext GetTextContext()
    {
        // Try 1: clipboard — texte sélectionné avant ouverture du picker (Ctrl+C préalable)
        LoggerService.Debug("SelectionHandler: tentative lecture clipboard");
        try
        {
            var clipText = ClipboardService.GetText();
            if (!string.IsNullOrEmpty(clipText))
            {
                LoggerService.Info($"SelectionHandler: texte trouvé via clipboard ({clipText.Length} chars)");
                return new TextContext
                {
                    Text = clipText,
                    Source = TextSource.Selection,
                    HasSelection = true,
                    IsFromClipboard = true,
                };
            }
        }
        catch (Exception ex)
        {
            LoggerService.Error("SelectionHandler: échec lecture clipboard", ex);
        }

        // Try 2: UI Automation — récupère la sélection ou le contexte autour du curseur
        LoggerService.Debug("SelectionHandler: tentative UI Automation");
        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            var element = AutomationElement.FromHandle(hwnd);
            if (element != null &&
                element.TryGetCurrentPattern(TextPattern.Pattern, out var patternObj) &&
                patternObj is TextPattern textPattern)
            {
                var selections = textPattern.GetSelection();
                if (selections is { Length: > 0 })
                {
                    var selectedText = selections[0].GetText(-1);
                    if (!string.IsNullOrEmpty(selectedText))
                    {
                        LoggerService.Info($"SelectionHandler: sélection via UI Automation ({selectedText.Length} chars)");
                        return new TextContext
                        {
                            Text = selectedText,
                            Source = TextSource.CursorContext,
                            HasSelection = true,
                            IsFromClipboard = false,
                        };
                    }
                }

                // Pas de sélection — contexte du document comme fallback
                var docText = textPattern.DocumentRange.GetText(500);
                if (!string.IsNullOrEmpty(docText))
                {
                    LoggerService.Info("SelectionHandler: contexte document via UI Automation");
                    return new TextContext
                    {
                        Text = docText,
                        Source = TextSource.CursorContext,
                        HasSelection = false,
                        IsFromClipboard = false,
                    };
                }
            }
        }
        catch (Exception ex)
        {
            LoggerService.Warn($"SelectionHandler: UI Automation échoué — {ex.Message}");
        }

        LoggerService.Debug("SelectionHandler: aucun texte disponible, retour None");
        return new TextContext { Source = TextSource.None, Text = "" };
    }
}
