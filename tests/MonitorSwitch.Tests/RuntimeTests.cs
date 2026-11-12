using System.Text.Json;
using MonitorSwitch.Core;
using MonitorSwitch.Runtime;
using Xunit;

namespace MonitorSwitch.Tests;

public sealed class RuntimeTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "monitor-switch-tests", Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    private string PathOf(string name) => Path.Combine(directory, name);

    [Fact]
    public void StateRoundTripsAndMissingStateIsAllowed()
    {
        var store = new JsonStateStore(PathOf("state.json"));
        Assert.Null(store.Read());

        var state = new OperationState(1, TestData.Identity,
            new("op", TestData.Identity, "toggle", 5, 6, Now), null, new("bootid:7", 42));
        store.ReplaceAtomically(state);

        var read = store.Read()!;
        Assert.Equal(state.Pending, read.Pending);
        Assert.Equal(state.LastCompletion, read.LastCompletion);
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{}")]
    [InlineData("null")]
    public void CorruptExistingStateFailsClosed(string content)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(PathOf("state.json"), content);
        Assert.Throws<StateStoreException>(() => new JsonStateStore(PathOf("state.json")).Read());
    }

    [Fact]
    public void ConfigReplacementKeepsABackup()
    {
        var store = new FileConfigStore(PathOf("config.json"), PathOf("config.previous.json"));
        store.ReplaceAtomically(TestData.Config());
        store.ReplaceAtomically(TestData.Config(cooldownMs: 2000));
        Assert.Equal(1500, ConfigParser.Parse(File.ReadAllText(PathOf("config.previous.json"))).Operation.CooldownMs);
        Assert.Equal(2000, ConfigParser.Parse(store.ReadText()!).Operation.CooldownMs);
    }

    [Fact]
    public void LockIsExclusiveAndNonblocking()
    {
        var operationLock = new FileOperationLock(PathOf("operation.lock"));
        using (var first = operationLock.TryAcquire())
        {
            Assert.NotNull(first);
            Assert.Null(operationLock.TryAcquire());
        }
        using var again = operationLock.TryAcquire();
        Assert.NotNull(again);
    }

    [Fact]
    public void ReportsCannotTargetTheAppDirectory()
    {
        var writer = new ReportWriter(directory);
        Assert.Throws<ConfigurationException>(() => writer.Write(PathOf("state.json"), new { }));
        Assert.Throws<ConfigurationException>(() => writer.Write(Path.Combine(directory, "logs", "x.json"), new { }));
        Assert.Throws<ConfigurationException>(() => writer.Write(directory, new { }));
    }

    [Fact]
    public void ReportPathsWithSpacesAndNonAsciiAreLiteral()
    {
        var output = Path.Combine(directory + "-out", "diagnóstico $(literal) & more.json");
        try
        {
            new ReportWriter(directory).Write(output, new { ok = true });
            Assert.True(JsonDocument.Parse(File.ReadAllText(output)).RootElement.GetProperty("ok").GetBoolean());
        }
        finally { Directory.Delete(directory + "-out", recursive: true); }
    }

    [Fact]
    public void BusyResultsDoNotOverwriteLastResult()
    {
        var reporter = new FileResultReporter(PathOf("logs"), PathOf("last-result.json"), new Fakes.FakeClock(), "hash");
        reporter.Report(new("op-1", "toggle", Outcomes.Confirmed, 5, 6, 6, 0));
        reporter.Report(new("op-2", "toggle", Outcomes.Busy, null, null, null, 2));

        Assert.Contains("op-1", File.ReadAllText(PathOf("last-result.json")));
        Assert.Equal(2, File.ReadAllLines(Path.Combine(PathOf("logs"), "2026-11-12.jsonl")).Length);
    }

    [Fact]
    public void LogsRotateAtOneMegabyte()
    {
        var logs = PathOf("logs");
        Directory.CreateDirectory(logs);
        File.WriteAllBytes(Path.Combine(logs, "2026-11-12.jsonl"), new byte[JsonlLog.MaxFileBytes - 10]);

        JsonlLog.Append(logs, "", "{\"line\":\"long enough to overflow\"}", Now);

        Assert.True(File.Exists(Path.Combine(logs, "2026-11-12.1.jsonl")));
        Assert.Equal(JsonlLog.MaxFileBytes - 10, new FileInfo(Path.Combine(logs, "2026-11-12.jsonl")).Length);
    }

    [Fact]
    public void LogsAreRetainedSevenDaysPerPrefix()
    {
        var logs = PathOf("logs");
        Directory.CreateDirectory(logs);
        string[] old = ["2026-11-05.jsonl", "2026-11-01.3.jsonl", "trigger-probe-2026-11-05.jsonl"];
        string[] kept = ["2026-11-06.jsonl", "trigger-probe-2026-11-06.jsonl", "notes.txt"];
        foreach (var name in old.Concat(kept)) File.WriteAllText(Path.Combine(logs, name), "{}\n");

        JsonlLog.Append(logs, "", "{}", Now);
        JsonlLog.Append(logs, AppPaths.ProbeLogPrefix, "{}", Now);

        Assert.All(old, name => Assert.False(File.Exists(Path.Combine(logs, name)), name));
        Assert.All(kept, name => Assert.True(File.Exists(Path.Combine(logs, name)), name));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
