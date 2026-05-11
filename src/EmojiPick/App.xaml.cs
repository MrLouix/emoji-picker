using System;
using System.Windows;
using EmojiPick.Services;
using EmojiPick.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace EmojiPick;

public partial class App : Application
{
    private TrayIcon? _trayIcon;
    private HotKeyManager? _hotKeyManager;
    private OverlayWindow? _overlay;
    private bool _isShuttingDown;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            LoggerService.Initialize();
            LoggerService.Info("EmojiPick v1.0.0 starting...");

            ConfigService.EnsureDirectories();
            var config = ConfigService.Load();
            LoggerService.Info("Configuration loaded");

            _trayIcon = new TrayIcon();
            _trayIcon.QuitRequested += (_, _) => Shutdown();
            _trayIcon.Show();

            _hotKeyManager = new HotKeyManager(config.Hotkey);
            _hotKeyManager.HotKeyPressed += OnHotKeyPressed;
            _hotKeyManager.Register();

            _ = EmojiMatcher.GetMatches("");
            LoggerService.Info("EmojiPick v1.0.0 ready — waiting for hotkey trigger");
        }
        catch (Exception ex)
        {
            LoggerService.Error($"Startup failed: {ex.Message}", ex);
            MessageBox.Show(
                $"Startup error: {ex.Message}",
                "EmojiPick Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void OnHotKeyPressed(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(async () =>
        {
            if (_overlay != null)
            {
                try { _overlay.Close(); } catch { }
            }

            try
            {
                var selectionHandler = new SelectionHandler();
                _overlay = new OverlayWindow(selectionHandler);
                _overlay.Closed += (_, _) =>
                {
                    var emoji = _overlay?.SelectedEmoji;
                    if (!string.IsNullOrEmpty(emoji))
                    {
                        ClipboardService.SetText(emoji);
                        InputSimulator.SendCtrlV();
                        LoggerService.Info("Injected emoji: " + emoji);
                    }
                    _overlay = null;
                };

                await _overlay.InitializeAsync();
                _overlay.Show();
                _overlay.Activate();
                _overlay.Topmost = true;
            }
            catch (Exception ex)
            {
                LoggerService.Error("Overlay failed: " + ex.Message, ex);
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_isShuttingDown) return;
        _isShuttingDown = true;

        LoggerService.Info("EmojiPick shutting down...");

        try { ConfigService.Save(); } catch { }
        _hotKeyManager?.Unregister();
        _overlay?.Close();
        _trayIcon?.Dispose();

        base.OnExit(e);
    }
}
