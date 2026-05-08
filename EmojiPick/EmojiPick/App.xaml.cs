using System;
using System.Windows;
using EmojiPick.Services;

namespace EmojiPick;

/// <summary>
/// Application entry point.
/// Lifecycle: Startup → Init services → Hide window → Show TrayIcon → Wait for hotkey.
/// </summary>
public partial class App : Application
{
    private TrayIcon? _trayIcon;
    private HotKeyManager? _hotKeyManager;
    private bool _isShuttingDown;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // 1. Setup logging
            LoggerService.Initialize();
            LoggerService.Info("EmojiPick v1.0.0 starting...");

            // 2. Ensure installation directories & default config
            EnsureInstallation();

            // 3. Load configuration
            var config = ConfigService.Load();
            LoggerService.Info($"Configuration loaded from {ConfigService.ConfigFilePath}");

            // 4. Load emoji database (Phase 5)
            // var emojiDb = await EmojiMatcher.LoadDatabaseAsync();

            // 5. Initialize LLM provider (Phase 6-7)
            // var factory = new LlmProviderFactory(config);
            // var llmMatcher = await factory.CreateProvider();

            // 6. Initialize hotkey manager (Phase 3)
            // _hotKeyManager = new HotKeyManager(config.Hotkey);
            // _hotKeyManager.HotKeyPressed += OnHotKeyPressed;
            // _hotKeyManager.Register();

            // 7. Show system tray icon — main window stays hidden
            _trayIcon = new TrayIcon();
            _trayIcon.Show();

            LoggerService.Info("EmojiPick v1.0.0 ready — waiting for hotkey trigger");
        }
        catch (Exception ex)
        {
            LoggerService.Error($"Startup failed: {ex.Message}");
            MessageBox.Show(
                $"Startup error: {ex.Message}",
                "EmojiPick Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_isShuttingDown) return;
        _isShuttingDown = true;

        LoggerService.Info("EmojiPick shutting down...");

        // Save configuration
        try { ConfigService.Save(); } catch { }

        // Unregister hotkey
        _hotKeyManager?.Unregister();

        // Dispose tray icon
        _trayIcon?.Dispose();

        LoggerService.Info("EmojiPick shutdown complete");
        base.OnExit(e);
    }

    private static void EnsureInstallation()
    {
        // Create %APPDATA%\EmojiPick and subdirectories
        ConfigService.EnsureDirectories();
    }

    private void OnHotKeyPressed(object? sender, EventArgs e)
    {
        // Phase 4-5: capture text, match, show overlay
        // Dispatcher.Invoke(async () => { await HandleHotKeyTrigger(); });
    }
}
