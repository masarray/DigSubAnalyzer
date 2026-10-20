using ProcessBus.Core.Models;
using ProcessBus.Core.Presentation;
using Xunit;

namespace ProcessBus.Tests;

public sealed class DiagnosticEventSummarizerTests
{
    [Fact]
    public void RepeatedExactFindingAggregatesWithoutAlteringForensicSources()
    {
        var now = new DateTime(2026, 10, 20, 10, 30, 0, DateTimeKind.Utc);
        var originals = new[]
        {
            new DiagnosticEventItem { TimestampUtc = now, Severity = "Warning", Message = "SV arrival timing excursions >= 300 us" },
            new DiagnosticEventItem { TimestampUtc = now.AddSeconds(-5), Severity = "Warning", Message = "SV arrival timing excursions >= 300 us" },
            new DiagnosticEventItem { TimestampUtc = now.AddSeconds(-10), Severity = "Warning", Message = "SV arrival timing excursions >= 300 us" }
        };
        var view = DiagnosticEventSummarizer.Summarize(originals);
        var summary = Assert.Single(view);
        Assert.Equal(3, summary.OccurrenceCount);
        Assert.Equal(" ×3", summary.OccurrenceText);
        Assert.Equal(now, summary.TimestampUtc);
        Assert.Equal(now.AddSeconds(-10), summary.FirstOccurrenceUtc);
        Assert.Contains("3 occurrences", summary.EvidenceTooltip);
        Assert.All(originals, item => Assert.Equal(1, item.OccurrenceCount));
        Assert.NotSame(originals[0], summary);
    }

    [Fact]
    public void DifferentSourcesSeveritiesAndEngineeringValuesRemainSeparate()
    {
        var now = DateTime.UtcNow;
        var rows = new[]
        {
            new DiagnosticEventItem { TimestampUtc = now, Severity = "Warning", Message = "svID=SV_A; Δ=301 us" },
            new DiagnosticEventItem { TimestampUtc = now.AddSeconds(-1), Severity = "Warning", Message = "svID=SV_B; Δ=301 us" },
            new DiagnosticEventItem { TimestampUtc = now.AddSeconds(-2), Severity = "Warning", Message = "svID=SV_A; Δ=302 us" },
            new DiagnosticEventItem { TimestampUtc = now.AddSeconds(-3), Severity = "Info", Message = "svID=SV_A; Δ=301 us" },
            new DiagnosticEventItem { TimestampUtc = now.AddSeconds(-4), Severity = "Warning", Message = "svID=SV_A; Δ=301 us" },
        };
        var summaries = DiagnosticEventSummarizer.Summarize(rows);
        Assert.Equal(4, summaries.Count);
        Assert.Equal(2, summaries[0].OccurrenceCount);
        Assert.Equal(1, summaries[1].OccurrenceCount);
        Assert.Equal(1, summaries[2].OccurrenceCount);
        Assert.Equal(1, summaries[3].OccurrenceCount);
    }

    [Fact]
    public void ProjectionIsBoundedEvenWhenSourceIsOversized()
    {
        var rows = Enumerable.Range(0, 500)
            .Select(i => new DiagnosticEventItem
            {
                TimestampUtc = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc).AddSeconds(-i),
                Severity = "Info",
                Message = "event " + i
            })
            .ToArray();
        var summaries = DiagnosticEventSummarizer.Summarize(rows);
        Assert.Equal(DiagnosticEventSummarizer.MaxSourceRows, summaries.Count);
        Assert.Equal("event 0", summaries[0].Message);
        Assert.Equal("event 199", summaries[^1].Message);
    }
}
