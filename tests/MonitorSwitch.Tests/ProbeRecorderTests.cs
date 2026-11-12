using System.Text.Json;
using MonitorSwitch.Core;
using MonitorSwitch.Runtime;
using Xunit;

namespace MonitorSwitch.Tests;

public sealed class ProbeRecorderTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "monitor-switch-tests", Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
    private string LogPath => Path.Combine(directory, "trigger-probe-2026-11-12.jsonl");

    [Fact]
    public void RecordAppendsOneCompleteJsonLine()
    {
        Assert.Equal(ExitCodes.Success, ProbeRecorder.Record(directory, Now));

        var line = Assert.Single(File.ReadAllLines(LogPath));
        using var entry = JsonDocument.Parse(line);
        Assert.Equal("ProbeExecuted", entry.RootElement.GetProperty("outcome").GetString());
    }

    [Fact]
    public void ConcurrentRecordsAreAllPreserved()
    {
        const int count = 8;
        var results = new int[count];
        Parallel.For(0, count, i => results[i] = ProbeRecorder.Record(directory, Now));

        Assert.All(results, result => Assert.Equal(ExitCodes.Success, result));
        var lines = File.ReadAllLines(LogPath);
        Assert.Equal(count, lines.Length);
        Assert.All(lines, line => JsonDocument.Parse(line).Dispose());
    }

    [Fact]
    public void LoggingFailureIsReported()
    {
        // A file where the log directory should be makes every append fail.
        Directory.CreateDirectory(Path.GetDirectoryName(directory)!);
        File.WriteAllText(directory, "");
        try { Assert.Equal(ExitCodes.InternalError, ProbeRecorder.Record(directory, Now)); }
        finally { File.Delete(directory); }
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
