using System.Buffers.Binary;
using ProcessBus.Iec61850.Raw.Analysis;
using ProcessBus.Iec61850.Raw.Decoding;
using Xunit;

namespace ProcessBus.Tests;

/// <summary>
/// Regression for the 24 Oct 2026 ARStack ESP32-P4 M3.H 4I+4V reference
/// profile. Independent BER / Ethernet fixture, not a DigSub-generated profile.
/// This proves decoder acceptance, not packet arrival through a real NIC.
/// </summary>
public sealed class ArstackM3hSvInteroperabilityTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void M3hReference_SvWithPriorityTaggedVlanZeroAndUntagged_Decodes(bool tagged)
    {
        var frame = MakeM3hReferenceFrame(tagged, sampleCount: 3999);
        Assert.True(RawProcessBusDecoder.TryDecode(frame, out var result));
        Assert.True(result.HasDecodedApdu);
        Assert.NotNull(result.SampledValues);
        Assert.Equal((ushort)0x4000, result.Frame.AppId);
        if (tagged)
        {
            Assert.Equal((ushort)0, result.Frame.Ethernet.Vlan?.VlanId);
            Assert.Equal((byte)4, result.Frame.Ethernet.Vlan?.PriorityCodePoint);
        }
        else
        {
            Assert.Null(result.Frame.Ethernet.Vlan);
        }

        var asdu = Assert.Single(result.SampledValues!.Asdus);
        Assert.Equal("MU01_SV1", asdu.SvId);
        Assert.Equal((ushort)3999, asdu.SmpCnt);
        Assert.Equal((uint)1, asdu.ConfRev);
        Assert.Equal((byte)2, asdu.SmpSynch);
        Assert.Equal(64, asdu.SamplePayload.Length);
        Assert.Equal(2500, BinaryPrimitives.ReadInt32BigEndian(asdu.SamplePayload.Span[..4]));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32BigEndian(asdu.SamplePayload.Span.Slice(4, 4)));

        var analyzer = new RawProcessBusAnalyzer();
        analyzer.ObserveFrame(frame);
        var snapshot = analyzer.GetAnalyzerSnapshot();
        var stream = Assert.Single(snapshot.Streams);
        Assert.Equal("MU01_SV1", stream.SvId);
    }

    private static byte[] MakeM3hReferenceFrame(bool tagged, ushort sampleCount)
    {
        var values = new[] { 2500, -1250, -1250, 0, 330000, -165000, -165000, 0 };
        var payload = new byte[64];
        for (var i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(i * 8, 4), values[i]);

        // M3.H prebuilt template uses fixed 2-byte smpCnt and 4-byte confRev.
        // Optional gmIdentity uses context-specific tag 9.
        var asdu = GoldenFrames.Tlv(0x30,
            GoldenFrames.Tlv(0x80, GoldenFrames.Ascii("MU01_SV1")),
            GoldenFrames.Tlv(0x82, GoldenFrames.U16(sampleCount)),
            GoldenFrames.Tlv(0x83, new byte[] { 0x00, 0x00, 0x00, 0x01 }),
            GoldenFrames.Tlv(0x85, new byte[] { 0x02 }),
            GoldenFrames.Tlv(0x87, payload),
            GoldenFrames.Tlv(0x89, new byte[] { 0x02, 0x11, 0x22, 0xFF, 0xFE, 0x33, 0x44, 0x55 }));

        var apdu = GoldenFrames.Tlv(0x60,
            GoldenFrames.Tlv(0x80, new byte[] { 0x01 }),
            GoldenFrames.Tlv(0xA2, asdu));

        return GoldenFrames.EthernetFrame(
            GoldenFrames.SvEtherType,
            GoldenFrames.ProcessBusPayload(0x4000, apdu),
            tagged ? (ushort)0x8000 : null);
    }
}
