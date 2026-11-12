using System.Text.Json;
using MonitorSwitch.Core;
using MonitorSwitch.Runtime;
using MonitorSwitch.Windows;

namespace MonitorSwitch;

internal static class Program
{
    private static int Main(string[] args)
    {
        Command command;
        try { command = CommandLine.Parse(args); }
        catch (ArgumentException) { return ExitCodes.InvalidConfiguration; }
        if (command.Kind == CommandKind.ProbeTrigger) return ProbeRecorder.Record();

        try { return Execute(command); }
        catch (ConfigurationException) { return ExitCodes.InvalidConfiguration; }
        // Boundary: never a stack-trace dialog; the structured exit code is the result.
        catch (Exception) { return ExitCodes.InternalError; }
    }

    private static int Execute(Command command)
    {
        var clock = new SystemClock();
        var locator = new WindowsMonitorLocator();
        var store = new JsonStateStore(AppPaths.State);
        var operationLock = new FileOperationLock(AppPaths.Lock);
        var configStore = new FileConfigStore(AppPaths.Config, AppPaths.ConfigBackup);

        if (command.Kind is CommandKind.Toggle or CommandKind.Set)
        {
            var config = ConfigParser.Parse(configStore.ReadText() ?? throw new ConfigurationException("Not enrolled."));
            var reporter = new FileResultReporter(AppPaths.Logs, AppPaths.LastResult, clock, config.Monitor.EdidIdentityHash);
            var result = new SwitchOrchestrator(locator, store, operationLock, clock, reporter, config).Run(command);
            if (result.ExitCode == ExitCodes.Success && config.Feedback.SuccessSound) Feedback.Success();
            else if (result.ExitCode != ExitCodes.Success && result.Outcome != Outcomes.Busy && config.Feedback.ErrorSound) Feedback.Error();
            return result.ExitCode;
        }

        var maintenance = new MaintenanceCommands(locator, store, operationLock, configStore);
        var report = command.Kind switch
        {
            CommandKind.Discover => maintenance.Discover(),
            CommandKind.Status => maintenance.Status(),
            CommandKind.Diagnose => maintenance.Diagnose(),
            CommandKind.Enroll => maintenance.Enroll(command.DevicePath!, command.EdidHash!, command.ReplaceEnrollment),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };
        var body = new
        {
            generatedAtUtc = clock.UtcNow,
            version = typeof(Program).Assembly.GetName().Version?.ToString(),
            command = command.Kind.ToString().ToLowerInvariant(),
            report.Outcome,
            report.ExitCode,
            report.Details,
        };

        if (command.OutputPath is { } output) new ReportWriter(AppPaths.Root).Write(output, body);
        // Enrollment has no --output; its evidence goes to last-result.json unless another operation owns it.
        else if (report.Outcome != Outcomes.Busy)
            AtomicFile.Write(AppPaths.LastResult, JsonSerializer.SerializeToUtf8Bytes(body, StrictJson.Output));
        return report.ExitCode;
    }
}
