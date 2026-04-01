using System.Text;

namespace TaskbarUnhideZoner.Logging;

internal static class RollingFileLogger
{
    private static readonly object Sync = new();
    private static string _logPath = string.Empty;
    private const long MaxBytes = 256 * 1024;
    private const int MaxRollFiles = 3;

    public static void Initialize(string path)
    {
        lock (Sync)
        {
            _logPath = path;
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        }
    }

    public static void Info(string message) => Write("INF", message);

    public static void Error(string message) => Write("ERR", message);

    public static void Dispose()
    {
    }

    private static void Write(string level, string message)
    {
        lock (Sync)
        {
            if (string.IsNullOrWhiteSpace(_logPath))
            {
                return;
            }

            try
            {
                RollIfNeeded();
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(_logPath, line, Encoding.UTF8);
            }
            catch
            {
            }
        }
    }

    private static void RollIfNeeded()
    {
        if (!File.Exists(_logPath))
        {
            return;
        }

        var info = new FileInfo(_logPath);
        if (info.Length < MaxBytes)
        {
            return;
        }

        for (var i = MaxRollFiles - 1; i >= 1; i--)
        {
            var older = $"{_logPath}.{i}";
            var newer = $"{_logPath}.{i + 1}";
            if (!File.Exists(older))
            {
                continue;
            }

            if (File.Exists(newer))
            {
                File.Delete(newer);
            }

            File.Move(older, newer);
        }

        var first = $"{_logPath}.1";
        if (File.Exists(first))
        {
            File.Delete(first);
        }

        File.Move(_logPath, first);
    }
}
