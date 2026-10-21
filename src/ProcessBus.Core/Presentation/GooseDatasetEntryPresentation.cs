namespace ProcessBus.Core.Presentation;

/// <summary>
/// Human-readable entry label for unconfigured MMS data. The raw BER decoder
/// knows type and ordinal, not an electrical point name; only a matching SCL
/// DataSet entry can supply a signal reference.
/// </summary>
public static class GooseDatasetEntryPresentation
{
    public static string ResolveName(int index, string? matchedSclSignal)
        => string.IsNullOrWhiteSpace(matchedSclSignal)
            ? $"Data item {index}"
            : matchedSclSignal.Trim();
}
