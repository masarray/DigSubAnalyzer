using ProcessBus.Core.Models;
using Xunit;

namespace ProcessBus.Tests;

public sealed class OperatorReadoutContractTests
{
    [Fact]
    public void RawCountsRemainUncalibratedAndInSeparateUnitColumn()
    {
        var voltage = new ChannelValueModel("Ua")
        {
            Unit = "counts", RmsValue = 5773.162, AngleDegrees = -119.995
        };
        Assert.Equal("5773.16", voltage.RmsNumberText);
        Assert.Equal("counts", voltage.RmsUnitText);
        Assert.Equal("-120.00°", voltage.AngleNumberText);
        Assert.Equal("5773.16 counts", voltage.RmsDisplayText);
    }

    [Fact]
    public void ScalingUsesActualEngineeringUnitAndKeepsMagnitudeConsistent()
    {
        var voltage = new ChannelValueModel("Ua") { Unit = "V", RmsValue = 11000 };
        var current = new ChannelValueModel("Ia") { Unit = "A", RmsValue = 999.35 };
        Assert.Equal("11.00", voltage.RmsNumberText);
        Assert.Equal("kV", voltage.RmsUnitText);
        Assert.Equal("11.00 kV", voltage.RmsDisplayText);
        Assert.Equal("999.35", current.RmsNumberText);
        Assert.Equal("A", current.RmsUnitText);
    }

    [Fact]
    public void UnknownOrNonFiniteValuesNeverClaimPhysicalReadings()
    {
        var unknown = new ChannelValueModel("Ua") { RmsValue = 200, AngleDegrees = double.NaN };
        var invalid = new ChannelValueModel("Ia") { Unit = "A", RmsValue = double.PositiveInfinity };
        Assert.Equal("—", unknown.RmsUnitText);
        Assert.Equal("200.00", unknown.RmsNumberText);
        Assert.Equal("N/A", unknown.AngleNumberText);
        Assert.Equal("N/A", invalid.RmsNumberText);
        Assert.Equal("—", invalid.RmsUnitText);
        Assert.Equal("N/A", invalid.RmsDisplayText);
    }

    [Fact]
    public void DiagnosticRowSummarizesButNeverLosesOriginalEvidence()
    {
        var detail = "SV arrival timing excursions >= 300 us: 4 event(s); latest: svID=AR_DIAG_SV1; APPID=0x4F01";
        var row = new DiagnosticEventItem { Message = detail, Severity = "Warning" };
        Assert.Equal("SV arrival timing excursions >= 300 us", row.FindingText);
        Assert.Equal("AR_DIAG_SV1", row.SourceText);
        Assert.Equal(detail, row.Message);
        Assert.Equal("Capture", new DiagnosticEventItem { Message = "PTP observed" }.SourceText);
    }
}
