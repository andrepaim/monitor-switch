using MonitorSwitch.Core;

namespace MonitorSwitch.Windows;

/// <summary>
/// Safe scaffold boundary. No native handles or DDC calls exist yet.
/// Implement exact identity discovery before introducing any native write.
/// </summary>
public sealed class WindowsMonitorLocator : IMonitorLocator
{
    public IMonitorSession OpenUnique(MonitorIdentity enrolled) =>
        throw new NotSupportedException("Native monitor discovery is not implemented. See docs/IMPLEMENTATION.md.");
}
