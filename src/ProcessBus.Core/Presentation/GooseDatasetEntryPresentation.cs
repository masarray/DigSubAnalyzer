namespace ProcessBus.Core.Presentation;

/// <summary>
/// Human-readable entry label for unconfigured MMS data. The raw BER decoder
/// knows type and ordinal, not an electrical point name; only a matching SCL
/// DataSet entry can supply a signal reference.
/// </summary>
public static class GooseDatasetEntryPresentation
{
    public static string ResolveName(int index, string? matchedSclSignal)
        => ResolveName(index, matchedSclSignal, null);

    // When the wire type is known, the operator can read a type-number label
    // without an SCL match. This is an MMS type, not an invented signal name.
    public static string ResolveName(int index, string? matchedSclSignal, string? mmsType)
    {
        if (!string.IsNullOrWhiteSpace(matchedSclSignal))
            return matchedSclSignal.Trim();

        var type = mmsType?.Trim() switch
        {
            "BitString" => "Bitstring",
            "Boolean" => "Boolean",
            "Integer" => "Integer",
            "Unsigned" => "Unsigned",
            "Float" or "FloatingPoint" => "Float",
            "OctetString" => "Octetstring",
            "VisibleString" => "String",
            _ => "Data item"
        };
        return $"{type} {index}";
    }
}
