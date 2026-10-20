using System.Globalization;

namespace ProcessBus.Core.Presentation;

/// <summary>
/// Interprets only an explicitly identified IEC 61850 Quality (q) MMS BIT STRING.
/// The caller must establish q through a matching SCL DataSet entry; an arbitrary
/// BitString must never be presented as breaker position or signal quality.
/// </summary>
public static class GooseQualityPresentation
{
    public static bool TryDecode(string? rawHex, string? mmsType, out GooseQualityInfo? info)
    {
        info = null;
        if (!string.Equals(mmsType, "BitString", StringComparison.Ordinal) ||
            rawHex is null || rawHex.Length != 6)
            return false;

        Span<byte> bytes = stackalloc byte[3];
        for (var i = 0; i < 3; i++)
        {
            if (!byte.TryParse(rawHex.AsSpan(i * 2, 2),
                    NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out bytes[i]))
                return false;
        }

        // IEC 61850 q has exactly 13 bits. The MMS BER bitstring value starts
        // with the unused-bit count, then the MSB-first quality payload.
        if (bytes[0] != 3)
            return false;

        bool Set(int index) => (bytes[1 + index / 8] & (0x80 >> (index % 8))) != 0;
        var validityBits = (Set(0) ? 2 : 0) | (Set(1) ? 1 : 0);
        var validity = validityBits switch
        {
            0 => "good",
            1 => "invalid",
            2 => "reserved",
            _ => "questionable"
        };
        var flags = new List<string>(8);
        var details = new (int Index, string Name)[]
        {
            (2, "overflow"), (3, "outOfRange"), (4, "badReference"),
            (5, "oscillatory"), (6, "failure"), (7, "oldData"),
            (8, "inconsistent"), (9, "inaccurate")
        };
        foreach (var (index, name) in details)
            if (Set(index)) flags.Add(name);

        info = new GooseQualityInfo(validity, Set(10) ? "substituted" : "process",
            Set(11), Set(12), flags);
        return true;
    }
}

public sealed record GooseQualityInfo(
    string Validity,
    string Source,
    bool Test,
    bool OperatorBlocked,
    IReadOnlyList<string> DetailFlags)
{
    public string Summary => Validity;
    public string DetailText =>
        $"Validity: {Validity}  |  Source: {Source}  |  Test: {Test.ToString().ToLowerInvariant()}  |  " +
        $"Operator blocked: {OperatorBlocked.ToString().ToLowerInvariant()}  |  " +
        $"Detail quality flags: {(DetailFlags.Count == 0 ? "none asserted" : string.Join(", ", DetailFlags))}";
}
