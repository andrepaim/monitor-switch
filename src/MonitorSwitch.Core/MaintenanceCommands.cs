namespace MonitorSwitch.Core;

public sealed record CommandReport(string Outcome, int ExitCode, object? Details = null);

/// <summary>
/// Discover/status/diagnose never write the monitor or state and never resolve pending operations.
/// Enrollment writes only config and the state's enrolled identity, never the monitor.
/// All of them take the same nonblocking lock as switching.
/// </summary>
public sealed class MaintenanceCommands(IMonitorLocator locator, IOperationStore store,
    IOperationLock operationLock, IConfigStore configStore)
{
    private static readonly CommandReport BusyReport = new(Outcomes.Busy, ExitCodes.BusyOrDebounced);

    public CommandReport Discover()
    {
        using var lease = operationLock.TryAcquire();
        if (lease is null) return BusyReport;
        return new("Discovered", ExitCodes.Success, new { monitors = Inventory() });
    }

    public CommandReport Status()
    {
        var config = LoadConfig() ?? throw new ConfigurationException("Not enrolled.");
        using var lease = operationLock.TryAcquire();
        if (lease is null) return BusyReport;

        var (state, stateError) = ReadState();
        var input = ReadEnrolled(config, out var targetError);
        var (outcome, exitCode) = input switch
        {
            null => (Outcomes.TargetMissingOrAmbiguous, ExitCodes.TargetMissingOrAmbiguous),
            { Success: false } => (Outcomes.ReadUnavailable, ExitCodes.ReadUnavailable),
            { Value: var value } when !DeviceProfile.Allows(value!.Value) => (Outcomes.UnexpectedInput, ExitCodes.UnexpectedInput),
            _ => ("Read", ExitCodes.Success),
        };
        return new(outcome, exitCode, new
        {
            enrolled = Summary(config),
            currentInput = input?.Value,
            currentLabel = input?.Value is { } current && DeviceProfile.Allows(current) ? config.LabelFor(current) : null,
            readError = input?.Win32Error,
            targetError,
            stateError,
            stateMatchesEnrollment = state is null ? (bool?)null : state.EnrolledIdentity == config.Identity,
            pending = state?.Pending,
            lastCompleted = state?.LastCompleted,
        });
    }

    public CommandReport Diagnose()
    {
        string? configError = null;
        AppConfig? config = null;
        try { config = LoadConfig(); }
        catch (ConfigurationException error) { configError = error.Message; }

        using var lease = operationLock.TryAcquire();
        if (lease is null) return BusyReport;

        var (state, stateError) = ReadState();
        var monitors = Inventory();
        object? enrolledTarget = null;
        object? proposal = null;
        if (config is not null)
        {
            string? targetError;
            string? capabilities = null;
            InputRead? input = null;
            try
            {
                using var session = locator.OpenUnique(config.Identity);
                input = session.ReadInput();
                capabilities = session.ReadCapabilities();
                targetError = null;
            }
            catch (TargetUnavailableException error) { targetError = error.Message; }
            enrolledTarget = new { summary = Summary(config), currentInput = input, capabilitiesAdvisory = capabilities, targetError };

            // A changed path with the same EDID is only proposed here; normal toggle never accepts it.
            if (targetError is not null)
            {
                var sameEdid = monitors.Where(m => m.Candidate.EdidIdentityHash == config.Monitor.EdidIdentityHash
                    && !string.Equals(m.Candidate.DeviceInterfacePath, config.Monitor.DeviceInterfacePath, StringComparison.OrdinalIgnoreCase)).ToList();
                if (sameEdid.Count == 1)
                    proposal = new { reason = "Enrolled EDID found at a different device path. Re-enroll explicitly with --replace if correct.", candidate = sameEdid[0].Candidate };
            }
        }

        return new("Diagnosed", ExitCodes.Success, new
        {
            configValid = config is not null,
            configError,
            stateError,
            stateMatchesEnrollment = state is null || config is null ? (bool?)null : state.EnrolledIdentity == config.Identity,
            pending = state?.Pending,
            lastCompleted = state?.LastCompleted,
            enrolledTarget,
            reenrollmentProposal = proposal,
            monitors,
        });
    }

    public CommandReport Enroll(string devicePath, string edidHash, bool replace)
    {
        using var lease = operationLock.TryAcquire();
        if (lease is null) return BusyReport;

        var (state, stateError) = ReadState();
        if (stateError is not null)
            return new(Outcomes.InternalError, ExitCodes.InternalError, new { error = $"Existing state is unusable: {stateError}" });
        if (state?.Pending is not null)
            return Refuse("A pending operation exists; run status/toggle to reconcile before enrolling.");

        var existingText = configStore.ReadText();
        if (existingText is not null && !replace)
            return Refuse("Already enrolled. Re-enrollment requires --replace.");
        AppConfig? previous = null;
        if (existingText is not null)
        {
            try { previous = ConfigParser.Parse(existingText); }
            catch (ConfigurationException) { previous = null; }
        }

        var matches = locator.Discover().Where(c => c.EdidIdentityHash == edidHash
            && string.Equals(c.DeviceInterfacePath, devicePath, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count != 1)
            return new(Outcomes.TargetMissingOrAmbiguous, ExitCodes.TargetMissingOrAmbiguous,
                new { error = $"Fresh discovery found {matches.Count} monitors with that exact device path and EDID hash; expected 1." });
        var selected = matches[0];
        if (!selected.MatchesProfile)
            return Refuse($"Selected monitor is {selected.ProductId ?? "unknown"}, not {DeviceProfile.ProductId}.");
        if (selected.Problem is { } problem)
            return new(Outcomes.TargetMissingOrAmbiguous, ExitCodes.TargetMissingOrAmbiguous, new { error = problem });

        var config = ConfigParser.ForEnrollment(selected, previous);
        configStore.ReplaceAtomically(config);
        if (state is not null)
            store.ReplaceAtomically(state with { EnrolledIdentity = config.Identity, LastCompleted = null });

        return new("Enrolled", ExitCodes.Success, new { selected, replacedPrevious = existingText is not null });

        static CommandReport Refuse(string reason) =>
            new(Outcomes.InvalidConfiguration, ExitCodes.InvalidConfiguration, new { error = reason });
    }

    private AppConfig? LoadConfig() => configStore.ReadText() is { } text ? ConfigParser.Parse(text) : null;

    private (OperationState? State, string? Error) ReadState()
    {
        try { return (store.Read(), null); }
        catch (StateStoreException error) { return (null, error.Message); }
    }

    private InputRead? ReadEnrolled(AppConfig config, out string? error)
    {
        try
        {
            using var session = locator.OpenUnique(config.Identity);
            var read = session.ReadInput();
            for (var attempt = 1; !read.Success && attempt < config.Operation.ReadAttempts; attempt++) read = session.ReadInput();
            error = null;
            return read;
        }
        catch (TargetUnavailableException e)
        {
            error = e.Message;
            return null;
        }
    }

    // Inputs are read only from monitors matching the fixed profile; unrelated monitors are left alone.
    private List<InventoryEntry> Inventory() => locator.Discover().Select(candidate =>
    {
        InputRead? input = null;
        if (candidate.MatchesProfile && candidate.Identity is { } identity && candidate.Problem is null)
        {
            try
            {
                using var session = locator.OpenUnique(identity);
                input = session.ReadInput();
            }
            catch (TargetUnavailableException) { }
        }
        return new InventoryEntry(candidate, input);
    }).ToList();

    private static object Summary(AppConfig config) => new
    {
        config.Monitor.ProductId,
        config.Monitor.FriendlyName,
        config.Monitor.EdidIdentityHash,
    };

    private sealed record InventoryEntry(MonitorCandidate Candidate, InputRead? CurrentInput);
}
