using ProcessBus.Core.Models;
using ProcessBus.Core.Presentation;
using Xunit;

namespace ProcessBus.Tests;

public sealed class GoosePublisherSelectionTests
{
    private static GooseMessageItem Frame(
        string source = "02:00:4C:4E:4E:50",
        string goCb = "IED/LLN0$GO$gcb",
        uint st = 1, uint sq = 1, string vlan = "0") => new()
    {
        MessageId = $"{source}|{goCb}",
        SourceMac = source,
        GoCbRef = goCb,
        GoId = "SharedGoID",
        AppId = "0x0001",
        VlanId = vlan,
        StNum = st,
        SqNum = sq,
        DataValues = [new GooseDatasetValueItem { Index = 1, Name = "stVal", Type = "Boolean", Value = st.ToString() }],
        LastSeenUtc = new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(sq)
    };

    [Fact]
    public void RetransmissionsKeepSameRowAndExposeLatestTypedFrame()
    {
        var catalog = new GoosePublisherCatalog();
        var first = Frame();
        var key = GoosePublisherKey.From(first);
        catalog.Reconcile([first], null);
        var selected = Assert.Single(catalog.Rows);
        var resets = 0;
        catalog.Rows.CollectionChanged += (_, _) => resets++;

        for (uint sq = 2; sq <= 1000; sq++)
            catalog.Reconcile([Frame(sq: sq)], key);

        Assert.Same(selected, catalog.Find(key));
        Assert.Equal((uint)1000, selected.Current.SqNum);
        Assert.Single(selected.Current.DataValues);
        Assert.Equal(0, resets);
        Assert.False(selected.IsStale);
    }

    [Fact]
    public void SameGoIdAcrossMacAndVlanNeverCoalesces()
    {
        var catalog = new GoosePublisherCatalog();
        catalog.Reconcile([Frame(), Frame(source: "02:00:4C:4E:4E:51"), Frame(vlan: "7")], null);
        Assert.Equal(3, catalog.Rows.Count);
    }

    [Fact]
    public void SelectedPublisherSurvivesAlternatingTrafficAndTemporaryAbsence()
    {
        var catalog = new GoosePublisherCatalog();
        var a = Frame();
        var b = Frame(source: "02:00:4C:4E:4E:51");
        var key = GoosePublisherKey.From(a);
        catalog.Reconcile([a,b], null);
        var selected = catalog.Find(key);
        catalog.Reconcile([Frame(source: b.SourceMac, sq: 10)], key);
        Assert.Same(selected, catalog.Find(key));
        Assert.True(selected!.IsStale);
        Assert.Single(catalog.Rows);
        catalog.Reconcile([Frame(sq: 30), Frame(source: b.SourceMac, sq: 11)], key);
        Assert.Same(selected, catalog.Find(key));
        Assert.False(selected!.IsStale);
        Assert.Equal((uint)30, selected.Current.SqNum);
    }

    [Fact]
    public void StateNumbersAndDataSetDoNotBecomePublisherIdentity()
    {
        var catalog = new GoosePublisherCatalog();
        var a = Frame(st: 1, sq: 2);
        var k = GoosePublisherKey.From(a);
        catalog.Reconcile([a], null);
        var same = catalog.Find(k);
        catalog.Reconcile([Frame(st: 15, sq: 5)], k);
        Assert.Same(same, catalog.Find(k));
        Assert.Equal((uint)15, same!.Current.StNum);
        Assert.Equal("15", same.Current.DataValues[0].Value);
    }

    [Fact]
    public void ExplicitClearIsDifferentFromTransientEmptySnapshot()
    {
        var catalog = new GoosePublisherCatalog();
        var a = Frame();
        var key = GoosePublisherKey.From(a);
        catalog.Reconcile([a], key);
        catalog.Reconcile([], key);
        Assert.True(catalog.Find(key)!.IsStale);
        catalog.Clear();
        Assert.Empty(catalog.Rows);
        Assert.Null(catalog.Find(key));
    }

    [Fact]
    public void HighTrafficDoesNotCauseUnboundedRows()
    {
        var catalog = new GoosePublisherCatalog();
        var frames = Enumerable.Range(0, 500)
            .Select(i => Frame(source: $"02:00:00:{i / 65536:X2}:{i / 256 % 256:X2}:{i % 256:X2}"))
            .ToArray();
        catalog.Reconcile(frames, null);
        Assert.InRange(catalog.Rows.Count, 1, 256);
        catalog.Reconcile([], null);
        Assert.Empty(catalog.Rows);
    }
}
