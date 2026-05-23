namespace ProcessBus.Core.Models;

public sealed class DiagnosticEventItem
{
    public DateTime TimestampUtc { get; set; }
    public string Severity { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
}
