using ProcessBus.Iec61850.Raw.Asn1;
using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace ProcessBus.Iec61850.Raw.Goose;

public static class GooseMmsDataDecoder
{
    public static IReadOnlyList<GooseDataValue> Decode(ReadOnlyMemory<byte> allData)
    {
        if (allData.IsEmpty)
            return Array.Empty<GooseDataValue>();

        var result = new List<GooseDataValue>();
        var index = 1;

        foreach (var tlv in BerReader.ReadChildren(allData))
        {
            result.Add(DecodeValue(index, tlv));
            index++;
        }

        return result;
    }

    private static GooseDataValue DecodeValue(int index, BerTlv tlv)
    {
        var type = ResolveTypeName(tlv);
        var value = ResolveValueText(tlv, type);

        return new GooseDataValue
        {
            Index = index,
            Name = $"{type} {index}",
            Type = type,
            Value = value,
            RawHex = ToHex(tlv.Value.Span)
        };
    }

    private static string ResolveTypeName(BerTlv tlv)
    {
        if (tlv.Class != BerClass.ContextSpecific)
            return $"BER tag {tlv.TagNumber}";

        return tlv.TagNumber switch
        {
            1 => "Array",
            2 => "Structure",
            3 => "Boolean",
            4 => "BitString",
            5 => "Integer",
            6 => "Unsigned",
            7 => "Float",
            9 => "OctetString",
            10 => "VisibleString",
            12 => "BinaryTime",
            13 => "BCD",
            14 => "BooleanArray",
            15 => "ObjectId",
            16 => "MMSString",
            17 => "UtcTime",
            _ => $"Data[{tlv.TagNumber}]"
        };
    }

    private static string ResolveValueText(BerTlv tlv, string type)
    {
        var span = tlv.Value.Span;

        return type switch
        {
            "Boolean" => span.Length == 1 ? (span[0] != 0).ToString().ToLowerInvariant() : ToHex(span),
            "BitString" => DecodeBitString(span),
            "Integer" => DecodeSignedInteger(span),
            "Unsigned" => DecodeUnsignedInteger(span),
            "Float" => DecodeFloatingPoint(span),
            "VisibleString" => DecodeAscii(span),
            "MMSString" => DecodeAscii(span),
            "OctetString" => ToHex(span),
            "UtcTime" => DecodeUtcTime(span),
            "Structure" => DescribeConstructed(span, "structure"),
            "Array" => DescribeConstructed(span, "array"),
            _ => ToHex(span)
        };
    }

    private static string DecodeBitString(ReadOnlySpan<byte> span)
    {
        if (span.Length == 0)
            return string.Empty;

        if (span.Length == 1)
            return ToHex(span);

        var unusedBits = span[0];
        var totalBits = ((span.Length - 1) * 8) - unusedBits;
        if (totalBits <= 0 || totalBits > 128)
            return ToHex(span);

        var sb = new StringBuilder(totalBits);
        for (var bit = 0; bit < totalBits; bit++)
        {
            var byteIndex = 1 + (bit / 8);
            var bitInByte = 7 - (bit % 8);
            var isSet = (span[byteIndex] & (1 << bitInByte)) != 0;
            sb.Append(isSet ? '1' : '0');
        }

        return sb.ToString();
    }

    private static string DecodeSignedInteger(ReadOnlySpan<byte> span)
    {
        if (span.Length == 0)
            return "0";

        if (span.Length > 8)
            return ToHex(span);

        if (span.Length == 8)
            return BinaryPrimitives.ReadInt64BigEndian(span).ToString(CultureInfo.InvariantCulture);

        long value = 0;
        for (var i = 0; i < span.Length; i++)
            value = (value << 8) | span[i];

        if ((span[0] & 0x80) != 0)
            value -= 1L << (span.Length * 8);

        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string DecodeUnsignedInteger(ReadOnlySpan<byte> span)
    {
        if (span.Length == 0)
            return "0";

        if (span.Length > 8)
            return ToHex(span);

        ulong value = 0;
        for (var i = 0; i < span.Length; i++)
            value = (value << 8) | span[i];

        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string DecodeFloatingPoint(ReadOnlySpan<byte> span)
    {
        try
        {
            if (span.Length == 5)
            {
                var raw = BinaryPrimitives.ReadUInt32BigEndian(span[1..]);
                var value = BitConverter.Int32BitsToSingle(unchecked((int)raw));
                return value.ToString("0.###", CultureInfo.InvariantCulture);
            }

            if (span.Length == 4)
            {
                var raw = BinaryPrimitives.ReadUInt32BigEndian(span);
                var value = BitConverter.Int32BitsToSingle(unchecked((int)raw));
                return value.ToString("0.###", CultureInfo.InvariantCulture);
            }
        }
        catch
        {
            // Fall through to hex when the MMS floating-point flavor is not recognized.
        }

        return ToHex(span);
    }

    private static string DecodeAscii(ReadOnlySpan<byte> span)
    {
        if (span.Length == 0)
            return string.Empty;

        return Encoding.ASCII.GetString(span);
    }

    private static string DecodeUtcTime(ReadOnlySpan<byte> span)
    {
        if (span.Length != 8)
            return ToHex(span);

        var seconds = BinaryPrimitives.ReadUInt32BigEndian(span[..4]);
        var fraction = (span[4] << 16) | (span[5] << 8) | span[6];
        var quality = span[7];

        try
        {
            var utc = DateTimeOffset.FromUnixTimeSeconds(seconds)
                .AddSeconds(fraction / 16777216.0)
                .UtcDateTime;
            return $"{utc:yyyy-MM-dd HH:mm:ss.fff} UTC (q=0x{quality:X2})";
        }
        catch
        {
            return ToHex(span);
        }
    }

    private static string DescribeConstructed(ReadOnlySpan<byte> span, string kind)
    {
        var count = BerReader.ReadChildren(span.ToArray()).Count();
        return $"{kind} ({count} item{(count == 1 ? string.Empty : "s")})";
    }

    private static string ToHex(ReadOnlySpan<byte> span)
    {
        if (span.Length == 0)
            return string.Empty;

        return Convert.ToHexString(span);
    }
}
