using System.Buffers.Binary;
using ProcessBus.Iec61850.Raw.Protocol;
using Xunit;

namespace ProcessBus.Tests;

public sealed class M4ProcessBusLengthTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeclaredApduLongerThanWireIsRejectedInsteadOfTruncated(bool goose)
    {
        var original = goose ? GoldenFrames.GooseFrame() : GoldenFrames.SvFrame();
        Assert.True(ProcessBusFrameParser.TryParse(original, out _));

        var bad = (byte[])original.Clone();
        BinaryPrimitives.WriteUInt16BigEndian(bad.AsSpan(14 + 2, 2),
            (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bad.AsSpan(14 + 2, 2)) + 10));
        Assert.False(ProcessBusFrameParser.TryParse(bad, out _));

        var tooShort = (byte[])original.Clone();
        BinaryPrimitives.WriteUInt16BigEndian(tooShort.AsSpan(14 + 2, 2), 7);
        Assert.False(ProcessBusFrameParser.TryParse(tooShort, out _));
    }
}
