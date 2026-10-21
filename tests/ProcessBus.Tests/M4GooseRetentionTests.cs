using ProcessBus.Iec61850.Raw.Analysis;
using Xunit;

namespace ProcessBus.Tests;

public sealed class M4GooseRetentionTests
{
    private static byte[] Goose(int id)
    {
        var apdu = GoldenFrames.GooseApdu(
            goCbRef: $"LABLD0/LLN0$GO$CB{id:000}",
            dataSet: "LABLD0/LLN0$DS",
            goId: $"G{id:000}");
        return GoldenFrames.EthernetFrame(GoldenFrames.GooseEtherType,
            GoldenFrames.ProcessBusPayload(0x0001, apdu));
    }


    [Fact]
    public void SameGoCbRefOnTwoSourceMacsRemainsTwoIndependentPublishers()
    {
        var analyzer = new RawProcessBusAnalyzer();
        var first = Goose(1);
        var second = (byte[])first.Clone();
        // Ethernet II source MAC occupies bytes 6..11.
        second[11] ^= 0x01;

        analyzer.ObserveFrame(first, DateTime.UtcNow);
        analyzer.ObserveFrame(second, DateTime.UtcNow.AddMilliseconds(1));

        var publishers = analyzer.GetGooseSnapshot().Messages;
        Assert.Equal(2, publishers.Count);
        Assert.Equal(2, publishers.Select(x => x.MessageId).Distinct().Count());
        Assert.Equal(2, publishers.Select(x => x.SourceMac).Distinct().Count());
        Assert.All(publishers, x => Assert.Equal("LABLD0/LLN0$GO$CB001", x.GoCbRef));
    }

    [Fact]
    public void ChurningPublisherIdentitiesCannotAllocateUnboundedGooseStates()
    {
        var analyzer = new RawProcessBusAnalyzer();
        var start = DateTime.UtcNow;
        for (var id = 0; id < 300; id++)
            analyzer.ObserveFrame(Goose(id), start.AddMilliseconds(id));

        Assert.Equal(256, analyzer.GetGooseSnapshot().Messages.Count);

        analyzer.ObserveFrame(Goose(300), start.AddSeconds(20));
        var result = analyzer.GetGooseSnapshot();
        Assert.Equal(256, result.Messages.Count);
        Assert.Contains(result.Messages, x => x.GoId == "G300");
        Assert.DoesNotContain(result.Messages, x => x.GoId == "G000");
    }
}
