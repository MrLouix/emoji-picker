using System.Runtime.InteropServices;
using EmojiPick.Models;

namespace EmojiPick.Helpers;

/// <summary>Finds the system caret (text cursor) of another application's window.</summary>
public static class CaretLocator
{
    public static ScreenRect? TryGetCaret(IntPtr targetWindow)
    {
        if (targetWindow == IntPtr.Zero) return null;

        try
        {
            var threadId = NativeMethods.GetWindowThreadProcessId(targetWindow, out _);
            var info = new NativeMethods.GUITHREADINFO { cbSize = Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };
            if (!NativeMethods.GetGUIThreadInfo(threadId, ref info) || info.hwndCaret == IntPtr.Zero)
                return null;

            var rc = info.rcCaret;
            if (rc.Right <= rc.Left && rc.Bottom <= rc.Top) return null;

            var origin = new NativeMethods.POINT { X = rc.Left, Y = rc.Top };
            if (!NativeMethods.ClientToScreen(info.hwndCaret, ref origin)) return null;

            return new ScreenRect(origin.X, origin.Y, Math.Max(1, rc.Right - rc.Left), Math.Max(1, rc.Bottom - rc.Top));
        }
        catch
        {
            return null;
        }
    }
}
