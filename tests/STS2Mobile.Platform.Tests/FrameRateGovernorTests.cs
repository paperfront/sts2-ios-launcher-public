namespace STS2Mobile.Platform.Tests;

public class FrameRateGovernorTests
{
    private static readonly PhoneGraphicsSettings Off = PhoneGraphicsSettings.Default.WithPreset(BatteryPreset.Off);
    private static readonly PhoneGraphicsSettings Idle = Off with { LowerIdleFrameRate = true };
    private static readonly PhoneGraphicsSettings Cool = Off with { CoolDownWhenHot = true };

    [Fact]
    public void WithoutChoicesTheGameSettingStands() =>
        Assert.Equal(60, FrameRateGovernor.Target(60, Off, false, 100, DevicePower.Critical));

    [Fact]
    public void UnlimitedGameSettingStaysUnlimited() =>
        Assert.Equal(0, FrameRateGovernor.Target(0, Off, false, 100, DevicePower.Nominal));

    [Fact]
    public void TouchingOrARecentTouchKeepsTheFullRate()
    {
        Assert.Equal(60, FrameRateGovernor.Target(60, Idle, true, 100, DevicePower.Nominal));
        Assert.Equal(60, FrameRateGovernor.Target(60, Idle, false, 1.9, DevicePower.Nominal));
    }

    [Fact]
    public void IdleLowersTo30()
    {
        Assert.Equal(30, FrameRateGovernor.Target(60, Idle, false, 2, DevicePower.Nominal));
        Assert.Equal(30, FrameRateGovernor.Target(0, Idle, false, 5, DevicePower.Nominal));
    }

    [Fact]
    public void CapNeverRaisesTheGameSetting() =>
        Assert.Equal(24, FrameRateGovernor.Target(24, Idle, false, 5, DevicePower.Nominal));

    [Theory]
    [InlineData(DevicePower.Fair, 60)]
    [InlineData(DevicePower.Serious, 30)]
    [InlineData(DevicePower.Critical, 20)]
    [InlineData(DevicePower.UnknownThermalState, 60)]
    public void HeatLowersTheCapByThermalState(int thermalState, int expected) =>
        Assert.Equal(expected, FrameRateGovernor.Target(60, Cool, true, 0, thermalState));

    [Theory]
    [InlineData(0, 60, 60)]
    [InlineData(144, 60, 60)]
    [InlineData(144, 120, 120)]
    [InlineData(24, 120, 24)]
    [InlineData(0, -1, 0)]
    public void DisplayCapBoundsOnlyFramesTheScreenCannotPresent(int gameLimit, int displayLimit, int expected) =>
        Assert.Equal(expected, FrameRateGovernor.Target(gameLimit, Off, true, 0, DevicePower.Nominal, displayLimit));

    [Fact]
    public void IdleAndHeatNeverRaiseASlowerDisplayCap()
    {
        Assert.Equal(20, FrameRateGovernor.Target(120, Idle, false, 10, DevicePower.Nominal, 20));
        Assert.Equal(15, FrameRateGovernor.Target(120, Cool, true, 0, DevicePower.Critical, 15));
    }
}
