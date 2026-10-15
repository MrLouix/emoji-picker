using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EmojiPick.Models;
using EmojiPick.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using L = EmojiPick.Helpers.Localization;
using MessageBox = System.Windows.MessageBox;

namespace EmojiPick.Windows;

/// <summary>
/// Edits a copy of the configuration; "Save" validates it, writes config.json and
/// raises <see cref="ConfigService.Changed"/> so the app re-applies hotkey, language, etc.
/// </summary>
public partial class SettingsWindow : System.Windows.Window
{
    private static readonly string[] ToneModifiers = { "", "\U0001F3FB", "\U0001F3FC", "\U0001F3FD", "\U0001F3FE", "\U0001F3FF" };

    private readonly Config _config;
    private string _hotkeyKey;

    public SettingsWindow()
    {
        InitializeComponent();

        _config = ConfigService.Clone(ConfigService.Current);
        _hotkeyKey = _config.Hotkey.Key;

        ApplyTexts();
        LoadValues();

        HotkeyKeyBox.PreviewKeyDown += OnHotkeyKeyDown;
        SaveButton.Click += (_, _) => Save();
        CancelButton.Click += (_, _) => Close();
        OpenFileLink.MouseLeftButtonUp += (_, _) => OpenConfigFile();
    }

    private void ApplyTexts()
    {
        Title = L.T("SettingsTitle");
        TitleText.Text = L.T("SettingsTitle");
        ShortcutTitle.Text = L.T("SectionShortcut");
        AppearanceTitle.Text = L.T("SectionAppearance");
        BehaviorTitle.Text = L.T("SectionBehavior");
        AiTitle.Text = L.T("SectionAi");

        HotkeyLabel.Text = L.T("Hotkey");
        HotkeyKeyBox.ToolTip = L.T("PressKey");
        ThemeLabel.Text = L.T("Theme");
        LanguageLabel.Text = L.T("Language");
        SkinToneLabel.Text = L.T("SkinTone");
        ColorEmojiBox.Content = L.T("ColorEmoji");
        SizeLabel.Text = L.T("EmojiSize");
        ColumnsLabel.Text = L.T("Columns");
        RowsLabel.Text = L.T("Rows");
        PositionLabel.Text = L.T("Position");
        InjectLabel.Text = L.T("InjectMode");
        RestoreClipboardBox.Content = L.T("RestoreClipboard");
        AutoStartBox.Content = L.T("StartWithWindows");
        AiEnabledBox.Content = L.T("AiEnabled");
        EndpointLabel.Text = L.T("Endpoint");
        ModelLabel.Text = L.T("Model");
        OpenFileLink.Text = L.T("OpenSettingsFile");
        SaveButton.Content = L.T("Save");
        CancelButton.Content = L.T("Cancel");
    }

    private void LoadValues()
    {
        var mods = _config.Hotkey.Modifiers.Select(m => m.Trim().ToUpperInvariant()).ToHashSet();
        ModCtrl.IsChecked = mods.Contains("CTRL") || mods.Contains("CONTROL");
        ModAlt.IsChecked = mods.Contains("ALT");
        ModShift.IsChecked = mods.Contains("SHIFT");
        ModWin.IsChecked = mods.Contains("WIN") || mods.Contains("WINDOWS");
        HotkeyKeyBox.Text = DisplayKey(_hotkeyKey);

        Fill(ThemeBox, _config.Ui.Theme,
            ("system", L.T("ThemeSystem")), ("dark", L.T("ThemeDark")), ("light", L.T("ThemeLight")));
        Fill(LanguageBox, _config.Language,
            ("auto", L.T("LanguageAuto")), ("en", "English"), ("fr", "Français"));
        Fill(PositionBox, _config.Ui.PositionMode,
            ("caret", L.T("PositionCaret")), ("cursor", L.T("PositionCursor")), ("center", L.T("PositionCenter")));
        Fill(InjectBox, _config.Behavior.InjectMode,
            ("paste", L.T("InjectPaste")), ("type", L.T("InjectType")), ("clipboard", L.T("InjectClipboard")));

        for (int tone = 0; tone < ToneModifiers.Length; tone++)
        {
            var sample = new Emoji.Wpf.TextBlock();
            sample.Text = "✋" + ToneModifiers[tone] + (tone == 0 ? "  " + L.T("ToneDefault") : "");
            sample.FontSize = 15;
            SkinToneBox.Items.Add(new ComboBoxItem { Content = sample, Tag = tone });
        }
        SkinToneBox.SelectedIndex = _config.Ui.SkinTone;

        ColorEmojiBox.IsChecked = _config.Ui.ColorEmoji;
        SizeSlider.Value = _config.Ui.FontSize;
        ColumnsSlider.Value = _config.Ui.GridColumns;
        RowsSlider.Value = _config.Ui.GridRows;

        RestoreClipboardBox.IsChecked = _config.Behavior.RestoreClipboard;
        AutoStartBox.IsChecked = AutoStartService.IsEnabled();

        var ollama = Ollama();
        AiEnabledBox.IsChecked = _config.Llm.Enabled && ollama.Enabled;
        EndpointBox.Text = ollama.Endpoint;
        ModelBox.Text = ollama.Model;
    }

    private ProviderConfig Ollama()
    {
        if (!_config.Llm.Providers.TryGetValue("ollama", out var ollama))
        {
            ollama = new Config().Llm.Providers["ollama"];
            _config.Llm.Providers["ollama"] = ollama;
        }
        return ollama;
    }

    private static void Fill(System.Windows.Controls.ComboBox box, string current, params (string Value, string Label)[] options)
    {
        foreach (var (value, label) in options)
            box.Items.Add(new ComboBoxItem { Content = label, Tag = value });

        var index = Array.FindIndex(options, o => o.Value.Equals(current?.Trim(), StringComparison.OrdinalIgnoreCase));
        box.SelectedIndex = Math.Max(0, index);
    }

    private static string Selected(System.Windows.Controls.ComboBox box, string fallback) =>
        (box.SelectedItem as ComboBoxItem)?.Tag as string ?? fallback;

    /// <summary>The next non-modifier key pressed in the box becomes the hotkey key.</summary>
    private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Tab or Key.Escape or Key.Enter) return; // keep dialog navigation
        e.Handled = true;

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        var vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0) return;

        _hotkeyKey = ((System.Windows.Forms.Keys)vk).ToString();
        HotkeyKeyBox.Text = DisplayKey(_hotkeyKey);
    }

    private static string DisplayKey(string key) =>
        key.Length == 2 && key[0] == 'D' && char.IsDigit(key[1]) ? key[1..] : key.ToUpperInvariant();

    private void Save()
    {
        var modifiers = new List<string>();
        if (ModCtrl.IsChecked == true) modifiers.Add("Ctrl");
        if (ModAlt.IsChecked == true) modifiers.Add("Alt");
        if (ModShift.IsChecked == true) modifiers.Add("Shift");
        if (ModWin.IsChecked == true) modifiers.Add("Win");
        if (modifiers.Count == 0)
        {
            MessageBox.Show(this, L.T("NeedModifier"), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _config.Hotkey.Modifiers = modifiers.ToArray();
        _config.Hotkey.Key = _hotkeyKey;

        _config.Ui.Theme = Selected(ThemeBox, "system");
        _config.Language = Selected(LanguageBox, "auto");
        _config.Ui.SkinTone = (SkinToneBox.SelectedItem as ComboBoxItem)?.Tag as int? ?? 0;
        _config.Ui.ColorEmoji = ColorEmojiBox.IsChecked == true;
        _config.Ui.FontSize = (int)SizeSlider.Value;
        _config.Ui.GridColumns = (int)ColumnsSlider.Value;
        _config.Ui.GridRows = (int)RowsSlider.Value;
        _config.Ui.PositionMode = Selected(PositionBox, "caret");

        _config.Behavior.InjectMode = Selected(InjectBox, "paste");
        _config.Behavior.RestoreClipboard = RestoreClipboardBox.IsChecked == true;

        var ollama = Ollama();
        bool aiEnabled = AiEnabledBox.IsChecked == true;
        _config.Llm.Enabled = aiEnabled;
        ollama.Enabled = aiEnabled;
        ollama.Endpoint = EndpointBox.Text.Trim();
        ollama.Model = ModelBox.Text.Trim();

        bool autoStart = AutoStartBox.IsChecked == true;
        if (autoStart != AutoStartService.IsEnabled()) AutoStartService.SetEnabled(autoStart);
        _config.App.AutoStart = autoStart;

        try
        {
            ConfigService.Update(_config);
            LoggerService.Info("Settings saved from the settings window");
            Close();
        }
        catch (Exception ex)
        {
            LoggerService.Error("Could not save settings", ex);
            MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void OpenConfigFile()
    {
        try
        {
            Process.Start(new ProcessStartInfo(ConfigService.ConfigFilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LoggerService.Warn($"SettingsWindow: could not open config file — {ex.Message}");
        }
    }
}
