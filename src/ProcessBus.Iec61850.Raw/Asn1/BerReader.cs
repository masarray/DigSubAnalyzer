using System.Buffers.Binary;
using System.Text;

namespace ProcessBus.Iec61850.Raw.Asn1;

public static class BerReader
{
    public static bool TryReadTlv(ReadOnlyMemory<byte> source, ref int offset, out BerTlv tlv)
    {
        tlv = default;

        if (offset >= source.Length)
            return false;

        var span = source.Span;
        var tag = span[offset++];
        var tagNumber = tag & 0x1F;

        if (tagNumber == 0x1F)
            return false;

        if (offset >= source.Length)
            return false;

        var lengthByte = span[offset++];
        int length;

        if ((lengthByte & 0x80) == 0)
        {
            length = lengthByte;
        }
        else
        {
            var lengthBytes = lengthByte & 0x7F;

            if (lengthBytes is 0 or > 4 || offset + lengthBytes > source.Length)
                return false;

            length = 0;
            for (var i = 0; i < lengthBytes; i++)
                length = (length << 8) | span[offset++];
        }

        // Compare as "length > remaining" instead of "offset + length > total" so a
        // hostile 4-byte length near int.MaxValue cannot overflow the addition and
        // slip past validation into Slice(). Try-methods must never throw on raw traffic.
        if (length < 0 || length > source.Length - offset)
            return false;

        tlv = new BerTlv(
            tag,
            (BerClass)((tag >> 6) & 0x03),
            (tag & 0x20) != 0,
            tagNumber,
            source.Slice(offset, length));

        offset += length;
        return true;
    }

    public static IEnumerable<BerTlv> ReadChildren(ReadOnlyMemory<byte> source)
    {
        var offset = 0;

        while (offset < source.Length && TryReadTlv(source, ref offset, out var tlv))
            yield return tlv;
    }

    public static string? ReadString(BerTlv tlv)
    {
        if (tlv.Value.IsEmpty)
            return string.Empty;

        return Encoding.ASCII.GetString(tlv.Value.Span);
    }

    public static uint? ReadUnsignedInteger(BerTlv tlv)
    {
        var span = tlv.Value.Span;

        return span.Length switch
        {
            0 => 0,
            1 => span[0],
            2 => BinaryPrimitives.ReadUInt16BigEndian(span),
            3 => (uint)((span[0] << 16) | (span[1] << 8) | span[2]),
            4 => BinaryPrimitives.ReadUInt32BigEndian(span),
            _ => null
        };
    }

    public static bool? ReadBoolean(BerTlv tlv)
    {
        return tlv.Value.Length == 1 ? tlv.Value.Span[0] != 0 : null;
    }
}
