using ProcessBus.Iec61850.Raw.Replay;
using System.Buffers.Binary;
using Xunit;

namespace ProcessBus.Tests;

public sealed class PcapFormatVariantTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [Trait("Category", "RuntimeArchitecture")]
    public void Reader_AcceptsAllClassicEndianAndResolutionVariants(bool littleEndian, bool nanosecond)
    {
        var fraction = nanosecond ? 123_456_700u : 123_456u;
        using var pcap = BuildSingleFramePcap(
            GoldenFrames.SvFrame(),
            littleEndian,
            nanosecond,
            seconds: 1_700_000_000u,
            fraction: fraction);

        var frame = new PcapReplayReader().Read(pcap).Single();
        var expected = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000).UtcDateTime.AddTicks(
            nanosecond ? fraction / 100u : fraction * 10u);

        Assert.Equal(expected, frame.CaptureTimeUtc);
        Assert.Equal(GoldenFrames.SvFrame(), frame.FrameBytes.ToArray());
    }

    [Theory]
    [InlineData(false, 1_000_000u)]
    [InlineData(true, 1_000_000_000u)]
    [Trait("Category", "RuntimeArchitecture")]
    public void Reader_RejectsInvalidTimestampFraction(bool nanosecond, uint fraction)
    {
        using var pcap = BuildSingleFramePcap(
            GoldenFrames.SvFrame(),
            littleEndian: true,
            nanosecond: nanosecond,
            seconds: 1_700_000_000u,
            fraction: fraction);

        var error = Assert.Throws<InvalidDataException>(() => new PcapReplayReader().Read(pcap).ToArray());
        Assert.Contains("timestamp fraction", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "RuntimeArchitecture")]
    public void Reader_EnforcesConfiguredFrameBoundary()
    {
        var oversized = new byte[101];
        using var pcap = BuildSingleFramePcap(
            oversized,
            littleEndian: true,
            nanosecond: false,
            seconds: 1_700_000_000u,
            fraction: 0);

        var reader = new PcapReplayReader(maximumFrameBytes: 100);
        var error = Assert.Throws<InvalidDataException>(() => reader.Read(pcap).ToArray());
        Assert.Contains("frame boundary", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static MemoryStream BuildSingleFramePcap(
        byte[] frame,
        bool littleEndian,
        bool nanosecond,
        uint seconds,
        uint fraction)
    {
        var stream = new MemoryStream();
        var global = new byte[24];

        var magic = (littleEndian, nanosecond) switch
        {
            (true, false) => new byte[] { 0xD4, 0xC3, 0xB2, 0xA1 },
            (false, false) => new byte[] { 0xA1, 0xB2, 0xC3, 0xD4 },
            (true, true) => new byte[] { 0x4D, 0x3C, 0xB2, 0xA1 },
            _ => new byte[] { 0xA1, 0xB2, 0x3C, 0x4D }
        };
        magic.CopyTo(global, 0);
        WriteUInt16(global.AsSpan(4, 2), 2, littleEndian);
        WriteUInt16(global.AsSpan(6, 2), 4, littleEndian);
        WriteUInt32(global.AsSpan(16, 4), 65_535, littleEndian);
        WriteUInt32(global.AsSpan(20, 4), 1, littleEndian);
        stream.Write(global);

        var record = new byte[16];
        WriteUInt32(record.AsSpan(0, 4), seconds, littleEndian);
        WriteUInt32(record.AsSpan(4, 4), fraction, littleEndian);
        WriteUInt32(record.AsSpan(8, 4), checked((uint)frame.Length), littleEndian);
        WriteUInt32(record.AsSpan(12, 4), checked((uint)frame.Length), littleEndian);
        stream.Write(record);
        stream.Write(frame);
        stream.Position = 0;
        return stream;
    }

    private static void WriteUInt16(Span<byte> destination, ushort value, bool littleEndian)
    {
        if (littleEndian)
            BinaryPrimitives.WriteUInt16LittleEndian(destination, value);
        else
            BinaryPrimitives.WriteUInt16BigEndian(destination, value);
    }

    private static void WriteUInt32(Span<byte> destination, uint value, bool littleEndian)
    {
        if (littleEndian)
            BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
        else
            BinaryPrimitives.WriteUInt32BigEndian(destination, value);
    }
}
