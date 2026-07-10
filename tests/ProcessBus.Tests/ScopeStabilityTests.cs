using ProcessBus.Iec61850.Raw.Analysis;
using Xunit;

namespace ProcessBus.Tests;

/// <summary>
/// Regression suite for the SV scope's oscilloscope-style trigger. These tests encode the
/// exact failure modes that produced visible waveform flicker:
///  - window phase must not depend on WHEN the UI refreshes (cycle-anchored trigger),
///  - smpCnt rollover must not shift the window phase (monotonic unwrapping),
///  - the rendered trace must be the actual decoded samples (harmonics stay visible),
///  - snapshot refresh must recompute from the latest locked window without stretching partial windows.
/// </summary>
public class ScopeStabilityTests
{
    private const int SamplesPerCycle = 80;

    private static int[] ChannelValuesAt(int sampleIndex, Func<double, double> currentShape)
    {
        // Ia..In then Ua..Un ordering per the 4I4V instMag/q profile.
        var phase = 2.0 * Math.PI * (sampleIndex % SamplesPerCycle) / SamplesPerCycle;
        var current = (int)Math.Round(currentShape(phase) * 1000.0);
        var voltage = (int)Math.Round(Math.Sin(phase) * 8000.0);

        return new[]
        {
            current,                                     // Ia
            (int)Math.Round(currentShape(phase - (2.0 * Math.PI / 3.0)) * 1000.0), // Ib
            (int)Math.Round(currentShape(phase + (2.0 * Math.PI / 3.0)) * 1000.0), // Ic
            0,                                           // In
            voltage,                                     // Ua
            (int)Math.Round(Math.Sin(phase - (2.0 * Math.PI / 3.0)) * 8000.0),     // Ub
            (int)Math.Round(Math.Sin(phase + (2.0 * Math.PI / 3.0)) * 8000.0),     // Uc
            0                                            // Un
        };
    }

    private static RawProcessBusAnalyzer FeedSine(int frameCount, int startSmpCnt = 0, ushort wrapBase = 4000)
    {
        var analyzer = new RawProcessBusAnalyzer();
        FeedSine(analyzer, frameCount, startSmpCnt, wrapBase);
        return analyzer;
    }

    private static void FeedSine(RawProcessBusAnalyzer analyzer, int frameCount, int startSmpCnt, ushort wrapBase = 4000)
    {
        for (var i = 0; i < frameCount; i++)
        {
            var absoluteIndex = startSmpCnt + i;
            var smpCnt = (ushort)(absoluteIndex % wrapBase);
            var frame = GoldenFrames.SvFrameWithChannelSamples(smpCnt, ChannelValuesAt(absoluteIndex, Math.Sin));
            analyzer.ObserveFrame(frame);
        }
    }

    private static IReadOnlyList<double> UaSamples(RawProcessBusAnalyzer analyzer)
    {
        var waveform = analyzer.GetAnalyzerSnapshot().Waveform;
        var ua = waveform.VoltageSeries.FirstOrDefault(series =>
            string.Equals(series.Name, "Ua", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(ua);
        return ua!.Samples;
    }

    [Fact]
    public void Scope_WindowIsPhaseLocked_AcrossWholeCycleAdvances()
    {
        var analyzer = FeedSine(SamplesPerCycle * 10);
        var first = UaSamples(analyzer).ToArray();

        // Advance the stream by exactly one whole cycle: a triggered scope must render the
        // same phase framing, so sample[0] of the window may not move.
        FeedSine(analyzer, SamplesPerCycle, startSmpCnt: SamplesPerCycle * 10);
        var second = UaSamples(analyzer).ToArray();

        Assert.Equal(first.Length, second.Length);
        Assert.True(Math.Abs(first[0] - second[0]) <= 1.0,
            $"Window phase moved between refreshes: first[0]={first[0]}, second[0]={second[0]}");
    }

    [Fact]
    public void Scope_SmpCntRollover_DoesNotTearTheTrace()
    {
        // Straddle the 4000 rollover (50 Hz / 80 spc publishers wrap smpCnt at the sample
        // rate). The previous modulo-window implementation shifted phase by 160 slots here,
        // producing a hard flicker once per second.
        var analyzer = FeedSine(SamplesPerCycle * 8, startSmpCnt: 4000 - (SamplesPerCycle * 4));
        var samples = UaSamples(analyzer);

        // A continuous 8000-peak sine sampled at 80 spc cannot move more than
        // peak * 2*pi/80 per sample; anything above that is a stitching discontinuity.
        var maxTheoreticalStep = 8000.0 * (2.0 * Math.PI / SamplesPerCycle) * 1.25;
        for (var i = 1; i < samples.Count; i++)
        {
            var step = Math.Abs(samples[i] - samples[i - 1]);
            Assert.True(step <= maxTheoreticalStep,
                $"Discontinuity at sample {i}: step={step:0.#} exceeds {maxTheoreticalStep:0.#}");
        }
    }

    [Fact]
    public void Scope_RendersActualSamples_HarmonicDistortionStaysVisible()
    {
        // Flat-topped current (clipped at 60% of peak) must render as a plateau, not be
        // silently re-synthesized into a clean sine from RMS + angle.
        static double Clipped(double phase) => Math.Clamp(Math.Sin(phase), -0.6, 0.6);

        var analyzer = new RawProcessBusAnalyzer();
        for (var i = 0; i < SamplesPerCycle * 10; i++)
        {
            var frame = GoldenFrames.SvFrameWithChannelSamples(
                (ushort)(i % 4000),
                ChannelValuesAt(i, Clipped));
            analyzer.ObserveFrame(frame);
        }

        var waveform = analyzer.GetAnalyzerSnapshot().Waveform;
        var ia = waveform.CurrentSeries.FirstOrDefault(series =>
            string.Equals(series.Name, "Ia", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(ia);

        var samples = ia!.Samples;
        var max = samples.Max();
        Assert.InRange(max, 550.0, 650.0); // clip level 600, never the sine peak 1000

        var plateauSamples = samples.Count(sample => Math.Abs(sample - max) <= max * 0.02);
        Assert.True(plateauSamples >= samples.Count / 10,
            $"Expected a flat-top plateau; only {plateauSamples} of {samples.Count} samples near max.");
    }


    [Fact]
    public void Scope_HarmonicShapeChange_UpdatesWithoutSelectionChange()
    {
        static double Clipped(double phase) => Math.Clamp(Math.Sin(phase), -0.6, 0.6);

        var analyzer = FeedSine(SamplesPerCycle * 10);
        var sineWaveform = analyzer.GetAnalyzerSnapshot().Waveform;
        var sineIa = sineWaveform.CurrentSeries.First(series =>
            string.Equals(series.Name, "Ia", StringComparison.OrdinalIgnoreCase));
        Assert.True(sineIa.Samples.Max() > 900.0);

        for (var i = 0; i < SamplesPerCycle * 2; i++)
        {
            var absoluteIndex = (SamplesPerCycle * 10) + i;
            var frame = GoldenFrames.SvFrameWithChannelSamples(
                (ushort)(absoluteIndex % 4000),
                ChannelValuesAt(absoluteIndex, Clipped));
            analyzer.ObserveFrame(frame);
        }

        var clippedWaveform = analyzer.GetAnalyzerSnapshot().Waveform;
        var clippedIa = clippedWaveform.CurrentSeries.First(series =>
            string.Equals(series.Name, "Ia", StringComparison.OrdinalIgnoreCase));

        Assert.InRange(clippedIa.Samples.Max(), 550.0, 650.0);
        Assert.True(clippedWaveform.HasShapeWarning);
    }

    [Fact]
    public void Scope_RefreshWithoutNewSamples_KeepsSameWaveformValues()
    {
        var analyzer = FeedSine(SamplesPerCycle * 10);

        var first = analyzer.GetAnalyzerSnapshot().Waveform;
        var second = analyzer.GetAnalyzerSnapshot().Waveform;

        // ARSVIN recomputes a snapshot on every refresh, but with no new SV samples the
        // plotted values must remain identical. Do not require object identity; that cache
        // made harmonic changes appear late in DigSubAnalyzer.
        Assert.Equal(first.VoltageSeries.First().Samples, second.VoltageSeries.First().Samples);
        Assert.Equal(first.CurrentSeries.First().Samples, second.CurrentSeries.First().Samples);
    }

    [Fact]
    public void Scope_PartialCycleUpdate_KeepsFixedWindowLength()
    {
        var analyzer = FeedSine(SamplesPerCycle * 10);
        var first = analyzer.GetAnalyzerSnapshot().Waveform;

        FeedSine(analyzer, SamplesPerCycle / 2, startSmpCnt: SamplesPerCycle * 10);
        var second = analyzer.GetAnalyzerSnapshot().Waveform;

        Assert.Equal(first.VoltageSeries.First().Samples.Count, second.VoltageSeries.First().Samples.Count);
        Assert.Equal(SamplesPerCycle * 2, second.VoltageSeries.First().Samples.Count);
        Assert.NotEmpty(second.CurrentSeries);
    }
    [Fact]
    public void Scope_DoesNotPublishSlidingPartialWindow_WhileStarting()
    {
        // Fewer than the requested two-cycle locked scope window used to publish a moving
        // tail, which looked like the waveform was stretching/shrinking at start-up.
        var analyzer = FeedSine((SamplesPerCycle * 2) - 1);
        var waveform = analyzer.GetAnalyzerSnapshot().Waveform;

        Assert.Empty(waveform.VoltageSeries);
        Assert.Empty(waveform.CurrentSeries);
        Assert.Contains("complete coherent", waveform.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Scope_CounterReset_HoldsLastPublishedWindow_UntilNewWindowIsComplete()
    {
        var analyzer = FeedSine(SamplesPerCycle * 8);
        var stable = analyzer.GetAnalyzerSnapshot().Waveform;
        Assert.NotEmpty(stable.VoltageSeries);

        // A large discontinuity clears acquisition, but the display must hold the last
        // complete window instead of stitching old and new states or publishing a sliding tail.
        FeedSine(analyzer, SamplesPerCycle, startSmpCnt: 30000, wrapBase: 4000);
        var held = analyzer.GetAnalyzerSnapshot().Waveform;
        Assert.Same(stable, held);

        FeedSine(analyzer, SamplesPerCycle * 2, startSmpCnt: 30000 + SamplesPerCycle, wrapBase: 4000);
        var renewed = analyzer.GetAnalyzerSnapshot().Waveform;
        Assert.NotSame(stable, renewed);
        Assert.NotEmpty(renewed.VoltageSeries);
    }

}
