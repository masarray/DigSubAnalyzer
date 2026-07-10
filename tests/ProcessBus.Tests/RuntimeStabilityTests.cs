using ProcessBus.Core.Models;
using ProcessBus.Iec61850.Raw.Analysis;
using Xunit;

namespace ProcessBus.Tests;

/// <summary>
/// Deterministic runtime-stability evidence for the v1.3.0-beta.2 stabilization gate.
/// These tests exercise the public analyzer through the same ObserveFrame/GetAnalyzerSnapshot
/// surface used by live capture, without requiring Npcap or customer traffic.
/// </summary>
public sealed class RuntimeStabilityTests
{
    private const int SamplesPerCycle = 80;

    private static int[] ChannelValuesAt(int sampleIndex, int currentPeak)
    {
        var phase = 2.0 * Math.PI * (sampleIndex % SamplesPerCycle) / SamplesPerCycle;
        var voltagePeak = currentPeak * 8;

        return
        [
            (int)Math.Round(Math.Sin(phase) * currentPeak),
            (int)Math.Round(Math.Sin(phase - (2.0 * Math.PI / 3.0)) * currentPeak),
            (int)Math.Round(Math.Sin(phase + (2.0 * Math.PI / 3.0)) * currentPeak),
            0,
            (int)Math.Round(Math.Sin(phase) * voltagePeak),
            (int)Math.Round(Math.Sin(phase - (2.0 * Math.PI / 3.0)) * voltagePeak),
            (int)Math.Round(Math.Sin(phase + (2.0 * Math.PI / 3.0)) * voltagePeak),
            0
        ];
    }

    private static void FeedStream(
        RawProcessBusAnalyzer analyzer,
        string svId,
        ushort appId,
        int currentPeak,
        int frameCount,
        int startSample = 0,
        int wrapBase = 4000)
    {
        for (var index = 0; index < frameCount; index++)
        {
            var absoluteSample = startSample + index;
            var smpCnt = (ushort)(absoluteSample % wrapBase);
            analyzer.ObserveFrame(GoldenFrames.SvFrameWithChannelSamples(
                smpCnt,
                ChannelValuesAt(absoluteSample, currentPeak),
                svId,
                appId));
        }
    }

    private static AnalyzerSelection SelectStream(RawProcessBusAnalyzer analyzer, string svId)
    {
        var discovered = analyzer.GetAnalyzerSnapshot().Streams.Single(stream =>
            string.Equals(stream.SvId, svId, StringComparison.OrdinalIgnoreCase));

        analyzer.SelectStream(discovered.StreamId);
        return new AnalyzerSelection(discovered.StreamId, analyzer.GetAnalyzerSnapshot());
    }

    private static IReadOnlyList<double> IaSamples(AnalyzerSelection selection)
    {
        var series = selection.Snapshot.Waveform.CurrentSeries.Single(item =>
            string.Equals(item.Name, "Ia", StringComparison.OrdinalIgnoreCase));

        return series.Samples;
    }

    [Fact]
    [Trait("Category", "RuntimeStability")]
    public void EightInterleavedStreams_RemainStrictlyIsolated()
    {
        var analyzer = new RawProcessBusAnalyzer();
        const int streamCount = 8;
        const int framesPerStream = SamplesPerCycle * 8;

        for (var sample = 0; sample < framesPerStream; sample++)
        {
            for (var streamIndex = 0; streamIndex < streamCount; streamIndex++)
            {
                var svId = $"MU_{streamIndex + 1:00}";
                var appId = (ushort)(0x4000 + streamIndex);
                var currentPeak = 500 * (streamIndex + 1);

                analyzer.ObserveFrame(GoldenFrames.SvFrameWithChannelSamples(
                    (ushort)(sample % 4000),
                    ChannelValuesAt(sample, currentPeak),
                    svId,
                    appId));
            }
        }

        var inventory = analyzer.GetAnalyzerSnapshot();
        Assert.Equal(streamCount, inventory.Streams.Count);

        for (var streamIndex = 0; streamIndex < streamCount; streamIndex++)
        {
            var svId = $"MU_{streamIndex + 1:00}";
            var expectedPeak = 500.0 * (streamIndex + 1);
            var selected = SelectStream(analyzer, svId);
            var samples = IaSamples(selected);

            Assert.Equal((long)framesPerStream, selected.Snapshot.Diagnostics.TotalPackets);
            Assert.Equal(SamplesPerCycle * 2, samples.Count);
            Assert.InRange(samples.Max(), expectedPeak * 0.98, expectedPeak * 1.02);
            Assert.Equal(svId, selected.Snapshot.SelectedStreamDetails?.SvId);
            Assert.Equal(selected.StreamId, selected.Snapshot.SelectedStreamId);
        }
    }

    [Fact]
    [Trait("Category", "RuntimeStability")]
    public void ScopeTimebase_ProducesExactTwoFourAndEightCycleWindows()
    {
        var analyzer = new RawProcessBusAnalyzer();
        FeedStream(analyzer, "MU_TIMEBASE", 0x4100, 1200, SamplesPerCycle * 8);

        analyzer.SetScopeCycles(2);
        Assert.Equal(SamplesPerCycle * 2, IaSamples(SelectStream(analyzer, "MU_TIMEBASE")).Count);

        analyzer.SetScopeCycles(4);
        Assert.Equal(SamplesPerCycle * 4, IaSamples(SelectStream(analyzer, "MU_TIMEBASE")).Count);

        analyzer.SetScopeCycles(8);
        Assert.Equal(SamplesPerCycle * 8, IaSamples(SelectStream(analyzer, "MU_TIMEBASE")).Count);
    }

    [Fact]
    [Trait("Category", "RuntimeStability")]
    public void SampleCounter65536Rollover_RemainsContiguous()
    {
        var analyzer = new RawProcessBusAnalyzer();
        var start = 65536 - (SamplesPerCycle * 4);

        FeedStream(
            analyzer,
            "MU_65536",
            0x4200,
            1500,
            SamplesPerCycle * 8,
            startSample: start,
            wrapBase: 65536);

        var selected = SelectStream(analyzer, "MU_65536");

        Assert.Equal(0L, selected.Snapshot.Diagnostics.SequenceErrors);
        Assert.Equal(0L, selected.Snapshot.Diagnostics.MissingSamples);
        Assert.Equal(SamplesPerCycle * 2, IaSamples(selected).Count);
    }

    [Fact]
    [Trait("Category", "RuntimeStability")]
    public void DuplicateAndForwardGap_AreReportedWithoutReplacingLastCoherentWindow()
    {
        var analyzer = new RawProcessBusAnalyzer();
        FeedStream(analyzer, "MU_SEQUENCE", 0x4300, 1700, SamplesPerCycle * 4);

        var stable = SelectStream(analyzer, "MU_SEQUENCE").Snapshot.Waveform;

        analyzer.ObserveFrame(GoldenFrames.SvFrameWithChannelSamples(
            (ushort)((SamplesPerCycle * 4) - 1),
            ChannelValuesAt((SamplesPerCycle * 4) - 1, 1700),
            "MU_SEQUENCE",
            0x4300));

        analyzer.ObserveFrame(GoldenFrames.SvFrameWithChannelSamples(
            (ushort)((SamplesPerCycle * 4) + 3),
            ChannelValuesAt((SamplesPerCycle * 4) + 3, 1700),
            "MU_SEQUENCE",
            0x4300));

        var afterFault = SelectStream(analyzer, "MU_SEQUENCE").Snapshot;

        Assert.Equal(2L, afterFault.Diagnostics.SequenceErrors);
        Assert.Equal(3L, afterFault.Diagnostics.MissingSamples);
        Assert.Same(stable, afterFault.Waveform);
    }

    [Fact]
    [Trait("Category", "RuntimeStability")]
    public void WaveformAndRms_AreDerivedFromTheSameCoherentWindow()
    {
        var analyzer = new RawProcessBusAnalyzer();
        FeedStream(analyzer, "MU_COHERENT", 0x4400, 2000, SamplesPerCycle * 4);

        var selected = SelectStream(analyzer, "MU_COHERENT");
        var samples = IaSamples(selected);
        var waveformRms = Math.Sqrt(samples.Average(value => value * value));
        var analogRms = selected.Snapshot.AnalogValues.Ia.RmsValue;

        Assert.True(analogRms.HasValue);
        Assert.InRange(analogRms!.Value, waveformRms * 0.999, waveformRms * 1.001);
        Assert.InRange(samples.Max(), 1960.0, 2040.0);
    }

    [Fact]
    [Trait("Category", "RuntimeStability")]
    public async Task ConcurrentObservationAndSnapshotReads_DoNotCrossStreamState()
    {
        var analyzer = new RawProcessBusAnalyzer();

        var writer = Task.Run(() =>
        {
            for (var sample = 0; sample < SamplesPerCycle * 12; sample++)
            {
                for (var streamIndex = 0; streamIndex < 4; streamIndex++)
                {
                    analyzer.ObserveFrame(GoldenFrames.SvFrameWithChannelSamples(
                        (ushort)(sample % 4000),
                        ChannelValuesAt(sample, 700 * (streamIndex + 1)),
                        $"MU_CONCURRENT_{streamIndex + 1}",
                        (ushort)(0x4500 + streamIndex)));
                }
            }
        });

        var reader = Task.Run(() =>
        {
            for (var iteration = 0; iteration < 600; iteration++)
            {
                var snapshot = analyzer.GetAnalyzerSnapshot();

                if (snapshot.SelectedStreamId is not null)
                {
                    Assert.Contains(snapshot.Streams, stream =>
                        string.Equals(stream.StreamId, snapshot.SelectedStreamId, StringComparison.Ordinal));
                }

                foreach (var series in snapshot.Waveform.VoltageSeries.Concat(snapshot.Waveform.CurrentSeries))
                {
                    Assert.True(series.Samples.Count is 160 or 320 or 640);
                }
            }
        });

        await Task.WhenAll(writer, reader);

        var finalSnapshot = analyzer.GetAnalyzerSnapshot();
        Assert.Equal(4, finalSnapshot.Streams.Count);
        Assert.Equal((long)SamplesPerCycle * 12 * 4, finalSnapshot.ProtocolMonitor.SvFrames);
    }

    private sealed record AnalyzerSelection(string StreamId, AnalyzerSnapshot Snapshot);
}
