using ProcessBus.Iec61850.Raw.Replay;
using System.Buffers.Binary;
using Xunit;

namespace ProcessBus.Tests;

public sealed class PcapReplayRuntimeTests
{
    private const int SamplesPerCycle = 80;

    [Fact]
    [Trait("Category", "RuntimeArchitecture")]
    public void ClassicPcap_ReplaysThroughLiveDecoderAndPublishesCoherentSnapshot()
    {
        var start = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);
        using var pcap = BuildPcap(Enumerable.Range(0, SamplesPerCycle * 2)
            .Select(index => (
                start.AddTicks(index * TimeSpan.TicksPerMillisecond / 4),
                GoldenFrames.SvFrameWithChannelSamples(
                    (ushort)index,
                    ChannelValuesAt(index, 1200),
                    "MU_REPLAY",
                    0x4000))));

        var session = new ProcessBusReplaySession();
        var result = session.Replay(pcap);

        Assert.Equal(SamplesPerCycle * 2, result.FramesRead);
        Assert.Equal(SamplesPerCycle * 2, result.SvFrames);
        Assert.Equal(0, result.DecodeErrors);
        Assert.Equal(1, result.RuntimeSnapshot.Generation);
        Assert.Equal("MU_REPLAY", result.RuntimeSnapshot.Identity?.SvId);
        Assert.Equal(SamplesPerCycle * 2, Channel(result.RuntimeSnapshot, "Ia").Samples.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(39.75), result.CaptureDuration);
    }

    [Fact]
    [Trait("Category", "RuntimeArchitecture")]
    public void PublishedGeneration_RemainsImmutableAfterAnalyzerAdvances()
    {
        var start = new DateTime(2026, 7, 11, 1, 0, 0, DateTimeKind.Utc);
        using var pcap = BuildPcap(Enumerable.Range(0, SamplesPerCycle * 2)
            .Select(index => (
                start.AddTicks(index * TimeSpan.TicksPerMillisecond / 4),
                GoldenFrames.SvFrameWithChannelSamples(
                    (ushort)index,
                    ChannelValuesAt(index, 1000),
                    "MU_IMMUTABLE",
                    0x4100))));

        var session = new ProcessBusReplaySession();
        var first = session.Replay(pcap).RuntimeSnapshot;
        var firstMaximum = Channel(first, "Ia").Samples.Max();

        for (var index = SamplesPerCycle * 2; index < SamplesPerCycle * 4; index++)
        {
            session.Analyzer.ObserveFrame(GoldenFrames.SvFrameWithChannelSamples(
                (ushort)index,
                ChannelValuesAt(index, 3000),
                "MU_IMMUTABLE",
                0x4100));
        }

        var second = session.PublishSelectedStreamSnapshot();

        Assert.Equal(1, first.Generation);
        Assert.Equal(2, second.Generation);
        Assert.InRange(firstMaximum, 980, 1020);
        Assert.InRange(Channel(first, "Ia").Samples.Max(), 980, 1020);
        Assert.InRange(Channel(second, "Ia").Samples.Max(), 2940, 3060);
    }

    [Fact]
    [Trait("Category", "RuntimeArchitecture")]
    public void ThreeInterleavedReplayStreams_RemainIsolatedAcrossPublishedSnapshots()
    {
        var start = new DateTime(2026, 7, 11, 2, 0, 0, DateTimeKind.Utc);
        var frames = new List<(DateTime TimestampUtc, byte[] Frame)>();

        for (var sample = 0; sample < SamplesPerCycle * 2; sample++)
        {
            for (var streamIndex = 0; streamIndex < 3; streamIndex++)
            {
                frames.Add((
                    start.AddTicks(frames.Count * TimeSpan.TicksPerMillisecond / 12),
                    GoldenFrames.SvFrameWithChannelSamples(
                        (ushort)sample,
                        ChannelValuesAt(sample, 700 * (streamIndex + 1)),
                        $"MU_REPLAY_{streamIndex + 1}",
                        (ushort)(0x4200 + streamIndex))));
            }
        }

        using var pcap = BuildPcap(frames);
        var session = new ProcessBusReplaySession();
        var result = session.Replay(pcap);

        Assert.Equal(3, result.AnalyzerSnapshot.Streams.Count);

        for (var streamIndex = 0; streamIndex < 3; streamIndex++)
        {
            var svId = $"MU_REPLAY_{streamIndex + 1}";
            var stream = session.Analyzer.GetAnalyzerSnapshot().Streams.Single(item => item.SvId == svId);
            session.Analyzer.SelectStream(stream.StreamId);
            var snapshot = session.PublishSelectedStreamSnapshot();
            var expectedPeak = 700.0 * (streamIndex + 1);

            Assert.Equal(svId, snapshot.Identity?.SvId);
            Assert.InRange(Channel(snapshot, "Ia").Samples.Max(), expectedPeak * 0.98, expectedPeak * 1.02);
        }
    }

    [Fact]
    [Trait("Category", "RuntimeArchitecture")]
    public void Reader_RejectsUnsupportedLinkType()
    {
        using var pcap = BuildPcap(Array.Empty<(DateTime, byte[])>(), linkType: 101);
        var reader = new PcapReplayReader();

        var error = Assert.Throws<InvalidDataException>(() => reader.Read(pcap).ToArray());
        Assert.Contains("Ethernet link type 1", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "RuntimeArchitecture")]
    public void Reader_RejectsTruncatedFrameRecord()
    {
        var start = new DateTime(2026, 7, 11, 3, 0, 0, DateTimeKind.Utc);
        using var complete = BuildPcap(new[] { (start, GoldenFrames.SvFrame()) });
        var bytes = complete.ToArray()[..^3];
        using var truncated = new MemoryStream(bytes, writable: false);
        var reader = new PcapReplayReader();

        Assert.Throws<EndOfStreamException>(() => reader.Read(truncated).ToArray());
    }

    private static ProcessBus.Iec61850.Raw.Runtime.SvRuntimeChannelSnapshot Channel(
        ProcessBus.Iec61850.Raw.Runtime.SvRuntimeSnapshot snapshot,
        string name)
        => snapshot.Channels.Single(channel => string.Equals(channel.Name, name, StringComparison.OrdinalIgnoreCase));

    private static int[] ChannelValuesAt(int sampleIndex, int currentPeak)
    {
        var phase = 2.0 * Math.PI * (sampleIndex % SamplesPerCycle) / SamplesPerCycle;
        var voltagePeak = currentPeak * 8;

        return
        [
            (int)Math.Round(Math.Sin(phase) * currentPeak),
            (int)Math.Round(Math.Sin(phase - (2.0 * Math.PI / 3.0)) * currentPeak),
            (int)Math.Round(Math.Sin(phase + (2.0 * Math.PI / 3.0)) * currentPeak),
            0,
            (int)Math.Round(Math.Sin(phase) * voltagePeak),
            (int)Math.Round(Math.Sin(phase - (2.0 * Math.PI / 3.0)) * voltagePeak),
            (int)Math.Round(Math.Sin(phase + (2.0 * Math.PI / 3.0)) * voltagePeak),
            0
        ];
    }

    private static MemoryStream BuildPcap(
        IEnumerable<(DateTime TimestampUtc, byte[] Frame)> frames,
        uint linkType = 1)
    {
        var stream = new MemoryStream();
        var globalHeader = new byte[24];
        globalHeader[0] = 0xD4;
        globalHeader[1] = 0xC3;
        globalHeader[2] = 0xB2;
        globalHeader[3] = 0xA1;
        BinaryPrimitives.WriteUInt16LittleEndian(globalHeader.AsSpan(4, 2), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(globalHeader.AsSpan(6, 2), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(globalHeader.AsSpan(16, 4), 65_535);
        BinaryPrimitives.WriteUInt32LittleEndian(globalHeader.AsSpan(20, 4), linkType);
        stream.Write(globalHeader);

        foreach (var (timestampUtc, frame) in frames)
        {
            var timestamp = new DateTimeOffset(DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc));
            var seconds = timestamp.ToUnixTimeSeconds();
            var secondStart = DateTimeOffset.FromUnixTimeSeconds(seconds);
            var microseconds = (timestamp - secondStart).Ticks / 10;

            var recordHeader = new byte[16];
            BinaryPrimitives.WriteUInt32LittleEndian(recordHeader.AsSpan(0, 4), checked((uint)seconds));
            BinaryPrimitives.WriteUInt32LittleEndian(recordHeader.AsSpan(4, 4), checked((uint)microseconds));
            BinaryPrimitives.WriteUInt32LittleEndian(recordHeader.AsSpan(8, 4), checked((uint)frame.Length));
            BinaryPrimitives.WriteUInt32LittleEndian(recordHeader.AsSpan(12, 4), checked((uint)frame.Length));
            stream.Write(recordHeader);
            stream.Write(frame);
        }

        stream.Position = 0;
        return stream;
    }
}
