using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;

namespace ProcessBus.Iec61850.Raw.Protocol;

public static class ProcessBusFrameParser
{
    private const int ProcessBusHeaderLength = 8;

    public static bool TryParse(
        ReadOnlyMemory<byte> frameBytes,
        out ProcessBusFrame frame,
        DateTime? captureTimeUtc = null,
        long? captureTicks = null)
    {
        frame = null!;

        if (!EthernetFrame.TryParse(frameBytes, out var ethernet))
            return false;

        var kind = ethernet.EtherType switch
        {
            EthernetFrame.SampledValuesEtherType => ProcessBusFrameKind.SampledValues,
            EthernetFrame.GooseEtherType => ProcessBusFrameKind.Goose,
            EthernetFrame.PtpEtherType => ProcessBusFrameKind.Ptp,
            _ => ProcessBusFrameKind.Unknown
        };

        if (kind == ProcessBusFrameKind.Unknown)
        {
            if (!TryExtractPtpOverUdp(
                    ethernet,
                    out var ptpPayload,
                    out var ptpDeclaredLength,
                    out var ptpTransportText,
                    out var networkSourceText,
                    out var networkDestinationText))
            {
                return false;
            }

            frame = new ProcessBusFrame(
                ethernet,
                ProcessBusFrameKind.Ptp,
                0,
                ptpDeclaredLength,
                0,
                0,
                ptpPayload,
                captureTimeUtc ?? DateTime.UtcNow,
                captureTicks ?? Stopwatch.GetTimestamp(),
                ptpTransportText,
                networkSourceText,
                networkDestinationText);

            return true;
        }

        if (kind == ProcessBusFrameKind.Ptp)
        {
            frame = new ProcessBusFrame(
                ethernet,
                kind,
                0,
                (ushort)Math.Min(ushort.MaxValue, ethernet.Payload.Length),
                0,
                0,
                ethernet.Payload,
                captureTimeUtc ?? DateTime.UtcNow,
                captureTicks ?? Stopwatch.GetTimestamp(),
                "Ethernet / IEEE 1588",
                "N/A",
                "N/A");

            return true;
        }

        if (ethernet.Payload.Length < ProcessBusHeaderLength)
            return false;

        var payload = ethernet.Payload.Span;
        var appId = BinaryPrimitives.ReadUInt16BigEndian(payload[..2]);
        var declaredLength = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(2, 2));
        var reserved1 = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(4, 2));
        var reserved2 = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(6, 2));
        var apduLength = ResolveApduLength(declaredLength, ethernet.Payload.Length);

        if (apduLength < 0)
            return false;

        frame = new ProcessBusFrame(
            ethernet,
            kind,
            appId,
            declaredLength,
            reserved1,
            reserved2,
            ethernet.Payload.Slice(ProcessBusHeaderLength, apduLength),
            captureTimeUtc ?? DateTime.UtcNow,
            captureTicks ?? Stopwatch.GetTimestamp());

        return true;
    }


    private static bool TryExtractPtpOverUdp(
        EthernetFrame ethernet,
        out ReadOnlyMemory<byte> ptpPayload,
        out ushort declaredLength,
        out string transportText,
        out string networkSourceText,
        out string networkDestinationText)
    {
        ptpPayload = ReadOnlyMemory<byte>.Empty;
        declaredLength = 0;
        transportText = "N/A";
        networkSourceText = "N/A";
        networkDestinationText = "N/A";

        return ethernet.EtherType switch
        {
            EthernetFrame.Ipv4EtherType => TryExtractPtpOverIpv4Udp(
                ethernet.Payload,
                out ptpPayload,
                out declaredLength,
                out transportText,
                out networkSourceText,
                out networkDestinationText),
            EthernetFrame.Ipv6EtherType => TryExtractPtpOverIpv6Udp(
                ethernet.Payload,
                out ptpPayload,
                out declaredLength,
                out transportText,
                out networkSourceText,
                out networkDestinationText),
            _ => false
        };
    }

    private static bool TryExtractPtpOverIpv4Udp(
        ReadOnlyMemory<byte> ipPayload,
        out ReadOnlyMemory<byte> ptpPayload,
        out ushort declaredLength,
        out string transportText,
        out string networkSourceText,
        out string networkDestinationText)
    {
        ptpPayload = ReadOnlyMemory<byte>.Empty;
        declaredLength = 0;
        transportText = "N/A";
        networkSourceText = "N/A";
        networkDestinationText = "N/A";

        if (ipPayload.Length < 28)
            return false;

        var span = ipPayload.Span;
        var version = span[0] >> 4;
        var headerLength = (span[0] & 0x0F) * 4;
        if (version != 4 || headerLength < 20 || ipPayload.Length < headerLength + 8)
            return false;

        if (span[9] != 17) // UDP
            return false;

        // Ignore non-first fragments. PTP/UDP should not be fragmented in normal process-bus timing traffic.
        var flagsAndFragment = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(6, 2));
        if ((flagsAndFragment & 0x1FFF) != 0)
            return false;

        var totalLength = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(2, 2));
        var availableIpLength = Math.Min(totalLength == 0 ? ipPayload.Length : totalLength, ipPayload.Length);
        if (availableIpLength < headerLength + 8)
            return false;

        var udp = span.Slice(headerLength, availableIpLength - headerLength);
        var sourcePort = BinaryPrimitives.ReadUInt16BigEndian(udp[..2]);
        var destinationPort = BinaryPrimitives.ReadUInt16BigEndian(udp.Slice(2, 2));
        if (!IsPtpUdpPort(sourcePort) && !IsPtpUdpPort(destinationPort))
            return false;

        var udpLength = BinaryPrimitives.ReadUInt16BigEndian(udp.Slice(4, 2));
        var availableUdpLength = udpLength >= 8
            ? Math.Min(udpLength, udp.Length)
            : udp.Length;
        if (availableUdpLength < 8)
            return false;

        var payloadLength = availableUdpLength - 8;
        if (payloadLength < 34)
            return false;

        ptpPayload = ipPayload.Slice(headerLength + 8, payloadLength);
        declaredLength = (ushort)Math.Min(ushort.MaxValue, payloadLength);
        transportText = "UDP/IPv4";
        networkSourceText = $"{FormatIpv4(span.Slice(12, 4))}:{sourcePort}";
        networkDestinationText = $"{FormatIpv4(span.Slice(16, 4))}:{destinationPort}";
        return true;
    }

    private static bool TryExtractPtpOverIpv6Udp(
        ReadOnlyMemory<byte> ipPayload,
        out ReadOnlyMemory<byte> ptpPayload,
        out ushort declaredLength,
        out string transportText,
        out string networkSourceText,
        out string networkDestinationText)
    {
        ptpPayload = ReadOnlyMemory<byte>.Empty;
        declaredLength = 0;
        transportText = "N/A";
        networkSourceText = "N/A";
        networkDestinationText = "N/A";

        if (ipPayload.Length < 48)
            return false;

        var span = ipPayload.Span;
        var version = span[0] >> 4;
        if (version != 6 || span[6] != 17) // Direct UDP only; extension headers are not decoded in phase 1.
            return false;

        var udpOffset = 40;
        var udp = span[udpOffset..];
        var sourcePort = BinaryPrimitives.ReadUInt16BigEndian(udp[..2]);
        var destinationPort = BinaryPrimitives.ReadUInt16BigEndian(udp.Slice(2, 2));
        if (!IsPtpUdpPort(sourcePort) && !IsPtpUdpPort(destinationPort))
            return false;

        var udpLength = BinaryPrimitives.ReadUInt16BigEndian(udp.Slice(4, 2));
        var availableUdpLength = udpLength >= 8
            ? Math.Min(udpLength, udp.Length)
            : udp.Length;
        if (availableUdpLength < 8)
            return false;

        var payloadLength = availableUdpLength - 8;
        if (payloadLength < 34)
            return false;

        ptpPayload = ipPayload.Slice(udpOffset + 8, payloadLength);
        declaredLength = (ushort)Math.Min(ushort.MaxValue, payloadLength);
        transportText = "UDP/IPv6";
        networkSourceText = $"[{FormatIpv6(span.Slice(8, 16))}]:{sourcePort}";
        networkDestinationText = $"[{FormatIpv6(span.Slice(24, 16))}]:{destinationPort}";
        return true;
    }

    private static bool IsPtpUdpPort(ushort port) => port is 319 or 320;

    private static string FormatIpv4(ReadOnlySpan<byte> bytes) => new IPAddress(bytes).ToString();

    private static string FormatIpv6(ReadOnlySpan<byte> bytes) => new IPAddress(bytes).ToString();

    private static int ResolveApduLength(ushort declaredLength, int payloadLength)
    {
        var availableApduLength = payloadLength - ProcessBusHeaderLength;

        if (availableApduLength < 0)
            return -1;

        // IEC 61850 process-bus length includes APPID, length and reserved fields.
        var declaredApduLength = declaredLength >= ProcessBusHeaderLength
            ? declaredLength - ProcessBusHeaderLength
            : availableApduLength;

        return Math.Min(declaredApduLength, availableApduLength);
    }
}
