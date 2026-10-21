using ProcessBus.Core.Models.Scl;

namespace ProcessBus.Core.Presentation;

/// <summary>
/// Operator-friendly wording for GOOSE evidence. Never infer a real-world
/// switch position or quality from an unidentified raw MMS value.
/// Exact protocol names remain available in the expanded evidence view.
/// </summary>
public static class GooseEngineeringLanguage
{
    public static string DataItemName(int index, string? sclSignalName) =>
        string.IsNullOrWhiteSpace(sclSignalName) ? $"Data item {index}" : sclSignalName.Trim();

    public static string DataType(string? mmsType) => mmsType?.Trim() switch
    {
        "BitString" => "Binary bit pattern",
        "Boolean" => "True / false value",
        "Integer" => "Whole number",
        "Unsigned" => "Positive whole number",
        "Float" => "Decimal number",
        "FloatingPoint" => "Decimal number",
        "VisibleString" => "Text",
        "OctetString" => "Bytes",
        "UtcTime" => "Date and time",
        "BinaryTime" => "Time value",
        "Structure" => "Group of values",
        "Array" => "List of values",
        "Enum" => "Enumerated value",
        "Enumerated" => "Enumerated value",
        "Unknown" or null or "" => "Unknown data type",
        var other => $"Recorded type: {other}"
    };

    public static string SignalSource(bool matchedScl) => matchedScl
        ? "Signal name identified from the imported configuration"
        : "Signal not identified — configuration file not matched";

    public static string Event(string? eventType) => eventType switch
    {
        "New" => "First seen",
        "State Change" => "State changed",
        "Retrans" => "Repeated",
        _ => "Message"
    };

    /// <summary>
    /// Decode DPC.stVal only when the loaded configuration identifies the exact
    /// signal. Do not turn unidentified bit patterns into physical switch states.
    /// </summary>
    public static bool TryDescribeDoublePoint(
        SclDataSetEntryModel? signal, string? wireType, string? twoBits,
        out string description)
    {
        description = string.Empty;
        if (signal is null || signal.IsQuality || signal.IsTimestamp ||
            !string.Equals(signal.Cdc, "DPC", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(signal.DaName, "stVal", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(wireType, "BitString", StringComparison.OrdinalIgnoreCase))
            return false;

        var switchPosition = string.Equals(signal.DoName, "Pos", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(signal.LnClass, "XCBR", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(signal.LnClass, "XSWI", StringComparison.OrdinalIgnoreCase));
        description = twoBits switch
        {
            "00" => "Between positions (00)",
            "01" => switchPosition ? "Open (01)" : "Off (01)",
            "10" => switchPosition ? "Closed (10)" : "On (10)",
            "11" => "Invalid position (11)",
            _ => string.Empty
        };
        return description.Length != 0;
    }

    public static string QualitySummary(string? rawQualitySummary)
    {
        if (string.IsNullOrWhiteSpace(rawQualitySummary))
            return "";
        return rawQualitySummary.Replace("questionable", "Questionable quality", StringComparison.Ordinal)
            .Replace("invalid", "Invalid quality", StringComparison.Ordinal)
            .Replace("reserved", "Reserved quality code", StringComparison.Ordinal)
            .Replace("good", "Good quality", StringComparison.Ordinal)
            .Replace("substituted", "Substituted", StringComparison.Ordinal)
            .Replace("blocked", "Operator blocked", StringComparison.Ordinal);
    }
}
