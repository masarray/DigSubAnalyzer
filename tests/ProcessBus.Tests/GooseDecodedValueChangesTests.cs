using ProcessBus.Core.Presentation;
using ProcessBus.Iec61850.Raw.Analysis;
using Xunit;

namespace ProcessBus.Tests;

public sealed class GooseDecodedValueChangesTests
{
    private static void Observe(RawProcessBusAnalyzer analyzer, uint st, uint sq, bool value,
        byte confRev = 1)
    {
        analyzer.ObserveOwnedFrame(GoldenFrames.GooseFrame(stNum: st, sqNum: sq,
            signalValue: value, confRev: confRev));
    }

    [Fact]
    public void BaselineStateOnlyAndRetransmissionAreNotDecodedValueChanges()
    {
        var analyzer = new RawProcessBusAnalyzer();
        Observe(analyzer, 3, 1, true);
        Assert.Empty(analyzer.GetGooseSnapshot().ValueChanges);

        Observe(analyzer, 3, 2, true); // sqNum-only retransmission
        Observe(analyzer, 4, 1, true); // stNum-only state update
        Assert.Empty(analyzer.GetGooseSnapshot().ValueChanges);

        Observe(analyzer, 5, 1, false); // actual DataSet value transition
        var first = Assert.Single(analyzer.GetGooseSnapshot().ValueChanges);
        Assert.Equal(1, first.Revision);
        var packet = Assert.Single(analyzer.GetGooseSnapshot().Messages);
        Assert.Equal(GoosePublisherKey.From(packet), first.PublisherKey);

        Observe(analyzer, 5, 2, false); // same value, new sqNum
        Assert.Single(analyzer.GetGooseSnapshot().ValueChanges);
        Observe(analyzer, 6, 1, true); // reverse transition
        var changes = analyzer.GetGooseSnapshot().ValueChanges;
        Assert.Equal(2, changes.Count);
        Assert.Equal(2, changes[1].Revision);
    }

    [Fact]
    public void ConfigurationRevisionChangeIsSafelyRebaselined()
    {
        var analyzer = new RawProcessBusAnalyzer();
        Observe(analyzer, 3, 1, true, 1);
        Observe(analyzer, 4, 1, false, 2); // changed confRev invalidates pairing
        Assert.Empty(analyzer.GetGooseSnapshot().ValueChanges);
        Observe(analyzer, 5, 1, true, 2);
        Assert.Single(analyzer.GetGooseSnapshot().ValueChanges);
    }

    [Fact]
    public void ChangeEnvelopeRemainsBoundedAndReportsEviction()
    {
        var analyzer = new RawProcessBusAnalyzer();
        Observe(analyzer, 3, 1, true);
        for (uint st = 4; st < 620; st++)
            Observe(analyzer, st, 1, st % 2 == 0);

        var snapshot = analyzer.GetGooseSnapshot();
        Assert.Equal(512, snapshot.ValueChanges.Count);
        Assert.True(snapshot.ValueChangeOverflowCount > 0);
        Assert.True(snapshot.ValueChanges[0].Revision > 1);
        Assert.Equal(snapshot.ValueChanges[^1].Revision - snapshot.ValueChanges[0].Revision + 1,
            snapshot.ValueChanges.Count);
    }

    [Fact]
    public void PublisherNoticeExpiresWithoutChangingSelectionOrIdentity()
    {
        var packet = new ProcessBus.Core.Models.GooseMessageItem
        {
            SourceMac = "00:11:22:33:44:55",
            AppId = "0x0001",
            GoCbRef = "IED/LLN0$GO$gcb",
            VlanId = "0"
        };
        var row = new GoosePublisherRow(GoosePublisherKey.From(packet), packet);
        var now = DateTime.UtcNow;
        row.MarkDecodedValueChange(now, 1);
        Assert.True(row.HasRecentValueChange);
        row.MarkDecodedValueChange(now.AddMilliseconds(-100), 1);
        Assert.Equal(now, row.LastValueChangeUtc);
        row.ExpireValueChangeNotice(now.AddMilliseconds(2000));
        Assert.True(row.HasRecentValueChange);
        row.ExpireValueChangeNotice(now.AddMilliseconds(2600));
        Assert.False(row.HasRecentValueChange);
        Assert.Equal(1, row.ChangeRevision);
    }
}
