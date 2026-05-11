using System.Drawing;
using System.Windows.Forms;
using EmojiPick.Helpers;

namespace EmojiPick.Services;

public class TrayIcon : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private Icon? _loadedIcon;
    private bool _disposed;

    public event EventHandler? QuitRequested;

    public void Show()
    {
        _loadedIcon = LoadIcon();

        _notifyIcon = new NotifyIcon
        {
            Icon = _loadedIcon,
            Text = "EmojiPick — Press Ctrl+Alt+E to pick an emoji",
            Visible = true,
        };

        var menu = new ContextMenuStrip();

        var aboutItem = new ToolStripMenuItem("About EmojiPick");
        aboutItem.Click += (_, _) =>
            MessageBox.Show(
                "EmojiPick v1.0.0\n\nPress Ctrl+Alt+E to open the emoji picker.\n" +
                "Use arrow keys to navigate, Enter to select.\n" +
                "The selected emoji will be inserted at the cursor position.",
                "About EmojiPick",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

        var quitItem = new ToolStripMenuItem("Quit");
        quitItem.Click += (_, _) => QuitRequested?.Invoke(this, EventArgs.Empty);

        menu.Items.Add(aboutItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(quitItem);

        _notifyIcon.ContextMenuStrip = menu;

        _notifyIcon.ShowBalloonTip(
            3000,
            "EmojiPick",
            "Running in background. Press Ctrl+Alt+E to pick an emoji.",
            ToolTipIcon.Info);

        LoggerService.Info("TrayIcon shown");
    }

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
    }

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
