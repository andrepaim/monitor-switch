using MonitorSwitch.Core;
using MonitorSwitch.Tests.Fakes;
using Xunit;

namespace MonitorSwitch.Tests;

public sealed class SwitchOrchestratorTests
{
    private readonly FakeLocator locator = new();
    private readonly FakeStore store = new();
    private readonly FakeLock operationLock = new();
    private readonly FakeClock clock = new();
    private readonly FakeReporter reporter;

    public SwitchOrchestratorTests() => reporter = new(operationLock);

    private SwitchResult Run(Command command, AppConfig? config = null) =>
        new SwitchOrchestrator(locator, store, operationLock, clock, reporter, config ?? TestData.Config()).Run(command);

    private static Command Toggle => new(CommandKind.Toggle);
    private static Command DryRun => new(CommandKind.Toggle, DryRun: true);
    private static Command Set(uint input) => new(CommandKind.Set, input);

    private OperationState StateWithPending(uint before = 5, uint target = 6) => new(1, TestData.Identity,
        new("op-1", TestData.Identity, "toggle", before, target, clock.UtcNow), null, null);

    [Theory]
    [InlineData(5u, 6u)]
    [InlineData(6u, 5u)]
    public void ToggleWritesOtherInputOnceAndConfirms(uint before, uint target)
    {
        var session = new FakeMonitorSession(before, target);
        locator.Returns(session);

        var result = Run(Toggle);

        Assert.Equal((Outcomes.Confirmed, ExitCodes.Success), (result.Outcome, result.ExitCode));
        Assert.Equal([target], session.Writes);
        Assert.Equal(1, session.DisposeCount);
        Assert.Equal(2, store.Saved.Count);
        Assert.NotNull(store.Saved[0].Pending);
        Assert.Null(store.State!.Pending);
        Assert.Equal(Outcomes.Confirmed, store.State.LastCompleted!.Outcome);
        Assert.NotNull(store.State.LastCompletion);
        Assert.False(operationLock.Held);
    }

    [Fact]
    public void PendingIsPersistedBeforeTheWrite()
    {
        var session = new FakeMonitorSession(5, 6);
        locator.Returns(session);
        Run(Toggle);
        var pending = store.Saved[0].Pending!;
        Assert.Equal((5u, 6u), (pending.Before, pending.Target));
    }

    [Fact]
    public void ExplicitSetToCurrentInputDoesNotWrite()
    {
        var session = new FakeMonitorSession(6);
        locator.Returns(session);

        var result = Run(Set(6));

        Assert.Equal((Outcomes.AlreadySelected, ExitCodes.Success), (result.Outcome, result.ExitCode));
        Assert.Empty(session.Writes);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public void ExplicitSetWritesRequestedInput()
    {
        var session = new FakeMonitorSession(5, 6);
        locator.Returns(session);
        Assert.Equal(ExitCodes.Success, Run(Set(6)).ExitCode);
        Assert.Equal([6u], session.Writes);
    }

    [Fact]
    public void BusyReturnsWithoutTouchingStateOrMonitor()
    {
        operationLock.Busy = true;
        var result = Run(Toggle);
        Assert.Equal((Outcomes.Busy, ExitCodes.BusyOrDebounced), (result.Outcome, result.ExitCode));
        Assert.Equal(0, locator.OpenCalls);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public void UnreadableInputCausesZeroWrites()
    {
        var session = new FakeMonitorSession(null, null, null);
        locator.Returns(session);
        var result = Run(Toggle);
        Assert.Equal(ExitCodes.ReadUnavailable, result.ExitCode);
        Assert.Empty(session.Writes);
        Assert.Empty(store.Saved);
        Assert.Empty(session.Reads); // all three attempts used
    }

    [Fact]
    public void ReadRetriesUpToConfiguredAttempts()
    {
        var session = new FakeMonitorSession(null, null, 5, 6);
        locator.Returns(session);
        Assert.Equal(ExitCodes.Success, Run(Toggle).ExitCode);
    }

    [Theory]
    [InlineData(17u)]
    [InlineData(18u)]
    [InlineData(3u)]
    public void UnexpectedInputCausesZeroWrites(uint current)
    {
        var session = new FakeMonitorSession(current);
        locator.Returns(session);
        var result = Run(Toggle);
        Assert.Equal((ExitCodes.UnexpectedInput, (uint?)current), (result.ExitCode, result.Observed));
        Assert.Empty(session.Writes);
    }

    [Fact]
    public void MissingTargetCausesZeroWrites()
    {
        var result = Run(Toggle);
        Assert.Equal(ExitCodes.TargetMissingOrAmbiguous, result.ExitCode);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public void PendingSaveFailurePreventsTheWrite()
    {
        var session = new FakeMonitorSession(5, 6);
        locator.Returns(session);
        store.FailOnSave = 1;

        var result = Run(Toggle);

        Assert.Equal(ExitCodes.InternalError, result.ExitCode);
        Assert.Empty(session.Writes);
        Assert.Null(store.State);
    }

    [Fact]
    public void FailedWriteReplyWithTargetReadbackIsConfirmed()
    {
        var session = new FakeMonitorSession(5, 6) { WriteReply = new(false, 0x1F) };
        locator.Returns(session);

        var result = Run(Toggle);

        Assert.Equal(Outcomes.Confirmed, result.Outcome);
        Assert.Equal(new WriteReply(false, 0x1F), result.Write);
        Assert.Single(session.Writes);
    }

    [Fact]
    public void ConsistentOriginalReadbackIsNotChanged()
    {
        var session = new FakeMonitorSession([5u, .. Enumerable.Repeat<uint?>(5, 20)]);
        locator.Returns(session);

        var result = Run(Toggle);

        Assert.Equal((Outcomes.NotChanged, ExitCodes.NotChanged), (result.Outcome, result.ExitCode));
        Assert.Single(session.Writes);
        Assert.Null(store.State!.Pending);
    }

    [Fact]
    public void OriginalSamplesWithoutARecentOneAreUnverified()
    {
        var session = new FakeMonitorSession(5, 5, 5); // two early originals, then every read fails
        locator.Returns(session);

        var result = Run(Toggle);

        Assert.Equal(Outcomes.OutcomeUnverified, result.Outcome);
        Assert.NotNull(store.State!.Pending);
    }

    [Fact]
    public void UnreadableVerificationRetainsPending()
    {
        var session = new FakeMonitorSession(5);
        locator.Returns(session);

        var result = Run(Toggle);

        Assert.Equal((Outcomes.OutcomeUnverified, ExitCodes.OutcomeUnverified), (result.Outcome, result.ExitCode));
        Assert.Single(session.Writes);
        Assert.Single(store.Saved);
        Assert.NotNull(store.State!.Pending);
    }

    [Fact]
    public void UnexpectedVerificationValueIsUnverified()
    {
        var session = new FakeMonitorSession([5u, 17u, .. Enumerable.Repeat<uint?>(5, 20)]);
        locator.Returns(session);
        Assert.Equal(Outcomes.OutcomeUnverified, Run(Toggle).Outcome);
        Assert.NotNull(store.State!.Pending);
    }

    [Fact]
    public void VerificationIsBoundedByDeadlineAndNeverWritesTwice()
    {
        var session = new FakeMonitorSession(5);
        locator.Returns(session);
        var start = clock.MonotonicMilliseconds;

        Run(Toggle, TestData.Config(deadlineMs: 1000));

        Assert.InRange(clock.MonotonicMilliseconds - start, 1000, 1100);
        Assert.Single(session.Writes);
    }

    [Fact]
    public void CompletionSaveFailureLeavesPendingIntact()
    {
        var session = new FakeMonitorSession(5, 6);
        locator.Returns(session);
        store.FailOnSave = 2;

        var result = Run(Toggle);

        Assert.Equal((Outcomes.Confirmed, ExitCodes.InternalError), (result.Outcome, result.ExitCode));
        Assert.NotNull(store.State!.Pending);
    }

    [Fact]
    public void InvalidHandleReenumeratesOnceWithSameIdentity()
    {
        var first = new FakeMonitorSession(5);
        first.Reads.Enqueue(new(false, null, NativeErrors.InvalidPhysicalMonitorHandle));
        var second = new FakeMonitorSession(6);
        locator.Returns(first).Returns(second);

        var result = Run(Toggle);

        Assert.Equal(Outcomes.Confirmed, result.Outcome);
        Assert.Equal(2, locator.OpenCalls);
        Assert.Equal((1, 1), (first.DisposeCount, second.DisposeCount));
        Assert.Equal([6u], locator.AllWrites);
    }

    [Fact]
    public void FailedReenumerationIsUnverifiedWithoutAnotherWrite()
    {
        var first = new FakeMonitorSession(5);
        first.Reads.Enqueue(new(false, null, NativeErrors.InvalidHandle));
        locator.Returns(first);

        var result = Run(Toggle);

        Assert.Equal(Outcomes.OutcomeUnverified, result.Outcome);
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal([6u], locator.AllWrites);
    }

    [Theory]
    [InlineData(6u, Outcomes.Confirmed)]
    [InlineData(5u, Outcomes.NotChanged)]
    public void PendingIsReconciledWithoutWriting(uint current, string resolved)
    {
        store.State = StateWithPending();
        var session = new FakeMonitorSession(current);
        locator.Returns(session);

        var result = Run(Toggle);

        Assert.Equal((Outcomes.ReconciledWithoutToggle, ExitCodes.ReconciledWithoutToggle), (result.Outcome, result.ExitCode));
        Assert.Empty(session.Writes);
        Assert.Null(store.State!.Pending);
        Assert.Equal(resolved, store.State.LastCompleted!.Outcome);
        Assert.Equal("op-1", store.State.LastCompleted.OperationId);
        Assert.Equal(1, session.DisposeCount);
    }

    [Theory]
    [InlineData(17u)]
    [InlineData(null)]
    public void UnresolvablePendingIsRetainedWithoutWriting(uint? current)
    {
        store.State = StateWithPending();
        var session = new FakeMonitorSession(current, current, current);
        locator.Returns(session);

        var result = Run(Set(5));

        Assert.Equal(ExitCodes.OutcomeUnverified, result.ExitCode);
        Assert.Empty(session.Writes);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public void PendingWithMissingTargetIsRetained()
    {
        store.State = StateWithPending();
        Assert.Equal(ExitCodes.OutcomeUnverified, Run(Toggle).ExitCode);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public void DryRunNeverResolvesPending()
    {
        store.State = StateWithPending();
        var session = new FakeMonitorSession(6);
        locator.Returns(session);

        var result = Run(DryRun);

        Assert.Equal(Outcomes.DryRun, result.Outcome);
        Assert.Empty(store.Saved);
        Assert.Empty(session.Writes);
    }

    [Fact]
    public void DryRunPlansWithoutWritingMonitorOrState()
    {
        var session = new FakeMonitorSession(5);
        locator.Returns(session);

        var result = Run(DryRun);

        Assert.Equal((Outcomes.DryRun, 5u, 6u), (result.Outcome, result.Before!.Value, result.Target!.Value));
        Assert.Empty(session.Writes);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public void CooldownDebouncesWithoutExtendingIt()
    {
        var stamp = new CooldownStamp(clock.BootIdentity, clock.MonotonicMilliseconds - 500);
        store.State = new(1, TestData.Identity, null, null, stamp);

        var result = Run(Toggle);

        Assert.Equal((Outcomes.Debounced, ExitCodes.BusyOrDebounced), (result.Outcome, result.ExitCode));
        Assert.Equal(0, locator.OpenCalls);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public void CooldownExpires()
    {
        store.State = new(1, TestData.Identity, null, null, new(clock.BootIdentity, clock.MonotonicMilliseconds - 1500));
        locator.Returns(new FakeMonitorSession(5, 6));
        Assert.Equal(ExitCodes.Success, Run(Toggle).ExitCode);
    }

    [Theory]
    [InlineData("boot-2", -500)]   // reboot: a stamp from another boot never applies
    [InlineData("boot-1", 60_000)] // monotonic reading behind the stamp never creates an infinite cooldown
    public void CooldownCannotBecomeIndefinite(string stampBoot, long stampOffset)
    {
        store.State = new(1, TestData.Identity, null, null, new(stampBoot, clock.MonotonicMilliseconds + stampOffset));
        locator.Returns(new FakeMonitorSession(5, 6));
        Assert.Equal(ExitCodes.Success, Run(Toggle).ExitCode);
    }

    [Fact]
    public void CorruptStateFailsClosed()
    {
        store.Corrupt = true;
        locator.Returns(new FakeMonitorSession(5, 6));
        Assert.Equal(ExitCodes.InternalError, Run(Toggle).ExitCode);
        Assert.Equal(0, locator.OpenCalls);
    }

    [Fact]
    public void StateFromAnotherEnrollmentFailsClosed()
    {
        store.State = new(1, TestData.Identity with { EdidIdentityHash = new string('b', 64) }, null, null, null);
        locator.Returns(new FakeMonitorSession(5, 6));
        Assert.Equal(ExitCodes.InvalidConfiguration, Run(Toggle).ExitCode);
        Assert.Equal(0, locator.OpenCalls);
    }

    [Fact]
    public void ReportFailureDoesNotChangeOutcomeOrLosePendingSemantics()
    {
        reporter.Throw = true;
        locator.Returns(new FakeMonitorSession(5, 6));
        var result = Run(Toggle);
        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Null(store.State!.Pending);
    }

    [Fact]
    public void ReportsAreEmittedWhileTheLockIsHeld()
    {
        locator.Returns(new FakeMonitorSession(5, 6));
        Run(Toggle);
        Assert.Single(reporter.Reports);
        Assert.False(reporter.ReportedWhileUnlocked);
    }

    [Fact]
    public void ToggleNeverWritesValuesOutsideTheProfile()
    {
        foreach (var start in new uint[] { 5, 6 })
        {
            store.State = null;
            var session = new FakeMonitorSession(start, null, null, null, null, null, null, null, null, null, null, null);
            locator.Returns(session);
            Run(Toggle);
            Assert.All(session.Writes, value => Assert.True(DeviceProfile.Allows(value)));
            Assert.True(session.Writes.Count <= 1);
        }
    }
}
