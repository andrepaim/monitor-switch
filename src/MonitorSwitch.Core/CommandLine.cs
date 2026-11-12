namespace MonitorSwitch.Core;

public enum CommandKind { Toggle, Discover, Status, Diagnose, ProbeTrigger, Set, Enroll }

public sealed record Command(CommandKind Kind, uint? Input = null, bool DryRun = false,
    string? OutputPath = null, string? DevicePath = null, string? EdidHash = null,
    bool ReplaceEnrollment = false);

public static class CommandLine
{
    public static Command Parse(string[] args)
    {
        if (args.Length == 0) return new(CommandKind.Toggle);
        var kind = args[0] switch
        {
            "toggle" => CommandKind.Toggle,
            "discover" => CommandKind.Discover,
            "status" => CommandKind.Status,
            "diagnose" => CommandKind.Diagnose,
            "probe-trigger" => CommandKind.ProbeTrigger,
            "set" => CommandKind.Set,
            "enroll" => CommandKind.Enroll,
            _ => throw new ArgumentException("Unknown command.")
        };

        var options = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i++)
        {
            var key = args[i];
            if (!key.StartsWith("--", StringComparison.Ordinal) || options.ContainsKey(key))
                throw new ArgumentException("Invalid or duplicate option.");
            if (key is "--json" or "--dry-run" or "--replace") options.Add(key, null);
            else if (key is "--output" or "--input" or "--device-path" or "--edid-hash")
            {
                if (++i >= args.Length || string.IsNullOrWhiteSpace(args[i]) || args[i].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException("Missing option value.");
                options.Add(key, args[i]);
            }
            else throw new ArgumentException("Unknown option.");
        }

        string[] permitted = kind switch
        {
            CommandKind.Toggle => ["--dry-run"],
            CommandKind.Discover => ["--json", "--output"],
            CommandKind.Status or CommandKind.Diagnose => ["--output"],
            CommandKind.Set => ["--input"],
            CommandKind.Enroll => ["--device-path", "--edid-hash", "--replace"],
            _ => []
        };
        if (options.Keys.Any(key => !permitted.Contains(key)))
            throw new ArgumentException("Option is not supported for this command.");

        uint? input = null;
        if (kind == CommandKind.Set)
        {
            // Exact decimal literals only, never generic VCP values or expressions.
            var value = options.GetValueOrDefault("--input");
            input = value switch { "5" => 5u, "6" => 6u, _ => throw new ArgumentException("Input must be 5 or 6.") };
        }
        if (kind == CommandKind.Discover && !options.ContainsKey("--json"))
            throw new ArgumentException("Discovery requires --json.");
        if (kind is CommandKind.Discover or CommandKind.Status or CommandKind.Diagnose && !options.ContainsKey("--output"))
            throw new ArgumentException("This command requires --output.");
        if (kind == CommandKind.Enroll && (!options.ContainsKey("--device-path") || !options.ContainsKey("--edid-hash")))
            throw new ArgumentException("Enrollment requires device path and EDID hash.");
        // SHA-256 as 64 lowercase hex characters, exactly as discovery reports it.
        if (kind == CommandKind.Enroll && !IsSha256Hex(options["--edid-hash"]!))
            throw new ArgumentException("EDID hash must be 64 lowercase hex characters.");

        return new(kind, input, options.ContainsKey("--dry-run"), options.GetValueOrDefault("--output"),
            options.GetValueOrDefault("--device-path"), options.GetValueOrDefault("--edid-hash"), options.ContainsKey("--replace"));
    }

    private static bool IsSha256Hex(string value) =>
        value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
