using ProcessBus.Iec61850.Raw.Analysis;
using Xunit;

namespace ProcessBus.Tests;

public sealed class BoundedStreamCatalogTests
{
    [Fact]
    public void IdentityChurnCannotGrowCatalogForeverOrEvictSelectedActiveStream()
    {
        var analyzer = new RawProcessBusAnalyzer();
        var epoch = DateTime.UtcNow;
        var signals = new int[8];

        for (var i = 0; i < 300; i++)
        {
            analyzer.ObserveFrame(
                GoldenFrames.SvFrameWithChannelSamples(0, signals, $"MU_{i:000}"),
                epoch.AddMilliseconds(i),
                i);
        }

        var first = analyzer.GetAnalyzerSnapshot();
        Assert.Equal(256, first.Streams.Count);
        Assert.Contains(first.Events, x => x.Message.Contains("capacity", StringComparison.OrdinalIgnoreCase));

        var selected = first.Streams[0].StreamId;
        analyzer.SelectStream(selected);

        analyzer.ObserveFrame(
            GoldenFrames.SvFrameWithChannelSamples(0, signals, "MU_NEW"),
            epoch.AddSeconds(12),
            12000);
        var after = analyzer.GetAnalyzerSnapshot();
        Assert.Equal(256, after.Streams.Count);
        Assert.Contains(after.Streams, x => x.SvId == "MU_NEW");
        Assert.Contains(after.Streams, x => x.StreamId == selected);
    }
}
