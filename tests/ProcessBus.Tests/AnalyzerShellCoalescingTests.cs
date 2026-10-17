using ProcessBus.Core.Models;
using ProcessBus.Core.Services;
using Xunit;

namespace ProcessBus.Tests;

public sealed class AnalyzerShellCoalescingTests
{
    [Fact]
    public void RepeatedSnapshotsReuseSelectedStreamRowWithoutEventDuplicates()
    {
        var state = new AnalyzerShellState();
        var time = new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);
        var row = new SvStreamItem { StreamId = "SV|A", SvId = "A", IsActive = true };
        var ev = new DiagnosticEventItem { TimestampUtc = time, Severity = "Warning", Message = "jitter" };
        var ptp = new PtpEventItem { TimestampUtc = time, MessageType = "Sync",
            Transport = "L2", SequenceIdText = "7", ClockIdentity = "gm" };
        var snap = new AnalyzerSnapshot { Streams = [row], Events = [ev], PtpEvents = [ptp] };
        state.ApplySnapshot(snap);
        var retained = Assert.Single(state.Streams);

        for (var i = 0; i < 100; i++)
        {
            var identical = new AnalyzerSnapshot
            {
                Streams = [new SvStreamItem { StreamId = "SV|A", SvId = "A", IsActive = true }],
                Events = [new DiagnosticEventItem { TimestampUtc = time, Severity = "Warning", Message = "jitter" }],
                PtpEvents = [new PtpEventItem { TimestampUtc = time, MessageType = "Sync",
                    Transport = "L2", SequenceIdText = "7", ClockIdentity = "gm" }]
            };
            state.ApplySnapshot(identical);
        }

        Assert.Same(retained, Assert.Single(state.Streams));
        Assert.Single(state.Events);
        Assert.Single(state.PtpEvents);
    }

    [Fact]
    public void BoundedDeduplicationClearsAtReset()
    {
        var state = new AnalyzerShellState();
        var start = new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 300; i++)
        {
            state.ApplySnapshot(new AnalyzerSnapshot
            {
                Events = [new DiagnosticEventItem { TimestampUtc = start.AddMilliseconds(i),
                    Severity = "Info", Message = $"message {i}" }],
                PtpEvents = [new PtpEventItem { TimestampUtc = start.AddMilliseconds(i),
                    MessageType = "Sync", Transport = "L2", SequenceIdText = i.ToString(),
                    ClockIdentity = "PTP-A" }]
            });
        }
        Assert.Equal(200, state.Events.Count);
        Assert.Equal(200, state.PtpEvents.Count);

        state.ClearRuntimeData();
        Assert.Empty(state.Events);
        Assert.Empty(state.PtpEvents);
        state.ApplySnapshot(new AnalyzerSnapshot
        {
            Events = [new DiagnosticEventItem { TimestampUtc = start, Severity = "Info", Message = "message 0" }],
            PtpEvents = [new PtpEventItem { TimestampUtc = start, MessageType = "Sync",
                Transport = "L2", SequenceIdText = "0", ClockIdentity = "PTP-A" }]
        });
        Assert.Single(state.Events);
        Assert.Single(state.PtpEvents);
    }
}
