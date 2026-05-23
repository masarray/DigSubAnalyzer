namespace ProcessBus.Iec61850.Raw.Ptp;

public sealed class PtpMessage
{
    public string MessageType { get; init; } = "Unknown";
    public byte MessageTypeValue { get; init; }
    public byte Version { get; init; }
    public ushort MessageLength { get; init; }
    public byte DomainNumber { get; init; }
    public ushort Flags { get; init; }
    public long CorrectionField { get; init; }
    public string SourceClockIdentity { get; init; } = "N/A";
    public ushort SourcePortNumber { get; init; }
    public string SourcePortIdentity => $"{SourceClockIdentity}:{SourcePortNumber}";
    public ushort SequenceId { get; init; }
    public byte ControlField { get; init; }
    public sbyte LogMessageInterval { get; init; }
    public string SourceMac { get; init; } = "N/A";
    public string DestinationMac { get; init; } = "N/A";
    public string VlanText { get; init; } = "N/A";
    public string TransportText { get; init; } = "Ethernet";
    public string NetworkSourceText { get; init; } = "N/A";
    public string NetworkDestinationText { get; init; } = "N/A";
    public DateTime CaptureTimeUtc { get; init; }
    public long CaptureTicks { get; init; }

    public short? CurrentUtcOffset { get; init; }
    public byte? GrandmasterPriority1 { get; init; }
    public byte? GrandmasterClockClass { get; init; }
    public byte? GrandmasterClockAccuracy { get; init; }
    public ushort? GrandmasterOffsetScaledLogVariance { get; init; }
    public byte? GrandmasterPriority2 { get; init; }
    public string? GrandmasterIdentity { get; init; }
    public ushort? StepsRemoved { get; init; }
    public byte? TimeSource { get; init; }

    public bool IsAnnounce => MessageTypeValue == 0x0B;
    public bool IsSync => MessageTypeValue == 0x00;
    public bool IsFollowUp => MessageTypeValue == 0x08;
    public bool IsDelayRequest => MessageTypeValue == 0x01;
    public bool IsDelayResponse => MessageTypeValue == 0x09;
    public bool IsPeerDelay => MessageTypeValue is 0x02 or 0x03 or 0x0A;

    public string ClockAccuracyText => GrandmasterClockAccuracy.HasValue
        ? $"0x{GrandmasterClockAccuracy.Value:X2}"
        : "N/A";

    public string ProfileHintText
    {
        get
        {
            if (!IsAnnounce)
                return "PTP v2 message";

            if (GrandmasterClockClass is 6 or 7 or 13 or 14 or 52 or 187 or 248)
                return "PTP power-utility profile candidate";

            return "PTP v2 announce observed";
        }
    }
}
