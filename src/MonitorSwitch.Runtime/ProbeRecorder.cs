using System.Text;
using System.Text.Json;
using MonitorSwitch.Core;

namespace MonitorSwitch.Runtime;

/// <summary>Harmless trigger evidence only. No monitor, enrollment or operation state access.</summary>
public static class ProbeRecorder
{
    public static int Record()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.ProbeLog)!);
            var entry = JsonSerializer.Serialize(new
            {
                timestampUtc = DateTimeOffset.UtcNow,
                processId = Environment.ProcessId,
                appVersion = typeof(ProbeRecorder).Assembly.GetName().Version?.ToString(),
                outcome = "ProbeExecuted"
            }) + "\n";

            // Deny concurrent writers while appending one complete JSON line.
            // A bounded retry preserves separate executions rather than silently dropping them.
            for (var attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    using var stream = new FileStream(AppPaths.ProbeLog, FileMode.Append, FileAccess.Write, FileShare.Read);
                    stream.Write(Encoding.UTF8.GetBytes(entry));
                    stream.Flush(flushToDisk: true);
                    return ExitCodes.Success;
                }
                catch (IOException) when (attempt < 9) { Thread.Sleep(50); }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return ExitCodes.InternalError;
        }
        return ExitCodes.InternalError;
    }
}
