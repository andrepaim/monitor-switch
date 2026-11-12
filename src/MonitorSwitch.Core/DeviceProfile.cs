namespace MonitorSwitch.Core;

public static class DeviceProfile
{
    public const byte VcpCode = 0x60;
    public const string Manufacturer = "SAM";
    public const string ProductId = "SAM1017";

    public static bool Allows(uint input) => input is 5 or 6;

    public static uint OtherInput(uint current) => current switch
    {
        5 => 6,
        6 => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(current), current, "Expected input 5 or 6.")
    };
}
