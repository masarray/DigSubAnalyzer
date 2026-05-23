namespace ProcessBus.Core.Models;

public sealed class WaveformSeriesModel
{
    public required string Name { get; init; }
    public string Unit { get; init; } = string.Empty;
    public IReadOnlyList<double> Samples { get; init; } = Array.Empty<double>();
}
