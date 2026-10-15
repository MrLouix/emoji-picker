using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using EmojiPick.Helpers;
using EmojiPick.Models;
using EmojiPick.Services;
using Microsoft.Win32;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using L = EmojiPick.Helpers.Localization;
using TextBlock = System.Windows.Controls.TextBlock;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace EmojiPick.Windows;

/// <summary>
/// One grid cell: the emoji, the character shown for the current skin tone, and whether the
/// AI refinement added it (marked with a dot).
/// </summary>
public sealed record EmojiTile(EmojiEntry Entry, string Display, bool IsAi = false);

public partial class OverlayWindow : System.Windows.Window
{
    /// <summary>Skin tone modifiers, index 0 = default (none), 1..5 = light → dark.</summary>
    private static readonly string[] ToneModifiers = { "", "\U0001F3FB", "\U0001F3FC", "\U0001F3FD", "\U0001F3FE", "\U0001F3FF" };

    private const int LlmCandidateCount = 40;
    private const int MaxLabelQueryLength = 32;

    private readonly TextContext _context;
    private readonly ILlmMatcher? _llm;
    private readonly Config _config;
    private readonly int _columns;
    private readonly int _visibleRows;
    private readonly int _maxResults;
    private readonly int _threshold;
    private readonly string _contextQuery;
    private readonly string _language;
    private readonly ObservableCollection<EmojiTile> _items = new();
    private int _skinTone;

    private CancellationTokenSource? _llmCts;
    private Point? _lastMousePosition;
    private bool _isClosing;
    private readonly HashSet<string> _aiChars = new();

    public TextContext InitialContext => _context;
    /// <summary>Character to insert (with the chosen skin tone).</summary>
    public string? SelectedEmoji { get; private set; }

    /// <summary>Base character of the selection (without skin tone), used for the recent list.</summary>
    public string? SelectedBaseEmoji { get; private set; }

    /// <summary>Raised when the user clicks the settings button; the window closes right after.</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>True when the user asked to copy the emoji instead of inserting it.</summary>
    public bool CopyOnly { get; private set; }

    public OverlayWindow(TextContext context, ILlmMatcher? llm = null)
    {
        _context = context;
        _llm = llm;
        _config = ConfigService.Current;
        _columns = _config.Ui.GridColumns;
        _visibleRows = _config.Ui.GridRows;
        _maxResults = _config.Behavior.MaxResults;
        _threshold = (int)Math.Round(_config.Behavior.FuzzyThreshold * 100);
        _contextQuery = BuildContextQuery(context);
        _language = L.ContentLanguage;
        _skinTone = _config.Ui.SkinTone;

        InitializeComponent();

        ApplyTheme();
        ConfigureGrid();

        EmojiList.ItemTemplate = (DataTemplate)Resources[_config.Ui.ColorEmoji ? "ColorTileTemplate" : "MonoTileTemplate"];
        EmojiList.ItemsSource = _items;
        Placeholder.Text = L.T(_contextQuery.Length > 0 ? "Refine" : "Search");
        UpdateContextLine();
        InsertHint.Text = L.T("Insert");
        CopyHint.Text = L.T("Copy");
        ClearButton.ToolTip = L.T("Clear");
        ToneButton.ToolTip = L.T("SkinToneTip");
        SettingsButton.ToolTip = L.T("SettingsTip");
        UpdateToneButton();

        SearchBox.TextChanged += (_, _) => OnSearchChanged();
        ClearButton.Click += (_, _) => { SearchBox.Clear(); SearchBox.Focus(); };
        ToneButton.Click += (_, _) => { CycleSkinTone(); SearchBox.Focus(); };
        SettingsButton.Click += (_, _) =>
        {
            SettingsRequested?.Invoke(this, EventArgs.Empty);
            Close();
        };
        EmojiList.PreviewMouseLeftButtonUp += (_, e) => OnTileClicked(e, copyOnly: Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
        EmojiList.PreviewMouseRightButtonUp += (_, e) => OnTileClicked(e, copyOnly: true);
        EmojiList.MouseMove += OnListMouseMove;
        Root.MouseLeftButtonDown += OnRootMouseLeftButtonDown;

        PreviewKeyDown += OnPreviewKeyDown;
        Deactivated += (_, _) => { if (!_isClosing) Close(); };
        Closing += (_, _) =>
        {
            _isClosing = true;
            _llmCts?.Cancel();
        };
        Loaded += OnLoaded;

        RefreshResults();
    }

    // ─── Setup ────────────────────────────────────────────────────────────────

    private void ConfigureGrid()
    {
        // Default font size 24 → 52px tiles with 28px emoji.
        double emojiSize = _config.Ui.FontSize + 4;
        double tile = Math.Round(_config.Ui.FontSize + 28.0);
        Resources["EmojiFontSize"] = emojiSize;
        Resources["TileSize"] = tile;

        var panel = new FrameworkElementFactory(typeof(UniformGrid));
        panel.SetValue(UniformGrid.ColumnsProperty, _columns);
        EmojiList.ItemsPanel = new ItemsPanelTemplate(panel);

        double cell = tile + 4; // tile margin
        EmojiList.Width = _columns * cell;
        EmojiList.Height = _visibleRows * cell;
        // SizeToContent would otherwise widen the window to fit a long emoji name or context.
        Body.Width = EmojiList.Width;
    }

    private void ApplyTheme()
    {
        var palette = UseLightTheme(_config.Ui.Theme) ? Palette.Light : Palette.Dark;
        var surface = palette.Surface;
        surface.A = (byte)Math.Round(_config.Ui.WindowOpacity * 255);

        Resources["SurfaceBrush"] = Freeze(surface);
        Resources["SurfaceAltBrush"] = Freeze(palette.SurfaceAlt);
        Resources["StrokeBrush"] = Freeze(palette.Stroke);
        Resources["TextBrush"] = Freeze(palette.Text);
        Resources["MutedBrush"] = Freeze(palette.Muted);
        Resources["AccentBrush"] = Freeze(palette.Accent);
        Resources["AccentSoftBrush"] = Freeze(Color.FromArgb(0x40, palette.Accent.R, palette.Accent.G, palette.Accent.B));
        Resources["HoverBrush"] = Freeze(palette.Hover);
    }

    private static bool UseLightTheme(string theme)
    {
        switch (theme.Trim().ToLowerInvariant())
        {
            case "light": return true;
            case "dark": return false;
            default:
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                    return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
                }
                catch
                {
                    return false;
                }
        }
    }

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        bool above = PositionWindow();

        var duration = TimeSpan.FromMilliseconds(140);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        RootTranslate.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(above ? -8 : 8, 0, duration) { EasingFunction = ease });

        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
    }

    // ─── Positioning ──────────────────────────────────────────────────────────

    /// <summary>
    /// Places the window next to the text caret (or mouse cursor), flipping above it when there
    /// is no room below and keeping it inside the monitor's working area.
    /// Screen APIs return physical pixels while WPF uses DIPs, hence the DPI conversion.
    /// Returns true when the window was placed above the anchor.
    /// </summary>
    private bool PositionWindow()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        const int shadowMargin = 16;

        var mouse = System.Windows.Forms.Cursor.Position;
        var mode = _config.Ui.PositionMode.Trim().ToLowerInvariant();

        int anchorX = mouse.X, anchorTop = mouse.Y, anchorBottom = mouse.Y + 20;
        if (mode == "caret" && _context.Caret is { } caret)
        {
            anchorX = caret.Left;
            anchorTop = caret.Top;
            anchorBottom = caret.Bottom;
        }

        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(anchorX, anchorTop));
        var work = screen.WorkingArea;

        double width = ActualWidth * dpi.DpiScaleX;
        double height = ActualHeight * dpi.DpiScaleY;
        double margin = shadowMargin * dpi.DpiScaleX;

        double x, y;
        bool above = false;

        if (mode == "center")
        {
            x = work.Left + (work.Width - width) / 2;
            y = work.Top + (work.Height - height) / 3;
        }
        else if (mode == "cursor" || _context.Caret is null)
        {
            // Mouse pointer: centered horizontally, just below it.
            x = anchorX - width / 2;
            y = anchorBottom - margin;
            if (y + height > work.Bottom)
            {
                y = anchorTop - height - 8 + margin;
                above = true;
            }
        }
        else
        {
            // Text caret: left edge aligned with the caret, like an autocomplete popup.
            x = anchorX - margin;
            y = anchorBottom + 4 - margin;
            if (y + height > work.Bottom)
            {
                y = anchorTop - height - 4 + margin;
                above = true;
            }
        }

        x = Math.Clamp(x, work.Left, Math.Max(work.Left, work.Right - width));
        y = Math.Clamp(y, work.Top, Math.Max(work.Top, work.Bottom - height));

        Left = x / dpi.DpiScaleX;
        Top = y / dpi.DpiScaleY;
        return above;
    }

    // ─── Results ──────────────────────────────────────────────────────────────

    private static string BuildContextQuery(TextContext context)
    {
        var text = context.Source == TextSource.CursorContext ? context.BeforeCursor : context.Text;
        if (string.IsNullOrWhiteSpace(text)) text = context.Text;
        return string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private void OnSearchChanged()
    {
        bool hasText = SearchBox.Text.Length > 0;
        Placeholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
        ClearButton.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
        RefreshResults();
    }

    /// <summary>"From selection “…”" / "Near the cursor “…”" line under the search field.</summary>
    private void UpdateContextLine()
    {
        if (_contextQuery.Length == 0)
        {
            CtxPanel.Visibility = Visibility.Collapsed;
            return;
        }

        CtxPanel.Visibility = Visibility.Visible;
        CtxSource.Text = L.T(_context.Source == TextSource.Selection ? "FromSelection" : "FromCursor");
        // Newlines are already collapsed by BuildContextQuery; TextTrimming adds the ellipsis.
        CtxLabel.Text = _context.Source == TextSource.Selection
            ? $"“{_contextQuery}”"
            : $"“…{ShortenEnd(_contextQuery)}”";
        CtxLabel.ToolTip = _contextQuery;
    }

    private void RefreshResults()
    {
        _llmCts?.Cancel();
        _aiChars.Clear();
        SetAiStatus(false);

        var typed = SearchBox.Text.Trim();
        if (typed.Length > 0)
        {
            var matches = EmojiMatcher.GetMatches(typed, _maxResults, _threshold);
            ShowResults(matches.Select(m => m.Emoji), L.T("SectionResults"), typed);
            return;
        }

        if (_contextQuery.Length > 0)
        {
            var matches = EmojiMatcher.GetMatches(_contextQuery, _maxResults, _threshold).Select(m => m.Emoji).ToList();

            if (matches.Count > 0)
            {
                ShowResults(matches, L.T("SectionSuggestions"), _contextQuery);
                RequestLlmSuggestions(matches);
                return;
            }

            // Nothing matched the context: show recents, the LLM may still find something.
            ShowDefaults();
            RequestLlmSuggestions(_items.Select(t => t.Entry).ToList());
            return;
        }

        ShowDefaults();
    }

    private void ShowDefaults()
    {
        var recent = RecentEmojiService.GetRecent()
            .Select(EmojiMatcher.FindByChar)
            .OfType<EmojiEntry>()
            .ToList();

        var fill = EmojiMatcher.GetPopularEmoji(_maxResults)
            .Where(p => recent.All(r => r.Char != p.Char));

        ShowResults(recent.Concat(fill).Take(_maxResults),
            L.T(recent.Count > 0 ? "SectionRecent" : "SectionPopular"), null);
    }

    /// <param name="query">Text the results were searched for, quoted in the "no emoji" message.</param>
    private void ShowResults(IEnumerable<EmojiEntry> entries, string label, string? query, int selectIndex = 0)
    {
        _items.Clear();
        foreach (var entry in entries) _items.Add(CreateTile(entry));

        SectionLabel.Text = label;
        EmptyText.Text = query is null ? L.T("NoEmoji") : L.T("NoResultsTitle", Shorten(query));
        EmptyHint.Text = query is null ? "" : L.T("NoResultsHint");
        EmptyState.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        Select(_items.Count > 0 ? Math.Clamp(selectIndex, 0, _items.Count - 1) : -1);
    }

    private EmojiTile CreateTile(EmojiEntry entry) =>
        new(entry, entry.WithSkinTone(_skinTone), _aiChars.Contains(entry.Char));

    /// <summary>
    /// Asks the local LLM to re-rank candidates for the captured context (not for typed searches)
    /// and moves its picks to the front. Emoji it adds are marked with a dot; the highlighted
    /// emoji stays highlighted. Cancelled when the user types or the window closes.
    /// </summary>
    private async void RequestLlmSuggestions(List<EmojiEntry> fuzzy)
    {
        if (_llm is null || _contextQuery.Length == 0) return;

        _llmCts?.Cancel();
        var cts = _llmCts = new CancellationTokenSource();

        var candidates = fuzzy
            .Concat(EmojiMatcher.GetPopularEmoji(LlmCandidateCount))
            .DistinctBy(e => e.Char)
            .Take(LlmCandidateCount)
            .ToList();

        SetAiStatus(true);
        try
        {
            var ranked = await _llm.GetLlmRecommendations(_contextQuery, candidates, cts.Token);
            if (cts.IsCancellationRequested || _isClosing || SearchBox.Text.Length > 0 || ranked.Count == 0) return;

            var shown = _items.Select(t => t.Entry.Char).ToHashSet();
            foreach (var entry in ranked.Where(e => !shown.Contains(e.Char))) _aiChars.Add(entry.Char);

            var keep = (EmojiList.SelectedItem as EmojiTile)?.Entry.Char;
            var merged = ranked.Concat(_items.Select(t => t.Entry)).DistinctBy(e => e.Char).Take(_maxResults).ToList();
            ShowResults(merged, SectionLabel.Text, _contextQuery,
                keep is null ? 0 : Math.Max(0, merged.FindIndex(e => e.Char == keep)));
        }
        catch (Exception ex)
        {
            LoggerService.Warn($"OverlayWindow: LLM suggestions failed — {ex.Message}");
        }
        finally
        {
            if (ReferenceEquals(_llmCts, cts)) SetAiStatus(false);
        }
    }

    private void SetAiStatus(bool refining)
    {
        AiText.Text = L.T("AiRefining");
        AiIndicator.Visibility = refining ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string Shorten(string text) =>
        text.Length <= MaxLabelQueryLength ? text : text[..MaxLabelQueryLength].TrimEnd() + "…";

    private static string ShortenEnd(string text) =>
        text.Length <= MaxLabelQueryLength ? text : text[^MaxLabelQueryLength..].TrimStart();

    // ─── Selection & navigation ───────────────────────────────────────────────

    private void Select(int index)
    {
        EmojiList.SelectedIndex = index;
        if (index >= 0) EmojiList.ScrollIntoView(_items[index]);
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (EmojiList.SelectedItem is EmojiTile tile)
        {
            PreviewChar.Content = CreateEmojiText(tile.Display, 20);
            PreviewName.Text = Capitalize(tile.Entry.GetDisplayName(_language));
            PreviewName.ToolTip = tile.Entry.Name;
        }
        else
        {
            PreviewChar.Content = null;
            PreviewName.Text = "";
        }
    }

    /// <summary>Emoji text in color (Emoji.Wpf) or monochrome, following ui.colorEmoji.</summary>
    private FrameworkElement CreateEmojiText(string text, double size)
    {
        TextBlock block;
        if (_config.Ui.ColorEmoji)
        {
            // Emoji.Wpf.TextBlock redeclares Text: set it on the derived type.
            var colored = new Emoji.Wpf.TextBlock();
            colored.Text = text;
            block = colored;
        }
        else
        {
            block = new TextBlock { Text = text, FontFamily = (FontFamily)FindResource("EmojiFont") };
        }

        block.FontSize = size;
        block.VerticalAlignment = VerticalAlignment.Center;
        block.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        return block;
    }

    // ─── Skin tone ────────────────────────────────────────────────────────────

    /// <summary>Cycles default → light → … → dark, re-renders the grid and remembers the choice.</summary>
    private void CycleSkinTone()
    {
        _skinTone = (_skinTone + 1) % ToneModifiers.Length;

        int selected = EmojiList.SelectedIndex;
        var entries = _items.Select(t => t.Entry).ToList();
        _items.Clear();
        foreach (var entry in entries) _items.Add(CreateTile(entry));
        Select(Math.Min(selected, _items.Count - 1));
        UpdateToneButton();

        ConfigService.Current.Ui.SkinTone = _skinTone;
        try { ConfigService.Save(); }
        catch (Exception ex) { LoggerService.Warn($"OverlayWindow: could not save skin tone — {ex.Message}"); }
    }

    private void UpdateToneButton() =>
        ToneButton.Content = CreateEmojiText("\u270B" + ToneModifiers[_skinTone], 15);

    private static string Capitalize(string text) =>
        string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>←/→/Tab: wraps around from the last emoji to the first and back.</summary>
    private void MoveHorizontal(int delta)
    {
        int n = _items.Count;
        if (n == 0) return;
        Select(((Math.Max(0, EmojiList.SelectedIndex) + delta) % n + n) % n);
    }

    /// <summary>↑/↓: one row, wrapping within the same column.</summary>
    private void MoveVerticalWrapped(int direction)
    {
        int n = _items.Count, c = _columns;
        if (n == 0) return;

        int s = Math.Max(0, EmojiList.SelectedIndex);
        if (direction > 0)
            s = s + c < n ? s + c : s % c;
        else if (s - c >= 0)
            s -= c;
        else
        {
            int last = s % c;
            while (last + c < n) last += c;
            s = Math.Min(last, n - 1);
        }
        Select(s);
    }

    /// <summary>PageUp/PageDown: several rows, stopping at the first/last row.</summary>
    private void MoveVertical(int rows)
    {
        if (_items.Count == 0) return;

        int current = Math.Max(0, EmojiList.SelectedIndex);
        int target = current + rows * _columns;
        if (target < 0)
            target = rows < -1 ? current % _columns : current;            // PageUp goes to the first row
        else if (target >= _items.Count)
        {
            int lastRow = (_items.Count - 1) / _columns;
            target = current / _columns < lastRow ? _items.Count - 1 : current; // partial last row
        }
        Select(target);
    }

    private void Commit(bool copyOnly)
    {
        if (EmojiList.SelectedItem is not EmojiTile tile) return;

        SelectedEmoji = tile.Display;
        SelectedBaseEmoji = tile.Entry.Char;
        CopyOnly = copyOnly;
        LoggerService.Info($"OverlayWindow: emoji selected '{SelectedEmoji}' ({(copyOnly ? "copy" : "insert")})");
        Close();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        switch (e.Key)
        {
            case Key.Escape:
                Close();
                break;

            // Ctrl+C copies the highlighted emoji and keeps the picker open,
            // unless text is selected in the search box (then it copies that text).
            case Key.C when ctrl && SearchBox.SelectionLength == 0:
                CopyHighlighted();
                break;

            case Key.Enter:
                Commit(copyOnly: shift);
                break;

            case Key.Left when !ctrl:
                MoveHorizontal(-1);
                break;

            case Key.Right when !ctrl:
                MoveHorizontal(1);
                break;

            case Key.T when ctrl:
                CycleSkinTone();
                break;

            case Key.Tab:
                MoveHorizontal(shift ? -1 : 1);
                break;

            case Key.Up:
                MoveVerticalWrapped(-1);
                break;

            case Key.Down:
                MoveVerticalWrapped(1);
                break;

            case Key.PageUp:
                MoveVertical(-_visibleRows);
                break;

            case Key.PageDown:
                MoveVertical(_visibleRows);
                break;

            default:
                return; // Let the search box handle typing, Backspace, Home/End, Ctrl+arrows…
        }

        e.Handled = true;
    }

    private void CopyHighlighted()
    {
        if (EmojiList.SelectedItem is not EmojiTile tile) return;

        if (ClipboardService.SetText(tile.Display))
        {
            RecentEmojiService.Add(tile.Entry.Char);
            PreviewName.Text = L.T("Copied");
            LoggerService.Info($"OverlayWindow: emoji copied '{tile.Display}'");
        }
    }

    // ─── Mouse ────────────────────────────────────────────────────────────────

    private int IndexFromEvent(RoutedEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return -1;
        var container = ItemsControl.ContainerFromElement(EmojiList, source) as ListBoxItem;
        return container is null ? -1 : EmojiList.ItemContainerGenerator.IndexFromContainer(container);
    }

    private void OnTileClicked(MouseButtonEventArgs e, bool copyOnly)
    {
        int index = IndexFromEvent(e);
        if (index < 0) return;

        Select(index);
        Commit(copyOnly);
        e.Handled = true;
    }

    /// <summary>
    /// The window has no title bar: dragging any non-interactive area moves it. The search box,
    /// buttons and tiles handle the mouse themselves, so the event never bubbles up from them.
    /// </summary>
    private void OnRootMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The button was released before the drag started.
        }
    }

    /// <summary>
    /// Hovering highlights a tile (and shows its name), but only on real mouse movement:
    /// MouseMove also fires when the grid re-renders under a still cursor.
    /// </summary>
    private void OnListMouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(this);
        if (_lastMousePosition == position) return;
        bool firstEvent = _lastMousePosition is null;
        _lastMousePosition = position;
        if (firstEvent) return;

        int index = IndexFromEvent(e);
        if (index >= 0 && index != EmojiList.SelectedIndex)
        {
            EmojiList.SelectedIndex = index;
            UpdatePreview();
        }
    }

    // ─── Themes ───────────────────────────────────────────────────────────────

    private readonly record struct Palette(
        Color Surface, Color SurfaceAlt, Color Stroke, Color Text, Color Muted, Color Accent, Color Hover)
    {
        public static readonly Palette Dark = new(
            Surface: Color.FromRgb(0x1C, 0x1D, 0x24),
            SurfaceAlt: Color.FromRgb(0x2A, 0x2C, 0x36),
            Stroke: Color.FromRgb(0x3A, 0x3D, 0x4A),
            Text: Color.FromRgb(0xF2, 0xF3, 0xF7),
            Muted: Color.FromRgb(0x9A, 0x9D, 0xAD),
            Accent: Color.FromRgb(0x8B, 0x7C, 0xFF),
            Hover: Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF));

        public static readonly Palette Light = new(
            Surface: Color.FromRgb(0xFB, 0xFB, 0xFD),
            SurfaceAlt: Color.FromRgb(0xEE, 0xF0, 0xF5),
            Stroke: Color.FromRgb(0xD9, 0xDC, 0xE5),
            Text: Color.FromRgb(0x1C, 0x1D, 0x22),
            Muted: Color.FromRgb(0x6B, 0x6F, 0x80),
            Accent: Color.FromRgb(0x6A, 0x55, 0xF5),
            Hover: Color.FromArgb(0x14, 0x00, 0x00, 0x00));
    }
}
