namespace ProcessBus.Iec61850.Raw.Ptp;

/// <summary>
/// Passive PTP evidence, isolated by domain + sourcePortIdentity + transport.
/// A received Announce is evidence of a source, never evidence of servo LOCKED.
/// The source catalog is bounded to avoid memory growth on noisy/malicious links.
/// </summary>
public sealed class PtpHealthTracker
{
    private static readonly TimeSpan AnnounceFreshness = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan SyncFreshness = TimeSpan.FromSeconds(2);
    private const int MaximumObservedSources = 64;

    private readonly Dictionary<string, SourceState> _sources = new(StringComparer.Ordinal);
    private readonly HashSet<string> _transports = new(StringComparer.OrdinalIgnoreCase);
    private long _totalMessages;
    private long _grandmasterChangeCount;
    private string? _pendingEvent;

    public void Observe(PtpMessage message)
    {
        _totalMessages++;
        if (!string.IsNullOrWhiteSpace(message.TransportText))
            _transports.Add(message.TransportText);

        var key = $"{message.DomainNumber}|{message.SourcePortIdentity}|{message.TransportText}";
        if (!_sources.TryGetValue(key, out var source))
        {
            if (_sources.Count == MaximumObservedSources)
            {
                // Preserve currently active sources; remove the least recent identity.
                var oldest = _sources.OrderBy(x => x.Value.LastMessageUtc).First().Key;
                _sources.Remove(oldest);
            }

            source = new SourceState(message.DomainNumber, message.SourcePortIdentity);
            _sources.Add(key, source);
        }

        if (message.IsAnnounce && !string.IsNullOrWhiteSpace(message.GrandmasterIdentity) &&
            !string.IsNullOrWhiteSpace(source.GrandmasterIdentity) &&
            !string.Equals(source.GrandmasterIdentity, message.GrandmasterIdentity, StringComparison.OrdinalIgnoreCase))
        {
            _grandmasterChangeCount++;
            _pendingEvent = $"PTP grandmaster changed within source {source.Identity}, domain {source.Domain}: " +
                $"{source.GrandmasterIdentity} -> {message.GrandmasterIdentity}.";
        }

        source.Observe(message);
    }

    public void Reset()
    {
        _sources.Clear();
        _transports.Clear();
        _totalMessages = 0;
        _grandmasterChangeCount = 0;
        _pendingEvent = null;
    }

    public string? ConsumeEvent()
    {
        var eventText = _pendingEvent;
        _pendingEvent = null;
        return eventText;
    }

    public PtpHealthSnapshot CreateSnapshot(DateTime nowUtc)
    {
        // Latest *fresh* Announce takes precedence over unrelated Sync traffic.
        // Deterministic tie breaking is important with interleaved sources.
        var source = _sources.Values
            .OrderByDescending(x => x.LastAnnounceUtc.HasValue &&
                IsFresh(nowUtc, x.LastAnnounceUtc.Value, AnnounceFreshness))
            .ThenByDescending(x => x.LastAnnounceUtc)
            .ThenByDescending(x => x.LastMessageUtc)
            .ThenBy(x => x.Identity, StringComparer.Ordinal)
            .FirstOrDefault();

        if (source is null)
        {
            return new PtpHealthSnapshot
            {
                Observed = false,
                StatusText = "Not observed",
                TotalMessages = _totalMessages,
                GrandmasterChangeCount = _grandmasterChangeCount,
                ProfileHintText = "PTP not observed"
            };
        }

        source.Trim(nowUtc);
        var announceFresh = source.LastAnnounceUtc.HasValue &&
            IsFresh(nowUtc, source.LastAnnounceUtc.Value, AnnounceFreshness);
        var syncFresh = source.LastSyncUtc.HasValue &&
            IsFresh(nowUtc, source.LastSyncUtc.Value, SyncFreshness);
        var status = source.LastAnnounceUtc.HasValue && !announceFresh
            ? "Announce timeout"
            : source.LastSyncUtc.HasValue && !syncFresh
                ? "Sync timeout"
                : announceFresh || syncFresh
                    ? "Observed"
                    : "Observed message flow (unverified)";

        return new PtpHealthSnapshot
        {
            Observed = true,
            StatusText = status,
            TotalMessages = _totalMessages,
            DomainNumber = source.Domain,
            SourcePortIdentity = source.Identity,
            ObservedSourceCount = _sources.Count,
            GrandmasterIdentity = source.GrandmasterIdentity ?? "N/A",
            ClockClass = source.LastAnnounce?.GrandmasterClockClass,
            ClockAccuracyText = source.LastAnnounce?.ClockAccuracyText ?? "N/A",
            StepsRemoved = source.LastAnnounce?.StepsRemoved,
            SyncRatePerSecond = source.SyncTimes.Rate(nowUtc),
            AnnounceRatePerSecond = source.AnnounceTimes.Rate(nowUtc),
            FollowUpRatePerSecond = source.FollowUpTimes.Rate(nowUtc),
            MatchedTwoStepCount = source.MatchedTwoStepCount,
            UnmatchedFollowUpCount = source.UnmatchedFollowUpCount,
            LastMessageUtc = source.LastMessageUtc,
            LastSyncUtc = source.LastSyncUtc,
            LastAnnounceUtc = source.LastAnnounceUtc,
            LastFollowUpUtc = source.LastFollowUpUtc,
            GrandmasterChangeCount = _grandmasterChangeCount,
            ProfileHintText = $"{source.LastAnnounce?.ProfileHintText ?? "PTP v2 observed"}; " +
                $"{_sources.Count} source(s); no passive clock-lock claim",
            TransportText = _transports.Count == 0
                ? "N/A"
                : string.Join(", ", _transports.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        };
    }

    private static bool IsFresh(DateTime now, DateTime sample, TimeSpan duration) =>
        sample <= now && now - sample <= duration;

    private sealed class SourceState
    {
        public SourceState(byte domain, string identity)
        {
            Domain = domain;
            Identity = identity;
        }

        public byte Domain { get; }
        public string Identity { get; }
        public DateTime LastMessageUtc { get; private set; }
        public DateTime? LastSyncUtc { get; private set; }
        public DateTime? LastAnnounceUtc { get; private set; }
        public DateTime? LastFollowUpUtc { get; private set; }
        public string? GrandmasterIdentity { get; private set; }
        public PtpMessage? LastAnnounce { get; private set; }
        public PtpRateWindow SyncTimes { get; } = new();
        public PtpRateWindow AnnounceTimes { get; } = new();
        public PtpRateWindow FollowUpTimes { get; } = new();
        private readonly Dictionary<ushort, DateTime> _pendingTwoStepSync = new();
        public long MatchedTwoStepCount { get; private set; }
        public long UnmatchedFollowUpCount { get; private set; }

        public void Observe(PtpMessage message)
        {
            LastMessageUtc = message.CaptureTimeUtc;
            if (message.IsSync)
            {
                LastSyncUtc = message.CaptureTimeUtc;
                SyncTimes.Observe(message.CaptureTimeUtc);
                if ((message.Flags & 0x0200) != 0) // IEEE1588 twoStepFlag
                {
                    if (_pendingTwoStepSync.Count >= 64 && !_pendingTwoStepSync.ContainsKey(message.SequenceId))
                    {
                        var oldest = _pendingTwoStepSync.OrderBy(x => x.Value).First().Key;
                        _pendingTwoStepSync.Remove(oldest);
                    }
                    _pendingTwoStepSync[message.SequenceId] = message.CaptureTimeUtc;
                }
            }
            else if (message.IsFollowUp)
            {
                LastFollowUpUtc = message.CaptureTimeUtc;
                FollowUpTimes.Observe(message.CaptureTimeUtc);
                if (_pendingTwoStepSync.Remove(message.SequenceId, out var syncTime) &&
                    message.CaptureTimeUtc >= syncTime &&
                    message.CaptureTimeUtc - syncTime <= SyncFreshness)
                    MatchedTwoStepCount++;
                else
                    UnmatchedFollowUpCount++;
            }
            else if (message.IsAnnounce)
            {
                LastAnnounceUtc = message.CaptureTimeUtc;
                LastAnnounce = message;
                GrandmasterIdentity = message.GrandmasterIdentity;
                AnnounceTimes.Observe(message.CaptureTimeUtc);
            }
        }

        public void Trim(DateTime now)
        {
            // Sequence correlation is per-source and strictly bounded. A missing
            // Follow_Up can never remain eligible after its evidence expires.
            foreach (var key in _pendingTwoStepSync
                         .Where(entry => now >= entry.Value && now - entry.Value > SyncFreshness)
                         .Select(entry => entry.Key).ToArray())
                _pendingTwoStepSync.Remove(key);
        }
    }
}

public sealed class PtpHealthSnapshot
{
    public bool Observed { get; init; }
    public string StatusText { get; init; } = "Not observed";
    public long TotalMessages { get; init; }
    public byte? DomainNumber { get; init; }
    public string SourcePortIdentity { get; init; } = "N/A";
    public int ObservedSourceCount { get; init; }
    public string GrandmasterIdentity { get; init; } = "N/A";
    public byte? ClockClass { get; init; }
    public string ClockAccuracyText { get; init; } = "N/A";
    public ushort? StepsRemoved { get; init; }
    public double? SyncRatePerSecond { get; init; }
    public double? AnnounceRatePerSecond { get; init; }
    public double? FollowUpRatePerSecond { get; init; }
    public long MatchedTwoStepCount { get; init; }
    public long UnmatchedFollowUpCount { get; init; }
    public DateTime? LastMessageUtc { get; init; }
    public DateTime? LastSyncUtc { get; init; }
    public DateTime? LastAnnounceUtc { get; init; }
    public DateTime? LastFollowUpUtc { get; init; }
    public long GrandmasterChangeCount { get; init; }
    public string ProfileHintText { get; init; } = "PTP not observed";
    public string TransportText { get; init; } = "N/A";
}
