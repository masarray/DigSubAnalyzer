using ProcessBus.Iec61850.Raw.Ptp;
using Xunit;

namespace ProcessBus.Tests;

public class PtpHealthTrackerTests
{
    private static PtpMessage Message(
        DateTime when, byte domain, string source, byte type, string? gm = null) =>
        new()
        {
            CaptureTimeUtc = when,
            DomainNumber = domain,
            SourceClockIdentity = source,
            SourcePortNumber = 1,
            MessageType = type switch { 0x0B => "Announce", 0 => "Sync", _ => "Follow_Up" },
            MessageTypeValue = type,
            GrandmasterIdentity = gm,
            GrandmasterClockClass = 248,
            TransportText = "Ethernet / IEEE 1588"
        };

    [Fact]
    public void InterleavedDomainsMustNotBlendGrandmasters()
    {
        var tracker = new PtpHealthTracker();
        var now = new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);
        tracker.Observe(Message(now, 0, "CLOCK-A", 0x0B, "GM-A"));
        tracker.Observe(Message(now.AddMilliseconds(100), 1, "CLOCK-B", 0x0B, "GM-B"));
        tracker.Observe(Message(now.AddMilliseconds(150), 1, "CLOCK-B", 0));
        var selected = tracker.CreateSnapshot(now.AddMilliseconds(200));
        Assert.Equal(1, selected.DomainNumber);
        Assert.Equal("GM-B", selected.GrandmasterIdentity);
        Assert.Equal(2, selected.ObservedSourceCount);
        Assert.Equal(0, selected.GrandmasterChangeCount);

        tracker.Observe(Message(now.AddMilliseconds(300), 0, "CLOCK-A", 0x0B, "GM-A"));
        selected = tracker.CreateSnapshot(now.AddMilliseconds(350));
        Assert.Equal(0, selected.DomainNumber);
        Assert.Equal("GM-A", selected.GrandmasterIdentity);
        Assert.Equal(0, selected.GrandmasterChangeCount);

        tracker.Observe(Message(now.AddMilliseconds(400), 0, "CLOCK-A", 0x0B, "GM-X"));
        Assert.Equal(1, tracker.CreateSnapshot(now.AddMilliseconds(500)).GrandmasterChangeCount);
        Assert.Contains("CLOCK-A", tracker.ConsumeEvent()!);
    }

    [Fact]
    public void OldAnnounceIsObservedButNotActiveOrLocked()
    {
        var tracker = new PtpHealthTracker();
        var now = new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);
        tracker.Observe(Message(now, 0, "CLOCK-A", 0x0B, "GM-A"));
        var expired = tracker.CreateSnapshot(now.AddSeconds(9));
        Assert.True(expired.Observed);
        Assert.Equal("Announce timeout", expired.StatusText);
        Assert.Contains("no passive clock-lock claim", expired.ProfileHintText);
        Assert.Null(expired.AnnounceRatePerSecond);
    }

    [Fact]
    public void SourceCatalogStaysBounded()
    {
        var tracker = new PtpHealthTracker();
        var now = new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 90; i++)
            tracker.Observe(Message(now.AddMilliseconds(i), (byte)(i % 3), $"CLOCK-{i:000}", 0x0B, $"GM-{i:000}"));
        var result = tracker.CreateSnapshot(now.AddSeconds(1));
        Assert.Equal(64, result.ObservedSourceCount);
        Assert.Equal(90, result.TotalMessages);
    }
}
