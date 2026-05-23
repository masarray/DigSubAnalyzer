using System.Buffers.Binary;

namespace ProcessBus.Iec61850.Raw.Protocol;

public sealed class EthernetFrame
{
    public const ushort VlanTaggedFrame = 0x8100;
    public const ushort ProviderBridgeTaggedFrame = 0x88A8;
    public const ushort Ipv4EtherType = 0x0800;
    public const ushort Ipv6EtherType = 0x86DD;
    public const ushort SampledValuesEtherType = 0x88BA;
    public const ushort GooseEtherType = 0x88B8;
    public const ushort PtpEtherType = 0x88F7;

    private EthernetFrame(
        string destinationMac,
        string sourceMac,
        ushort etherType,
        VlanTag? vlan,
        int payloadOffset,
        ReadOnlyMemory<byte> payload)
    {
        DestinationMac = destinationMac;
        SourceMac = sourceMac;
        EtherType = etherType;
        Vlan = vlan;
        PayloadOffset = payloadOffset;
        Payload = payload;
    }

    public string DestinationMac { get; }
    public string SourceMac { get; }
    public ushort EtherType { get; }
    public VlanTag? Vlan { get; }
    public int PayloadOffset { get; }
    public ReadOnlyMemory<byte> Payload { get; }

    public static bool TryParse(ReadOnlyMemory<byte> frameBytes, out EthernetFrame frame)
    {
        frame = null!;

        if (frameBytes.Length < 14)
            return false;

        var span = frameBytes.Span;
        var destinationMac = FormatMac(span[..6]);
        var sourceMac = FormatMac(span.Slice(6, 6));
        var etherType = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(12, 2));
        var payloadOffset = 14;
        VlanTag? vlan = null;

        if (etherType is VlanTaggedFrame or ProviderBridgeTaggedFrame)
        {
            if (frameBytes.Length < 18)
                return false;

            var tci = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(14, 2));
            vlan = new VlanTag(
                PriorityCodePoint: (byte)((tci >> 13) & 0x7),
                DropEligible: ((tci >> 12) & 0x1) != 0,
                VlanId: (ushort)(tci & 0x0FFF));

            etherType = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(16, 2));
            payloadOffset = 18;
        }

        frame = new EthernetFrame(
            destinationMac,
            sourceMac,
            etherType,
            vlan,
            payloadOffset,
            frameBytes[payloadOffset..]);

        return true;
    }

    private static string FormatMac(ReadOnlySpan<byte> value)
    {
        return string.Create(17, value.ToArray(), static (chars, bytes) =>
        {
            const string hex = "0123456789ABCDEF";

            for (var i = 0; i < 6; i++)
            {
                if (i > 0)
                    chars[(i * 3) - 1] = ':';

                chars[i * 3] = hex[bytes[i] >> 4];
                chars[(i * 3) + 1] = hex[bytes[i] & 0x0F];
            }
        });
    }
}
