using System.Text.Json;
using MonitorSwitch.Core;
using MonitorSwitch.Tests.Fakes;
using Xunit;

namespace MonitorSwitch.Tests;

public sealed class MaintenanceCommandsTests
{
    private readonly FakeLocator locator = new();
    private readonly FakeStore store = new();
    private readonly FakeLock operationLock = new();
    private readonly FakeConfigStore configStore = new();

    private MaintenanceCommands Commands => new(locator, store, operationLock, configStore);

    private void Enrolled() => configStore.Text = JsonSerializer.Serialize(TestData.Config(), StrictJson.Options);

    [Fact]
    public void StatusReadsWithoutWritingOrResolvingPending()
    {
        Enrolled();
        store.State = new(1, TestData.Identity, new("op-1", TestData.Identity, "toggle", 5, 6, DateTimeOffset.UnixEpoch), null, null);
        var session = new FakeMonitorSession(6);
        locator.Returns(session);

        var report = Commands.Status();

        Assert.Equal(ExitCodes.Success, report.ExitCode);
        Assert.Empty(session.Writes);
        Assert.Empty(store.Saved);
        Assert.Equal(1, session.DisposeCount);
    }

    [Fact]
    public void StatusWithoutEnrollmentIsInvalidConfiguration() =>
        Assert.Throws<ConfigurationException>(() => Commands.Status());

    [Fact]
    public void ReadOnlyCommandsReturnBusyUnderAnActiveOperation()
    {
        Enrolled();
        operationLock.Busy = true;
        Assert.All(new[] { Commands.Status(), Commands.Discover(), Commands.Diagnose() },
            report => Assert.Equal(ExitCodes.BusyOrDebounced, report.ExitCode));
        Assert.Equal(0, locator.OpenCalls);
    }

    [Fact]
    public void DiscoverReadsOnlyProfileMatchingMonitors()
    {
        locator.Candidates.Add(TestData.Candidate());
        locator.Candidates.Add(TestData.Candidate(productId: "DEL4321", path: "other"));
        locator.Candidates.Add(TestData.Candidate(path: "clone", targets: 2));
        locator.Returns(new FakeMonitorSession(5));

        var report = Commands.Discover();

        Assert.Equal(ExitCodes.Success, report.ExitCode);
        Assert.Equal(1, locator.OpenCalls);
        Assert.Empty(locator.AllWrites);
    }

    [Fact]
    public void DiagnoseProposesButDoesNotAcceptAChangedPath()
    {
        Enrolled();
        locator.Candidates.Add(TestData.Candidate(path: @"\\?\DISPLAY#SAM1017#moved"));
        var report = Commands.Diagnose();
        Assert.Contains("moved", JsonSerializer.Serialize(report.Details, StrictJson.Output));
        Assert.Empty(configStore.Saved);
    }

    [Fact]
    public void EnrollmentRequiresExactUniqueFreshDiscovery()
    {
        locator.Candidates.Add(TestData.Candidate());

        var report = Commands.Enroll(TestData.DevicePath.ToUpperInvariant(), TestData.Hash, replace: false);

        Assert.Equal(ExitCodes.Success, report.ExitCode);
        // The stored path is exactly what discovery reported, not the operator's spelling.
        Assert.Equal(TestData.DevicePath, Assert.Single(configStore.Saved).Monitor.DeviceInterfacePath);
        Assert.Equal(0, locator.OpenCalls);
    }

    [Theory]
    [InlineData("SAM1018", 1, 1, ExitCodes.InvalidConfiguration)]
    [InlineData("SAM1017", 2, 1, ExitCodes.TargetMissingOrAmbiguous)]
    [InlineData("SAM1017", 1, 2, ExitCodes.TargetMissingOrAmbiguous)]
    public void EnrollmentRefusesWrongProfileOrAmbiguousPhysicalMapping(string productId, int physical, int targets, int expected)
    {
        locator.Candidates.Add(TestData.Candidate(productId: productId, physical: physical, targets: targets));
        Assert.Equal(expected, Commands.Enroll(TestData.DevicePath, TestData.Hash, false).ExitCode);
        Assert.Empty(configStore.Saved);
    }

    [Fact]
    public void EnrollmentRefusesDuplicateOrMissingMatches()
    {
        Assert.Equal(ExitCodes.TargetMissingOrAmbiguous, Commands.Enroll(TestData.DevicePath, TestData.Hash, false).ExitCode);
        locator.Candidates.Add(TestData.Candidate());
        locator.Candidates.Add(TestData.Candidate());
        Assert.Equal(ExitCodes.TargetMissingOrAmbiguous, Commands.Enroll(TestData.DevicePath, TestData.Hash, false).ExitCode);
        Assert.Empty(configStore.Saved);
    }

    [Fact]
    public void ReenrollmentRequiresReplaceAndNoPendingOperation()
    {
        Enrolled();
        locator.Candidates.Add(TestData.Candidate(hash: new string('c', 64)));
        Assert.Equal(ExitCodes.InvalidConfiguration, Commands.Enroll(TestData.DevicePath, new string('c', 64), false).ExitCode);

        store.State = new(1, TestData.Identity, new("op-1", TestData.Identity, "toggle", 5, 6, DateTimeOffset.UnixEpoch), null, null);
        Assert.Equal(ExitCodes.InvalidConfiguration, Commands.Enroll(TestData.DevicePath, new string('c', 64), true).ExitCode);
        Assert.Empty(configStore.Saved);

        store.State = store.State with { Pending = null, LastCompletion = new("boot-1", 5) };
        Assert.Equal(ExitCodes.Success, Commands.Enroll(TestData.DevicePath, new string('c', 64), true).ExitCode);
        Assert.Equal(new string('c', 64), store.State!.EnrolledIdentity.EdidIdentityHash);
        Assert.NotNull(store.State.LastCompletion);
    }
}
