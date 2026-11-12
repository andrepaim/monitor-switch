using MonitorSwitch.Core;
using Xunit;

namespace MonitorSwitch.Tests;

public sealed class CommandLineTests
{
    [Fact]
    public void NoArgumentsMeansToggle() =>
        Assert.Equal(CommandLine.Parse(["toggle"]), CommandLine.Parse([]));

    [Theory]
    [InlineData("5", 5u)]
    [InlineData("6", 6u)]
    public void ExplicitSetAcceptsOnlyProfileInputs(string text, uint expected) =>
        Assert.Equal(expected, CommandLine.Parse(["set", "--input", text]).Input);

    [Theory]
    [InlineData("17")]
    [InlineData("18")]
    [InlineData("0x05")]
    [InlineData("5; arbitrary-command")]
    [InlineData("96")]
    public void ArbitraryInputIsRejected(string text) =>
        Assert.Throws<ArgumentException>(() => CommandLine.Parse(["set", "--input", text]));

    [Theory]
    [InlineData("toggle", "--input", "5")]
    [InlineData("probe-trigger", "--output", "file.json")]
    [InlineData("status", "--output", "--output")]
    [InlineData("toggle", "--vcp", "96")]
    public void InvalidOptionsAreRejected(string first, string second, string third) =>
        Assert.Throws<ArgumentException>(() => CommandLine.Parse([first, second, third]));

    [Fact]
    public void DuplicateOptionsAreRejected() =>
        Assert.Throws<ArgumentException>(() => CommandLine.Parse(["toggle", "--dry-run", "--dry-run"]));

    [Fact]
    public void DryRunIsExplicit() => Assert.True(CommandLine.Parse(["toggle", "--dry-run"]).DryRun);

    [Fact]
    public void PathsArePreservedLiterally()
    {
        const string path = "C:\\Users\\André Paim\\diagnóstico $(literal).json";
        Assert.Equal(path, CommandLine.Parse(["discover", "--json", "--output", path]).OutputPath);
    }

    [Theory]
    [InlineData("discover")]
    [InlineData("status")]
    [InlineData("diagnose")]
    [InlineData("enroll")]
    [InlineData("set")]
    [InlineData("arbitrary-command")]
    public void RequiredArgumentsAreEnforced(string command) =>
        Assert.Throws<ArgumentException>(() => CommandLine.Parse([command]));

    [Theory]
    [InlineData("abc")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("gaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void EnrollmentRequiresLowercaseSha256Hex(string hash) =>
        Assert.Throws<ArgumentException>(() => CommandLine.Parse(["enroll", "--device-path", "device", "--edid-hash", hash]));

    [Fact]
    public void EnrollmentDoesNotImplyReplacement()
    {
        var command = CommandLine.Parse(["enroll", "--device-path", "device", "--edid-hash", new string('a', 64)]);
        Assert.Equal(CommandKind.Enroll, command.Kind);
        Assert.False(command.ReplaceEnrollment);
    }
}
