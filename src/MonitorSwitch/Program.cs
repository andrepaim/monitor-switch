using MonitorSwitch.Core;
using MonitorSwitch.Runtime;

namespace MonitorSwitch;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var command = CommandLine.Parse(args);
            if (command.Kind == CommandKind.ProbeTrigger) return ProbeRecorder.Record();

            // Scaffold only: never open a monitor, enroll, mutate state, or claim a switch.
            // Structured monitor-command reports are a pending implementation milestone.
            return ExitCodes.NotImplemented;
        }
        catch (ArgumentException) { return ExitCodes.InvalidConfiguration; }
        catch (Exception) { return ExitCodes.InternalError; }
    }
}
