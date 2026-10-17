using ProcessBus.Iec61850.Raw.Ptp;
using Xunit;

namespace ProcessBus.Tests;

public sealed class PtpRateWindowTests
{
    [Fact]
    public void HighRateTrafficIsCountedWithoutRetainingPerPacketObjects()
    {
        var window = new PtpRateWindow();
        var now = new DateTime(2026, 10, 17, 1, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 100_000; i++)
            window.Observe(now.AddMilliseconds(i % 900));
        Assert.Equal(20_000, window.Rate(now.AddSeconds(1))!.Value);
        Assert.Null(window.Rate(now.AddSeconds(6)));
    }

    [Fact]
    public void LatePacketsCannotEvictNewerTimeBucket()
    {
        var window = new PtpRateWindow();
        var now = new DateTime(2026, 10, 17, 1, 0, 0, DateTimeKind.Utc);
        window.Observe(now.AddSeconds(11));
        window.Observe(now.AddSeconds(6)); // old and same modulus slot
        window.Observe(now.AddSeconds(1)); // too old
        Assert.Equal(0.2, window.Rate(now.AddSeconds(11)));
    }

    [Fact]
    public void PtpTwoStepCorrelationMustMatchSourceDomainSequenceAndTimeout()
    {
        var tracker = new PtpHealthTracker();
        var now = new DateTime(2026, 10, 17, 1, 0, 0, DateTimeKind.Utc);
        static PtpMessage Message(DateTime t, byte domain, string source, byte type, ushort seq) => new()
        {
            CaptureTimeUtc = t, DomainNumber = domain,
            SourceClockIdentity = source, SourcePortNumber = 1,
            TransportText = "L2", MessageTypeValue = type,
            Flags = type == 0 ? (ushort)0x0200 : (ushort)0,
            SequenceId = seq
        };

        tracker.Observe(Message(now, 0, "A", 0, 42));
        tracker.Observe(Message(now.AddMilliseconds(20), 0, "A", 8, 42));
        tracker.Observe(Message(now.AddMilliseconds(30), 0, "A", 8, 43));
        tracker.Observe(Message(now.AddMilliseconds(40), 1, "B", 8, 42));
        tracker.Observe(Message(now.AddMilliseconds(50), 0, "A", 0, 44));
        tracker.Observe(Message(now.AddSeconds(5), 0, "A", 8, 44)); // stale
        tracker.Observe(Message(now.AddSeconds(6), 0, "A", 0, 46)); // selected A

        var latest = tracker.CreateSnapshot(now.AddSeconds(6));
        Assert.Equal((byte)0, latest.DomainNumber);
        Assert.Equal(1, latest.MatchedTwoStepCount);
        Assert.Equal(2, latest.UnmatchedFollowUpCount);
    }
}
