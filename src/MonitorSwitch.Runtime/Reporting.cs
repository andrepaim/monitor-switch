using System.Text.Json;
using MonitorSwitch.Core;

namespace MonitorSwitch.Runtime;

/// <summary>Derived reports: a JSONL line per result plus last-result.json. Never recovery authority.</summary>
public sealed class FileResultReporter(string logDirectory, string lastResultPath, IClock clock, string? identityHash) : IResultReporter
{
    private static readonly string? Version = typeof(FileResultReporter).Assembly.GetName().Version?.ToString();

    public void Report(SwitchResult result)
    {
        var entry = new { timestampUtc = clock.UtcNow, version = Version, identityHash, result };
        JsonlLog.Append(logDirectory, "", JsonSerializer.Serialize(entry, LogJson), clock.UtcNow);
        // Busy launches do not hold the lock, so they must not overwrite the active operation's result.
        if (result.Outcome != Outcomes.Busy)
            AtomicFile.Write(lastResultPath, JsonSerializer.SerializeToUtf8Bytes(entry, StrictJson.Output));
    }

    private static readonly JsonSerializerOptions LogJson = new(StrictJson.Output) { WriteIndented = false };
}

public sealed class ReportWriter(string protectedRoot)
{
    /// <summary>Writes a requested report. Refuses paths inside the app directory so reports cannot alias config, state or logs.</summary>
    public void Write(string outputPath, object report)
    {
        var full = Path.GetFullPath(outputPath);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(protectedRoot));
        if (full.Equals(root, StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ConfigurationException("Report path must be outside the MonitorSwitch data directory.");
        if (Directory.Exists(full)) throw new ConfigurationException("Report path is a directory.");
        AtomicFile.Write(full, JsonSerializer.SerializeToUtf8Bytes(report, StrictJson.Output));
    }
}
