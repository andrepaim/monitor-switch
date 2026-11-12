using MonitorSwitch.Core;

namespace MonitorSwitch.Tests;

internal static class TestData
{
    public static readonly string Hash = new('a', 64);
    public const string DevicePath = @"\\?\DISPLAY#SAM1017#5&test&0&UID1#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";

    public static AppConfig Config(int cooldownMs = 1500, int readAttempts = 3, int deadlineMs = 8000) => new(1,
        new("SAM", "SAM1017", "LU28R55", DevicePath, Hash, true),
        new(96, [5, 6], new() { ["5"] = "Input 5", ["6"] = "Input 6" }),
        new(cooldownMs, readAttempts, deadlineMs),
        new(false, true));

    public static MonitorIdentity Identity => Config().Identity;

    public static MonitorCandidate Candidate(string? hash = null, string path = DevicePath, string productId = "SAM1017",
        int physical = 1, int targets = 1) =>
        new(@"\\.\DISPLAY1", path, "LU28R55", "SAM", productId, hash ?? Hash, physical, targets);

    /// <summary>A valid single-block EDID for manufacturer SAM, product code 0x1017.</summary>
    public static byte[] SamsungEdid(byte extensions = 0, byte serial = 1)
    {
        var edid = new byte[128 * (1 + extensions)];
        byte[] header = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];
        header.CopyTo(edid, 0);
        // "SAM": S=19, A=1, M=13 packed as 0 10011 00001 01101.
        var packed = (19 << 10) | (1 << 5) | 13;
        edid[8] = (byte)(packed >> 8);
        edid[9] = (byte)packed;
        edid[10] = 0x17;
        edid[11] = 0x10;
        edid[12] = serial;
        edid[126] = extensions;
        for (var block = 0; block <= extensions; block++) FixChecksum(edid, block);
        return edid;
    }

    public static void FixChecksum(byte[] edid, int block)
    {
        var offset = block * 128;
        var sum = 0;
        for (var i = offset; i < offset + 127; i++) sum += edid[i];
        edid[offset + 127] = (byte)((256 - sum % 256) % 256);
    }
}
