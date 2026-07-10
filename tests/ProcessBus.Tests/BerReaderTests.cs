using ProcessBus.Iec61850.Raw.Asn1;
using Xunit;

namespace ProcessBus.Tests;

public class BerReaderTests
{
    [Fact]
    public void TryReadTlv_ShortFormLength_ReadsTagClassAndValue()
    {
        var bytes = GoldenFrames.Tlv(0x80, GoldenFrames.Ascii("MU01"));
        var offset = 0;

        Assert.True(BerReader.TryReadTlv(bytes, ref offset, out var tlv));
        Assert.Equal(BerClass.ContextSpecific, tlv.Class);
        Assert.False(tlv.Constructed);
        Assert.Equal(0, tlv.TagNumber);
        Assert.Equal("MU01", BerReader.ReadString(tlv));
        Assert.Equal(bytes.Length, offset);
    }

    [Fact]
    public void TryReadTlv_LongFormLength_ReadsFullValue()
    {
        var value = new byte[200];
        var bytes = GoldenFrames.Tlv(0x04, value);
        var offset = 0;

        Assert.True(BerReader.TryReadTlv(bytes, ref offset, out var tlv));
        Assert.Equal(200, tlv.Value.Length);
        Assert.Equal(bytes.Length, offset);
    }

    [Fact]
    public void TryReadTlv_TruncatedValue_ReturnsFalse()
    {
        var bytes = new byte[] { 0x80, 0x05, 0x41, 0x42 };
        var offset = 0;

        Assert.False(BerReader.TryReadTlv(bytes, ref offset, out _));
    }

    [Fact]
    public void TryReadTlv_HighTagNumberForm_ReturnsFalse()
    {
        var bytes = new byte[] { 0x1F, 0x81, 0x00, 0x00 };
        var offset = 0;

        Assert.False(BerReader.TryReadTlv(bytes, ref offset, out _));
    }

    [Fact]
    public void TryReadTlv_HostileLengthNearIntMax_ReturnsFalseWithoutThrowing()
    {
        var bytes = new byte[] { 0x30, 0x02, 0x01, 0x00, 0x04, 0x84, 0x7F, 0xFF, 0xFF, 0xFF, 0x00, 0x00 };
        var offset = 0;

        Assert.True(BerReader.TryReadTlv(bytes, ref offset, out _));
        Assert.False(BerReader.TryReadTlv(bytes, ref offset, out _));
    }

    [Fact]
    public void TryReadTlv_NegativeFourByteLength_ReturnsFalse()
    {
        var bytes = new byte[] { 0x04, 0x84, 0xFF, 0xFF, 0xFF, 0xF8, 0x00 };
        var offset = 0;

        Assert.False(BerReader.TryReadTlv(bytes, ref offset, out _));
    }

    [Theory]
    [InlineData(new byte[] { 0x05 }, 5u)]
    [InlineData(new byte[] { 0x01, 0x00 }, 256u)]
    [InlineData(new byte[] { 0x01, 0x00, 0x00 }, 65536u)]
    [InlineData(new byte[] { 0x01, 0x00, 0x00, 0x00 }, 16777216u)]
    public void ReadUnsignedInteger_SupportsOneToFourBytes(byte[] value, uint expected)
    {
        var bytes = GoldenFrames.Tlv(0x85, value);
        var offset = 0;

        Assert.True(BerReader.TryReadTlv(bytes, ref offset, out var tlv));
        Assert.Equal(expected, BerReader.ReadUnsignedInteger(tlv));
    }

    [Fact]
    public void ReadChildren_IteratesSiblingTlvs()
    {
        var bytes = GoldenFrames.Concat(
            GoldenFrames.Tlv(0x80, GoldenFrames.Ascii("A")),
            GoldenFrames.Tlv(0x81, new byte[] { 0x01 }),
            GoldenFrames.Tlv(0x82, GoldenFrames.Ascii("BC")));

        var children = BerReader.ReadChildren(bytes).ToList();

        Assert.Equal(3, children.Count);
        Assert.Equal(0, children[0].TagNumber);
        Assert.Equal(1, children[1].TagNumber);
        Assert.Equal(2, children[2].TagNumber);
        Assert.Equal("BC", BerReader.ReadString(children[2]));
    }
}
