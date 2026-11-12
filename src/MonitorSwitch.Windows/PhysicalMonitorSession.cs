using System.Runtime.InteropServices;
using System.Text;
using MonitorSwitch.Core;
using static MonitorSwitch.Windows.NativeMethods;

namespace MonitorSwitch.Windows;

/// <summary>
/// Owns exactly one physical monitor handle array and destroys it exactly once.
/// Reads and writes only VCP 0x60, and revalidates 5/6 before any write. Never outlives the process.
/// </summary>
internal sealed class PhysicalMonitorSession : IMonitorSession
{
    private PhysicalMonitor[]? monitors;

    private PhysicalMonitorSession(PhysicalMonitor[] monitors) => this.monitors = monitors;

    public static int Count(IntPtr monitor) =>
        GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out var count) ? (int)count : 0;

    public static PhysicalMonitorSession Open(IntPtr monitor)
    {
        if (!GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out var count))
            throw new TargetUnavailableException($"Physical monitor count unavailable (0x{Marshal.GetLastPInvokeError():X8}).");
        if (count != 1)
            throw new TargetUnavailableException($"Display maps to {count} physical monitors; refusing ambiguous target.");
        var array = new PhysicalMonitor[1];
        if (!GetPhysicalMonitorsFromHMONITOR(monitor, 1, array))
            throw new TargetUnavailableException($"Physical monitor handle unavailable (0x{Marshal.GetLastPInvokeError():X8}).");
        return new PhysicalMonitorSession(array);
    }

    private IntPtr Handle => monitors?[0].Handle ?? throw new ObjectDisposedException(nameof(PhysicalMonitorSession));

    public InputRead ReadInput()
    {
        if (GetVCPFeatureAndVCPFeatureReply(Handle, DeviceProfile.VcpCode, out _, out var current, out _))
            return new(true, current, null);
        return new(false, null, Marshal.GetLastPInvokeError());
    }

    public WriteReply WriteInput(uint allowedValue)
    {
        if (!DeviceProfile.Allows(allowedValue)) throw new ArgumentOutOfRangeException(nameof(allowedValue));
        return SetVCPFeature(Handle, DeviceProfile.VcpCode, allowedValue)
            ? new(true, null)
            : new(false, Marshal.GetLastPInvokeError());
    }

    public string? ReadCapabilities()
    {
        if (!GetCapabilitiesStringLength(Handle, out var length) || length == 0) return null;
        var buffer = new byte[length];
        if (!CapabilitiesRequestAndCapabilitiesReply(Handle, buffer, length)) return null;
        return Encoding.ASCII.GetString(buffer).TrimEnd('\0');
    }

    public void Dispose()
    {
        var owned = Interlocked.Exchange(ref monitors, null);
        if (owned is not null) DestroyPhysicalMonitors((uint)owned.Length, owned);
    }
}
