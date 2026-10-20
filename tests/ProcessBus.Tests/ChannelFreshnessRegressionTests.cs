using System.Buffers.Binary;
using ProcessBus.Core.Models;
using ProcessBus.Core.Presentation;
using ProcessBus.Iec61850.Raw.Analysis;
using ProcessBus.Iec61850.Raw.Sv;
using Xunit;

namespace ProcessBus.Tests;

/// <summary>
/// Real decoder -> coherent snapshot quality/freshness regression for 4I4V SV.
/// Does not invent missing phases from a valid zero-amplitude sample.
/// </summary>
public sealed class ChannelFreshnessRegressionTests
{
    private const int SamplesPerCycle = 80;

    private static int[] Samples(int index)
    {
        var phase = 2 * Math.PI * index / SamplesPerCycle;
        static int Sample(double angle, double peak) => (int)Math.Round(Math.Sin(angle) * peak);
        return
        [
            Sample(phase, 600), Sample(phase - 2 * Math.PI / 3, 1500),
            Sample(phase + 2 * Math.PI / 3, 1000), 0,
            Sample(phase, 8160), Sample(phase - 2 * Math.PI / 3, 8160),
            Sample(phase + 2 * Math.PI / 3, 4080), 0
        ];
    }

    private static byte[] Frame(int index, int badChannel = -1)
    {
        var frame = GoldenFrames.SvFrameWithChannelSamples(
            (ushort)index, Samples(index), "QUALITY_FRESHNESS_SV", 0x4000);
        if (badChannel >= 0)
        {
            // The sample payload is the final 64 bytes of this synthetic frame,
            // with eight instMag/q pairs. Corrupt only the requested q word.
            BinaryPrimitives.WriteUInt32BigEndian(
                frame.AsSpan(frame.Length - 64 + badChannel * 8 + 4, 4), 1u << 7);
        }
        return frame;
    }

    [Fact]
    public void PerChannelQualityWordIsReadFromItsOwnInstMagPair()
    {
        var payload = new byte[64];
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(5 * 8 + 4, 4), 1u << 7);
        Assert.Equal(FourI4VQuality.MeasurementState.Untrusted,
            FourI4VQuality.AssessChannel(payload, 10)); // Ub
        Assert.Equal(FourI4VQuality.MeasurementState.Good,
            FourI4VQuality.AssessChannel(payload, 8)); // Ua
        Assert.Equal(FourI4VQuality.MeasurementState.UnknownLayout,
            FourI4VQuality.AssessChannel(payload, 11)); // misaligned q
    }

    [Fact]
    public void OnlyBadIcIsHeldWhileOtherFiveChannelsStayLiveThenRecover()
    {
        var engine = new RawProcessBusAnalyzer();
        for (var index = 0; index < 400; index++)
            engine.ObserveFrame(Frame(index));

        var first = engine.GetAnalyzerSnapshot();
        var initialIc = first.Waveform.CurrentSeries.Single(x => x.Name == "Ic");
        Assert.Equal(ChannelFreshness.Live, initialIc.Freshness);
        Assert.True(initialIc.Samples.Count >= 80);

        for (var index = 400; index < 440; index++)
            engine.ObserveFrame(Frame(index, badChannel: 2)); // Ic, quality invalid

        var partial = engine.GetAnalyzerSnapshot();
        Assert.Equal(ChannelFreshness.Held, partial.Waveform.CurrentSeries.Single(x => x.Name == "Ic").Freshness);
        Assert.Equal(ChannelFreshness.Held, partial.AnalogValues.Ic.Freshness);
        Assert.Equal(initialIc.Samples.ToArray(),
            partial.Waveform.CurrentSeries.Single(x => x.Name == "Ic").Samples.ToArray());
        foreach (var channel in new[] { "Ia", "Ib" })
            Assert.Equal(ChannelFreshness.Live,
                partial.Waveform.CurrentSeries.Single(x => x.Name == channel).Freshness);
        foreach (var channel in new[] { "Ua", "Ub", "Uc" })
            Assert.Equal(ChannelFreshness.Live,
                partial.Waveform.VoltageSeries.Single(x => x.Name == channel).Freshness);
        Assert.True(partial.Streams.Single().IsActive);
        Assert.Equal("LIVE · WARN", partial.Streams.Single().DisplayStatusText);
        Assert.Contains("partial", partial.Diagnostics.ValidityText, StringComparison.OrdinalIgnoreCase);
        Assert.False(SvPhysicalPowerMeter.TryCompute(partial.AnalogValues, out _));

        // One fresh sample after a fault cannot turn the held phase bright.
        engine.ObserveFrame(Frame(440));
        Assert.Equal(ChannelFreshness.Held,
            engine.GetAnalyzerSnapshot().Waveform.CurrentSeries.Single(x => x.Name == "Ic").Freshness);

        for (var index = 441; index < 760; index++)
            engine.ObserveFrame(Frame(index));
        var recovered = engine.GetAnalyzerSnapshot();
        Assert.Equal(ChannelFreshness.Live, recovered.Waveform.CurrentSeries.Single(x => x.Name == "Ic").Freshness);
        Assert.Equal(ChannelFreshness.Live, recovered.AnalogValues.Ic.Freshness);
        Assert.Equal("4I+4V quality good", recovered.Diagnostics.ValidityText);
    }

    [Fact]
    public void ZeroCurrentIsStillLiveAndMustNeverBeCalledStale()
    {
        var engine = new RawProcessBusAnalyzer();
        for (var index = 0; index < 360; index++)
        {
            var values = Samples(index);
            values[2] = 0;
            engine.ObserveFrame(GoldenFrames.SvFrameWithChannelSamples(
                (ushort)index, values, "ZERO_CURRENT", 0x4000));
        }
        var snapshot = engine.GetAnalyzerSnapshot();
        Assert.Equal(ChannelFreshness.Live,
            snapshot.Waveform.CurrentSeries.Single(x => x.Name == "Ic").Freshness);
        Assert.Equal(ChannelFreshness.Live, snapshot.AnalogValues.Ic.Freshness);
        Assert.Equal(0, snapshot.AnalogValues.Ic.RmsValue);
    }

    [Fact]
    public void PresentationOpacityDoesNotModifyEngineeringSamples()
    {
        Assert.Equal(1.0, ChannelFreshnessDisplay.Opacity(ChannelFreshness.Live));
        Assert.Equal(0.38, ChannelFreshnessDisplay.Opacity(ChannelFreshness.Held));
        Assert.Equal(0.22, ChannelFreshnessDisplay.Opacity(ChannelFreshness.Missing));
        var signal = new WaveformSeriesModel
        {
            Name = "Ic", Unit = "A", Samples = [1.0, -1.0],
            Freshness = ChannelFreshness.Held
        };
        Assert.Equal(new[] { 1.0, -1.0 }, signal.Samples);
    }
}
