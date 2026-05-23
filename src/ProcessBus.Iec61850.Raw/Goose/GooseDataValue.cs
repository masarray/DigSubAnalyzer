namespace ProcessBus.Iec61850.Raw.Goose;

public sealed class GooseDataValue
{
    public int Index { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = "Unknown";
    public string Value { get; init; } = "-";
    public string RawHex { get; init; } = string.Empty;
}
