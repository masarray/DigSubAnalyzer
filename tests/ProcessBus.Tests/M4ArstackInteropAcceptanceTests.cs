using System.Buffers.Binary;
using ProcessBus.Core.Models.Scl;
using ProcessBus.Iec61850.Raw.Interop;
using Xunit;

namespace ProcessBus.Tests;

public sealed class M4ArstackInteropAcceptanceTests
{
    private static readonly DateTime Epoch = new(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);
    private static readonly int[] Values = [100, -50, -50, 0, 800, -400, -400, 0];

    private static SclSvStreamModel Expected(ushort app = 0x4000, string vlan = "0064",
        string dest = "01-0C-CD-04-00-01") => new()
    {
        ControlBlockReference = "ARSTACK/LLN0$SV$SMVCB01",
        SvId = "ARSTACK_SV01",
        AppId = $"0x{app:X4}",
        DestinationMac = dest,
        VlanId = vlan, VlanPriority = "4",
        ConfRev = 1, NofAsdu = 1
    };

    private static byte[] Canonical(ushort smpCnt, ushort tci = 0x8064)
        => GoldenFrames.EthernetFrame(GoldenFrames.SvEtherType,
            GoldenFrames.ProcessBusPayload(0x4000,
                GoldenFrames.SvApduWithChannelSamples(smpCnt, Values, "ARSTACK_SV01")),
            tci);

    private static byte[] Mirror(ushort smpCnt)
    {
        var result = GoldenFrames.SvFrameWithChannelSamples(smpCnt, Values, "AR_DIAG_SV1", 0x4F01);
        Array.Fill(result, (byte)0xFF, 0, 6);
        return result;
    }

    private static byte[] Ptp(byte type, ushort sequence, ushort flags = 0, byte domain = 0)
    {
        var len = type == 11 ? 64 : 44;
        var data = new byte[len];
        data[0] = type;
        data[1] = 2;
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(2, 2), (ushort)len);
        data[4] = domain;
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(6, 2), flags);
        data[20] = 0xAA; data[27] = 0x01;
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(28, 2), 1);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(30, 2), sequence);
        if (type == 11) { data[48] = 248; data[49] = 0xFE; data[53] = 0xAA; }
        return GoldenFrames.EthernetFrame(0x88F7, data);
    }

    private static MemoryStream Pcap(IEnumerable<(DateTime Timestamp, byte[] Data)> packets)
    {
        var output = new MemoryStream();
        var global = new byte[24];
        global[0] = 0xD4; global[1] = 0xC3; global[2] = 0xB2; global[3] = 0xA1;
        BinaryPrimitives.WriteUInt16LittleEndian(global.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(global.AsSpan(6), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(global.AsSpan(16), 65535);
        BinaryPrimitives.WriteUInt32LittleEndian(global.AsSpan(20), 1);
        output.Write(global);
        foreach (var (timestamp, bytes) in packets)
        {
            var seconds = new DateTimeOffset(timestamp).ToUnixTimeSeconds();
            var secondStart = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
            var micros = (timestamp - secondStart).Ticks / 10;
            var header = new byte[16];
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0), checked((uint)seconds));
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), checked((uint)micros));
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), (uint)bytes.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12), (uint)bytes.Length);
            output.Write(header);
            output.Write(bytes);
        }
        output.Position = 0;
        return output;
    }

    [Fact]
    [Trait("Category", "M4Interop")]
    public void CanonicalTaggedAndMirrorWithConcurrentPtpStaySeparateAndReportObservedSclFields()
    {
        var records = new List<(DateTime, byte[])>();
        for (ushort i = 0; i < 240; i++)
        {
            var when = Epoch.AddTicks(i * 2500);
            records.Add((when, Canonical(i)));
            records.Add((when, Mirror(i)));
            if (i == 0)
                records.Add((when, Ptp(11, 10)));  // Announce, L2 domain 0
            if (i % 80 == 0)
            {
                records.Add((when, Ptp(0, i, 0x0200))); // two-step Sync
                records.Add((when.AddTicks(10), Ptp(8, i))); // Follow_Up, same source/sequence
            }
        }

        using var capture = Pcap(records.OrderBy(x => x.Item1));
        var result = new PcapInteropInspector().Inspect(capture, [Expected()]);
        Assert.Equal(2, result.SvStreams.Count);
        Assert.Equal(240 * 2, result.SvFrames);
        Assert.Equal(3, result.MatchedTwoStep);
        Assert.Equal(0, result.UnmatchedFollowUp);
        Assert.Equal("WIRE_FIELDS_MATCH", Assert.Single(result.Comparisons).Status);
        Assert.Equal((ushort)100, result.SvStreams.Single(x => x.SvId == "ARSTACK_SV01").VlanId);
        Assert.Equal((byte)4, result.SvStreams.Single(x => x.SvId == "ARSTACK_SV01").VlanPriority);
        Assert.True(result.SvStreams.Single(x => x.SvId == "AR_DIAG_SV1").IsArstackDiagnosticMirror);
        Assert.False(result.IsPhysicallyAccepted);
        Assert.Contains("IED", result.EvidenceLimitations);
    }

    [Fact]
    [Trait("Category", "M4Interop")]
    public void MirrorOnlyMustNeverClaimCanonicalOrVlanCompatibility()
    {
        using var capture = Pcap(Enumerable.Range(0, 160)
            .Select(i => (Epoch.AddTicks(i * 2500), Mirror((ushort)i))));
        var result = new PcapInteropInspector().Inspect(capture, [Expected()]);
        Assert.Equal("MIRROR_ONLY", Assert.Single(result.Comparisons).Status);
        Assert.Null(result.SvStreams[0].VlanId);
        Assert.False(result.IsPhysicallyAccepted);
    }

    [Theory]
    [InlineData("0065", "VLAN")]
    [InlineData("0000", "VLAN")]
    [Trait("Category", "M4Interop")]
    public void ConflictingSclVlanIsNotSilentlyOverwritten(string vlan, string field)
    {
        using var capture = Pcap([(Epoch, Canonical(10))]);
        var result = new PcapInteropInspector().Inspect(capture, [Expected(vlan: vlan)]);
        Assert.Equal("CONFLICT", Assert.Single(result.Comparisons).Status);
        Assert.Contains(field, result.Comparisons[0].Explanation);
    }

    [Fact]
    [Trait("Category", "M4Interop")]
    public void Observed4000CounterResetRemainsCandidateNotAutomaticModulusProof()
    {
        var records = Enumerable.Range(3970, 80).Select(i =>
            (Epoch.AddTicks((i - 3970) * 2500), Canonical((ushort)(i % 4000))));
        using var capture = Pcap(records);
        var evidence = Assert.Single(new PcapInteropInspector().Inspect(capture).SvStreams);
        Assert.Equal(1, evidence.Candidate4000Wraps);
        Assert.Equal(0, evidence.NonUnitCounterSteps);
    }

    [Fact]
    [Trait("Category", "M4Interop")]
    public void SingleObservedAsduDoesNotClaimSequenceEvidence()
    {
        using var capture = Pcap([(Epoch, Canonical(1))]);
        var comparison = Assert.Single(new PcapInteropInspector().Inspect(capture, [Expected()]).Comparisons);
        Assert.Equal("LIMITED_EVIDENCE", comparison.Status);
    }

    [Fact]
    [Trait("Category", "M4Interop")]
    public void RepeatedAndSkippedCountersMustNotYieldCleanWireMatch()
    {
        using var capture = Pcap(new[]
        {
            (Epoch, Canonical(1)),
            (Epoch.AddTicks(2500), Canonical(1)),
            (Epoch.AddTicks(5000), Canonical(4))
        });
        var result = new PcapInteropInspector().Inspect(capture, [Expected()]);
        Assert.Equal("SEQUENCE_WARNING", result.Comparisons[0].Status);
        Assert.Equal(1, result.SvStreams[0].DuplicateCounters);
        Assert.Equal(1, result.SvStreams[0].NonUnitCounterSteps);
    }

    [Fact]
    [Trait("Category", "M4Interop")]
    public void TwoPublishersWithSameSclIdentityRequireOperatorResolution()
    {
        var other = Canonical(2);
        other[6] = 0x99; // different source MAC; same SCL svID/APPID/destination
        using var capture = Pcap(new[]
        {
            (Epoch, Canonical(1)),
            (Epoch.AddTicks(100), other),
            (Epoch.AddTicks(2500), Canonical(2)),
            (Epoch.AddTicks(2600), Canonical(3) is { } canonical ? WithOtherSource(canonical) : throw new Exception())
        });
        var result = new PcapInteropInspector().Inspect(capture, [Expected()]);
        Assert.Equal(2, result.SvStreams.Count);
        Assert.Equal("AMBIGUOUS_WIRE", result.Comparisons[0].Status);
    }

    private static byte[] WithOtherSource(byte[] frame)
    {
        frame[6] = 0x99;
        return frame;
    }

    [Fact]
    [Trait("Category", "M4Interop")]
    public void ReplayFrameLimitDowngradesOtherwiseCleanComparison()
    {
        using var capture = Pcap(Enumerable.Range(0, 10)
            .Select(i => (Epoch.AddTicks(i * 2500), Canonical((ushort)i))));
        var result = new PcapInteropInspector().Inspect(capture, [Expected()], maximumFrames: 2);
        Assert.True(result.FrameLimitReached);
        Assert.Equal(2, result.FramesRead);
        Assert.Equal("CAPTURE_INCOMPLETE", result.Comparisons[0].Status);
    }

    [Fact]
    [Trait("Category", "M4Interop")]
    public void InvalidCaptureDoesNotBecomePositivePtpEvidence()
    {
        var malformed = Canonical(0);
        BinaryPrimitives.WriteUInt16BigEndian(malformed.AsSpan(18 + 2, 2), 0x7FFF);
        using var capture = Pcap([(Epoch, malformed)]);
        var result = new PcapInteropInspector().Inspect(capture, [Expected()]);
        Assert.Equal(0, result.SvFrames);
        Assert.Equal(1, result.RejectedOrUnsupportedFrames);
        Assert.Equal("NOT_OBSERVED", result.Comparisons[0].Status);
    }
}
