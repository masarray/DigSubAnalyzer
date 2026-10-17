using System.Buffers.Binary;
using ProcessBus.Iec61850.Raw.Protocol;
using Xunit;

namespace ProcessBus.Tests;

public sealed class PtpUdpLengthTests
{
    private static byte[] ValidPtp()
    {
        var payload = new byte[64];
        payload[0] = 0x0B;
        payload[1] = 2;
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(2, 2), 64);
        return payload;
    }

    private static byte[] Udp(byte[] ptp)
    {
        var udp = new byte[8 + ptp.Length];
        BinaryPrimitives.WriteUInt16BigEndian(udp.AsSpan(0, 2), 319);
        BinaryPrimitives.WriteUInt16BigEndian(udp.AsSpan(2, 2), 320);
        BinaryPrimitives.WriteUInt16BigEndian(udp.AsSpan(4, 2), (ushort)udp.Length);
        ptp.CopyTo(udp, 8);
        return udp;
    }

    private static byte[] Ipv4(byte[] udp)
    {
        var ip = new byte[20 + udp.Length];
        ip[0] = 0x45;
        ip[8] = 64;
        ip[9] = 17;
        BinaryPrimitives.WriteUInt16BigEndian(ip.AsSpan(2, 2), (ushort)ip.Length);
        udp.CopyTo(ip, 20);
        return GoldenFrames.EthernetFrame(0x0800, ip);
    }

    [Fact]
    public void PtpUdpIpv4AcceptsCompletePacketAndRejectsInvalidBoundaries()
    {
        var valid = Ipv4(Udp(ValidPtp()));
        Assert.True(ProcessBusFrameParser.TryParse(valid, out var decoded));
        Assert.Equal(ProcessBusFrameKind.Ptp, decoded.Kind);

        var udpShort = (byte[])valid.Clone();
        BinaryPrimitives.WriteUInt16BigEndian(udpShort.AsSpan(14 + 20 + 4, 2), 0);
        Assert.False(ProcessBusFrameParser.TryParse(udpShort, out _));

        var udpTruncated = (byte[])valid.Clone();
        BinaryPrimitives.WriteUInt16BigEndian(udpTruncated.AsSpan(14 + 20 + 4, 2), 200);
        Assert.False(ProcessBusFrameParser.TryParse(udpTruncated, out _));

        var invalidIp = (byte[])valid.Clone();
        BinaryPrimitives.WriteUInt16BigEndian(invalidIp.AsSpan(14 + 2, 2), 0);
        Assert.False(ProcessBusFrameParser.TryParse(invalidIp, out _));

        var fragmented = (byte[])valid.Clone();
        BinaryPrimitives.WriteUInt16BigEndian(fragmented.AsSpan(14 + 6, 2), 0x2000);
        Assert.False(ProcessBusFrameParser.TryParse(fragmented, out _));
    }

    [Fact]
    public void PtpUdpIpv6RequiresMatchingIpv6AndUdpLengths()
    {
        var udp = Udp(ValidPtp());
        var ipv6 = new byte[40 + udp.Length];
        ipv6[0] = 0x60;
        ipv6[6] = 17;
        BinaryPrimitives.WriteUInt16BigEndian(ipv6.AsSpan(4, 2), (ushort)udp.Length);
        udp.CopyTo(ipv6, 40);
        var valid = GoldenFrames.EthernetFrame(0x86DD, ipv6);
        Assert.True(ProcessBusFrameParser.TryParse(valid, out var frame));
        Assert.Equal(ProcessBusFrameKind.Ptp, frame.Kind);

        var bad = (byte[])valid.Clone();
        BinaryPrimitives.WriteUInt16BigEndian(bad.AsSpan(14 + 4, 2), 200);
        Assert.False(ProcessBusFrameParser.TryParse(bad, out _));
    }
}
