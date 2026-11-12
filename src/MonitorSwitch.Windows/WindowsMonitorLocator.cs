using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using MonitorSwitch.Core;
using static MonitorSwitch.Windows.NativeMethods;

namespace MonitorSwitch.Windows;

/// <summary>
/// Joins active DisplayConfig paths (target device interface path) to GDI source names, HMONITORs,
/// physical monitor counts and SetupAPI EDID. Enumeration order is never used as identity.
/// </summary>
public sealed class WindowsMonitorLocator : IMonitorLocator
{
    public IReadOnlyList<MonitorCandidate> Discover() => Enumerate().Select(e => e.Candidate).ToList();

    public IMonitorSession OpenUnique(MonitorIdentity enrolled)
    {
        var matches = Enumerate().Where(e =>
            string.Equals(e.Candidate.DeviceInterfacePath, enrolled.DeviceInterfacePath, StringComparison.OrdinalIgnoreCase)
            && e.Candidate.EdidIdentityHash == enrolled.EdidIdentityHash).ToList();
        if (matches.Count == 0) throw new TargetUnavailableException("Enrolled monitor is not an active display target.");
        if (matches.Count > 1) throw new TargetUnavailableException("Enrolled identity matches more than one active target.");
        var match = matches[0];
        if (match.Candidate.Problem is { } problem) throw new TargetUnavailableException(problem);
        if (!match.Candidate.MatchesProfile) throw new TargetUnavailableException("Enrolled monitor no longer reports the SAM1017 profile.");
        return PhysicalMonitorSession.Open(match.Monitor);
    }

    private sealed record Entry(MonitorCandidate Candidate, IntPtr Monitor);
    private sealed record ActiveTarget(string GdiDeviceName, string DevicePath, string FriendlyName);

    private static List<Entry> Enumerate()
    {
        NativeLayout.Verify();
        var targets = ActiveTargets();
        var monitorsByGdiName = MonitorsByGdiName();
        var edids = EdidsByInterfacePath();
        var targetsPerSource = targets.GroupBy(t => t.GdiDeviceName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var entries = new List<Entry>();
        foreach (var target in targets)
        {
            var monitor = monitorsByGdiName.GetValueOrDefault(target.GdiDeviceName);
            var physicalCount = monitor == IntPtr.Zero ? 0 : PhysicalMonitorSession.Count(monitor);
            var edid = Edid.TryParse(edids.GetValueOrDefault(target.DevicePath));
            entries.Add(new(new MonitorCandidate(target.GdiDeviceName, target.DevicePath,
                string.IsNullOrEmpty(target.FriendlyName) ? null : target.FriendlyName,
                edid?.Manufacturer, edid?.ProductId, edid?.IdentityHash, physicalCount,
                targetsPerSource[target.GdiDeviceName]), monitor));
        }
        return entries;
    }

    private static List<ActiveTarget> ActiveTargets()
    {
        PathInfo[] paths;
        while (true)
        {
            Check(GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out var pathCount, out var modeCount), "GetDisplayConfigBufferSizes");
            paths = new PathInfo[pathCount];
            var modes = new ModeInfo[modeCount];
            var status = QueryDisplayConfig(QdcOnlyActivePaths, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
            if (status == ErrorInsufficientBuffer) continue; // topology changed between calls
            Check(status, "QueryDisplayConfig");
            Array.Resize(ref paths, (int)pathCount);
            break;
        }

        var result = new List<ActiveTarget>();
        foreach (var path in paths)
        {
            var source = new SourceDeviceName
            {
                Header = new() { Type = GetSourceName, Size = (uint)Marshal.SizeOf<SourceDeviceName>(),
                    AdapterId = path.SourceInfo.AdapterId, Id = path.SourceInfo.Id },
            };
            var target = new TargetDeviceName
            {
                Header = new() { Type = GetTargetName, Size = (uint)Marshal.SizeOf<TargetDeviceName>(),
                    AdapterId = path.TargetInfo.AdapterId, Id = path.TargetInfo.Id },
            };
            if (DisplayConfigGetDeviceInfo(ref source) != ErrorSuccess || DisplayConfigGetDeviceInfo(ref target) != ErrorSuccess) continue;
            if (string.IsNullOrEmpty(target.MonitorDevicePath)) continue;
            result.Add(new(source.ViewGdiDeviceName, target.MonitorDevicePath, target.MonitorFriendlyDeviceName));
        }
        return result;
    }

    private static Dictionary<string, IntPtr> MonitorsByGdiName()
    {
        var monitors = new Dictionary<string, IntPtr>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        MonitorEnumProc callback = (monitor, _, _, _) =>
        {
            var info = new MonitorInfoEx { Size = Marshal.SizeOf<MonitorInfoEx>() };
            if (GetMonitorInfo(monitor, ref info) && !monitors.TryAdd(info.Device, monitor)) duplicates.Add(info.Device);
            return true;
        };
        if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero))
            throw new Win32Exception("EnumDisplayMonitors failed.");
        GC.KeepAlive(callback);
        // A GDI name with more than one HMONITOR is ambiguous; drop it so its targets report no physical monitor.
        foreach (var name in duplicates) monitors.Remove(name);
        return monitors;
    }

    private static Dictionary<string, byte[]> EdidsByInterfacePath()
    {
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var classGuid = MonitorInterfaceClass;
        var set = SetupDiGetClassDevs(ref classGuid, IntPtr.Zero, IntPtr.Zero, DigcfPresent | DigcfDeviceInterface);
        if (set == InvalidHandleValue) throw new Win32Exception(Marshal.GetLastPInvokeError(), "SetupDiGetClassDevs failed.");
        try
        {
            for (uint index = 0; ; index++)
            {
                var data = new DeviceInterfaceData { Size = (uint)Marshal.SizeOf<DeviceInterfaceData>() };
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref classGuid, index, ref data)) break;
                var devInfo = new DevInfoData { Size = (uint)Marshal.SizeOf<DevInfoData>() };
                var path = InterfacePath(set, ref data, ref devInfo);
                if (path is null) continue;
                var edid = ReadEdid(set, ref devInfo);
                if (edid is not null) result[path] = edid;
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return result;
    }

    private static string? InterfacePath(IntPtr set, ref DeviceInterfaceData data, ref DevInfoData devInfo)
    {
        SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out var required, ref devInfo);
        if (required == 0) return null;
        var buffer = Marshal.AllocHGlobal((int)required);
        try
        {
            // cbSize is the fixed part of SP_DEVICE_INTERFACE_DETAIL_DATA_W: 8 on 64-bit, 6 on 32-bit.
            Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);
            return SetupDiGetDeviceInterfaceDetail(set, ref data, buffer, required, out _, ref devInfo)
                ? Marshal.PtrToStringUni(buffer + 4)
                : null;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static byte[]? ReadEdid(IntPtr set, ref DevInfoData devInfo)
    {
        var key = SetupDiOpenDevRegKey(set, ref devInfo, DicsFlagGlobal, 0, DiregDev, KeyRead);
        if (key == InvalidHandleValue) return null;
        using var registryKey = RegistryKey.FromHandle(new SafeRegistryHandle(key, ownsHandle: true));
        return registryKey.GetValue("EDID") as byte[];
    }

    private static void Check(int status, string api)
    {
        if (status != ErrorSuccess) throw new Win32Exception(status, $"{api} failed.");
    }
}
