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
    public void PeriodicSvExcursionReportsWithVolatileCountersCoalescePerStreamWithoutErasingRawEvidence()
    {
        var now = new DateTime(2026, 10, 20, 13, 10, 0, DateTimeKind.Utc);
        const string family = "SV arrival timing excursions >= 300 us";
        var originals = new[]
        {
            new DiagnosticEventItem { TimestampUtc = now, Severity = "Warning", Message = family + ": 3 event(s) in 5s, total=37. Latest: svID=ARSTACK_SV01; smpCnt=2599->2600; variation=-45.1 us" },
            new DiagnosticEventItem { TimestampUtc = now.AddSeconds(-5), Severity = "Warning", Message = family + ": 4 event(s) in 5s, total=34. Latest: svID=ARSTACK_SV01; smpCnt=2399->2400; variation=202.0 us" },
            new DiagnosticEventItem { TimestampUtc = now.AddSeconds(-10), Severity = "Warning", Message = family + ": 1 event(s) in 5s, total=30. Latest: svID=ARSTACK_SV02; smpCnt=399->400; variation=300.1 us" },
            new DiagnosticEventItem { TimestampUtc = now.AddSeconds(-15), Severity = "Diagnostic", Message = family + ": 2 event(s) in 5s, total=28. Latest: svID=ARSTACK_SV01; smpCnt=2099->2100; variation=301.0 us" },
            new DiagnosticEventItem { TimestampUtc = now.AddSeconds(-20), Severity = "Warning", Message = "Other problem: svID=ARSTACK_SV01; total=5" },
            new DiagnosticEventItem { TimestampUtc = now.AddSeconds(-25), Severity = "Warning", Message = "Other problem: svID=ARSTACK_SV01; total=6" }
        };

        var grouped = DiagnosticEventSummarizer.Summarize(originals);
        Assert.Equal(5, grouped.Count);
        Assert.Equal(2, grouped[0].OccurrenceCount);
        Assert.True(grouped[0].IsPeriodicSummary);
        Assert.Equal(" ×2 reports", grouped[0].OccurrenceText);
        Assert.Equal(now, grouped[0].TimestampUtc);
        Assert.Equal(now.AddSeconds(-5), grouped[0].FirstOccurrenceUtc);
        Assert.Equal(originals[0].Message, grouped[0].Message);
        Assert.Contains("2 periodic reports", grouped[0].EvidenceTooltip);
        Assert.Equal("ARSTACK_SV01", grouped[0].SourceText);
        Assert.Equal("ARSTACK_SV02", grouped[1].SourceText);
        Assert.Single(grouped.Where(x => x.Severity == "Diagnostic"));
        Assert.Equal(2, grouped.Count(x => x.Message.StartsWith("Other problem:", StringComparison.Ordinal)));
        Assert.All(originals, e => Assert.Equal(1, e.OccurrenceCount));
    }

    [Fact]
    public void PeriodicReportsWithoutExplicitSvIdMustNotMergeVolatileEvidence()
    {
        const string family = "SV arrival timing excursions >= 300 us";
        var rows = new[]
        {
            new DiagnosticEventItem { Severity = "Warning", Message = family + ": total=10; variation=300 us" },
            new DiagnosticEventItem { Severity = "Warning", Message = family + ": total=11; variation=301 us" },
        };
        var grouped = DiagnosticEventSummarizer.Summarize(rows);
        Assert.Equal(2, grouped.Count);
        Assert.All(grouped, row => Assert.False(row.IsPeriodicSummary));
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
