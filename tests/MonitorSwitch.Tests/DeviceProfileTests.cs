using MonitorSwitch.Core;
using Xunit;

namespace MonitorSwitch.Tests;

public sealed class DeviceProfileTests
{
    [Theory]
    [InlineData(5u, 6u)]
    [InlineData(6u, 5u)]
    public void ToggleUsesDemonstratedValues(uint current, uint target) =>
        Assert.Equal(target, DeviceProfile.OtherInput(current));

    [Theory]
    [InlineData(0u)]
    [InlineData(3u)]
    [InlineData(17u)]
    [InlineData(18u)]
    public void UnexpectedInputCannotProduceATarget(uint current)
    {
        Assert.False(DeviceProfile.Allows(current));
        Assert.Throws<ArgumentOutOfRangeException>(() => DeviceProfile.OtherInput(current));
    }

    [Fact]
    public void FeatureIsHex60NotDecimal60() => Assert.Equal(96, (int)DeviceProfile.VcpCode);
}
