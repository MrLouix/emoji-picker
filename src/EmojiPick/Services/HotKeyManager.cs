using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Interop;
using EmojiPick.Helpers;
using EmojiPick.Models;

namespace EmojiPick.Services;

public class HotKeyManager : IDisposable
{
    private const int HotKeyId = 9000;

    private readonly HotKeyConfig _config;
    private readonly uint _modifiers;
    private readonly uint _vkCode;
    private HwndSource? _hwndSource;
    private bool _disposed;

    public event EventHandler? HotKeyPressed;

    public HotKeyManager(HotKeyConfig config)
    {
        _config = config;
        _modifiers = BuildModifiers(config.Modifiers);
        _vkCode = ResolveVirtualKey(config.Key);
    }

    public void Register()
    {
        var parameters = new HwndSourceParameters("EmojiPickHotkeyWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
        };

        _hwndSource = new HwndSource(parameters);
        _hwndSource.AddHook(WndProc);

        var success = NativeMethods.RegisterHotKey(_hwndSource.Handle, HotKeyId, _modifiers, _vkCode);
        if (!success)
        {
            var error = Marshal.GetLastWin32Error();
            LoggerService.Warn($"RegisterHotKey failed (error {error}) — another app may hold {string.Join("+", _config.Modifiers)}+{_config.Key}");
        }
        else
        {
            LoggerService.Info($"Hotkey registered: {string.Join("+", _config.Modifiers)}+{_config.Key}");
        }
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
            flags |= mod.ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => (uint)NativeMethods.MOD_CONTROL,
                "ALT"               => (uint)NativeMethods.MOD_ALT,
                "SHIFT"             => (uint)NativeMethods.MOD_SHIFT,
                _                   => 0u,
            };
        }
        return flags;
    }

    private static uint ResolveVirtualKey(string key)
    {
        if (Enum.TryParse<Keys>(key, ignoreCase: true, out var parsedKey))
            return (uint)parsedKey;

        LoggerService.Warn($"Cannot resolve virtual key '{key}' — hotkey may not trigger");
        return 0;
    }
}
