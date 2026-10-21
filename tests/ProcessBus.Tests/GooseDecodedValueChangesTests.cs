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
        Assert.Equal((uint)5, first.Frame.StNum);
        Assert.Equal((uint)1, first.Frame.SqNum);
        Assert.Equal(first.TimeUtc, first.Frame.LastSeenUtc);
        var exactDelta = Assert.Single(first.Frame.DataValues);
        Assert.True(exactDelta.IsChanged);
        Assert.Equal("True", exactDelta.PreviousValue, ignoreCase: true);
        Assert.Equal("False", exactDelta.Value, ignoreCase: true);
        var packet = Assert.Single(analyzer.GetGooseSnapshot().Messages);
        Assert.Equal(GoosePublisherKey.From(packet), first.PublisherKey);

        Observe(analyzer, 5, 2, false); // same value, new sqNum
        Assert.Single(analyzer.GetGooseSnapshot().ValueChanges);
        Observe(analyzer, 6, 1, true); // reverse transition
        var changes = analyzer.GetGooseSnapshot().ValueChanges;
        Assert.Equal(2, changes.Count);
        Assert.Equal(2, changes[1].Revision);
        Assert.Equal((uint)5, changes[0].Frame.StNum);
        Assert.Equal((uint)6, changes[1].Frame.StNum);
        Assert.Equal(changes[0].Frame.DataValues[0].Value, changes[1].Frame.DataValues[0].PreviousValue,
            ignoreCase: true);
        // The old event's exact typed evidence must survive later frames.
        Assert.Equal("False", changes[0].Frame.DataValues[0].Value, ignoreCase: true);
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
    public void PublisherNoticeLatchesUntilExplicitAckAndRearmsOnNextRealChange()
    {
        var packet = new ProcessBus.Core.Models.GooseMessageItem
        {
            SourceMac = "00:11:22:33:44:55",
            AppId = "0x0001",
            GoCbRef = "IED/LLN0$GO$gcb",
            VlanId = "0"
        };
        var other = new ProcessBus.Core.Models.GooseMessageItem
        {
            SourceMac = "00:11:22:33:44:56",
            AppId = "0x0001",
            GoCbRef = "IED/LLN0$GO$gcb",
            VlanId = "0"
        };
        var catalog = new GoosePublisherCatalog();
        catalog.Reconcile([packet, other], null);
        var row = catalog.Find(GoosePublisherKey.From(packet))!;
        var otherRow = catalog.Find(GoosePublisherKey.From(other))!;
        var notifications = new List<string?>();
        row.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        var t = DateTime.UtcNow;

        row.MarkDecodedValueChange(t, 1);
        otherRow.MarkDecodedValueChange(t, 2);
        Assert.True(row.HasUnacknowledgedValueChange);
        Assert.True(otherRow.HasUnacknowledgedValueChange);
        Assert.Equal(t, row.LastValueChangeUtc);

        // Repeated rendering/snapshot reconciliation never ACKs the event.
        for (int i = 0; i < 100; i++)
            catalog.Reconcile([packet, other], GoosePublisherKey.From(packet));
        Assert.True(row.HasUnacknowledgedValueChange);
        Assert.Equal(1, notifications.Count(n => n == nameof(GoosePublisherRow.HasUnacknowledgedValueChange)));

        // A subsequent change while unread updates evidence but doesn't spam
        // the visual state nor trigger an unbounded notification loop.
        row.MarkDecodedValueChange(t.AddSeconds(7), 3);
        Assert.True(row.HasUnacknowledgedValueChange);
        Assert.Equal(t.AddSeconds(7), row.LastValueChangeUtc);
        Assert.Equal(1, notifications.Count(n => n == nameof(GoosePublisherRow.HasUnacknowledgedValueChange)));

        row.AcknowledgeValueChange();
        Assert.False(row.HasUnacknowledgedValueChange);
        Assert.True(otherRow.HasUnacknowledgedValueChange); // ACK scoped to one key
        Assert.Equal(2, notifications.Count(n => n == nameof(GoosePublisherRow.HasUnacknowledgedValueChange)));
        row.AcknowledgeValueChange(); // idempotent ACK
        Assert.Equal(2, notifications.Count(n => n == nameof(GoosePublisherRow.HasUnacknowledgedValueChange)));

        row.MarkDecodedValueChange(t, 3); // same revision, no re-alert
        Assert.False(row.HasUnacknowledgedValueChange);
        row.MarkDecodedValueChange(t.AddSeconds(8), 4);
        Assert.True(row.HasUnacknowledgedValueChange);
        Assert.Equal(4, row.ChangeRevision);
        Assert.Equal(3, notifications.Count(n => n == nameof(GoosePublisherRow.HasUnacknowledgedValueChange)));

        // A fresh decoder session can safely restart revision numbering.
        row.AcknowledgeValueChange();
        var firstSession = Guid.NewGuid();
        var secondSession = Guid.NewGuid();
        row.MarkDecodedValueChange(t.AddSeconds(9), 42, firstSession);
        row.AcknowledgeValueChange();
        row.MarkDecodedValueChange(t.AddSeconds(10), 1, secondSession);
        Assert.Equal(1, row.ChangeRevision);
        Assert.True(row.HasUnacknowledgedValueChange);
        Assert.Equal(t.AddSeconds(10), row.LastValueChangeUtc);
    }
}
