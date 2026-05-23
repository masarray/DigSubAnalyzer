namespace ProcessBus.Core.Models;

public sealed class PtpEventItem
{
    public DateTime TimestampUtc { get; init; }
    public string TimeText => TimestampUtc.ToString("HH:mm:ss.fff");
    public string Transport { get; init; } = "N/A";
    public string MessageType { get; init; } = "N/A";
    public string Source { get; init; } = "N/A";
    public string Destination { get; init; } = "N/A";
    public string DomainText { get; init; } = "N/A";
    public string SequenceIdText { get; init; } = "N/A";
    public string ClockIdentity { get; init; } = "N/A";
    public string SummaryText => $"{MessageType} · {Transport} · {Source} → {Destination}";
}
