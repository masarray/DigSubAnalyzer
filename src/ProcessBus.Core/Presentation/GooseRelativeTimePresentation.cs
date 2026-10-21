namespace ProcessBus.Core.Presentation;

/// <summary>
/// Capture-relative time starts at the first observed GOOSE message and
/// never substitutes time since the previous state-change event.
/// </summary>
public static class GooseRelativeTimePresentation
{
    public static double ElapsedMilliseconds(DateTime firstObservedUtc, DateTime observedUtc)
    {
        var elapsed = (observedUtc - firstObservedUtc).TotalMilliseconds;
        return double.IsFinite(elapsed) ? Math.Max(0, elapsed) : 0;
    }
}
