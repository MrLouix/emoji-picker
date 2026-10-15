using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using EmojiPick.Helpers;
using L = EmojiPick.Helpers.Localization;

namespace EmojiPick.Services;

public class TrayIcon : IDisposable
{
    private string _hotkeyText;
    private NotifyIcon? _notifyIcon;
    private Icon? _loadedIcon;
    private bool _disposed;

    public event EventHandler? QuitRequested;
    public event EventHandler? OpenRequested;
    public event EventHandler? SettingsRequested;

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
            Visible = true,
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) OpenRequested?.Invoke(this, EventArgs.Empty);
        };

        BuildMenu();
        ShowInfo(L.T("Running", _hotkeyText));

        LoggerService.Info("TrayIcon shown");
    }

    /// <summary>Re-creates tooltip and menu after the hotkey or the language changed.</summary>
    public void Refresh(string hotkeyText)
    {
        _hotkeyText = hotkeyText;
        BuildMenu();
    }

    private void BuildMenu()
    {
        if (_notifyIcon is null) return;

        _notifyIcon.Text = Truncate(L.T("TrayTip", _hotkeyText), 127);

        var menu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem($"{L.T("OpenPicker")}\t{_hotkeyText}") { Font = new Font(menu.Font, System.Drawing.FontStyle.Bold) };
        openItem.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);

        var settingsItem = new ToolStripMenuItem(L.T("Settings"));
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);

        var autoStartItem = new ToolStripMenuItem(L.T("StartWithWindows")) { CheckOnClick = true, Checked = AutoStartService.IsEnabled() };
        autoStartItem.Click += (_, _) =>
        {
            if (!AutoStartService.SetEnabled(autoStartItem.Checked))
                autoStartItem.Checked = AutoStartService.IsEnabled();
            ConfigService.Current.App.AutoStart = autoStartItem.Checked;
            TrySave();
        };
        // The settings window can change this too: refresh the check mark when the menu opens.
        menu.Opening += (_, _) => autoStartItem.Checked = AutoStartService.IsEnabled();

        var configFileItem = new ToolStripMenuItem(L.T("OpenSettingsFile"));
        configFileItem.Click += (_, _) => OpenPath(ConfigService.ConfigFilePath);

        var logsItem = new ToolStripMenuItem(L.T("OpenLogs"));
        logsItem.Click += (_, _) => OpenPath(ConfigService.LogDirectory);

        var aboutItem = new ToolStripMenuItem(L.T("About"));
        aboutItem.Click += (_, _) =>
            MessageBox.Show(
                L.T("AboutText", App.Version, _hotkeyText),
                L.T("About"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

        var quitItem = new ToolStripMenuItem(L.T("Quit"));
        quitItem.Click += (_, _) => QuitRequested?.Invoke(this, EventArgs.Empty);

        menu.Items.Add(openItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(autoStartItem);
        menu.Items.Add(configFileItem);
        menu.Items.Add(logsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(aboutItem);
        menu.Items.Add(quitItem);

        var previous = _notifyIcon.ContextMenuStrip;
        _notifyIcon.ContextMenuStrip = menu;
        previous?.Dispose();
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
