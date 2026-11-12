using System.Security.Cryptography;

namespace MonitorSwitch.Core;

public sealed record EdidInfo(string Manufacturer, string ProductId, string IdentityHash);

public static class Edid
{
    private const int BlockSize = 128;
    private static readonly byte[] Header = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];

    public static bool IsIdentityHash(string value) =>
        value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    /// <summary>
    /// Validates the complete raw EDID (base block plus every advertised extension, each checksummed)
    /// and hashes all of it, unnormalized, as lowercase SHA-256 hex. Returns null if anything is invalid.
    /// </summary>
    public static EdidInfo? TryParse(byte[]? raw)
    {
        if (raw is null || raw.Length < BlockSize || raw.Length % BlockSize != 0) return null;
        if (!raw.AsSpan(0, Header.Length).SequenceEqual(Header)) return null;
        if (raw.Length != BlockSize * (1 + raw[126])) return null;
        for (var offset = 0; offset < raw.Length; offset += BlockSize)
        {
            var sum = 0;
            foreach (var b in raw.AsSpan(offset, BlockSize)) sum += b;
            if (sum % 256 != 0) return null;
        }

        var packed = (raw[8] << 8) | raw[9];
        Span<char> letters = stackalloc char[3];
        for (var i = 0; i < 3; i++)
        {
            var code = (packed >> (10 - 5 * i)) & 0x1F;
            if (code is < 1 or > 26) return null;
            letters[i] = (char)('A' + code - 1);
        }
        var manufacturer = new string(letters);
        var product = raw[10] | (raw[11] << 8);
        return new(manufacturer, manufacturer + product.ToString("X4"), Convert.ToHexStringLower(SHA256.HashData(raw)));
    }
}
