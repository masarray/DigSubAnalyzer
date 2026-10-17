using System.Buffers.Binary;
using ProcessBus.Iec61850.Raw.Protocol;
using ProcessBus.Iec61850.Raw.Ptp;
using Xunit;

namespace ProcessBus.Tests;

public class PtpPacketValidationTests
{
    private static byte[] PtpFrame(ushort length, byte version = 2, byte transportSpecific = 0)
    {
        var message = new byte[64];
        message[0] = (byte)((transportSpecific << 4) | 0x0B); // Announce
        message[1] = version;
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(2, 2), length);
        message[4] = 0; // domain
        return GoldenFrames.EthernetFrame(0x88F7, message);
    }

    [Fact]
    public void PtpAnnounceRequiresValidVersionAndDeclaredMessageLength()
    {
        Assert.True(ProcessBusFrameParser.TryParse(PtpFrame(64, transportSpecific: 1), out var frame));
        Assert.True(PtpPacketParser.TryParse(frame, out var valid));
        Assert.Equal((byte)1, valid.TransportSpecific);
        Assert.Equal((ushort)64, valid.MessageLength);

        Assert.True(ProcessBusFrameParser.TryParse(PtpFrame(80), out var tooLong));
        Assert.False(PtpPacketParser.TryParse(tooLong, out _));

        Assert.True(ProcessBusFrameParser.TryParse(PtpFrame(34), out var shortAnnounce));
        Assert.False(PtpPacketParser.TryParse(shortAnnounce, out _));

        Assert.True(ProcessBusFrameParser.TryParse(PtpFrame(64, version: 1), out var oldVersion));
        Assert.False(PtpPacketParser.TryParse(oldVersion, out _));
    }
}
