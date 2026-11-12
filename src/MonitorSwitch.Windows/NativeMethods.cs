using System.Runtime.InteropServices;

namespace MonitorSwitch.Windows;

// Hand-reviewed declarations. Layouts are checked at runtime by NativeLayout.Verify before any call.
internal static class NativeMethods
{
    public const int ErrorSuccess = 0;
    public const int ErrorInsufficientBuffer = 122;
    public const uint QdcOnlyActivePaths = 0x2;
    public const uint GetSourceName = 1;
    public const uint GetTargetName = 2;
    public const uint DigcfPresent = 0x2;
    public const uint DigcfDeviceInterface = 0x10;
    public const uint DicsFlagGlobal = 0x1;
    public const uint DiregDev = 0x1;
    public const int KeyRead = 0x20019;
    public const uint MbIconHand = 0x10;
    public static readonly IntPtr InvalidHandleValue = new(-1);
    public static readonly Guid MonitorInterfaceClass = new("e6f07b5f-ee97-4a90-b076-33f57bf4eaa7");

    [StructLayout(LayoutKind.Sequential)]
    public struct Luid { public uint LowPart; public int HighPart; }

    [StructLayout(LayoutKind.Sequential)]
    public struct PathSourceInfo { public Luid AdapterId; public uint Id; public uint ModeInfoIdx; public uint StatusFlags; }

    [StructLayout(LayoutKind.Sequential)]
    public struct PathTargetInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint OutputTechnology;
        public uint Rotation;
        public uint Scaling;
        public uint RefreshNumerator;
        public uint RefreshDenominator;
        public uint ScanLineOrdering;
        public int TargetAvailable;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PathInfo { public PathSourceInfo SourceInfo; public PathTargetInfo TargetInfo; public uint Flags; }

    // Contents are not used; only the 64-byte stride matters.
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ModeInfo { [FieldOffset(0)] public uint InfoType; }

    [StructLayout(LayoutKind.Sequential)]
    public struct DeviceInfoHeader { public uint Type; public uint Size; public Luid AdapterId; public uint Id; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SourceDeviceName
    {
        public DeviceInfoHeader Header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string ViewGdiDeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct TargetDeviceName
    {
        public DeviceInfoHeader Header;
        public uint Flags;
        public uint OutputTechnology;
        public ushort EdidManufactureId;
        public ushort EdidProductCodeId;
        public uint ConnectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string MonitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string MonitorDevicePath;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MonitorInfoEx
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct PhysicalMonitor
    {
        public IntPtr Handle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DeviceInterfaceData { public uint Size; public Guid InterfaceClassGuid; public uint Flags; public IntPtr Reserved; }

    [StructLayout(LayoutKind.Sequential)]
    public struct DevInfoData { public uint Size; public Guid ClassGuid; public uint DevInst; public IntPtr Reserved; }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr clip, IntPtr data);

    [DllImport("user32.dll")]
    public static extern int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);

    [DllImport("user32.dll")]
    public static extern int QueryDisplayConfig(uint flags, ref uint pathCount, [Out] PathInfo[] paths,
        ref uint modeCount, [Out] ModeInfo[] modes, IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    public static extern int DisplayConfigGetDeviceInfo(ref SourceDeviceName request);

    [DllImport("user32.dll")]
    public static extern int DisplayConfigGetDeviceInfo(ref TargetDeviceName request);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool MessageBeep(uint type);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor, out uint count);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor, uint count, [Out] PhysicalMonitor[] monitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyPhysicalMonitors(uint count, [In] PhysicalMonitor[] monitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr physicalMonitor, byte code,
        out uint codeType, out uint currentValue, out uint maximumValue);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetVCPFeature(IntPtr physicalMonitor, byte code, uint value);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCapabilitiesStringLength(IntPtr physicalMonitor, out uint length);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CapabilitiesRequestAndCapabilitiesReply(IntPtr physicalMonitor, [Out] byte[] ascii, uint length);

    [DllImport("setupapi.dll", SetLastError = true, EntryPoint = "SetupDiGetClassDevsW")]
    public static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr parent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr devInfo, ref Guid classGuid, uint index,
        ref DeviceInterfaceData data);

    [DllImport("setupapi.dll", SetLastError = true, EntryPoint = "SetupDiGetDeviceInterfaceDetailW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref DeviceInterfaceData data, IntPtr detail,
        uint detailSize, out uint requiredSize, ref DevInfoData devInfo);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern IntPtr SetupDiOpenDevRegKey(IntPtr set, ref DevInfoData devInfo, uint scope, uint hwProfile,
        uint keyType, int samDesired);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
}

internal static class NativeLayout
{
    private static readonly Lazy<bool> Verified = new(() =>
    {
        var pointer = IntPtr.Size;
        Check<NativeMethods.PathInfo>(72);
        Check<NativeMethods.ModeInfo>(64);
        Check<NativeMethods.SourceDeviceName>(84);
        Check<NativeMethods.TargetDeviceName>(420);
        Check<NativeMethods.MonitorInfoEx>(104);
        Check<NativeMethods.PhysicalMonitor>(pointer + 256);
        Check<NativeMethods.DeviceInterfaceData>(pointer == 8 ? 32 : 28);
        Check<NativeMethods.DevInfoData>(pointer == 8 ? 32 : 28);
        return true;
    });

    /// <summary>Refuses to make native calls if any marshaled layout differs from the Windows SDK size.</summary>
    public static void Verify() => _ = Verified.Value;

    private static void Check<T>(int expected)
    {
        var actual = Marshal.SizeOf<T>();
        if (actual != expected)
            throw new InvalidOperationException($"{typeof(T).Name} marshals as {actual} bytes; expected {expected}.");
    }
}
