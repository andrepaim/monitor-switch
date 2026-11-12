using Microsoft.Win32;
using MonitorSwitch.Core;

namespace MonitorSwitch.Windows;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    // Windows increments BootId on every boot. If unavailable, an unknown boot never matches a stamp,
    // so cooldown is skipped rather than extended indefinitely.
    public string BootIdentity { get; } = Registry.GetValue(
        @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters",
        "BootId", null) is int bootId ? $"bootid:{bootId}" : $"unknown:{Guid.NewGuid():N}";

    // GetTickCount64: monotonic since boot, unaffected by wall-clock corrections.
    public long MonotonicMilliseconds => Environment.TickCount64;

    public void Delay(TimeSpan duration) => Thread.Sleep(duration);
}

public static class Feedback
{
    // MessageBeep plays asynchronously and never activates a window.
    public static void Error() => NativeMethods.MessageBeep(NativeMethods.MbIconHand);
    public static void Success() => NativeMethods.MessageBeep(0);
}
