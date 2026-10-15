using Microsoft.Win32;

namespace EmojiPick.Services;

/// <summary>Registers EmojiPick in HKCU\...\Run so it starts with Windows.</summary>
public static class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "EmojiPick";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string;
        }
        catch
        {
            return false;
        }
    }

    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe)) return false;
                key.SetValue(ValueName, $"\"{exe}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            LoggerService.Info($"AutoStart {(enabled ? "enabled" : "disabled")}");
            return true;
        }
        catch (Exception ex)
        {
            LoggerService.Warn($"AutoStart: registry update failed — {ex.Message}");
            return false;
        }
    }
}
