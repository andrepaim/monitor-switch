namespace MonitorSwitch.Core;

/// <summary>
/// Design v1.1 §7: lock → pending reconciliation or cooldown → unique locate → read → persist pending
/// → exactly one write → bounded readback → atomic completion → derived reports, all under the lock.
/// </summary>
public sealed class SwitchOrchestrator(IMonitorLocator locator, IOperationStore store, IOperationLock operationLock,
    IClock clock, IResultReporter reporter, AppConfig config)
{
    private readonly IMonitorLocator locator = locator;
    private readonly IOperationStore store = store;
    private readonly IClock clock = clock;
    private readonly AppConfig config = config;
    private const int FinalWindowMs = 1000;
    private static readonly int[] ReadRetryDelaysMs = [200, 300];
    private static readonly int[] VerifyBackoffMs = [250, 500, 1000];

    public SwitchResult Run(Command command)
    {
        if (command.Kind is not (CommandKind.Toggle or CommandKind.Set))
            throw new ArgumentException("Only toggle and set are switching commands.", nameof(command));

        var operation = new Operation(this, command);
        using var lease = operationLock.TryAcquire();
        if (lease is null) return Report(operation.Result(Outcomes.Busy, ExitCodes.BusyOrDebounced));
        // Reports are emitted before the lease is released so read-only commands cannot race them.
        return Report(operation.Execute());
    }

    private SwitchResult Report(SwitchResult result)
    {
        // A derived report never changes the authoritative outcome.
        try { reporter.Report(result); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        return result;
    }

    private sealed class Operation(SwitchOrchestrator owner, Command command)
    {
        private readonly string id = Guid.NewGuid().ToString("N");
        private readonly long started = owner.clock.MonotonicMilliseconds;
        private readonly string name = command.Kind == CommandKind.Set ? $"set {command.Input}"
            : command.DryRun ? "toggle --dry-run" : "toggle";
        private AppConfig Config => owner.config;
        private MonitorIdentity Identity => owner.config.Identity;

        public SwitchResult Result(string outcome, int exitCode, uint? before = null, uint? target = null,
            uint? observed = null, WriteReply? write = null, string? detail = null) =>
            new(id, name, outcome, before, target, observed, exitCode, write, owner.clock.MonotonicMilliseconds - started, detail);

        public SwitchResult Execute()
        {
            OperationState? state;
            try { state = owner.store.Read(); }
            catch (StateStoreException error)
            {
                return Result(Outcomes.InternalError, ExitCodes.InternalError, detail: $"Existing state is unusable: {error.Message}");
            }

            if (state is not null && state.EnrolledIdentity != Identity)
                return Result(Outcomes.InvalidConfiguration, ExitCodes.InvalidConfiguration,
                    detail: "State belongs to a different enrollment; re-enroll explicitly.");
            if (state?.Pending is { } pending) return Reconcile(state, pending);
            if (InCooldown(state?.LastCompletion))
                return Result(Outcomes.Debounced, ExitCodes.BusyOrDebounced);

            IMonitorSession? session = null;
            try
            {
                try { session = owner.locator.OpenUnique(Identity); }
                catch (TargetUnavailableException error)
                {
                    return Result(Outcomes.TargetMissingOrAmbiguous, ExitCodes.TargetMissingOrAmbiguous, detail: error.Message);
                }

                var read = ReadCurrent(session);
                if (!read.Success)
                    return Result(Outcomes.ReadUnavailable, ExitCodes.ReadUnavailable, detail: ErrorDetail(read.Win32Error));
                var before = read.Value!.Value;
                if (!DeviceProfile.Allows(before))
                    return Result(Outcomes.UnexpectedInput, ExitCodes.UnexpectedInput, observed: before);

                var target = command.Kind == CommandKind.Set ? command.Input!.Value : DeviceProfile.OtherInput(before);
                if (target == before)
                    return Result(Outcomes.AlreadySelected, ExitCodes.Success, before, target, before);
                if (command.DryRun)
                    return Result(Outcomes.DryRun, ExitCodes.Success, before, target, detail: $"Would select {Config.LabelFor(target)}.");

                var withPending = new OperationState(OperationState.CurrentSchema, Identity,
                    new(id, Identity, name, before, target, owner.clock.UtcNow), state?.LastCompleted, state?.LastCompletion);
                try { owner.store.ReplaceAtomically(withPending); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or StateStoreException)
                {
                    return Result(Outcomes.InternalError, ExitCodes.InternalError, before, target,
                        detail: "Pending operation could not be saved; no write was attempted.");
                }

                // Exactly one write, regardless of its return: a failed reply can still mean the monitor changed.
                WriteReply write;
                try { write = session.WriteInput(target); }
                catch (Exception error) when (error is not OutOfMemoryException) { write = new(false, null); }

                var (outcome, observed) = Verify(ref session, before, target);
                var exitCode = outcome switch
                {
                    Outcomes.Confirmed => ExitCodes.Success,
                    Outcomes.NotChanged => ExitCodes.NotChanged,
                    _ => ExitCodes.OutcomeUnverified,
                };
                var result = Result(outcome, exitCode, before, target, observed, write);
                if (outcome == Outcomes.OutcomeUnverified)
                    return result with { Detail = "Pending operation retained; the next launch reconciles without switching." };

                try
                {
                    owner.store.ReplaceAtomically(new(OperationState.CurrentSchema, Identity, null, result, Stamp()));
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or StateStoreException)
                {
                    return result with
                    {
                        ExitCode = ExitCodes.InternalError,
                        Detail = $"{outcome} observed but completion could not be saved; pending operation retained.",
                    };
                }
                return result;
            }
            finally { session?.Dispose(); }
        }

        private SwitchResult Reconcile(OperationState state, PendingOperation pending)
        {
            InputRead read;
            try
            {
                using var session = owner.locator.OpenUnique(Identity);
                read = ReadCurrent(session);
            }
            catch (TargetUnavailableException error)
            {
                return Unresolved(null, error.Message);
            }

            if (!read.Success || (read.Value != pending.Target && read.Value != pending.Before))
                return Unresolved(read.Value, read.Success ? "Unexpected input." : ErrorDetail(read.Win32Error));

            var resolved = read.Value == pending.Target ? Outcomes.Confirmed : Outcomes.NotChanged;
            if (command.DryRun)
                return Result(Outcomes.DryRun, ExitCodes.Success, pending.Before, pending.Target, read.Value,
                    detail: $"Would reconcile pending operation {pending.OperationId} as {resolved}; no toggle.");

            var completed = new SwitchResult(pending.OperationId, pending.Command, resolved, pending.Before, pending.Target,
                read.Value, resolved == Outcomes.Confirmed ? ExitCodes.Success : ExitCodes.NotChanged,
                Detail: "Resolved by later reconciliation.");
            try { owner.store.ReplaceAtomically(state with { Pending = null, LastCompleted = completed, LastCompletion = Stamp() }); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or StateStoreException)
            {
                return Result(Outcomes.InternalError, ExitCodes.InternalError, pending.Before, pending.Target, read.Value,
                    detail: "Reconciliation could not be saved; pending operation retained.");
            }
            return Result(Outcomes.ReconciledWithoutToggle, ExitCodes.ReconciledWithoutToggle, pending.Before, pending.Target,
                read.Value, detail: $"Pending operation {pending.OperationId} resolved as {resolved}; press again to switch.");

            SwitchResult Unresolved(uint? observed, string reason) =>
                Result(Outcomes.OutcomeUnverified, ExitCodes.OutcomeUnverified, pending.Before, pending.Target, observed,
                    detail: $"Pending operation {pending.OperationId} unresolved and retained: {reason}");
        }

        private InputRead ReadCurrent(IMonitorSession session)
        {
            var read = session.ReadInput();
            for (var attempt = 1; !read.Success && attempt < Config.Operation.ReadAttempts; attempt++)
            {
                owner.clock.Delay(TimeSpan.FromMilliseconds(ReadRetryDelaysMs[attempt - 1]));
                read = session.ReadInput();
            }
            return read;
        }

        /// <summary>
        /// Confirmed when the target is observed. NotChanged needs two original samples, one completing in the
        /// final second of the window, and no unexpected sample. Anything else is unverified. Never writes.
        /// </summary>
        private (string Outcome, uint? Observed) Verify(ref IMonitorSession? session, uint before, uint target)
        {
            var deadline = owner.clock.MonotonicMilliseconds + Config.Operation.VerificationDeadlineMs;
            var originals = 0;
            var recentOriginal = false;
            var unexpected = false;
            var reopened = false;
            uint? observed = null;

            for (var poll = 0; session is not null; poll++)
            {
                var remaining = deadline - owner.clock.MonotonicMilliseconds;
                if (remaining <= 0) break;
                owner.clock.Delay(TimeSpan.FromMilliseconds(Math.Min(VerifyBackoffMs[Math.Min(poll, VerifyBackoffMs.Length - 1)], remaining)));

                var read = session.ReadInput();
                if (!read.Success && NativeErrors.IsInvalidHandle(read.Win32Error) && !reopened)
                {
                    // One re-enumeration, requiring the exact same identity; never a different display.
                    reopened = true;
                    session.Dispose();
                    session = null;
                    try { session = owner.locator.OpenUnique(Identity); }
                    catch (TargetUnavailableException) { break; }
                    read = session.ReadInput();
                }

                if (!read.Success) continue;
                observed = read.Value;
                if (read.Value == target) return (Outcomes.Confirmed, observed);
                if (read.Value == before)
                {
                    originals++;
                    if (owner.clock.MonotonicMilliseconds >= deadline - FinalWindowMs) recentOriginal = true;
                }
                else unexpected = true;
            }

            return originals >= 2 && recentOriginal && !unexpected
                ? (Outcomes.NotChanged, observed)
                : (Outcomes.OutcomeUnverified, observed);
        }

        private bool InCooldown(CooldownStamp? stamp)
        {
            // A different boot never extends cooldown; neither does a backwards monotonic reading.
            if (stamp is null || stamp.BootIdentity != owner.clock.BootIdentity) return false;
            var elapsed = owner.clock.MonotonicMilliseconds - stamp.MonotonicMilliseconds;
            return elapsed >= 0 && elapsed < Config.Operation.CooldownMs;
        }

        private CooldownStamp Stamp() => new(owner.clock.BootIdentity, owner.clock.MonotonicMilliseconds);

        private static string ErrorDetail(int? win32Error) =>
            win32Error is { } code ? $"Native error 0x{code:X8}." : "Read failed.";
    }
}
