using ProcessBus.Core.Presentation;

namespace ProcessBus.Core.Models;

// Bounded decoder-side evidence, independent of the UI snapshot cadence.
// The exact decoded frame is retained with its verified per-entry deltas.
// A snapshot of the publisher's latest value cannot reconstruct this evidence.
public sealed record GooseValueChangeEvent(
    GoosePublisherKey PublisherKey, long Revision, DateTime TimeUtc, GooseMessageItem Frame);

public sealed class GooseMonitorSnapshot
{
    public bool IsRunning { get; init; }
    public string StatusText { get; init; } = "Stopped";
    public long TotalMessages { get; init; }
    public IReadOnlyList<GooseMessageItem> Messages { get; init; } = Array.Empty<GooseMessageItem>();
    public IReadOnlyList<GooseValueChangeEvent> ValueChanges { get; init; } = Array.Empty<GooseValueChangeEvent>();
    public long ValueChangeOverflowCount { get; init; }
    public Guid ValueChangeSessionId { get; init; }
    public IReadOnlyList<DiagnosticEventItem> Events { get; init; } = Array.Empty<DiagnosticEventItem>();
}
