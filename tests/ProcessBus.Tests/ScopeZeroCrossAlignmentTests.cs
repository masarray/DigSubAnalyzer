using ProcessBus.Iec61850.Raw.Analysis;
using Xunit;

namespace ProcessBus.Tests;

public sealed class ScopeZeroCrossAlignmentTests
{
    private const int SamplesPerCycle = 80;

    private static int Sample(int index, double phaseDegrees, double magnitude = 8000) =>
        (int)Math.Round(magnitude * Math.Sin(2 * Math.PI * (index % SamplesPerCycle) / SamplesPerCycle +
                                              phaseDegrees * Math.PI / 180));

    private static int[] Channels(int index) =>
    [
        Sample(index, -30, 1400), Sample(index, -150, 1400),
        Sample(index, 90, 1400), 0,
        Sample(index, 0), Sample(index, -120),
        Sample(index, 120), 0
    ];

    [Fact]
    public void RealTwoCycleWindowBeginsNearRisingUaZeroRatherThanArbitraryCaptureOffset()
    {
        var analyzer = new RawProcessBusAnalyzer();
        for (var n = 17; n < 17 + 720; n++)
            analyzer.ObserveFrame(GoldenFrames.SvFrameWithChannelSamples(
                (ushort)(n % 4000), Channels(n)));

        var snapshot = analyzer.GetAnalyzerSnapshot();
        var ua = snapshot.Waveform.VoltageSeries.Single(x => x.Name == "Ua").Samples;
        var ub = snapshot.Waveform.VoltageSeries.Single(x => x.Name == "Ub").Samples;
        var uc = snapshot.Waveform.VoltageSeries.Single(x => x.Name == "Uc").Samples;
        var ia = snapshot.Waveform.CurrentSeries.Single(x => x.Name == "Ia").Samples;
        Assert.Equal(160, ua.Count);
        Assert.InRange(ua[0], 0, 650); // actual captured sample, <= one 4.5° step
        Assert.True(ua[1] > ua[0], "Ua must rise through zero");
        Assert.InRange(ub[0], -7600, -6500);
        Assert.InRange(uc[0], 6500, 7600);
        Assert.InRange(ia[0], -810, -600);
        Assert.Equal(ua[0], ua[80]);
        Assert.Equal(ub[0], ub[80]);
    }

    [Fact]
    public void MissingUaCannotCreateAnArtificialZeroCrossing()
    {
        var analyzer = new RawProcessBusAnalyzer();
        for (var n = 0; n < 700; n++)
        {
            var samples = Channels(n);
            samples[4] = 0;
            analyzer.ObserveFrame(GoldenFrames.SvFrameWithChannelSamples(
                (ushort)n, samples));
        }
        var snapshot = analyzer.GetAnalyzerSnapshot();
        var ua = snapshot.Waveform.VoltageSeries.Single(x => x.Name == "Ua").Samples;
        Assert.All(ua, point => Assert.Equal(0, point));
    }

    [Fact]
    public void PhaseAlignedPresentationDoesNotInventFixedElectricalAngles()
    {
        var analyzer = new RawProcessBusAnalyzer();
        for (var n = 17; n < 737; n++)
            analyzer.ObserveFrame(GoldenFrames.SvFrameWithChannelSamples(
                (ushort)n, Channels(n)));

        var snapshot = analyzer.GetAnalyzerSnapshot();
        Assert.NotNull(snapshot.AnalogValues.Ic.AngleDegrees);
        Assert.InRange(snapshot.AnalogValues.Ic.AngleDegrees!.Value, 89, 91);
        Assert.InRange(snapshot.AnalogValues.Uc.AngleDegrees!.Value, 119, 121);
    }
}
