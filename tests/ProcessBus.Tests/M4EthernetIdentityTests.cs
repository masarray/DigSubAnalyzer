using ProcessBus.Iec61850.Raw.Protocol;
using Xunit;

namespace ProcessBus.Tests;

public sealed class M4EthernetIdentityTests
{
    [Theory]
    [InlineData("001122334455", "FFEE01020304")]
    [InlineData("000000000000", "FFFFFFFFFFFF")]
    [InlineData("0123456789AB", "ABCDEF012345")]
    public void PackedMacFormatterRetainsExactWireIdentity(string source, string destination)
    {
        var frame = new byte[14];
        Convert.FromHexString(destination).CopyTo(frame, 0);
        Convert.FromHexString(source).CopyTo(frame, 6);
        frame[12] = 0x88;
        frame[13] = 0xBA;
        Assert.True(EthernetFrame.TryParse(frame, out var ethernet));
        static string Format(string compact) => string.Join(":",
            Enumerable.Range(0, 6).Select(i => compact.Substring(i * 2, 2)));
        Assert.Equal(Format(destination), ethernet.DestinationMac);
        Assert.Equal(Format(source), ethernet.SourceMac);
    }
}
