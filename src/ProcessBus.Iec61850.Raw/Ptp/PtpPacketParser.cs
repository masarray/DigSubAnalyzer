using ProcessBus.Iec61850.Raw.Protocol;
using System.Buffers.Binary;

namespace ProcessBus.Iec61850.Raw.Ptp;

public static class PtpPacketParser
{
    private const int CommonHeaderLength = 34;
    private const int AnnounceMinimumLength = 64;

    public static bool TryParse(ProcessBusFrame frame, out PtpMessage message)
    {
        message = null!;

        if (frame.Kind != ProcessBusFrameKind.Ptp || frame.Apdu.Length < CommonHeaderLength)
            return false;

        var payload = frame.Apdu.Span;
        var messageTypeValue = (byte)(payload[0] & 0x0F);
        var messageLength = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(2, 2));
        if (messageLength < CommonHeaderLength || messageLength > payload.Length)
            messageLength = (ushort)payload.Length;

        var sourceClockIdentity = FormatClockIdentity(payload.Slice(20, 8));
        var sourcePortNumber = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(28, 2));

        var builder = new PtpMessage
        {
            MessageType = ResolveMessageType(messageTypeValue),
            MessageTypeValue = messageTypeValue,
            Version = (byte)(payload[1] & 0x0F),
            MessageLength = messageLength,
            DomainNumber = payload[4],
            Flags = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(6, 2)),
            CorrectionField = BinaryPrimitives.ReadInt64BigEndian(payload.Slice(8, 8)),
            SourceClockIdentity = sourceClockIdentity,
            SourcePortNumber = sourcePortNumber,
            SequenceId = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(30, 2)),
            ControlField = payload[32],
            LogMessageInterval = unchecked((sbyte)payload[33]),
            SourceMac = frame.Ethernet.SourceMac,
            DestinationMac = frame.Ethernet.DestinationMac,
            VlanText = FormatVlan(frame.Ethernet.Vlan),
            TransportText = frame.TransportText,
            NetworkSourceText = frame.NetworkSourceText,
            NetworkDestinationText = frame.NetworkDestinationText,
            CaptureTimeUtc = frame.CaptureTimeUtc,
            CaptureTicks = frame.CaptureTicks
        };

        if (messageTypeValue == 0x0B && payload.Length >= AnnounceMinimumLength)
        {
            builder = new PtpMessage
            {
                MessageType = builder.MessageType,
                MessageTypeValue = builder.MessageTypeValue,
                Version = builder.Version,
                MessageLength = builder.MessageLength,
                DomainNumber = builder.DomainNumber,
                Flags = builder.Flags,
                CorrectionField = builder.CorrectionField,
                SourceClockIdentity = builder.SourceClockIdentity,
                SourcePortNumber = builder.SourcePortNumber,
                SequenceId = builder.SequenceId,
                ControlField = builder.ControlField,
                LogMessageInterval = builder.LogMessageInterval,
                SourceMac = builder.SourceMac,
                DestinationMac = builder.DestinationMac,
                VlanText = builder.VlanText,
                TransportText = builder.TransportText,
                NetworkSourceText = builder.NetworkSourceText,
                NetworkDestinationText = builder.NetworkDestinationText,
                CaptureTimeUtc = builder.CaptureTimeUtc,
                CaptureTicks = builder.CaptureTicks,
                CurrentUtcOffset = BinaryPrimitives.ReadInt16BigEndian(payload.Slice(44, 2)),
                GrandmasterPriority1 = payload[47],
                GrandmasterClockClass = payload[48],
                GrandmasterClockAccuracy = payload[49],
                GrandmasterOffsetScaledLogVariance = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(50, 2)),
                GrandmasterPriority2 = payload[52],
                GrandmasterIdentity = FormatClockIdentity(payload.Slice(53, 8)),
                StepsRemoved = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(61, 2)),
                TimeSource = payload[63]
            };
        }

        message = builder;
        return true;
    }

    private static string ResolveMessageType(byte messageType) => messageType switch
    {
        0x00 => "Sync",
        0x01 => "Delay_Req",
        0x02 => "Pdelay_Req",
        0x03 => "Pdelay_Resp",
        0x08 => "Follow_Up",
        0x09 => "Delay_Resp",
        0x0A => "Pdelay_Resp_Follow_Up",
        0x0B => "Announce",
        0x0C => "Signaling",
        0x0D => "Management",
        _ => $"Unknown(0x{messageType:X1})"
    };

    private static string FormatVlan(VlanTag? vlan)
    {
        if (!vlan.HasValue)
            return "untagged";

        var tag = vlan.Value;
        return $"VID {tag.VlanId} PCP {tag.PriorityCodePoint}";
    }

    private static string FormatClockIdentity(ReadOnlySpan<byte> bytes)
    {
        return string.Create(23, bytes.ToArray(), static (chars, value) =>
        {
            const string hex = "0123456789ABCDEF";
            for (var i = 0; i < value.Length; i++)
            {
                if (i > 0)
                    chars[(i * 3) - 1] = '-';

                chars[i * 3] = hex[value[i] >> 4];
                chars[(i * 3) + 1] = hex[value[i] & 0x0F];
            }
        });
    }
}
