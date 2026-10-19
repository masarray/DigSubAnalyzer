using ProcessBus.Core.Models;
using ProcessBus.Iec61850.Raw.Analysis;
using Xunit;

namespace ProcessBus.Tests;

/// <summary>
/// Keeps amber network-integrity health distinct from the green "receiving packets"
/// badge while preventing a nominal-scale readout from inventing calibration.
/// </summary>
public sealed class OperatorPresentationReleaseTests
{
    private const int SamplesPerCycle = 80;

    private static int[] Sample(int index)
    {
        var phase = 2.0 * Math.PI * index / SamplesPerCycle;
        static int S(double x, double peak) => (int)Math.Round(Math.Sin(x) * peak);
        return
        [
            S(phase, 1000), S(phase - 2 * Math.PI / 3, 1000),
            S(phase + 2 * Math.PI / 3, 1200), 0,
            S(phase, 8160), S(phase - 2 * Math.PI / 3, 8160),
            S(phase + 2 * Math.PI / 3, 8160), 0
        ];
    }

    [Fact]
    public void StreamContinuityWarningIsNotMisrepresentedAsHealthyLive()
    {
        var analyzer = new RawProcessBusAnalyzer();
        for (var i = 0; i < SamplesPerCycle * 4; i++)
            analyzer.ObserveFrame(GoldenFrames.SvFrameWithChannelSamples((ushort)i, Sample(i)));
        var healthy = Assert.Single(analyzer.GetAnalyzerSnapshot().Streams);
        Assert.Equal("LIVE", healthy.DisplayStatusText);

        // A forward counter gap is an integrity warning, not loss of live traffic.
        analyzer.ObserveFrame(GoldenFrames.SvFrameWithChannelSamples(325, Sample(325)));
        var warned = Assert.Single(analyzer.GetAnalyzerSnapshot().Streams);
        Assert.True(warned.IsActive);
        Assert.Equal("LIVE · WARN", warned.DisplayStatusText);
        Assert.Equal("Live with warning", warned.StatusText);
        Assert.Equal("#F0B533", warned.StatusBrush);
    }

    [Fact]
    public void ChannelRmsAnglesRemainIndependentlyMeasuredAfterCounterWarning()
    {
        var analyzer = new RawProcessBusAnalyzer();
        for (var i = 0; i < SamplesPerCycle * 4; i++)
            analyzer.ObserveFrame(GoldenFrames.SvFrameWithChannelSamples((ushort)i, Sample(i)));
        var values = analyzer.GetAnalyzerSnapshot().AnalogValues;
        Assert.InRange(values.Ua.AngleDegrees!.Value, -0.1, 0.1);
        Assert.InRange(values.Ic.RmsValue!.Value, 840, 860);
        Assert.InRange(values.Ic.AngleDegrees!.Value, 119.8, 120.2);
    }
}
