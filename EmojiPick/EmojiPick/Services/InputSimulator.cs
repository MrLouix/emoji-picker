using System.Runtime.InteropServices;
using System.Threading;
using EmojiPick.Helpers;

namespace EmojiPick.Services;

public static class InputSimulator
{
    public static void SendKeyStroke(byte vkCode, bool isKeyDown)
    {
        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = vkCode,
                    wScan = 0,
                    dwFlags = isKeyDown ? 0u : NativeMethods.KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero,
                }
            }
        };
        NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
    }

    public static void SendKeyDown(byte vkCode) => SendKeyStroke(vkCode, true);
    public static void SendKeyUp(byte vkCode) => SendKeyStroke(vkCode, false);

    public static void SendKeyPress(byte vkCode)
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        uint targetThreadId = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
        uint currentThreadId = NativeMethods.GetCurrentThreadId();

        bool attached = targetThreadId != 0 && targetThreadId != currentThreadId
            && NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, true);
        try
        {
            SendKeyDown(vkCode);
            Thread.Sleep(10);
            SendKeyUp(vkCode);
        }
        finally
        {
            if (attached)
                NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, false);
        }
    }

    public static void SendCtrlC()
    {
        SendKeyDown((byte)NativeMethods.VK_CONTROL);
        SendKeyPress((byte)NativeMethods.VK_C);
        SendKeyUp((byte)NativeMethods.VK_CONTROL);
        Thread.Sleep(200);
    }

    public static void SendCtrlV()
    {
        SendKeyDown((byte)NativeMethods.VK_CONTROL);
        SendKeyPress((byte)NativeMethods.VK_V);
        SendKeyUp((byte)NativeMethods.VK_CONTROL);
        Thread.Sleep(200);
    }

    public static void SendTextViaPaste(string text)
    {
        ClipboardService.SetText(text);
        SendCtrlV();
    }
}
