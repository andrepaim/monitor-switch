using MonitorSwitch.Core;

namespace MonitorSwitch.Tests.Fakes;

// Deterministic backend; never accesses native APIs.
internal sealed class FakeMonitorSession : IMonitorSession
{
    public Queue<InputRead> Reads { get; } = new();
    public List<uint> Writes { get; } = [];
    public WriteReply WriteReply { get; set; } = new(true, null);
    public int DisposeCount { get; private set; }

    public FakeMonitorSession(params uint?[] reads)
    {
        foreach (var read in reads) Reads.Enqueue(read is { } value ? new(true, value, null) : new(false, null, 0x1F));
    }

    public InputRead ReadInput()
    {
        if (DisposeCount > 0) throw new ObjectDisposedException(nameof(FakeMonitorSession));
        return Reads.Count > 0 ? Reads.Dequeue() : new(false, null, 0x1F);
    }

    public WriteReply WriteInput(uint allowedValue)
    {
        if (!DeviceProfile.Allows(allowedValue)) throw new ArgumentOutOfRangeException(nameof(allowedValue));
        Writes.Add(allowedValue);
        return WriteReply;
    }

    public string? ReadCapabilities() => "(vcp(60(01 03)))";
    public void Dispose() => DisposeCount++;
}

internal sealed class FakeLocator : IMonitorLocator
{
    public Queue<Func<FakeMonitorSession>> Sessions { get; } = new();
    public List<FakeMonitorSession> Opened { get; } = [];
    public List<MonitorCandidate> Candidates { get; } = [];
    public int OpenCalls { get; private set; }

    public IReadOnlyList<MonitorCandidate> Discover() => Candidates;

    public IMonitorSession OpenUnique(MonitorIdentity enrolled)
    {
        OpenCalls++;
        if (Sessions.Count == 0) throw new TargetUnavailableException("No fake target.");
        var session = Sessions.Dequeue()();
        Opened.Add(session);
        return session;
    }

    public FakeLocator Returns(FakeMonitorSession session)
    {
        Sessions.Enqueue(() => session);
        return this;
    }

    public IEnumerable<uint> AllWrites => Opened.SelectMany(s => s.Writes);
}

internal sealed class FakeStore : IOperationStore
{
    public OperationState? State { get; set; }
    public List<OperationState> Saved { get; } = [];
    public bool Corrupt { get; set; }
    // 1-based index of the save that fails, simulating interruption at that persistence boundary.
    public int FailOnSave { get; set; }

    public OperationState? Read() => Corrupt ? throw new StateStoreException("corrupt") : State;

    public void ReplaceAtomically(OperationState state)
    {
        if (Saved.Count + 1 == FailOnSave)
        {
            FailOnSave = 0;
            throw new IOException("Simulated interruption.");
        }
        Saved.Add(state);
        State = state;
    }
}

internal sealed class FakeLock : IOperationLock
{
    public bool Busy { get; set; }
    public bool Held { get; private set; }

    public IDisposable? TryAcquire()
    {
        if (Busy || Held) return null;
        Held = true;
        return new Lease(this);
    }

    private sealed class Lease(FakeLock owner) : IDisposable
    {
        public void Dispose() => owner.Held = false;
    }
}

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
    public string BootIdentity { get; set; } = "boot-1";
    public long MonotonicMilliseconds { get; set; } = 1_000_000;

    public void Delay(TimeSpan duration)
    {
        MonotonicMilliseconds += (long)duration.TotalMilliseconds;
        UtcNow += duration;
    }
}

internal sealed class FakeReporter(FakeLock? mustHold = null) : IResultReporter
{
    public List<SwitchResult> Reports { get; } = [];
    public bool Throw { get; set; }
    public bool ReportedWhileUnlocked { get; private set; }

    public void Report(SwitchResult result)
    {
        if (mustHold is { Held: false }) ReportedWhileUnlocked = true;
        Reports.Add(result);
        if (Throw) throw new IOException("Simulated report failure.");
    }
}

internal sealed class FakeConfigStore : IConfigStore
{
    public string? Text { get; set; }
    public List<AppConfig> Saved { get; } = [];

    public string? ReadText() => Text;

    public void ReplaceAtomically(AppConfig config)
    {
        Saved.Add(config);
        Text = System.Text.Json.JsonSerializer.Serialize(config, StrictJson.Options);
    }
}
