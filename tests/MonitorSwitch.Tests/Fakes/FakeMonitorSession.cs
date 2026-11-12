using MonitorSwitch.Core;

namespace MonitorSwitch.Tests.Fakes;

// Fixture for the future orchestration suite; never accesses native APIs.
internal sealed class FakeMonitorSession : IMonitorSession
{
    public Queue<InputRead> Reads { get; } = new();
    public List<uint> Writes { get; } = [];
    public WriteReply WriteReply { get; set; } = new(true, null);
    public int DisposeCount { get; private set; }

    public InputRead ReadInput() => Reads.Count > 0 ? Reads.Dequeue() : new(false, null, null);
    public WriteReply WriteInput(uint allowedValue)
    {
        if (!DeviceProfile.Allows(allowedValue)) throw new ArgumentOutOfRangeException(nameof(allowedValue));
        Writes.Add(allowedValue);
        return WriteReply;
    }
    public void Dispose() => DisposeCount++;
}
