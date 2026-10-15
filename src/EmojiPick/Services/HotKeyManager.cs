using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Interop;
using EmojiPick.Helpers;
using EmojiPick.Models;

namespace EmojiPick.Services;

public class HotKeyManager : IDisposable
{
    private const int HotKeyId = 9000;
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly HotKeyConfig _config;
    private readonly uint _modifiers;
    private readonly uint _vkCode;
    private HwndSource? _hwndSource;
    private bool _disposed;

    public event EventHandler? HotKeyPressed;

    /// <summary>Human-readable shortcut, e.g. "Ctrl+Alt+E".</summary>
    public string DisplayText => string.Join("+", _config.Modifiers.Append(_config.Key.ToUpperInvariant()));

    public HotKeyManager(HotKeyConfig config)
    {
        _config = config;
        _modifiers = BuildModifiers(config.Modifiers);
        _vkCode = ResolveVirtualKey(config.Key);
    }

    /// <summary>Registers the global hotkey. Returns false if another application already owns it.</summary>
    public bool Register()
    {
        if (_vkCode == 0) return false;

        // Message-only window: receives WM_HOTKEY without ever being visible.
        var parameters = new HwndSourceParameters("EmojiPickHotkeyWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ParentWindow = HwndMessage,
        };

        _hwndSource = new HwndSource(parameters);
        _hwndSource.AddHook(WndProc);

        // MOD_NOREPEAT: holding the shortcut down must not reopen the picker in a loop.
        var success = NativeMethods.RegisterHotKey(_hwndSource.Handle, HotKeyId, _modifiers | NativeMethods.MOD_NOREPEAT, _vkCode);
        if (!success)
        {
            var error = Marshal.GetLastWin32Error();
            LoggerService.Warn($"RegisterHotKey failed (error {error}) — another app may hold {DisplayText}");
            return false;
        }

        LoggerService.Info($"Hotkey registered: {DisplayText}");
        return true;
    }

    public void Unregister()
    {
        if (_hwndSource is null) return;

        NativeMethods.UnregisterHotKey(_hwndSource.Handle, HotKeyId);
        _hwndSource.RemoveHook(WndProc);
        _hwndSource.Dispose();
        _hwndSource = null;

        LoggerService.Info("Hotkey unregistered");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unregister();
        GC.SuppressFinalize(this);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotKeyId)
        {
            HotKeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static uint BuildModifiers(string[] modifiers)
    {
        uint flags = 0;
        foreach (var mod in modifiers)
        {
            flags |= mod.Trim().ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL"     => (uint)NativeMethods.MOD_CONTROL,
                "ALT"                   => (uint)NativeMethods.MOD_ALT,
                "SHIFT"                 => (uint)NativeMethods.MOD_SHIFT,
                "WIN" or "WINDOWS"      => (uint)NativeMethods.MOD_WIN,
                _                       => 0u,
            };
        }
        return flags;
    }

    internal static uint ResolveVirtualKey(string key)
    {
        key = key.Trim();

        // "1" would otherwise parse as the numeric enum value 1 (= Keys.LButton).
        if (key.Length == 1 && char.IsDigit(key[0]))
            key = "D" + key;

        if (!key.All(char.IsDigit) && Enum.TryParse<Keys>(key, ignoreCase: true, out var parsedKey))
            return (uint)parsedKey;

        LoggerService.Warn($"Cannot resolve virtual key '{key}' — hotkey may not trigger");
        return 0;
    }
}
