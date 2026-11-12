namespace MonitorSwitch.Core;

public sealed record MonitorIdentity(string DeviceInterfacePath, string EdidIdentityHash, string ProductId);
public sealed record InputRead(bool Success, uint? Value, int? Win32Error);
public sealed record WriteReply(bool ApiAccepted, int? Win32Error);
public sealed record SwitchResult(string OperationId, string Command, string Outcome,
    uint? Before, uint? Target, uint? Observed, int ExitCode,
    WriteReply? Write = null, long ElapsedMs = 0, string? Detail = null);
public sealed record PendingOperation(string OperationId, MonitorIdentity Identity,
    string Command, uint Before, uint Target, DateTimeOffset StartedAtUtc);
public sealed record CooldownStamp(string BootIdentity, long MonotonicMilliseconds);
// EnrolledIdentity binds the state to one enrollment; a mismatch with config fails closed.
public sealed record OperationState(int SchemaVersion, MonitorIdentity EnrolledIdentity,
    PendingOperation? Pending, SwitchResult? LastCompleted, CooldownStamp? LastCompletion)
{
    public const int CurrentSchema = 1;
}

/// <summary>One active display target as seen by native discovery. Never persisted by index.</summary>
public sealed record MonitorCandidate(string GdiDeviceName, string DeviceInterfacePath, string? FriendlyName,
    string? Manufacturer, string? ProductId, string? EdidIdentityHash, int PhysicalMonitorCount, int TargetsOnSource)
{
    public bool MatchesProfile => Manufacturer == DeviceProfile.Manufacturer && ProductId == DeviceProfile.ProductId;

    public string? Problem =>
        EdidIdentityHash is null ? "EDID missing, incomplete or invalid."
        : TargetsOnSource != 1 ? "Display source drives more than one target (clone mode)."
        : PhysicalMonitorCount != 1 ? "HMONITOR does not map to exactly one physical monitor."
        : null;

    public MonitorIdentity? Identity =>
        EdidIdentityHash is null ? null : new(DeviceInterfacePath, EdidIdentityHash, ProductId ?? "");
}

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

public static class Outcomes
{
    public const string Confirmed = nameof(Confirmed);
    public const string AlreadySelected = nameof(AlreadySelected);
    public const string DryRun = nameof(DryRun);
    public const string Busy = nameof(Busy);
    public const string Debounced = nameof(Debounced);
    public const string InvalidConfiguration = nameof(InvalidConfiguration);
    public const string TargetMissingOrAmbiguous = nameof(TargetMissingOrAmbiguous);
    public const string ReadUnavailable = nameof(ReadUnavailable);
    public const string UnexpectedInput = nameof(UnexpectedInput);
    public const string NotChanged = nameof(NotChanged);
    public const string OutcomeUnverified = nameof(OutcomeUnverified);
    public const string ReconciledWithoutToggle = nameof(ReconciledWithoutToggle);
    public const string InternalError = nameof(InternalError);
}

public static class NativeErrors
{
    public const int InvalidHandle = 6;
    // ERROR_GRAPHICS_INVALID_PHYSICAL_MONITOR_HANDLE
    public const int InvalidPhysicalMonitorHandle = unchecked((int)0xC026258B);

    public static bool IsInvalidHandle(int? error) => error is InvalidHandle or InvalidPhysicalMonitorHandle;
}

public sealed class ConfigurationException(string message) : Exception(message);
public sealed class StateStoreException(string message, Exception? inner = null) : Exception(message, inner);
/// <summary>The enrolled target is absent, ambiguous, or cannot be opened. Messages never contain device paths.</summary>
public sealed class TargetUnavailableException(string message) : Exception(message);

public interface IMonitorSession : IDisposable
{
    InputRead ReadInput();
    WriteReply WriteInput(uint allowedValue);
    // Advisory only; may block for seconds. Null when unavailable.
    string? ReadCapabilities();
}

public interface IMonitorLocator
{
    IReadOnlyList<MonitorCandidate> Discover();
    // Throws TargetUnavailableException unless exactly one candidate matches path and hash.
    IMonitorSession OpenUnique(MonitorIdentity enrolled);
}

public interface IOperationStore
{
    // Null when no state exists yet; StateStoreException when existing state is corrupt.
    OperationState? Read();
    // One authoritative document, including pending/completion/cooldown.
    // Reporting files are never part of this recovery transaction.
    void ReplaceAtomically(OperationState state);
}

public interface IConfigStore
{
    string? ReadText();
    // Backs up the previous file before replacing it.
    void ReplaceAtomically(AppConfig config);
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
