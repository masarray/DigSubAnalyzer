using ProcessBus.Core.Models;
using Xunit;

namespace ProcessBus.Tests;

public sealed class SvDiagnosticsReadoutTests
{
    [Fact]
    public void SelectedStreamShowsSignedArrivalJitterAndBoundedEvidence()
    {
        var d = new SvDiagnosticsSnapshot
        {
            CurrentJitterMicroseconds = -413.24,
            AverageAbsJitterMicroseconds = 77.12,
            MaxAbsJitterMicroseconds = 813.14,
            RecentJitterOver300MicrosecondsCount = 5,
            JitterOver300MicrosecondsCount = 1530,
            CurrentDeltaMicroseconds = 663.24,
            ExpectedDeltaMicroseconds = 250
        };
        Assert.Equal("-413.2 µs", d.SvJitterHeadlineText);
        Assert.Equal("Mean |Δ| 77.1 µs · Max |Δ| 813.1 µs (last 4000 intervals)", d.SvJitterStatisticsText);
        Assert.Equal("≥300 µs: 5 / 5s · 1530 total", d.SvJitterExcursionsText);
        Assert.Equal("Actual Δ 663.2 µs · Expected Δ 250.0 µs", d.SvArrivalIntervalText);
        Assert.Contains("Host/Npcap", d.SvArrivalAuthorityText);
        Assert.DoesNotContain("locked", d.SvArrivalAuthorityText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingArrivalTimingDoesNotBecomeInventedZero()
    {
        var d = new SvDiagnosticsSnapshot { CurrentJitterMicroseconds = null };
        Assert.Equal("N/A", d.SvJitterHeadlineText);
        Assert.Contains("N/A", d.SvJitterStatisticsText);
        Assert.Contains("unavailable", d.SvArrivalAuthorityText);
    }

    [Fact]
    public void NonFiniteInputRemainsUnavailable()
    {
        var d = new SvDiagnosticsSnapshot
        {
            CurrentJitterMicroseconds = double.NaN,
            AverageAbsJitterMicroseconds = double.PositiveInfinity,
            MaxAbsJitterMicroseconds = double.NegativeInfinity
        };
        Assert.Equal("N/A", d.SvJitterHeadlineText);
        Assert.Equal("Mean |Δ| N/A · Max |Δ| N/A (last 4000 intervals)", d.SvJitterStatisticsText);
    }

    [Fact]
    public void SequenceJumpEventsAndMissingSamplesAreNotInterchanged()
    {
        var d = new SvDiagnosticsSnapshot
        {
            SequenceErrors = 5, MissingSamples = 27, LastSampleCount = 0,
            DecodeErrors = 3
        };
        Assert.Equal("5", d.SvSequenceJumpsText);
        Assert.Equal("27", d.SvMissingSamplesText);
        Assert.Equal("Last smpCnt: 0", d.SvSequenceEvidenceText);
        Assert.Equal("Decoder rejects: 3 (capture-wide)", d.SvDecodeRejectText);
    }

    [Theory]
    [InlineData("4I+4V quality good", "GOOD")]
    [InlineData("4I+4V TEST data (laboratory stimulus)", "TEST")]
    [InlineData("4I+4V operator-blocked data", "BLOCKED")]
    [InlineData("4I+4V quality invalid: measurement withheld", "INVALID")]
    [InlineData("Quality mapping not verified", "UNVERIFIED")]
    public void QualityVerdictTracksObservedQualityWithoutInferringGood(string evidence, string expected)
    {
        var d = new SvDiagnosticsSnapshot { ValidityText = evidence, SmpSynchText = "0" };
        Assert.Equal(expected, d.SvQualityHeadlineText);
        Assert.Equal(evidence, d.SvQualityEvidenceText);
        Assert.Equal("NOT SYNCED", d.SvSampleSyncHeadlineText);
        Assert.Contains("smpSynch=0", d.SvSampleSyncEvidenceText);
    }

    [Theory]
    [InlineData("0", "NOT SYNCED")]
    [InlineData("1", "LOCAL REF REPORTED")]
    [InlineData("2", "GLOBAL REF REPORTED")]
    [InlineData("N/A", "UNVERIFIED")]
    public void QualityAndSampleSynchronizationRemainIndependent(string wire, string expected)
    {
        var d = new SvDiagnosticsSnapshot { ValidityText = "4I+4V quality good", SmpSynchText = wire };
        Assert.Equal("GOOD", d.SvQualityHeadlineText);
        Assert.Equal(expected, d.SvSampleSyncHeadlineText);
    }

    [Fact]
    public void NoSmpCntObservationIsPendingNotZero()
    {
        Assert.Equal("smpCnt: waiting for SV", new SvDiagnosticsSnapshot().SvSequenceEvidenceText);
    }
}
