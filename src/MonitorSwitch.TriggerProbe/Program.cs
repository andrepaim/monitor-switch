using MonitorSwitch.Core;
using MonitorSwitch.Runtime;

return args.Length == 0 ? ProbeRecorder.Record() : ExitCodes.InvalidConfiguration;
