using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EmojiPick.Models;
using EmojiPick.Services;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Orientation = System.Windows.Controls.Orientation;

namespace EmojiPick.Windows;

public partial class OverlayWindow : System.Windows.Window
{
    private readonly SelectionHandler _selectionHandler;
    private List<EmojiMatch> _matches = new List<EmojiMatch>();
    private int _selectedIndex = -1;
    private int _gridColumns = 4;

    public TextContext? InitialContext { get; private set; }
    public string? SelectedEmoji { get; private set; }

    public OverlayWindow(SelectionHandler selectionHandler)
    {
        _selectionHandler = selectionHandler;
        InitializeComponent();

        SearchBox.TextChanged += SearchBox_TextChanged;
        KeyDown += OverlayWindow_KeyDown;
        PreviewKeyDown += OverlayWindow_KeyDown;
        Deactivated += OverlayWindow_Deactivated;

        var config = ConfigService.Current;
        _gridColumns = config.Ui.GridColumns;
        EmojiGrid.Columns = _gridColumns;
        EmojiGrid.Rows = config.Ui.GridRows;

        var alpha = (byte)(config.Ui.WindowOpacity * 255);
        Background = new SolidColorBrush(Color.FromArgb(alpha, 0x22, 0x22, 0x33));
    }

    public void PositionAtCursor()
    {
        var pos = System.Windows.Forms.Cursor.Position;
        Left = pos.X - (Width / 2);
        Top = pos.Y + 16;
    }

    public async Task InitializeAsync()
    {
        InputSimulator.SendCtrlC();
        await Task.Delay(150);

        InitialContext = _selectionHandler.GetTextContext();

        var text = InitialContext?.Text ?? "";
        var display = text.Length > 40
            ? text[..40]
            : text;
        CtxLabel.Text = display;

        var query = text.Trim();
        _matches = string.IsNullOrEmpty(query) ? new List<EmojiMatch>() : EmojiMatcher.GetMatches(query);

        RenderGrid();
        PositionAtCursor();
        SearchBox.Focus();
    }

    private void RenderGrid()
    {
        EmojiGrid.Children.Clear();
        int maxItems = _gridColumns * EmojiGrid.Rows;

        for (int i = 0; i < maxItems; i++)
        {
            var btn = new Button { Margin = new Thickness(4) };

            if (i < _matches.Count)
            {
                var match = _matches[i];
                var panel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                };

                panel.Children.Add(new TextBlock
                {
                    Text = match.Emoji.Char,
                    FontSize = ConfigService.Current.Ui.FontSize,
                    HorizontalAlignment = HorizontalAlignment.Center,
                });

                panel.Children.Add(new TextBlock
                {
                    Text = string.Join(", ", match.Emoji.Tags),
                    FontSize = 9,
                    Foreground = Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 80,
                });

                btn.Content = panel;
                btn.ToolTip = $"{match.Emoji.Name} ({match.CombinedScore})";
            }
            else
            {
                btn.Visibility = Visibility.Collapsed;
            }

            btn.Tag = i;
            var capturedIndex = i;
            btn.Click += (_, _) => SelectAndClose(capturedIndex);
            EmojiGrid.Children.Add(btn);
        }

        UpdateSelection();
    }

    private void UpdateSelection()
    {
        if (_matches.Count > 0)
            _selectedIndex = Math.Clamp(_selectedIndex, 0, _matches.Count - 1);
        else
            _selectedIndex = -1;

        int idx = 0;
        foreach (Button btn in EmojiGrid.Children)
        {
            if (idx == _selectedIndex)
            {
                btn.BorderThickness = new Thickness(2);
                btn.BorderBrush = Brushes.White;
                btn.Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
            }
            else
            {
                btn.BorderThickness = new Thickness(1);
                btn.BorderBrush = Brushes.Transparent;
                btn.Background = null;
            }
            idx++;
        }
    }

    private void SelectAndClose(int index)
    {
        if (index >= 0 && index < _matches.Count)
        {
            SelectedEmoji = _matches[index].Emoji.Char;
            LoggerService.Info($"OverlayWindow: emoji sélectionné '{SelectedEmoji}' à l'index {index}");
            Close();
        }
    }

    private void OverlayWindow_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                break;

            case Key.Up:
                if (_matches.Count > 0)
                {
                    _selectedIndex -= _gridColumns;
                    if (_selectedIndex < 0)
                        _selectedIndex = Math.Max(0, _matches.Count - 1);
                    UpdateSelection();
                }
                e.Handled = true;
                break;

            case Key.Down:
                if (_matches.Count > 0)
                {
                    _selectedIndex += _gridColumns;
                    if (_selectedIndex >= _matches.Count)
                        _selectedIndex = 0;
                    UpdateSelection();
                }
                e.Handled = true;
                break;

            case Key.Left:
                if (_matches.Count > 0)
                {
                    _selectedIndex--;
                    if (_selectedIndex < 0)
                        _selectedIndex = _matches.Count - 1;
                    UpdateSelection();
                }
                e.Handled = true;
                break;

            case Key.Right:
                if (_matches.Count > 0)
                {
                    _selectedIndex++;
                    if (_selectedIndex >= _matches.Count)
                        _selectedIndex = 0;
                    UpdateSelection();
                }
                e.Handled = true;
                break;

            case Key.Enter:
                SelectAndClose(_selectedIndex);
                e.Handled = true;
                break;

            case Key.Back:
                SearchBox.Clear();
                SearchBox.Focus();
                e.Handled = true;
                break;
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text.Trim();
        if (string.IsNullOrEmpty(query))
            query = InitialContext?.Text.Trim() ?? "";

        _matches = string.IsNullOrEmpty(query) ? new List<EmojiMatch>() : EmojiMatcher.GetMatches(query);
        _selectedIndex = _matches.Count > 0 ? 0 : -1;
        RenderGrid();
    }

    private void OverlayWindow_Deactivated(object? sender, EventArgs e)
    {
        Close();
    }
}
