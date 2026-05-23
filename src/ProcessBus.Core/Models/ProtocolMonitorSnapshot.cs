namespace ProcessBus.Core.Models;

public sealed class ProtocolMonitorSnapshot
{
    public long TotalFrames { get; init; }
    public long SvFrames { get; init; }
    public long GooseFrames { get; init; }
    public long PtpFrames { get; init; }
    public int LiveSvStreams { get; init; }
    public int GoosePublishers { get; init; }
    public DateTime? LastSvSeenUtc { get; init; }
    public DateTime? LastGooseSeenUtc { get; init; }
    public DateTime? LastPtpSeenUtc { get; init; }
    public string SvStatusText { get; init; } = "SV not observed";
    public string GooseStatusText { get; init; } = "GOOSE not observed";
    public string PtpStatusText { get; init; } = "PTP not observed";
    public string TimingConfidenceText { get; init; } = "Timing confidence pending";

    public string SummaryText => $"SV {SvFrames} · GOOSE {GooseFrames} · PTP {PtpFrames}";
}
