using System.Text.Json;
using System.Text.Json.Serialization;

namespace MonitorSwitch.Core;

public sealed record MonitorConfig(string Manufacturer, string ProductId, string FriendlyName,
    string DeviceInterfacePath, string EdidIdentityHash, bool RequireUniqueMatch);
public sealed record InputConfig(int VcpCode, int[] AllowedValues, Dictionary<string, string> Labels);
public sealed record OperationSettings(int CooldownMs, int ReadAttempts, int VerificationDeadlineMs);
public sealed record FeedbackConfig(bool SuccessSound, bool ErrorSound);

public sealed record AppConfig(int SchemaVersion, MonitorConfig Monitor, InputConfig Input,
    OperationSettings Operation, FeedbackConfig Feedback)
{
    [JsonIgnore]
    public MonitorIdentity Identity => new(Monitor.DeviceInterfacePath, Monitor.EdidIdentityHash, Monitor.ProductId);

    public string LabelFor(uint input) => Input.Labels.GetValueOrDefault(input.ToString(), $"Input {input}");
}

public static class StrictJson
{
    // Case-sensitive camelCase, no unknown/duplicate/missing/null-for-non-nullable members, no numbers from strings.
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        WriteIndented = true,
    };

    // Reports and logs are derived output; strict null checks only matter when reading authority back.
    // Local files only, never embedded in HTML, so device paths keep a literal '&' for copy/paste.
    public static readonly JsonSerializerOptions Output = new(Options)
    {
        RespectNullableAnnotations = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}

public static class ConfigParser
{
    public const int SchemaVersion = 1;

    public static AppConfig Parse(string json)
    {
        AppConfig? config;
        try { config = JsonSerializer.Deserialize<AppConfig>(json, StrictJson.Options); }
        catch (JsonException error) { throw new ConfigurationException($"Configuration is not valid JSON for schema {SchemaVersion}: {error.Message}"); }
        if (config is null) throw new ConfigurationException("Configuration is empty.");
        Validate(config);
        return config;
    }

    public static void Validate(AppConfig config)
    {
        static void Require(bool condition, string message)
        {
            if (!condition) throw new ConfigurationException(message);
        }

        Require(config.SchemaVersion == SchemaVersion, "Unsupported schema version.");
        var monitor = config.Monitor;
        Require(monitor.Manufacturer == DeviceProfile.Manufacturer && monitor.ProductId == DeviceProfile.ProductId,
            "Monitor must be the demonstrated SAM/SAM1017 profile.");
        Require(!string.IsNullOrWhiteSpace(monitor.DeviceInterfacePath) && !monitor.DeviceInterfacePath.Contains('<'),
            "Device interface path is missing or a placeholder.");
        Require(Edid.IsIdentityHash(monitor.EdidIdentityHash), "EDID identity hash must be 64 lowercase hex characters.");
        Require(monitor.RequireUniqueMatch, "requireUniqueMatch must be true.");
        Require(config.Input.VcpCode == DeviceProfile.VcpCode, "vcpCode must be 96 (0x60).");
        Require(config.Input.AllowedValues.Order().SequenceEqual([5, 6]), "allowedValues must be exactly [5, 6].");
        Require(config.Input.Labels.Keys.Order(StringComparer.Ordinal).SequenceEqual(["5", "6"]), "labels must name exactly inputs 5 and 6.");
        var operation = config.Operation;
        Require(operation.CooldownMs is >= 1500 and <= 10000, "cooldownMs must be 1500-10000.");
        Require(operation.ReadAttempts is >= 1 and <= 3, "readAttempts must be 1-3.");
        Require(operation.VerificationDeadlineMs is >= 1000 and <= 8000, "verificationDeadlineMs must be 1000-8000.");
    }

    /// <summary>Builds an enrollment from fresh discovery evidence, keeping prior cosmetic/operation settings.</summary>
    public static AppConfig ForEnrollment(MonitorCandidate candidate, AppConfig? previous)
    {
        var config = new AppConfig(SchemaVersion,
            new(DeviceProfile.Manufacturer, DeviceProfile.ProductId, previous?.Monitor.FriendlyName ?? candidate.FriendlyName ?? "LU28R55",
                candidate.DeviceInterfacePath, candidate.EdidIdentityHash!, true),
            previous?.Input ?? new(DeviceProfile.VcpCode, [5, 6], new() { ["5"] = "Input 5", ["6"] = "Input 6" }),
            previous?.Operation ?? new(1500, 3, 8000),
            previous?.Feedback ?? new(false, true));
        Validate(config);
        return config;
    }
}
