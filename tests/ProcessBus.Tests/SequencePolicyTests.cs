using ProcessBus.Iec61850.Raw.Analysis;
using Xunit;

namespace ProcessBus.Tests;

public sealed class SequencePolicyTests
{
    [Fact]
    public void SamplesPerPeriodMetadataMustNotInventCounterModulus()
    {
        var analyzer = new RawProcessBusAnalyzer();
        var values = new[] { 10, 20, 30, 0, 100, 200, 300, 0 };
        for (ushort count = 0; count < 80; count++)
        {
            analyzer.ObserveFrame(GoldenFrames.SvFrameWithChannelSamples(
                count, values, smpRate: 80, smpMod: 2));
        }

        // Counter suddenly starts over; 80 samples/period does not authorize an
        // 80-count modulo. Do not quietly classify the restart as a valid wrap.
        analyzer.ObserveFrame(GoldenFrames.SvFrameWithChannelSamples(
            0, values, smpRate: 80, smpMod: 2));

        var snapshot = analyzer.GetAnalyzerSnapshot();
        Assert.Equal(1, snapshot.Diagnostics.SequenceErrors);
    }

    [Fact]
    public void VerifiedObserved4000BoundaryRetainsContinuityDespiteOptionalRateMismatch()
    {
        var analyzer = new RawProcessBusAnalyzer();
        var values = new int[8];
        for (var i = 3950; i < 4060; i++)
        {
            analyzer.ObserveFrame(GoldenFrames.SvFrameWithChannelSamples(
                (ushort)(i % 4000), values, smpRate: 96, smpMod: 2));
        }
        Assert.Equal(0, analyzer.GetAnalyzerSnapshot().Diagnostics.SequenceErrors);
    }
}
