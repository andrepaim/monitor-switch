using System.Text.Json;
using MonitorSwitch.Core;

namespace MonitorSwitch.Runtime;

/// <summary>Harmless trigger evidence only. No monitor, enrollment or operation state access.</summary>
public static class ProbeRecorder
{
    public static int Record() => Record(AppPaths.Logs, DateTimeOffset.UtcNow);

    // Explicit directory and time let hardware-free tests use a temporary directory.
    public static int Record(string logDirectory, DateTimeOffset now)
    {
        try
        {
            var entry = JsonSerializer.Serialize(new
            {
                timestampUtc = now,
                processId = Environment.ProcessId,
                appVersion = typeof(ProbeRecorder).Assembly.GetName().Version?.ToString(),
                outcome = "ProbeExecuted"
            });
            JsonlLog.Append(logDirectory, AppPaths.ProbeLogPrefix, entry, now);
            return ExitCodes.Success;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return ExitCodes.InternalError;
        }
    }
}
