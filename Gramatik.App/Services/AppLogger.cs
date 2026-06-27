using System.Diagnostics;
using System.IO;
using System.Text;

namespace Gramatik.App.Services;

public sealed class AppLogger
{
    private const long MaxLogBytes = 5 * 1024 * 1024;
    private readonly object _gate = new();

    public AppLogger()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Gramatik", "logs"))
    {
    }

    public AppLogger(string logDirectory)
    {
        LogDirectory = logDirectory;
        LogPath = Path.Combine(LogDirectory, "gramatik.log");
    }

    public string LogDirectory { get; }

    public string LogPath { get; }

    public void Info(string eventName, string? details = null)
    {
        Write("INFO", eventName, details, exception: null);
    }

    public void Warning(string eventName, string? details = null)
    {
        Write("WARN", eventName, details, exception: null);
    }

    public void Error(string eventName, Exception exception, string? details = null)
    {
        Write("ERROR", eventName, details, exception);
    }

    public string ReadAll()
    {
        lock (_gate)
        {
            if (!File.Exists(LogPath))
            {
                return string.Empty;
            }

            return File.ReadAllText(LogPath, Encoding.UTF8);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            Directory.CreateDirectory(LogDirectory);
            File.WriteAllText(LogPath, string.Empty, Encoding.UTF8);
        }
    }

    public void OpenLogDirectory()
    {
        Directory.CreateDirectory(LogDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = LogDirectory,
            UseShellExecute = true
        });
    }

    private void Write(string level, string eventName, string? details, Exception? exception)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(LogDirectory);
                RotateIfNeeded();

                var line = new StringBuilder()
                    .Append(DateTimeOffset.Now.ToString("O"))
                    .Append(" [")
                    .Append(level)
                    .Append("] ")
                    .Append(Sanitize(eventName));

                if (!string.IsNullOrWhiteSpace(details))
                {
                    line.Append(" | ").Append(Sanitize(details));
                }

                if (exception is not null)
                {
                    line.Append(" | ")
                        .Append(exception.GetType().Name)
                        .Append(": ")
                        .Append(Sanitize(exception.Message));
                }

                File.AppendAllText(LogPath, line.AppendLine().ToString(), Encoding.UTF8);
            }
        }
        catch
        {
        }
    }

    private void RotateIfNeeded()
    {
        var file = new FileInfo(LogPath);
        if (!file.Exists || file.Length < MaxLogBytes)
        {
            return;
        }

        var previousPath = Path.Combine(LogDirectory, "gramatik.previous.log");
        if (File.Exists(previousPath))
        {
            File.Delete(previousPath);
        }

        File.Move(LogPath, previousPath);
    }

    private static string Sanitize(string value)
    {
        return value
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }
}
