namespace ProcessBus.Core.Models;

public sealed class WaveformSnapshot
{
    public IReadOnlyList<WaveformSeriesModel> VoltageSeries { get; init; } = Array.Empty<WaveformSeriesModel>();
    public IReadOnlyList<WaveformSeriesModel> CurrentSeries { get; init; } = Array.Empty<WaveformSeriesModel>();
    public double SampleRateHz { get; init; }
    public double MeasuredFrequencyHz { get; init; }
    public double WindowDurationMilliseconds { get; init; }
    public string StatusText { get; init; } = "Waiting for SV samples.";
}
