namespace MonitorSwitch.Runtime;

public static class AppPaths
{
    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MonitorSwitch");
    public static string Config => Path.Combine(Root, "config.json");
    public static string State => Path.Combine(Root, "state.json");
    public static string LastResult => Path.Combine(Root, "last-result.json");
    public static string ProbeLog => Path.Combine(Root, "logs", "trigger-probe.jsonl");
}
