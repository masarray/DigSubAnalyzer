namespace ProcessBus.Iec61850.Raw.Ptp;

public sealed class PtpHealthTracker
{
    private static readonly TimeSpan RateWindow = TimeSpan.FromSeconds(5);
    private readonly Queue<DateTime> _syncTimes = new();
    private readonly Queue<DateTime> _announceTimes = new();
    private readonly Queue<DateTime> _followUpTimes = new();
    private readonly Dictionary<string, long> _messageCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _transports = new(StringComparer.OrdinalIgnoreCase);
    private long _totalMessages;
    private long _grandmasterChangeCount;
    private string? _lastGrandmasterIdentity;
    private byte? _lastDomainNumber;
    private DateTime? _lastMessageUtc;
    private DateTime? _lastSyncUtc;
    private DateTime? _lastAnnounceUtc;
    private DateTime? _lastFollowUpUtc;
    private PtpMessage? _lastAnnounce;
    private string? _pendingEvent;

    public void Observe(PtpMessage message)
    {
        _totalMessages++;
        _lastMessageUtc = message.CaptureTimeUtc;
        _lastDomainNumber = message.DomainNumber;

        if (!_messageCounts.TryAdd(message.MessageType, 1))
            _messageCounts[message.MessageType]++;

        if (!string.IsNullOrWhiteSpace(message.TransportText))
            _transports.Add(message.TransportText);

        if (message.IsSync)
        {
            _lastSyncUtc = message.CaptureTimeUtc;
            Track(_syncTimes, message.CaptureTimeUtc);
        }
        else if (message.IsFollowUp)
        {
            _lastFollowUpUtc = message.CaptureTimeUtc;
            Track(_followUpTimes, message.CaptureTimeUtc);
        }
        else if (message.IsAnnounce)
        {
            _lastAnnounceUtc = message.CaptureTimeUtc;
            _lastAnnounce = message;
            Track(_announceTimes, message.CaptureTimeUtc);

            if (!string.IsNullOrWhiteSpace(message.GrandmasterIdentity))
            {
                if (!string.IsNullOrWhiteSpace(_lastGrandmasterIdentity) &&
                    !string.Equals(_lastGrandmasterIdentity, message.GrandmasterIdentity, StringComparison.OrdinalIgnoreCase))
                {
                    _grandmasterChangeCount++;
                    _pendingEvent = $"PTP grandmaster changed: {_lastGrandmasterIdentity} -> {message.GrandmasterIdentity}.";
                }

                _lastGrandmasterIdentity = message.GrandmasterIdentity;
            }
        }
    }

    public void Reset()
    {
        _syncTimes.Clear();
        _announceTimes.Clear();
        _followUpTimes.Clear();
        _messageCounts.Clear();
        _transports.Clear();
        _totalMessages = 0;
        _grandmasterChangeCount = 0;
        _lastGrandmasterIdentity = null;
        _lastDomainNumber = null;
        _lastMessageUtc = null;
        _lastSyncUtc = null;
        _lastAnnounceUtc = null;
        _lastFollowUpUtc = null;
        _lastAnnounce = null;
        _pendingEvent = null;
    }

    public string? ConsumeEvent()
    {
        var result = _pendingEvent;
        _pendingEvent = null;
        return result;
    }

    public PtpHealthSnapshot CreateSnapshot(DateTime nowUtc)
    {
        Trim(_syncTimes, nowUtc);
        Trim(_announceTimes, nowUtc);
        Trim(_followUpTimes, nowUtc);

        var observed = _totalMessages > 0;
        var announceAge = _lastAnnounceUtc.HasValue ? nowUtc - _lastAnnounceUtc.Value : (TimeSpan?)null;
        var syncAge = _lastSyncUtc.HasValue ? nowUtc - _lastSyncUtc.Value : (TimeSpan?)null;

        var status = !observed
            ? "Not observed"
            : announceAge is { TotalSeconds: > 6 }
                ? "Announce timeout"
                : syncAge is { TotalSeconds: > 2 } && _syncTimes.Count > 0
                    ? "Sync timeout"
                    : _announceTimes.Count > 0 || _syncTimes.Count > 0
                        ? "Observed"
                        : "Observed message flow";

        var gm = _lastGrandmasterIdentity ?? _lastAnnounce?.GrandmasterIdentity ?? "N/A";
        var clockClass = _lastAnnounce?.GrandmasterClockClass;
        var clockAccuracy = _lastAnnounce?.GrandmasterClockAccuracy is { } acc ? $"0x{acc:X2}" : "N/A";
        var stepsRemoved = _lastAnnounce?.StepsRemoved;
        var profileHint = _lastAnnounce?.ProfileHintText ?? (observed ? "PTP v2 observed" : "PTP not observed");

        return new PtpHealthSnapshot
        {
            Observed = observed,
            StatusText = status,
            TotalMessages = _totalMessages,
            DomainNumber = _lastDomainNumber,
            GrandmasterIdentity = gm,
            ClockClass = clockClass,
            ClockAccuracyText = clockAccuracy,
            StepsRemoved = stepsRemoved,
            SyncRatePerSecond = CalculateRate(_syncTimes, nowUtc),
            AnnounceRatePerSecond = CalculateRate(_announceTimes, nowUtc),
            FollowUpRatePerSecond = CalculateRate(_followUpTimes, nowUtc),
            LastMessageUtc = _lastMessageUtc,
            LastSyncUtc = _lastSyncUtc,
            LastAnnounceUtc = _lastAnnounceUtc,
            LastFollowUpUtc = _lastFollowUpUtc,
            GrandmasterChangeCount = _grandmasterChangeCount,
            ProfileHintText = profileHint,
            TransportText = _transports.Count == 0
                ? "N/A"
                : string.Join(", ", _transports.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        };
    }

    private static void Track(Queue<DateTime> queue, DateTime timestampUtc)
    {
        queue.Enqueue(timestampUtc);
        Trim(queue, timestampUtc);
    }

    private static void Trim(Queue<DateTime> queue, DateTime nowUtc)
    {
        while (queue.Count > 0 && nowUtc - queue.Peek() > RateWindow)
            queue.Dequeue();
    }

    private static double? CalculateRate(Queue<DateTime> queue, DateTime nowUtc)
    {
        if (queue.Count == 0)
            return null;

        Trim(queue, nowUtc);
        return queue.Count / Math.Max(0.001, RateWindow.TotalSeconds);
    }
}

public sealed class PtpHealthSnapshot
{
    public bool Observed { get; init; }
    public string StatusText { get; init; } = "Not observed";
    public long TotalMessages { get; init; }
    public byte? DomainNumber { get; init; }
    public string GrandmasterIdentity { get; init; } = "N/A";
    public byte? ClockClass { get; init; }
    public string ClockAccuracyText { get; init; } = "N/A";
    public ushort? StepsRemoved { get; init; }
    public double? SyncRatePerSecond { get; init; }
    public double? AnnounceRatePerSecond { get; init; }
    public double? FollowUpRatePerSecond { get; init; }
    public DateTime? LastMessageUtc { get; init; }
    public DateTime? LastSyncUtc { get; init; }
    public DateTime? LastAnnounceUtc { get; init; }
    public DateTime? LastFollowUpUtc { get; init; }
    public long GrandmasterChangeCount { get; init; }
    public string ProfileHintText { get; init; } = "PTP not observed";
    public string TransportText { get; init; } = "N/A";
}
