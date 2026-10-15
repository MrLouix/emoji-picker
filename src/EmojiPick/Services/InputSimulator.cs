using System.Runtime.InteropServices;
using EmojiPick.Helpers;

namespace EmojiPick.Services;

public static class InputSimulator
{
    private static readonly int[] ModifierKeys =
    {
        NativeMethods.VK_CONTROL, NativeMethods.VK_MENU, NativeMethods.VK_SHIFT,
        NativeMethods.VK_LWIN, NativeMethods.VK_RWIN,
    };

    public static void SendCtrlC() => SendCtrlChord((ushort)NativeMethods.VK_C);

    public static void SendCtrlV() => SendCtrlChord((ushort)NativeMethods.VK_V);

    /// <summary>
    /// Types text directly as Unicode keystrokes (KEYEVENTF_UNICODE), leaving the clipboard untouched.
    /// Surrogate pairs are sent as two consecutive UTF-16 units, which is what Windows expects.
    /// </summary>
    public static void SendUnicodeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        var inputs = new List<NativeMethods.INPUT>(text.Length * 2);
        foreach (var unit in text)
        {
            inputs.Add(UnicodeInput(unit, keyUp: false));
            inputs.Add(UnicodeInput(unit, keyUp: true));
        }
        Send(inputs.ToArray());
    }

    /// <summary>
    /// Waits until the user has physically released Ctrl/Alt/Shift/Win (e.g. after the hotkey).
    /// Synthetic keystrokes sent while modifiers are still down get combined with them
    /// (Ctrl+C would become Ctrl+Alt+C). Returns false on timeout.
    /// </summary>
    public static async Task<bool> WaitForModifiersReleasedAsync(int timeoutMs = 800)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (AnyModifierDown())
        {
            if (Environment.TickCount64 >= deadline) return false;
            await Task.Delay(15);
        }
        return true;
    }

    private static bool AnyModifierDown() =>
        ModifierKeys.Any(vk => (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0);

    /// <summary>Sends Ctrl+key as one atomic SendInput batch so no other input can interleave.</summary>
    private static void SendCtrlChord(ushort vk)
    {
        var ctrl = (ushort)NativeMethods.VK_CONTROL;
        Send(
            KeyInput(ctrl, keyUp: false),
            KeyInput(vk, keyUp: false),
            KeyInput(vk, keyUp: true),
            KeyInput(ctrl, keyUp: true));
    }

    private static void Send(params NativeMethods.INPUT[] inputs)
    {
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent != inputs.Length)
            LoggerService.Warn($"SendInput injected {sent}/{inputs.Length} events (error {Marshal.GetLastWin32Error()})");
    }

    private static NativeMethods.INPUT KeyInput(ushort vk, bool keyUp) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT
            {
                wVk = vk,
                dwFlags = keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0u,
            },
        },
    };

    private static NativeMethods.INPUT UnicodeInput(char unit, bool keyUp) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT
            {
                wVk = 0,
                wScan = unit,
                dwFlags = NativeMethods.KEYEVENTF_UNICODE | (keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0u),
            },
        },
    };
}
