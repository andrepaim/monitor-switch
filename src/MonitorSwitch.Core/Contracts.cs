namespace MonitorSwitch.Core;

public sealed record MonitorIdentity(string DeviceInterfacePath, string EdidIdentityHash, string ProductId);
public sealed record InputRead(bool Success, uint? Value, int? Win32Error);
public sealed record WriteReply(bool ApiAccepted, int? Win32Error);
public sealed record SwitchResult(string OperationId, string Outcome,
    uint? Before, uint? Target, uint? Observed, int ExitCode);
public sealed record PendingOperation(string OperationId, MonitorIdentity Identity,
    string Command, uint Before, uint Target, DateTimeOffset StartedAtUtc);
public sealed record CooldownStamp(string BootIdentity, long MonotonicMilliseconds);
public sealed record OperationState(int SchemaVersion, PendingOperation? Pending,
    SwitchResult? LastCompleted, CooldownStamp? LastCompletion);

public static class ExitCodes
{
    public const int Success = 0;
    public const int BusyOrDebounced = 2;
    public const int InvalidConfiguration = 10;
    public const int TargetMissingOrAmbiguous = 11;
    public const int ReadUnavailable = 12;
    public const int UnexpectedInput = 13;
    public const int NotChanged = 14;
    public const int OutcomeUnverified = 15;
    public const int ReconciledWithoutToggle = 16;
    public const int InternalError = 20;
    public const int NotImplemented = 21;
}

public interface IMonitorSession : IDisposable
{
    InputRead ReadInput();
    WriteReply WriteInput(uint allowedValue);
}

public interface IMonitorLocator
{
    IMonitorSession OpenUnique(MonitorIdentity enrolled);
}

public interface IOperationStore
{
    OperationState? Read();
    // One authoritative document, including pending/completion/cooldown.
    // Reporting files are never part of this recovery transaction.
    void ReplaceAtomically(OperationState state);
}

public interface IOperationLock
{
    // Nonblocking. A null lease means Busy; no queued operation.
    // Implementations must document session scope and thread-affine ownership.
    IDisposable? TryAcquire();
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    string BootIdentity { get; }
    long MonotonicMilliseconds { get; }
    void Delay(TimeSpan duration);
}

public interface IResultReporter
{
    void Report(SwitchResult result);
}
