using ProcessBus.Core.Models;
using Xunit;

namespace ProcessBus.Tests;

public sealed class SvNominalProfilePolicyTests
{
    [Fact]
    public void IdleUiPresetIsNominalButCannotInventMeasurementEvidence()
    {
        Assert.Equal(SvEngineeringScale.Nominal4I4V, SvEngineeringScaleFactors.IdlePresentationDefault);
        var noSamples = new AnalogValuesSnapshot();
        Assert.Null(noSamples.Ua.RmsValue);
        Assert.Null(noSamples.Ia.RmsValue);
        Assert.Null(noSamples.Ic.AngleDegrees);
    }

    [Theory]
    [InlineData("Raw 4I4V instMag/q candidate profile", true)]
    [InlineData("OMICRON raw 4I4V instMag/q profile / OMICRON_CMC_SV1", true)]
    [InlineData("Raw SV payload evidence", false)]
    [InlineData("SCL generic 16 elements of uncertain layout", false)]
    [InlineData(null, false)]
    public void NominalAssumptionRequiresRecognizedDecodedLayout(string? mapping, bool eligible)
    {
        Assert.Equal(eligible, SvEngineeringScaleFactors.IsRecognizedNominalCandidate(mapping));
    }

    [Fact]
    public void ElectricalScalingRemainsAnAssumptionNotAPhasorAngleTransformation()
    {
        Assert.Equal(0.01, SvEngineeringScaleFactors.Factor(SvEngineeringScale.Nominal4I4V, "Ua"));
        Assert.Equal(0.001, SvEngineeringScaleFactors.Factor(SvEngineeringScale.Nominal4I4V, "Ic"));
        Assert.Equal("counts", SvEngineeringScaleFactors.Unit(SvEngineeringScale.RawCounts, "Ic"));
    }
}
