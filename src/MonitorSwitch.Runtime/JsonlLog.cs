using System.Globalization;
using System.Text;

namespace MonitorSwitch.Runtime;

/// <summary>Daily JSONL files named {prefix}YYYY-MM-DD[.sequence].jsonl, rotated at 1 MB and kept seven days.</summary>
public static class JsonlLog
{
    public const long MaxFileBytes = 1024 * 1024;
    public const int RetentionDays = 7;

    public static void Append(string directory, string prefix, string jsonLine, DateTimeOffset now)
    {
        Directory.CreateDirectory(directory);
        var bytes = Encoding.UTF8.GetBytes(jsonLine.TrimEnd('\n') + "\n");
        var date = now.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // Deny concurrent writers while appending one complete line.
        // A bounded retry preserves separate executions rather than silently dropping them.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var stream = OpenCurrent(directory, prefix, date, bytes.Length);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
                break;
            }
            catch (IOException) when (attempt < 9) { Thread.Sleep(50); }
        }
        Prune(directory, prefix, now);
    }

    private static FileStream OpenCurrent(string directory, string prefix, string date, int length)
    {
        for (var sequence = 0; ; sequence++)
        {
            var path = Path.Combine(directory, sequence == 0 ? $"{prefix}{date}.jsonl" : $"{prefix}{date}.{sequence}.jsonl");
            var info = new FileInfo(path);
            if (!info.Exists || info.Length == 0 || info.Length + length <= MaxFileBytes)
                return new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        }
    }

    private static void Prune(string directory, string prefix, DateTimeOffset now)
    {
        var oldestKept = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-(RetentionDays - 1));
        foreach (var file in Directory.EnumerateFiles(directory, $"{prefix}*.jsonl"))
        {
            var name = Path.GetFileName(file);
            if (name.Length < prefix.Length + 10) continue;
            if (!DateOnly.TryParseExact(name.AsSpan(prefix.Length, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                continue;
            if (day >= oldestKept) continue;
            try { File.Delete(file); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }
}
