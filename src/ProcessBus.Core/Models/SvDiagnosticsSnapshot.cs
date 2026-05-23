namespace ProcessBus.Core.Models;

public sealed class SvDiagnosticsSnapshot
{
    public bool IsRunning { get; set; }
    public string StreamStatusText { get; set; } = "Stopped";
    public double? PacketRatePps { get; set; }
    public long TotalPackets { get; set; }
    public long DecodeErrors { get; set; }
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
    public DateTime? LastPtpMessageTimestampUtc { get; set; }
    public DateTime? LastPtpSyncTimestampUtc { get; set; }
    public DateTime? LastPtpAnnounceTimestampUtc { get; set; }
    public string PtpProfileHintText { get; set; } = "PTP not observed";
    public string PtpTransportText { get; set; } = "N/A";
    public string TimingReferenceText { get; set; } = "Timing Reference: not observed";
    public string TimestampSourceText { get; set; } = "Timestamp Source: Npcap software timestamp";
    public string TimingMetricText { get; set; } = "Metric: arrival timing variation";
}
