using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace EmojiPick.Services;

/// <summary>
/// Static logging service using Serilog with file rotation.
/// Logs to %APPDATA%\EmojiPick\logs\EmojiPick-{date}.log
/// </summary>
public static class LoggerService
{
    private static Logger? _logger;
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;

        var logDir = ConfigService.LogDirectory;
        Directory.CreateDirectory(logDir);

        _logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                path: Path.Combine(logDir, "EmojiPick-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 10L * 1024 * 1024, // 10 MB
                rollOnFileSizeLimit: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        _initialized = true;
    }

    public static void Debug(string message) => _logger?.Debug(message);
    public static void Info(string message) => _logger?.Information(message);
    public static void Warn(string message) => _logger?.Warning(message);
    public static void Error(string message) => _logger?.Error(message);
    public static void Error(string message, Exception ex) => _logger?.Error(ex, message);
}
