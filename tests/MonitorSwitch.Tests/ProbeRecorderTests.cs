using System.Text.Json;
using MonitorSwitch.Core;
using MonitorSwitch.Runtime;
using Xunit;

namespace MonitorSwitch.Tests;

public sealed class ProbeRecorderTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "monitor-switch-tests", Guid.NewGuid().ToString("N"));
    private string LogPath => Path.Combine(directory, "logs", "trigger-probe.jsonl");

    [Fact]
    public void RecordAppendsOneCompleteJsonLine()
    {
        Assert.Equal(ExitCodes.Success, ProbeRecorder.Record(LogPath));

        var line = Assert.Single(File.ReadAllLines(LogPath));
        using var entry = JsonDocument.Parse(line);
        Assert.Equal("ProbeExecuted", entry.RootElement.GetProperty("outcome").GetString());
    }

    [Fact]
    public void ConcurrentRecordsAreAllPreserved()
    {
        const int count = 8;
        var results = new int[count];
        Parallel.For(0, count, i => results[i] = ProbeRecorder.Record(LogPath));

        Assert.All(results, result => Assert.Equal(ExitCodes.Success, result));
        var lines = File.ReadAllLines(LogPath);
        Assert.Equal(count, lines.Length);
        Assert.All(lines, line => JsonDocument.Parse(line).Dispose());
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
