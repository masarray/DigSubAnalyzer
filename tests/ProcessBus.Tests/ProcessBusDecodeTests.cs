using ProcessBus.Iec61850.Raw.Decoding;
using ProcessBus.Iec61850.Raw.Protocol;
using Xunit;

namespace ProcessBus.Tests;

public class ProcessBusFrameParserTests
{
    [Fact]
    public void TryParse_UntaggedSvFrame_ReadsSessionHeader()
    {
        var frame = GoldenFrames.SvFrame(appId: 0x4000);

        Assert.True(ProcessBusFrameParser.TryParse(frame, out var parsed));
        Assert.Equal(ProcessBusFrameKind.SampledValues, parsed.Kind);
        Assert.Equal(0x4000, parsed.AppId);
        Assert.Equal(8 + GoldenFrames.SvApdu().Length, parsed.DeclaredLength);
        Assert.Equal(GoldenFrames.SvApdu().Length, parsed.Apdu.Length);
        Assert.Null(parsed.Ethernet.Vlan);
    }

    [Fact]
    public void TryParse_VlanTaggedSvFrame_ReadsVlanAndDecodesNormally()
    {
        var frame = GoldenFrames.SvFrame(appId: 0x4001, vlanTci: 0x8064);

        Assert.True(ProcessBusFrameParser.TryParse(frame, out var parsed));
        Assert.Equal(ProcessBusFrameKind.SampledValues, parsed.Kind);
        Assert.NotNull(parsed.Ethernet.Vlan);
        Assert.Equal(100, parsed.Ethernet.Vlan!.Value.VlanId);
        Assert.Equal(4, parsed.Ethernet.Vlan!.Value.PriorityCodePoint);
    }

    [Fact]
    public void TryParse_RuntFrame_ReturnsFalse()
    {
        Assert.False(ProcessBusFrameParser.TryParse(new byte[10], out _));
    }

    [Fact]
    public void TryParse_DeclaredLengthBeyondCapture_TruncatesApduToAvailable()
    {
        var apdu = GoldenFrames.SvApdu();
        var payload = GoldenFrames.Concat(
            GoldenFrames.U16(0x4000),
            GoldenFrames.U16((ushort)(8 + apdu.Length + 500)),
            GoldenFrames.U16(0),
            GoldenFrames.U16(0),
            apdu);
        var frame = GoldenFrames.EthernetFrame(GoldenFrames.SvEtherType, payload);

        Assert.True(ProcessBusFrameParser.TryParse(frame, out var parsed));
        Assert.Equal(apdu.Length, parsed.Apdu.Length);
    }
}

public class SampledValueDecodeTests
{
    [Fact]
    public void TryDecode_GoldenSvFrame_DecodesAsduFields()
    {
        Assert.True(RawProcessBusDecoder.TryDecode(GoldenFrames.SvFrame(), out var result));

        Assert.NotNull(result.SampledValues);
        Assert.True(result.HasDecodedApdu);

        var asdu = Assert.Single(result.SampledValues!.Asdus);
        Assert.Equal("MU01", asdu.SvId);
        Assert.Equal((ushort)1, asdu.SmpCnt);
        Assert.Equal(1u, asdu.ConfRev);
        Assert.Equal((byte)2, asdu.SmpSynch);
        Assert.Equal(8, asdu.SamplePayload.Length);
    }

    [Fact]
    public void TryDecode_SvEtherTypeWithGarbageApdu_ParsesFrameButNotApdu()
    {
        var garbage = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
        var frame = GoldenFrames.EthernetFrame(
            GoldenFrames.SvEtherType,
            GoldenFrames.ProcessBusPayload(0x4000, garbage));

        Assert.True(RawProcessBusDecoder.TryDecode(frame, out var result));
        Assert.Null(result.SampledValues);
        Assert.False(result.HasDecodedApdu);
    }
}

public class GooseDecodeTests
{
    [Fact]
    public void TryDecode_GoldenGooseFrame_DecodesControlBlockFields()
    {
        Assert.True(RawProcessBusDecoder.TryDecode(GoldenFrames.GooseFrame(), out var result));

        Assert.NotNull(result.Goose);
        Assert.Equal("IED1LD0/LLN0$GO$GoCb01", result.Goose!.GoCbRef);
        Assert.Equal("IED1LD0/LLN0$DataSet01", result.Goose!.DataSet);
        Assert.Equal("GO_TRIP", result.Goose!.GoId);
        Assert.Equal(3u, result.Goose!.StNum);
        Assert.Equal(7u, result.Goose!.SqNum);
        Assert.Equal(1000u, result.Goose!.TimeAllowedToLiveMilliseconds);
        Assert.Equal(1u, result.Goose!.ConfRev);
        Assert.False(result.Goose!.Test);
    }
}
