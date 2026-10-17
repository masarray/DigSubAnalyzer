using ProcessBus.Core.Models;
using ProcessBus.Core.Presentation;
using Xunit;

namespace ProcessBus.Tests;

public sealed class LiveRenderCoalescerTests
{
    private static WaveformSnapshot Wave(params double[] samples) => new()
    {
        SampleRateHz = 4000,
        MeasuredFrequencyHz = 50,
        SamplesPerCycle = 80,
        VoltageSeries = [new WaveformSeriesModel
        {
            Name = "Ua", Unit = "counts", Samples = samples,
            ShapeSeverity = "Good", ShapeStatusText = "Stable"
        }]
    };

    private static AnalogValuesSnapshot Analog(double rms, double angle = 0) => new()
    {
        Ua = new ChannelValueModel("Ua") { RmsValue = rms, AngleDegrees = angle, Unit = "counts" }
    };

    [Fact]
    public void IdenticalValuesAcrossNewSnapshotsDoNotRepaint()
    {
        var gate = new LiveRenderCoalescer();
        Assert.True(gate.PublishWaveform("A", Wave(1, 2, 3)));
        Assert.False(gate.PublishWaveform("A", Wave(1, 2, 3)));
        Assert.True(gate.PublishAnalog("A", Analog(50)));
        Assert.False(gate.PublishAnalog("A", Analog(50)));
    }

    [Fact]
    public void TinyRealTransientStreamSwitchAndInvalidationMustPublish()
    {
        var gate = new LiveRenderCoalescer();
        Assert.True(gate.PublishWaveform("A", Wave(1, 2, 3)));
        Assert.True(gate.PublishWaveform("A", Wave(1, 2.00000000001, 3)));
        Assert.True(gate.PublishWaveform("B", Wave(1, 2.00000000001, 3)));
        Assert.True(gate.PublishWaveform("B", new WaveformSnapshot
        {
            StatusText = "Invalid quality: engineering measurements withheld"
        }));
        Assert.False(gate.PublishWaveform("B", new WaveformSnapshot
        {
            StatusText = "Invalid quality: engineering measurements withheld"
        }));
        Assert.True(gate.PublishAnalog("B", Analog(50)));
        Assert.True(gate.PublishAnalog("B", Analog(50, 0.0000000001)));
        gate.Reset();
        Assert.True(gate.PublishWaveform("B", Wave(1, 2, 3)));
    }

    [Fact]
    public void ShapeVerdictAndUnitChangesCannotBeHidden()
    {
        var gate = new LiveRenderCoalescer();
        Assert.True(gate.PublishWaveform("A", Wave(1, 2)));
        Assert.True(gate.PublishWaveform("A", new WaveformSnapshot
        {
            SampleRateHz = 4000, MeasuredFrequencyHz = 50, SamplesPerCycle = 80,
            VoltageSeries = [new WaveformSeriesModel {
                Name = "Ua", Unit = "V", Samples = new[] { 1d, 2d },
                ShapeSeverity = "Warning", ShapeStatusText = "Harmonic"
            }]
        }));
    }
}
