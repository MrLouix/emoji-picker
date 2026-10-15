using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using EmojiPick.Helpers;
using EmojiPick.Models;
using EmojiPick.Services;
using EmojiPick.Windows;
using Application = System.Windows.Application;
using L = EmojiPick.Helpers.Localization;
using MessageBox = System.Windows.MessageBox;

namespace EmojiPick;

public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\EmojiPick.SingleInstance";

    /// <summary>Delay before restoring the user's clipboard, so the target app has read the emoji.</summary>
    private const int ClipboardRestoreDelayMs = 400;

    private Mutex? _instanceMutex;
    private TrayIcon? _trayIcon;
    private HotKeyManager? _hotKeyManager;
    private OllamaMatcher? _llm;
    private OverlayWindow? _overlay;
    private SettingsWindow? _settings;
    private bool _opening;

    public static string Version { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        L.Apply("auto");

        _instanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            _instanceMutex.Dispose();
            _instanceMutex = null;
            MessageBox.Show(L.T("AlreadyRunning"), "EmojiPick", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LoggerService.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };

        try
        {
            LoggerService.Initialize();
            LoggerService.Info($"EmojiPick v{Version} starting...");

            ConfigService.EnsureDirectories();
            var config = ConfigService.Load();
            LoggerService.SetMinimumLevel(config.Logging.Level);
            L.Apply(config.Language);
            LoggerService.Info("Configuration loaded");

            var registered = RegisterHotKey(config);

            _trayIcon = new TrayIcon(_hotKeyManager!.DisplayText);
            _trayIcon.QuitRequested += (_, _) => Shutdown();
            _trayIcon.OpenRequested += (_, _) => _ = TogglePickerAsync(fromHotkey: false);
            _trayIcon.SettingsRequested += (_, _) => OpenSettings();
            _trayIcon.Show();

            if (!registered)
                _trayIcon.ShowWarning(L.T("HotkeyFailed", _hotKeyManager.DisplayText));

            _llm = CreateLlmMatcher(config);
            ConfigService.Changed += (_, _) => Dispatcher.Invoke(ApplyConfig);

            // Decompress the emoji database in the background so the first opening is instant.
            _ = Task.Run(() => EmojiMatcher.GetPopularEmoji());

            LoggerService.Info($"EmojiPick v{Version} ready — waiting for hotkey trigger");
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

    /// <summary>(Re)registers the global hotkey from the configuration.</summary>
    private bool RegisterHotKey(Config config)
    {
        _hotKeyManager?.Dispose();
        _hotKeyManager = new HotKeyManager(config.Hotkey);
        _hotKeyManager.HotKeyPressed += (_, _) => _ = TogglePickerAsync(fromHotkey: true);
        return _hotKeyManager.Register();
    }

    /// <summary>Applies a configuration saved from the settings window without restarting.</summary>
    private void ApplyConfig()
    {
        var config = ConfigService.Current;
        LoggerService.SetMinimumLevel(config.Logging.Level);
        L.Apply(config.Language);

        var registered = RegisterHotKey(config);
        _trayIcon?.Refresh(_hotKeyManager!.DisplayText);
        if (!registered)
            _trayIcon?.ShowWarning(L.T("HotkeyFailed", _hotKeyManager!.DisplayText));
        else
            _trayIcon?.ShowInfo(L.T("Saved"));

        _llm?.Dispose();
        _llm = CreateLlmMatcher(config);

        LoggerService.Info("Configuration re-applied");
    }

    private void OpenSettings()
    {
        _overlay?.Close();

        if (_settings != null)
        {
            _settings.Activate();
            return;
        }

        _settings = new SettingsWindow();
        _settings.Closed += (_, _) => _settings = null;
        _settings.Show();
        _settings.Activate();
    }

    private static OllamaMatcher? CreateLlmMatcher(Config config)
    {
        if (!config.Llm.Enabled) return null;
        if (!config.Llm.Providers.TryGetValue("ollama", out var ollama) || !ollama.Enabled) return null;

        try
        {
            LoggerService.Info($"LLM suggestions enabled via Ollama ({ollama.Endpoint}, model {ollama.Model})");
            return new OllamaMatcher(ollama);
        }
        catch (Exception ex)
        {
            LoggerService.Warn($"Ollama provider disabled: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Opens the picker, or closes it if it is already open (pressing the hotkey twice).
    /// </summary>
    private async Task TogglePickerAsync(bool fromHotkey)
    {
        if (_overlay != null)
        {
            _overlay.Close();
            return;
        }
        if (_opening) return;
        _opening = true;

        try
        {
            // Opened from the tray: the foreground window is the taskbar, so there is nowhere to paste.
            var target = fromHotkey ? NativeMethods.GetForegroundWindow() : IntPtr.Zero;
            var context = TextContext.Empty;

            if (fromHotkey)
            {
                var released = await InputSimulator.WaitForModifiersReleasedAsync();
                context = await new SelectionHandler().CaptureAsync(target, allowCopy: released);
            }

            var overlay = new OverlayWindow(context, _llm);
            overlay.Closed += async (_, _) =>
            {
                if (ReferenceEquals(_overlay, overlay)) _overlay = null;
                await InsertEmojiAsync(overlay.SelectedEmoji, overlay.SelectedBaseEmoji, overlay.CopyOnly, target, context.HasSelection);
            };
            // Open settings once the overlay has fully closed.
            overlay.SettingsRequested += (_, _) => Dispatcher.BeginInvoke(new Action(OpenSettings));

            _overlay = overlay;
            overlay.Show();
            overlay.Activate();
        }
        catch (Exception ex)
        {
            LoggerService.Error("Overlay failed: " + ex.Message, ex);
        }
        finally
        {
            _opening = false;
        }
    }

    /// <param name="afterSelection">Text was selected in the target: the emoji goes after it instead of replacing it.</param>
    private async Task InsertEmojiAsync(string? emoji, string? baseEmoji, bool copyOnly, IntPtr target, bool afterSelection)
    {
        if (string.IsNullOrEmpty(emoji)) return;

        try
        {
            // Recents keep the base emoji so they follow the current skin tone setting.
            RecentEmojiService.Add(baseEmoji ?? emoji);

            var behavior = ConfigService.Current.Behavior;
            var mode = copyOnly || target == IntPtr.Zero ? "clipboard" : behavior.InjectMode.Trim().ToLowerInvariant();

            if (mode != "clipboard")
            {
                await FocusTargetAsync(target);
                if (afterSelection) InputSimulator.SendRightArrow();
            }

            switch (mode)
            {
                case "clipboard":
                    ClipboardService.SetText(emoji);
                    LoggerService.Info("Copied emoji to clipboard: " + emoji);
                    break;

                case "type":
                    InputSimulator.SendUnicodeText(emoji);
                    LoggerService.Info("Typed emoji: " + emoji);
                    break;

                default:
                    var snapshot = behavior.RestoreClipboard ? ClipboardService.Capture() : null;
                    if (!ClipboardService.SetText(emoji)) return;
                    var sequence = ClipboardService.SequenceNumber;
                    InputSimulator.SendCtrlV();
                    LoggerService.Info("Injected emoji: " + emoji);

                    if (behavior.RestoreClipboard)
                    {
                        await Task.Delay(ClipboardRestoreDelayMs);
                        // Only restore if nobody (the user, another app) changed the clipboard meanwhile.
                        if (ClipboardService.SequenceNumber == sequence)
                            ClipboardService.Restore(snapshot);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            LoggerService.Error("Emoji injection failed: " + ex.Message, ex);
        }
    }

    /// <summary>Gives focus back to the window that was active before the picker opened.</summary>
    private static async Task FocusTargetAsync(IntPtr target)
    {
        if (target == IntPtr.Zero || NativeMethods.GetForegroundWindow() == target) return;

        NativeMethods.SetForegroundWindow(target);
        var deadline = Environment.TickCount64 + 300;
        while (NativeMethods.GetForegroundWindow() != target && Environment.TickCount64 < deadline)
            await Task.Delay(15);
        await Task.Delay(30);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LoggerService.Error("Unhandled UI exception: " + e.Exception.Message, e.Exception);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        LoggerService.Info("EmojiPick shutting down...");

        _overlay?.Close();
        _settings?.Close();
        _hotKeyManager?.Dispose();
        _trayIcon?.Dispose();
        _llm?.Dispose();

        if (_instanceMutex != null)
        {
            _instanceMutex.ReleaseMutex();
            _instanceMutex.Dispose();
        }

        LoggerService.Flush();
        base.OnExit(e);
    }
}
