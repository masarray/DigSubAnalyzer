using ProcessBus.Core.Models;
using ProcessBus.Iec61850.Raw.Analysis;
using Xunit;

namespace ProcessBus.Tests;

/// <summary>
/// Captured wire values define electrical phase. Never make Ic equal Uc or force a
/// convenient reference angle merely because the sample belongs to phase C.
/// </summary>
public sealed class LiveCurrentPhasorRegressionTests
{
    private const int PerCycle = 80;

    private static int Sample(int n, double degrees, double amplitude) =>
        (int)Math.Round(amplitude * Math.Sin(2 * Math.PI * (n % PerCycle) / PerCycle +
                                              degrees * Math.PI / 180.0));

    private static void Feed(RawProcessBusAnalyzer analyzer, int start, int count,
                             double icDegrees, bool zeroIc = false)
    {
        for (var n = start; n < start + count; n++)
        {
            var samples = new[]
            {
                Sample(n, -30, 1414.0),  // Ia
                Sample(n, -150, 1414.0), // Ib
                zeroIc ? 0 : Sample(n, icDegrees, 1414.0), // Ic is independently driven
                0,                       // In
                Sample(n, 0, 8165.0),   // Ua
                Sample(n, -120, 8165.0),// Ub
                Sample(n, 120, 8165.0), // Uc
                0                        // Un
            };
            analyzer.ObserveFrame(GoldenFrames.SvFrameWithChannelSamples(
                (ushort)(n % 4000), samples, "ARSTACK_SV01", 0x4000));
        }
    }

    private static void Near(double? value, double expected)
    {
        Assert.True(value.HasValue, "Expected a decoded sample-derived phasor.");
        var delta = (value.Value - expected + 540.0) % 360.0 - 180.0;
        Assert.InRange(delta, -1.25, 1.25);
    }

    [Fact]
    public void FourI4V_IndependentCurrentCIsPlus90WhileVoltageCIsPlus120()
    {
        var analyzer = new RawProcessBusAnalyzer();
        Feed(analyzer, 0, 560, 90.0);
        var snap = analyzer.GetAnalyzerSnapshot();
        var id = Assert.IsType<string>(snap.SelectedStreamId);
        analyzer.SetSvEngineeringScale(id, SvEngineeringScale.Nominal4I4V);
        snap = analyzer.GetAnalyzerSnapshot();

        Near(snap.AnalogValues.Ua.AngleDegrees, 0);
        Near(snap.AnalogValues.Ub.AngleDegrees, -120);
        Near(snap.AnalogValues.Uc.AngleDegrees, 120);
        Near(snap.AnalogValues.Ia.AngleDegrees, -30);
        Near(snap.AnalogValues.Ib.AngleDegrees, -150);
        Near(snap.AnalogValues.Ic.AngleDegrees, 90);
        Assert.Equal("A", snap.AnalogValues.Ic.Unit);
        Assert.NotEqual(snap.AnalogValues.Ic.AngleDegrees, snap.AnalogValues.Uc.AngleDegrees);
    }

    [Fact]
    public void CurrentCChangesInLiveWireSamples_NotInFixedPhaseTemplate()
    {
        var analyzer = new RawProcessBusAnalyzer();
        Feed(analyzer, 0, 480, 90);
        Near(analyzer.GetAnalyzerSnapshot().AnalogValues.Ic.AngleDegrees, 90);
        Feed(analyzer, 480, 480, 45);
        var changed = analyzer.GetAnalyzerSnapshot();
        Near(changed.AnalogValues.Ic.AngleDegrees, 45);
        Near(changed.AnalogValues.Uc.AngleDegrees, 120);
    }

    [Fact]
    public void ZeroMagnitudeIcNeverFabricatesA90DegreeVector()
    {
        var analyzer = new RawProcessBusAnalyzer();
        Feed(analyzer, 0, 480, 90, zeroIc: true);
        var snap = analyzer.GetAnalyzerSnapshot();
        Assert.Null(snap.AnalogValues.Ic.AngleDegrees);
        Near(snap.AnalogValues.Uc.AngleDegrees, 120);
    }

    [Fact]
    public void MissingVoltageOrCurrentDoesNotUseFixedPhaseFallback()
    {
        var analog = new AnalogValuesSnapshot();
        Assert.Null(analog.Ic.AngleDegrees);
        Assert.Null(analog.Uc.AngleDegrees);
    }
}
