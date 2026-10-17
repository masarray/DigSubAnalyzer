namespace ProcessBus.Core.Models;

public sealed class DiagnosticEventItem
{
    public DateTime TimestampUtc { get; set; }
    public string Severity { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;

    // Separate a concise finding from its unchanged forensic message.
    // This is presentation only: the full evidence remains in Message.
    public string FindingText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Message))
                return "No description";
            var separator = Message.IndexOf(':');
            if (separator is >= 8 and <= 96)
                return Message[..separator].Trim();
            return Message.Length <= 96 ? Message : Message[..93] + "…";
        }
    }

    public string SourceText
    {
        get
        {
            // Only surface an identity when it occurs explicitly in the captured evidence.
            var at = Message.IndexOf("svID=", StringComparison.OrdinalIgnoreCase);
            if (at < 0)
                return "Capture";
            at += 5;
            var end = at;
            while (end < Message.Length && end - at < 40 &&
                   Message[end] is not (';' or ',' or ' ' or '\r' or '\n'))
                end++;
            return end > at ? Message[at..end] : "Capture";
        }
    }
}
