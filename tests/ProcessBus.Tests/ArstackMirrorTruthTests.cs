using ProcessBus.Iec61850.Raw.Analysis;
using Xunit;

namespace ProcessBus.Tests;

public sealed class ArstackMirrorTruthTests
{
    [Fact]
    public void BroadcastDiagnosticMirrorIsNotMisrepresentedAsCanonicalSclStream()
    {
        var analyzer = new RawProcessBusAnalyzer();
        var values = new int[8];
        for (var i = 0; i < 400; i++)
        {
            var frame = GoldenFrames.SvFrameWithChannelSamples(
                (ushort)i, values, "AR_DIAG_SV1", 0x4F01);
            Array.Fill(frame, (byte)0xFF, 0, 6); // ARStack lab mirror broadcast
            analyzer.ObserveFrame(frame);
        }
        var evidence = analyzer.GetAnalyzerSnapshot();
        Assert.Contains("diagnostic mirror", evidence.Streams[0].IssueSummaryText,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not canonical", evidence.SelectedStreamDetails!.SampleValueMappingText,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("canonical stream requires separate capture",
            evidence.SelectedStreamDetails.PacketEvidenceText,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CanonicalMulticastIdentityIsKeptSeparateFromDiagnosticMirror()
    {
        var analyzer = new RawProcessBusAnalyzer();
        var values = new int[8];
        analyzer.ObserveFrame(GoldenFrames.SvFrameWithChannelSamples(
            0, values, "ARSTACK_SV01", 0x4000));
        var diagnostic = GoldenFrames.SvFrameWithChannelSamples(0, values, "AR_DIAG_SV1", 0x4F01);
        Array.Fill(diagnostic, (byte)0xFF, 0, 6);
        analyzer.ObserveFrame(diagnostic);
        var snapshots = analyzer.GetAnalyzerSnapshot();
        Assert.Equal(2, snapshots.Streams.Count);
        Assert.Contains(snapshots.Streams, x => x.SvId == "ARSTACK_SV01" &&
            !x.IssueSummaryText.Contains("mirror", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(snapshots.Streams, x => x.SvId == "AR_DIAG_SV1" &&
            x.IssueSummaryText.Contains("mirror", StringComparison.OrdinalIgnoreCase));
    }
}
