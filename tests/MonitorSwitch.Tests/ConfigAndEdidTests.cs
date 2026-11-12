using System.Text.Json;
using System.Text.Json.Nodes;
using MonitorSwitch.Core;
using Xunit;

namespace MonitorSwitch.Tests;

public sealed class ConfigTests
{
    private static string ValidJson() => JsonSerializer.Serialize(TestData.Config(), StrictJson.Options);

    private static string Mutate(Action<JsonObject> change)
    {
        var node = JsonNode.Parse(ValidJson())!.AsObject();
        change(node);
        return node.ToJsonString();
    }

    [Fact]
    public void ValidEnrollmentRoundTrips()
    {
        var config = ConfigParser.Parse(ValidJson());
        Assert.Equal(TestData.Identity, config.Identity);
        Assert.Equal("Input 6", config.LabelFor(6));
    }

    [Fact]
    public void ExampleConfigIsNotAValidEnrollment()
    {
        var example = File.ReadAllText(Path.Combine(RepoRoot(), "config.example.json"));
        Assert.Throws<ConfigurationException>(() => ConfigParser.Parse(example));
    }

    public static TheoryData<string> InvalidConfigs() => new()
    {
        Mutate(n => n["unknown"] = 1),
        Mutate(n => n["monitor"]!["serial"] = "x"),
        ValidJson().Replace("\"schemaVersion\": 1,", "\"schemaVersion\": 1, \"schemaVersion\": 1,"),
        Mutate(n => n["schemaVersion"] = 2),
        Mutate(n => n.Remove("feedback")),
        Mutate(n => n["monitor"]!["deviceInterfacePath"] = "<captured during explicit enrollment>"),
        Mutate(n => n["monitor"]!["deviceInterfacePath"] = null),
        Mutate(n => n["monitor"]!["edidIdentityHash"] = new string('A', 64)),
        Mutate(n => n["monitor"]!["productId"] = "SAM1018"),
        Mutate(n => n["monitor"]!["requireUniqueMatch"] = false),
        Mutate(n => n["input"]!["vcpCode"] = 60),
        Mutate(n => n["input"]!["allowedValues"] = new JsonArray(17, 18)),
        Mutate(n => n["input"]!["allowedValues"] = new JsonArray(5, 6, 17)),
        Mutate(n => n["input"]!["allowedValues"] = new JsonArray(5, 5)),
        Mutate(n => n["input"]!["vcpCode"] = "96"),
        Mutate(n => n["operation"]!["cooldownMs"] = 1499),
        Mutate(n => n["operation"]!["cooldownMs"] = 10001),
        Mutate(n => n["operation"]!["readAttempts"] = 0),
        Mutate(n => n["operation"]!["readAttempts"] = 4),
        Mutate(n => n["operation"]!["verificationDeadlineMs"] = 999),
        Mutate(n => n["operation"]!["verificationDeadlineMs"] = 8001),
        Mutate(n => n["Monitor"] = n["monitor"]!.DeepClone()),
        "not json",
        "null",
    };

    [Theory]
    [MemberData(nameof(InvalidConfigs))]
    public void InvalidConfigIsRejected(string json) =>
        Assert.Throws<ConfigurationException>(() => ConfigParser.Parse(json));

    [Fact]
    public void EnrollmentUsesDiscoveredIdentityAndKeepsPreviousSettings()
    {
        var previous = TestData.Config(cooldownMs: 3000);
        var candidate = TestData.Candidate(hash: new string('c', 64), path: @"\\?\DISPLAY#SAM1017#new");
        var config = ConfigParser.ForEnrollment(candidate, previous);
        Assert.Equal((candidate.DeviceInterfacePath, candidate.EdidIdentityHash), (config.Monitor.DeviceInterfacePath, config.Monitor.EdidIdentityHash));
        Assert.Equal(3000, config.Operation.CooldownMs);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MonitorSwitch.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}

public sealed class EdidTests
{
    [Fact]
    public void ValidEdidYieldsProfileAndLowercaseHash()
    {
        var info = Edid.TryParse(TestData.SamsungEdid())!;
        Assert.Equal(("SAM", "SAM1017"), (info.Manufacturer, info.ProductId));
        Assert.True(Edid.IsIdentityHash(info.IdentityHash));
    }

    [Fact]
    public void HashCoversEveryByteIncludingExtensions()
    {
        var one = Edid.TryParse(TestData.SamsungEdid(extensions: 1, serial: 1))!;
        var two = Edid.TryParse(TestData.SamsungEdid(extensions: 1, serial: 2))!;
        var edid = TestData.SamsungEdid(extensions: 1, serial: 1);
        edid[200] = 0x42;
        TestData.FixChecksum(edid, 1);
        Assert.NotEqual(one.IdentityHash, two.IdentityHash);
        Assert.NotEqual(one.IdentityHash, Edid.TryParse(edid)!.IdentityHash);
    }

    [Fact]
    public void BadChecksumIsRejected()
    {
        var edid = TestData.SamsungEdid();
        edid[20] ^= 1;
        Assert.Null(Edid.TryParse(edid));
    }

    [Fact]
    public void BadExtensionChecksumIsRejected()
    {
        var edid = TestData.SamsungEdid(extensions: 1);
        edid[130] ^= 1;
        Assert.Null(Edid.TryParse(edid));
    }

    [Fact]
    public void MissingAdvertisedExtensionIsRejected()
    {
        var edid = TestData.SamsungEdid(extensions: 1);
        Assert.Null(Edid.TryParse(edid[..128]));
    }

    [Fact]
    public void BadHeaderOrLengthIsRejected()
    {
        var edid = TestData.SamsungEdid();
        edid[0] = 1;
        Assert.Null(Edid.TryParse(edid));
        Assert.Null(Edid.TryParse(new byte[100]));
        Assert.Null(Edid.TryParse(null));
    }
}
