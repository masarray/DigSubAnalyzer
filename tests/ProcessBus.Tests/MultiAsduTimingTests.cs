using System.Buffers.Binary;
using ProcessBus.Iec61850.Raw.Analysis;
using Xunit;

namespace ProcessBus.Tests;

public sealed class MultiAsduTimingTests
{
    private static byte[] Frame(ushort first, ushort second)
    {
        static byte[] Asdu(ushort count)
        {
            var payload = new byte[64];
            for (var channel = 0; channel < 8; channel++)
                BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(channel * 8, 4), 1000 + channel);

            return GoldenFrames.Tlv(0x30,
                GoldenFrames.Tlv(0x80, GoldenFrames.Ascii("ARSTACK_2ASDU")),
                GoldenFrames.Tlv(0x82, GoldenFrames.U16(count)),
                GoldenFrames.Tlv(0x83, new byte[] { 1 }),
                GoldenFrames.Tlv(0x85, new byte[] { 0 }),
                GoldenFrames.Tlv(0x87, payload));
        }

        var sav = GoldenFrames.Tlv(0x60,
            GoldenFrames.Tlv(0x80, new byte[] { 2 }),
            GoldenFrames.Tlv(0xA2, Asdu(first), Asdu(second)));
        return GoldenFrames.EthernetFrame(GoldenFrames.SvEtherType,
            GoldenFrames.ProcessBusPayload(0x4000, sav));
    }

    [Fact]
    public void TwoAsdusOneFramePreserveSequenceWithoutInventingPerSampleJitter()
    {
        var analyzer = new RawProcessBusAnalyzer();
        var epoch = new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);
        for (var frame = 0; frame < 200; frame++)
        {
            var sample = frame * 2;
            analyzer.ObserveFrame(Frame((ushort)sample, (ushort)(sample + 1)),
                epoch.AddTicks(frame * 5000),
                (long)(frame * (System.Diagnostics.Stopwatch.Frequency / 2000.0)));
        }

        var snapshot = analyzer.GetAnalyzerSnapshot();
        Assert.Equal(200, snapshot.Diagnostics.TotalPackets); // Ethernet frames, not ASDUs
        Assert.Equal(0, snapshot.Diagnostics.SequenceErrors);
        Assert.Equal(0, snapshot.Diagnostics.MissingSamples);
        Assert.Null(snapshot.Diagnostics.CurrentJitterMicroseconds);
        Assert.Contains("Multi-ASDU", snapshot.Diagnostics.JitterStatusText);
        Assert.NotEmpty(snapshot.Waveform.VoltageSeries);
    }
}
