using ProcessBus.Core.Models;

namespace ProcessBus.Core.Presentation;

/// <summary>
/// Bounded presentation projection only: forensic events are never modified or
/// discarded. Group exact repeated messages from the same severity/source,
/// keeping the newest timestamp and number of occurrences in the visible
/// window. Deliberately do not normalize technical identifiers or numbers.
/// </summary>
public static class DiagnosticEventSummarizer
{
    public const int MaxSourceRows = 200;

    public static IReadOnlyList<DiagnosticEventItem> Summarize(
        IReadOnlyList<DiagnosticEventItem> newestFirst)
    {
        if (newestFirst.Count == 0)
            return Array.Empty<DiagnosticEventItem>();

        var result = new List<DiagnosticEventItem>(Math.Min(newestFirst.Count, MaxSourceRows));
        var byKey = new Dictionary<(string Severity, string Message, string Source), DiagnosticEventItem>();
        for (var i = 0; i < newestFirst.Count && i < MaxSourceRows; i++)
        {
            var source = newestFirst[i];
            var key = (source.Severity, source.Message, source.SourceText);
            if (byKey.TryGetValue(key, out var existing))
            {
                existing.OccurrenceCount++;
                if (source.TimestampUtc < existing.FirstOccurrenceUtc)
                    existing.FirstOccurrenceUtc = source.TimestampUtc;
                continue;
            }

            var item = new DiagnosticEventItem
            {
                TimestampUtc = source.TimestampUtc,
                FirstOccurrenceUtc = source.TimestampUtc,
                Severity = source.Severity,
                Message = source.Message,
                OccurrenceCount = 1
            };
            result.Add(item);
            byKey.Add(key, item);
        }
        return result;
    }
}
