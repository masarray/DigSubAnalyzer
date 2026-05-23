namespace ProcessBus.Core.Models;

public sealed class GooseMonitorSnapshot
{
    public bool IsRunning { get; init; }
    public string StatusText { get; init; } = "Stopped";
    public long TotalMessages { get; init; }
    public IReadOnlyList<GooseMessageItem> Messages { get; init; } = Array.Empty<GooseMessageItem>();
    public IReadOnlyList<DiagnosticEventItem> Events { get; init; } = Array.Empty<DiagnosticEventItem>();
}
