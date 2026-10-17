using System.Globalization;

namespace ProcessBus.Core.Models;

public sealed class SvDiagnosticsSnapshot
{
    public bool IsRunning { get; set; }
    public string StreamStatusText { get; set; } = "Stopped";
    public double? PacketRatePps { get; set; }
    public long TotalPackets { get; set; }
    public long DecodeErrors { get; set; }
    // Driver counters are a separate capture-path diagnostic. Never mix them with
    // inferred IEC 61850 missing smpCnt evidence.
    public long? CaptureDriverDroppedFrames { get; set; }
    public long? CaptureInterfaceDroppedFrames { get; set; }
    public string CaptureDropEvidenceText { get; set; } = "NIC capture-drop counters unavailable";
    public long SequenceErrors { get; set; }
    public long MissingSamples { get; set; }
    public int? LastSampleCount { get; set; }
    public double? CurrentDeltaMicroseconds { get; set; }
    public double? AverageDeltaMicroseconds { get; set; }
    public double? ExpectedDeltaMicroseconds { get; set; }
    public double? CurrentJitterMicroseconds { get; set; }
    public double? AverageAbsJitterMicroseconds { get; set; }
    public double? MaxAbsJitterMicroseconds { get; set; }
    public long JitterOver300MicrosecondsCount { get; set; }
    public long RecentJitterOver300MicrosecondsCount { get; set; }
    public string JitterStatusText { get; set; } = "Arrival variation pending";
    public DateTime? LastPacketTimestampUtc { get; set; }
    public string SmpSynchText { get; set; } = "N/A";
    public string ValidityText { get; set; } = "N/A";
    public string DecodeStatusText { get; set; } = "No data";
    public int? RmsBufferSizeSamples { get; set; }
    public double? MeasuredFrequencyHz { get; set; }
    public string FrequencyReferenceChannel { get; set; } = "N/A";
    public double? SamplesPerCycleEstimate { get; set; }
    public bool FrequencyEstimateValid { get; set; }
    public int? FrequencyCrossingCount { get; set; }
    public int? FrequencyInputSampleCount { get; set; }
    public double? FrequencyWindowMinimum { get; set; }
    public double? FrequencyWindowMaximum { get; set; }
    public double? LastMeasuredPeriodMilliseconds { get; set; }
    public string FrequencyRejectReason { get; set; } = "N/A";
    public string PacketRateMeaningText { get; set; } = "Raw frame receive/decode rate over the last 1 second window";
    public string TimebaseStatusText { get; set; } = "Timebase pending";

    public bool PtpObserved { get; set; }
    public string PtpStatusText { get; set; } = "PTP not observed";
    public long PtpTotalMessages { get; set; }
    public int? PtpDomainNumber { get; set; }
    public string PtpGrandmasterIdentity { get; set; } = "N/A";
    public int? PtpClockClass { get; set; }
    public string PtpClockAccuracyText { get; set; } = "N/A";
    public int? PtpStepsRemoved { get; set; }
    public double? PtpSyncRatePerSecond { get; set; }
    public double? PtpAnnounceRatePerSecond { get; set; }
    public double? PtpFollowUpRatePerSecond { get; set; }
    public long PtpGrandmasterChangeCount { get; set; }
    public long PtpMatchedTwoStepCount { get; set; }
    public long PtpUnmatchedFollowUpCount { get; set; }
    public DateTime? LastPtpMessageTimestampUtc { get; set; }
    public DateTime? LastPtpSyncTimestampUtc { get; set; }
    public DateTime? LastPtpAnnounceTimestampUtc { get; set; }
    public string PtpProfileHintText { get; set; } = "PTP not observed";
    public string PtpTransportText { get; set; } = "N/A";
    public string TimingReferenceText { get; set; } = "Timing Reference: not observed";
    public string TimestampSourceText { get; set; } = "Timestamp Source: Npcap software timestamp";
    public string TimingMetricText { get; set; } = "Metric: arrival timing variation";
    // Read-only operator telemetry computed from the selected stream snapshot.
    // Arrival timing is host/Npcap evidence, never publisher jitter proof.
    public string SvJitterHeadlineText => CurrentJitterMicroseconds is { } current && double.IsFinite(current)
        ? current.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " µs"
        : "N/A";

    public string SvJitterStatisticsText =>
        $"Mean |Δ| {FormatMicroseconds(AverageAbsJitterMicroseconds)} · Max |Δ| {FormatMicroseconds(MaxAbsJitterMicroseconds)}";

    public string SvJitterExcursionsText =>
        $"≥300 µs: {RecentJitterOver300MicrosecondsCount} / 5s · {JitterOver300MicrosecondsCount} total";

    // SequenceErrors means discontinuity events, not the number of missing sample values.
    public string SvSequenceJumpsText => SequenceErrors.ToString("N0", CultureInfo.InvariantCulture);
    public string SvMissingSamplesText => MissingSamples.ToString("N0", CultureInfo.InvariantCulture);
    public string SvSequenceEvidenceText => LastSampleCount.HasValue
        ? $"Last smpCnt: {LastSampleCount.Value.ToString(CultureInfo.InvariantCulture)}"
        : "smpCnt: waiting for SV";
    public string SvDecodeRejectText =>
        $"Decoder rejects: {DecodeErrors.ToString("N0", CultureInfo.InvariantCulture)} (capture-wide)";

    public string SvQualityHeadlineText => ValidityText switch
    {
        "4I+4V quality good" => "GOOD",
        "4I+4V TEST data (laboratory stimulus)" => "TEST",
        "4I+4V operator-blocked data" => "BLOCKED",
        "4I+4V quality invalid: measurement withheld" => "INVALID",
        _ => "UNVERIFIED"
    };

    public string SvQualityEvidenceText => $"{ValidityText} · smpSynch: {SmpSynchText}" +
        (SmpSynchText == "N/A" ? " (unavailable)" : " (observed)");
    public string SvArrivalIntervalText =>
        $"Actual Δ {FormatMicroseconds(CurrentDeltaMicroseconds)} · Expected Δ {FormatMicroseconds(ExpectedDeltaMicroseconds)}";
    public string SvArrivalAuthorityText =>
        CurrentJitterMicroseconds.HasValue
            ? "Host/Npcap arrival timestamps · capture-path screening only"
            : "Arrival interval unavailable · no hardware timing or clock-lock claim";

    private static string FormatMicroseconds(double? value) =>
        value is { } number && double.IsFinite(number)
            ? number.ToString("0.0", CultureInfo.InvariantCulture) + " µs"
            : "N/A";

}
