using ProcessBus.Core.Models;

namespace ProcessBus.Core.Presentation;

/// <summary>
/// Bounded, source-faithful UI projection of up to 200 newest forensic reports.
/// Raw events and their full evidence are never edited. Only known periodic
/// report families may aggregate despite changing detail fields.
/// </summary>
public static class DiagnosticEventSummarizer
{
    public const int MaxSourceRows = 200;
    private const string SvArrivalExcursionTitle = "SV arrival timing excursions >= 300 us";

    /// <summary>
    /// The analyzer emits periodic timing summaries with dynamic total/smpCnt/Δ.
    /// Those are successive reports about the same fault family, not separate
    /// categories. Keep severity and svID separate in the caller's key; never
    /// normalize arbitrary numeric evidence or unrelated diagnostic titles.
    /// </summary>
    public static string GetGroupingText(string message)
    {
        if (message.StartsWith(SvArrivalExcursionTitle, StringComparison.Ordinal) &&
            message.Length > SvArrivalExcursionTitle.Length &&
            message[SvArrivalExcursionTitle.Length] == ':' &&
            message.Contains("svID=", StringComparison.OrdinalIgnoreCase))
            return SvArrivalExcursionTitle;

        return message;
    }

    public static IReadOnlyList<DiagnosticEventItem> Summarize(
        IReadOnlyList<DiagnosticEventItem> newestFirst)
    {
        if (newestFirst.Count == 0)
            return Array.Empty<DiagnosticEventItem>();

        var result = new List<DiagnosticEventItem>(Math.Min(newestFirst.Count, MaxSourceRows));
        var byKey = new Dictionary<(string Severity, string Family, string Source), DiagnosticEventItem>();
        for (var i = 0; i < newestFirst.Count && i < MaxSourceRows; i++)
        {
            var source = newestFirst[i];
            var groupText = GetGroupingText(source.Message);
            var key = (source.Severity, groupText, source.SourceText);
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
                OccurrenceCount = 1,
                IsPeriodicSummary = groupText == SvArrivalExcursionTitle &&
                    source.Message.Length > SvArrivalExcursionTitle.Length
            };
            result.Add(item);
            byKey.Add(key, item);
        }

        return result;
    }
}
