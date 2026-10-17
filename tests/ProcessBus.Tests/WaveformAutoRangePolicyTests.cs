using ProcessBus.Core.Models;
using Xunit;

namespace ProcessBus.Tests;

public sealed class WaveformAutoRangePolicyTests
{
    [Theory]
    [InlineData("counts", "V", true)]
    [InlineData("counts", "A", true)]
    [InlineData("V", "V", false)]
    [InlineData("A", "A", false)]
    [InlineData(null, "V", true)]
    public void ChangingDisplayUnitReleasesOldScale(string? prior, string? current, bool expected)
    {
        Assert.Equal(expected, WaveformAutoRangePolicy.UnitsChanged(prior, current));
    }

    [Fact]
    public void RawCountToNominalVoltAndAmpResetImmediately()
    {
        Assert.True(WaveformAutoRangePolicy.ShouldReleasePeakHold(104500, 104.5));
        Assert.True(WaveformAutoRangePolicy.ShouldReleasePeakHold(1810, 1.81));
    }

    [Fact]
    public void NormalPeakHoldHoldsSmallFluctuationsAndReleasesTrueAmplitudeChange()
    {
        Assert.False(WaveformAutoRangePolicy.ShouldReleasePeakHold(104, 102));
        Assert.False(WaveformAutoRangePolicy.ShouldReleasePeakHold(104, 50));
        Assert.True(WaveformAutoRangePolicy.ShouldReleasePeakHold(104, 25));
        Assert.True(WaveformAutoRangePolicy.ShouldReleasePeakHold(104, 115));
        Assert.False(WaveformAutoRangePolicy.ShouldReleasePeakHold(104, double.NaN));
    }
}
