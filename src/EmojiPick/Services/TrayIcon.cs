using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using EmojiPick.Helpers;

namespace EmojiPick.Services;

public class TrayIcon : IDisposable
{
    private readonly string _hotkeyText;
    private NotifyIcon? _notifyIcon;
    private Icon? _loadedIcon;
    private bool _disposed;

    public event EventHandler? QuitRequested;
    public event EventHandler? OpenRequested;

    public TrayIcon(string hotkeyText)
    {
        _hotkeyText = hotkeyText;
    }

    public void Show()
    {
        _loadedIcon = LoadIcon();

        _notifyIcon = new NotifyIcon
        {
            Icon = _loadedIcon,
            Text = Truncate($"EmojiPick — {_hotkeyText} to pick an emoji", 127),
            Visible = true,
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) OpenRequested?.Invoke(this, EventArgs.Empty);
        };

        var menu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem($"Open picker\t{_hotkeyText}") { Font = new Font(menu.Font, System.Drawing.FontStyle.Bold) };
        openItem.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);

        var autoStartItem = new ToolStripMenuItem("Start with Windows") { CheckOnClick = true, Checked = AutoStartService.IsEnabled() };
        autoStartItem.Click += (_, _) =>
        {
            if (!AutoStartService.SetEnabled(autoStartItem.Checked))
                autoStartItem.Checked = AutoStartService.IsEnabled();
            ConfigService.Current.App.AutoStart = autoStartItem.Checked;
            TrySave();
        };

        var settingsItem = new ToolStripMenuItem("Open settings file");
        settingsItem.Click += (_, _) => OpenPath(ConfigService.ConfigFilePath);

        var logsItem = new ToolStripMenuItem("Open logs folder");
        logsItem.Click += (_, _) => OpenPath(ConfigService.LogDirectory);

        var aboutItem = new ToolStripMenuItem("About EmojiPick");
        aboutItem.Click += (_, _) =>
            MessageBox.Show(
                $"EmojiPick v{App.Version}\n\n" +
                $"Press {_hotkeyText} to open the emoji picker.\n" +
                "Type to search, arrow keys to navigate, Enter to insert,\n" +
                "Shift+Enter (or right-click) to copy, Esc to close.\n\n" +
                "The selected emoji will be inserted at the cursor position.",
                "About EmojiPick",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

        var quitItem = new ToolStripMenuItem("Quit");
        quitItem.Click += (_, _) => QuitRequested?.Invoke(this, EventArgs.Empty);

        menu.Items.Add(openItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(autoStartItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(logsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(aboutItem);
        menu.Items.Add(quitItem);

        _notifyIcon.ContextMenuStrip = menu;

        ShowInfo($"Running in background. Press {_hotkeyText} to pick an emoji.");

        LoggerService.Info("TrayIcon shown");
    }

    public void ShowInfo(string message) =>
        _notifyIcon?.ShowBalloonTip(3000, "EmojiPick", message, ToolTipIcon.Info);

    public void ShowWarning(string message) =>
        _notifyIcon?.ShowBalloonTip(5000, "EmojiPick", message, ToolTipIcon.Warning);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _loadedIcon?.Dispose();
        _loadedIcon = null;

        LoggerService.Info("TrayIcon disposed");
        GC.SuppressFinalize(this);
    }

    private static void TrySave()
    {
        try { ConfigService.Save(); }
        catch (Exception ex) { LoggerService.Warn($"TrayIcon: could not save config — {ex.Message}"); }
    }

    private static void OpenPath(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LoggerService.Warn($"TrayIcon: could not open {path} — {ex.Message}");
        }
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

    private static Icon LoadIcon()
    {
        try
        {
            var bytes = ResourceLoader.LoadEmbeddedResource("EmojiPick.app.ico");
            if (bytes is { Length: > 0 })
            {
                using var ms = new MemoryStream(bytes);
                return new Icon(ms);
            }
        }
        catch (Exception ex)
        {
            LoggerService.Warn($"TrayIcon: failed to load embedded icon — {ex.Message}");
        }

        return (Icon)SystemIcons.Application.Clone();
    }
}
