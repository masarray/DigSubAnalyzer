using ProcessBus.Iec61850.Raw.Analysis;
using ProcessBus.Iec61850.Raw.Sv;
using Xunit;

namespace ProcessBus.Tests;

public class FourI4VQualityTests
{
    [Theory]
    [InlineData(0u, FourI4VQuality.MeasurementState.Good)]
    [InlineData(1u, FourI4VQuality.MeasurementState.Untrusted)]
    [InlineData(3u, FourI4VQuality.MeasurementState.Untrusted)]
    [InlineData(1u << 6, FourI4VQuality.MeasurementState.Untrusted)]
    [InlineData(1u << 11, FourI4VQuality.MeasurementState.Test)]
    [InlineData(1u << 12, FourI4VQuality.MeasurementState.OperatorBlocked)]
    public void QualityInterpretationMatchesArstackWireFlags(
        uint quality, FourI4VQuality.MeasurementState state)
    {
        var payload = new byte[64];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4, 4), quality);
        Assert.Equal(state, FourI4VQuality.Assess(payload));
        Assert.Equal(FourI4VQuality.MeasurementState.UnknownLayout,
            FourI4VQuality.Assess(new byte[60]));
    }

    [Fact]
    public void BadQualityInvalidatesMeasurementsUntilNewCoherentWindow()
    {
        var analyzer = new RawProcessBusAnalyzer();
        var values = new[] { 1000, -500, -500, 0, 8000, -4000, -4000, 0 };
        for (var i = 0; i < 400; i++)
            analyzer.ObserveFrame(GoldenFrames.SvFrameWithChannelSamples((ushort)i, values));

        var before = analyzer.GetAnalyzerSnapshot();
        Assert.NotNull(before.AnalogValues.Ia.RmsValue);
        Assert.NotEmpty(before.Waveform.VoltageSeries);

        analyzer.ObserveFrame(GoldenFrames.SvFrameWithChannelSamples(400, values, qualityWord: 1));
        var invalid = analyzer.GetAnalyzerSnapshot();
        Assert.Contains("invalid", invalid.Diagnostics.ValidityText, StringComparison.OrdinalIgnoreCase);
        Assert.Null(invalid.AnalogValues.Ia.RmsValue);
        Assert.Empty(invalid.Waveform.VoltageSeries);

        // Do not cross-stitch the pre-failure window to the new valid samples.
        for (var i = 401; i < 1100; i++)
            analyzer.ObserveFrame(GoldenFrames.SvFrameWithChannelSamples((ushort)i, values));
        var recovered = analyzer.GetAnalyzerSnapshot();
        Assert.Equal("4I+4V quality good", recovered.Diagnostics.ValidityText);
        Assert.NotNull(recovered.AnalogValues.Ia.RmsValue);
        Assert.NotEmpty(recovered.Waveform.VoltageSeries);
    }

    [Fact]
    public void TestStimulusRemainsVisibleButMustBeExplicitlyLabeled()
    {
        var analyzer = new RawProcessBusAnalyzer();
        var values = new[] { 1000, -500, -500, 0, 8000, -4000, -4000, 0 };
        for (var i = 0; i < 400; i++)
            analyzer.ObserveFrame(GoldenFrames.SvFrameWithChannelSamples((ushort)i, values, qualityWord: 1u << 11));
        var snap = analyzer.GetAnalyzerSnapshot();
        Assert.Contains("TEST", snap.Diagnostics.ValidityText);
        Assert.NotNull(snap.AnalogValues.Ia.RmsValue);
    }
}
