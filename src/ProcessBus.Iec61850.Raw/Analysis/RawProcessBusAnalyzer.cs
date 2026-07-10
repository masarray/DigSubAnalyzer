using ProcessBus.Core.Models;
using ProcessBus.Iec61850.Raw.Decoding;
using ProcessBus.Iec61850.Raw.Goose;
using ProcessBus.Iec61850.Raw.Protocol;
using ProcessBus.Iec61850.Raw.Ptp;
using ProcessBus.Iec61850.Raw.Sv;
using System.Buffers.Binary;
using System.Diagnostics;

namespace ProcessBus.Iec61850.Raw.Analysis;

public sealed class RawProcessBusAnalyzer
{
    private static readonly TimeSpan MalformedEventInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan JitterEventInterval = TimeSpan.FromSeconds(5);
    private readonly object _gate = new();
    private readonly Dictionary<string, SvStreamState> _svStreams = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GooseState> _gooseStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AggregatedEventState> _aggregatedEvents = new(StringComparer.OrdinalIgnoreCase);
    private readonly PtpHealthTracker _ptpTracker = new();
    private readonly Queue<PtpEventItem> _recentPtpEvents = new();
    private readonly List<DiagnosticEventItem> _events = new();
    private IReadOnlyList<SvChannelMappingProfile> _svChannelMappings = Array.Empty<SvChannelMappingProfile>();
    private string _svChannelMappingSetSignature = string.Empty;
    private long _totalFrames;
    private long _svPackets;
    private long _goosePackets;
    private long _ptpPackets;
    private long _decodeErrors;
    private string? _selectedSvKey;
    private int _requestedScopeCycles = 2;
    private int _svFirstSeenSequence;
    private int _gooseFirstSeenSequence;
    private DateTime _startedUtc = DateTime.UtcNow;
    private DateTime? _lastSvSeenUtc;
    private DateTime? _lastGooseSeenUtc;
    private DateTime? _lastPtpSeenUtc;

    public void ObserveFrame(ReadOnlyMemory<byte> frameBytes, DateTime? captureTimeUtc = null, long? captureTicks = null)
    {
        // Defensive copy: callers may hand in memory backed by a reusable buffer, while
        // decoded ASDUs (RefrTm/SamplePayload) hold slices of these bytes long-term.
        ObserveOwnedFrame(frameBytes.ToArray(), captureTimeUtc, captureTicks);
    }

    /// <summary>
    /// Zero-copy variant for callers that transfer ownership of a freshly allocated,
    /// never-reused array (e.g. the Npcap pump, which already copies out of the pcap
    /// buffer per frame). Avoids a second full-frame allocation on the SV hot path.
    /// </summary>
    public void ObserveOwnedFrame(byte[] ownedFrameBytes, DateTime? captureTimeUtc = null, long? captureTicks = null)
    {
        var stableBytes = ownedFrameBytes;

        lock (_gate)
        {
            _totalFrames++;

            if (!RawProcessBusDecoder.TryDecode(stableBytes, out var result, captureTimeUtc, captureTicks))
            {
                _decodeErrors++;
                AddAggregatedEvent(
                    "raw.malformed",
                    "Warning",
                    "Rejected malformed process-bus frames",
                    "Capture filter matched SV/GOOSE/PTP EtherType, but frame/APDU decode failed.",
                    MalformedEventInterval);
                return;
            }

            if (result.SampledValues is not null)
                ObserveSampledValues(result.SampledValues);
            else if (result.Goose is not null)
                ObserveGoose(result.Goose);
            else if (result.Ptp is not null)
                ObservePtp(result.Ptp);
            else
                AddEvent("Warning", $"Raw frame decoded as {result.Frame.Kind}, but protocol fields are incomplete.");
        }
    }

    public AnalyzerSnapshot GetAnalyzerSnapshot()
    {
        lock (_gate)
        {
            FlushAggregatedEvents();

            var streams = _svStreams.Values
                .OrderBy(x => x.FirstSeenOrder)
                .Select(x => x.ToStreamItem())
                .ToArray();

            var selected = ResolveSelectedStream();

            return new AnalyzerSnapshot
            {
                Streams = streams,
                SelectedStreamId = selected?.Key,
                SelectedStreamDetails = selected?.ToDetails(),
                AnalogValues = selected?.ToAnalogValues() ?? new AnalogValuesSnapshot(),
                Waveform = selected?.ToWaveform() ?? new WaveformSnapshot
                {
                    StatusText = "Raw waveform waiting for decoded SV samples."
                },
                Diagnostics = BuildDiagnostics(selected),
                Events = _events.OrderByDescending(x => x.TimestampUtc).Take(100).ToArray(),
                ProtocolMonitor = BuildProtocolMonitorSnapshot(),
                PtpEvents = _recentPtpEvents.Reverse().Take(120).ToArray()
            };
        }
    }

    public GooseMonitorSnapshot GetGooseSnapshot()
    {
        lock (_gate)
        {
            FlushAggregatedEvents();

            return new GooseMonitorSnapshot
            {
                IsRunning = true,
                StatusText = _gooseStates.Count == 0
                    ? "Raw GOOSE monitor waiting for traffic"
                    : $"Raw GOOSE monitor active: {_gooseStates.Count} message(s)",
                TotalMessages = _goosePackets,
                Messages = _gooseStates.Values
                    .OrderBy(x => x.FirstSeenOrder)
                    .Select(x => x.Clone())
                    .ToArray(),
                Events = _events
                    .Where(x => x.Message.Contains("GOOSE", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.TimestampUtc)
                    .Take(100)
                    .ToArray()
            };
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _svStreams.Clear();
            _gooseStates.Clear();
            _events.Clear();
            _aggregatedEvents.Clear();
            _selectedSvKey = null;
            _svFirstSeenSequence = 0;
            _gooseFirstSeenSequence = 0;
            _totalFrames = 0;
            _svPackets = 0;
            _goosePackets = 0;
            _ptpPackets = 0;
            _decodeErrors = 0;
            _ptpTracker.Reset();
            _recentPtpEvents.Clear();
            _lastSvSeenUtc = null;
            _lastGooseSeenUtc = null;
            _lastPtpSeenUtc = null;
            _startedUtc = DateTime.UtcNow;
        }
    }

    public void SelectStream(string? streamId)
    {
        lock (_gate)
            _selectedSvKey = string.IsNullOrWhiteSpace(streamId) ? null : streamId;
    }

    public void SetScopeCycles(int cycles)
    {
        var clamped = Math.Clamp(cycles, 1, 8);
        lock (_gate)
        {
            if (_requestedScopeCycles == clamped)
                return;

            _requestedScopeCycles = clamped;
            foreach (var stream in _svStreams.Values)
                stream.SetScopeCycles(clamped);
        }
    }

    public void SetSvChannelMappings(IReadOnlyList<SvChannelMappingProfile> profiles)
    {
        lock (_gate)
        {
            var nextMappings = profiles.Where(x => x.HasRenderableChannels).ToArray();
            var nextSignature = BuildMappingSetSignature(nextMappings);
            if (string.Equals(_svChannelMappingSetSignature, nextSignature, StringComparison.Ordinal))
            {
                _svChannelMappings = nextMappings;
                return;
            }

            _svChannelMappings = nextMappings;
            _svChannelMappingSetSignature = nextSignature;

            foreach (var stream in _svStreams.Values)
                stream.ResetRenderMappingState();
        }
    }

    private void ObserveSampledValues(SampledValuePacket packet)
    {
        _svPackets++;
        _lastSvSeenUtc = packet.Frame.CaptureTimeUtc;

        if (packet.Asdus.Count == 0)
        {
            _decodeErrors++;
            AddEvent("Warning", "Raw SV frame has no decoded ASDU.");
            return;
        }

        foreach (var asdu in packet.Asdus)
        {
            var key = BuildSvKey(packet.Frame, asdu);

            if (!_svStreams.TryGetValue(key, out var state))
            {
                state = new SvStreamState(key, ++_svFirstSeenSequence, _requestedScopeCycles);
                _svStreams[key] = state;
                AddEvent("Info", $"Raw SV stream detected: {DescribeSv(packet.Frame, asdu)}.");
            }

            state.Observe(packet.Frame, asdu, ResolveSvMappingProfile(packet.Frame, asdu));
            if (state.ConsumeJitterEvidence() is { } jitterEvidence)
            {
                AddAggregatedEvent(
                    $"sv.jitter.{state.Key}",
                    state.HasIntegrityIssue ? "Warning" : "Diagnostic",
                    "SV arrival timing excursions >= 300 us",
                    jitterEvidence,
                    JitterEventInterval);
            }
        }
    }

    private SvChannelMappingProfile? ResolveSvMappingProfile(ProcessBusFrame frame, SampledValueAsdu asdu)
    {
        if (_svChannelMappings.Count == 0)
            return null;

        var appId = FormatAppId(frame.AppId);
        var vlanId = frame.Ethernet.Vlan?.VlanId.ToString() ?? "Untagged";
        var confRev = asdu.ConfRev?.ToString() ?? "N/A";

        return _svChannelMappings
            .Where(profile => IsSvProfileIdentityCompatible(profile, frame, asdu, appId, vlanId, confRev))
            .Select(profile => new
            {
                Profile = profile,
                Score =
                    (TextMatches(profile.SvId, asdu.SvId) ? 45 : 0) +
                    (AppIdMatches(profile.AppId, appId) ? 35 : 0) +
                    (TextMatches(profile.DestinationMac, frame.Ethernet.DestinationMac) ? 20 : 0) +
                    (VlanMatches(profile.VlanId, vlanId) ? 10 : 0) +
                    (TextMatches(profile.ConfRevText, confRev) ? 15 : 0) +
                    (TextMatches(profile.DataSetReference, asdu.DataSet) ? 20 : 0)
            })
            .Where(candidate => candidate.Score >= 65)
            .OrderByDescending(candidate => candidate.Score)
            .Select(candidate => candidate.Profile)
            .FirstOrDefault();
    }

    private static string BuildMappingSetSignature(IReadOnlyList<SvChannelMappingProfile> profiles)
    {
        return string.Join("|", profiles
            .OrderBy(profile => profile.ProfileKey, StringComparer.OrdinalIgnoreCase)
            .Select(profile =>
            {
                var elements = string.Join(",", profile.Elements
                    .OrderBy(element => element.ChannelName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(element => element.ElementIndex)
                    .Select(element => $"{element.ChannelName}:{element.ElementIndex}"));
                return $"{profile.ProfileKey}:{elements}";
            }));
    }

    private static bool IsSvProfileIdentityCompatible(
        SvChannelMappingProfile profile,
        ProcessBusFrame frame,
        SampledValueAsdu asdu,
        string observedAppId,
        string observedVlan,
        string observedConfRev)
    {
        if (HasValue(profile.SvId) && HasValue(asdu.SvId) && !TextMatches(profile.SvId, asdu.SvId))
            return false;
        if (HasValue(profile.AppId) && HasValue(observedAppId) && !AppIdMatches(profile.AppId, observedAppId))
            return false;
        if (HasValue(profile.DestinationMac) && HasValue(frame.Ethernet.DestinationMac) && !TextMatches(profile.DestinationMac, frame.Ethernet.DestinationMac))
            return false;
        if (HasValue(profile.VlanId) && HasValue(observedVlan) && !VlanMatches(profile.VlanId, observedVlan))
            return false;
        if (HasValue(profile.ConfRevText) && HasValue(observedConfRev) && !TextMatches(profile.ConfRevText, observedConfRev))
            return false;
        if (HasValue(profile.DataSetReference) && HasValue(asdu.DataSet) && !TextMatches(profile.DataSetReference, asdu.DataSet))
            return false;

        return true;
    }

    private static bool HasValue(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           !string.Equals(value, "N/A", StringComparison.OrdinalIgnoreCase) &&
           !string.Equals(value, "UNTAGGED", StringComparison.OrdinalIgnoreCase);

    private static bool TextMatches(string? expected, string? observed)
    {
        var a = NormalizeComparable(expected);
        var b = NormalizeComparable(observed);
        return !string.IsNullOrWhiteSpace(a) &&
               !string.IsNullOrWhiteSpace(b) &&
               string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static bool AppIdMatches(string? expected, string? observed)
    {
        var a = NormalizeAppId(expected);
        var b = NormalizeAppId(observed);
        return !string.IsNullOrWhiteSpace(a) &&
               !string.IsNullOrWhiteSpace(b) &&
               string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static bool VlanMatches(string? expected, string? observed)
    {
        var a = NormalizeVlan(expected);
        var b = NormalizeVlan(observed);
        return !string.IsNullOrWhiteSpace(a) &&
               !string.IsNullOrWhiteSpace(b) &&
               string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeComparable(string? value)
        => string.IsNullOrWhiteSpace(value) || string.Equals(value, "N/A", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : value.Trim().Replace("-", ":", StringComparison.Ordinal).ToUpperInvariant();

    private static string NormalizeAppId(string? value)
    {
        var text = NormalizeComparable(value).Replace("APPID", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        if (text.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
            text = text[2..];
        text = new string(text.Where(Uri.IsHexDigit).ToArray()).TrimStart('0');
        return string.IsNullOrWhiteSpace(text) ? "0" : text;
    }

    private static string NormalizeVlan(string? value)
    {
        var text = NormalizeComparable(value);
        if (string.IsNullOrWhiteSpace(text) || string.Equals(text, "UNTAGGED", StringComparison.OrdinalIgnoreCase))
            return text;

        var slashIndex = text.IndexOf('/', StringComparison.Ordinal);
        if (slashIndex >= 0)
            text = text[..slashIndex];

        var digits = new string(text.Where(char.IsDigit).ToArray()).TrimStart('0');
        return string.IsNullOrWhiteSpace(digits) ? "0" : digits;
    }


    private void ObservePtp(PtpMessage message)
    {
        _ptpPackets++;
        _lastPtpSeenUtc = message.CaptureTimeUtc;
        _ptpTracker.Observe(message);
        TrackPtpEvent(message);

        if (_ptpPackets == 1)
            AddEvent("Info", $"PTP timing traffic detected over {message.TransportText}: {message.MessageType}, domain={message.DomainNumber}, source={message.SourcePortIdentity}.");

        if (_ptpTracker.ConsumeEvent() is { } eventText)
            AddEvent("Warning", eventText);
    }

    private void TrackPtpEvent(PtpMessage message)
    {
        var source = !string.IsNullOrWhiteSpace(message.NetworkSourceText) && message.NetworkSourceText != "N/A"
            ? message.NetworkSourceText
            : message.SourceMac;
        var destination = !string.IsNullOrWhiteSpace(message.NetworkDestinationText) && message.NetworkDestinationText != "N/A"
            ? message.NetworkDestinationText
            : message.DestinationMac;

        _recentPtpEvents.Enqueue(new PtpEventItem
        {
            TimestampUtc = message.CaptureTimeUtc,
            Transport = message.TransportText,
            MessageType = message.MessageType,
            Source = source,
            Destination = destination,
            DomainText = $"D{message.DomainNumber}",
            SequenceIdText = message.SequenceId.ToString(),
            ClockIdentity = message.SourceClockIdentity
        });

        while (_recentPtpEvents.Count > 500)
            _recentPtpEvents.Dequeue();
    }

    private void ObserveGoose(GoosePacket packet)
    {
        _goosePackets++;
        _lastGooseSeenUtc = packet.Frame.CaptureTimeUtc;
        var item = BuildGooseItem(packet);
        var key = item.MessageId;

        if (!_gooseStates.TryGetValue(key, out var state))
        {
            AnnotateGooseChanges(item, previous: null);
            _gooseStates[key] = new GooseState(item, ++_gooseFirstSeenSequence);
            AddEvent("Info", $"Raw GOOSE message detected: {key}; values={item.ValuesText}.");
            return;
        }

        var previous = state.Item;
        AnnotateGooseChanges(item, previous);

        if (previous.StNum != item.StNum || previous.SqNum != item.SqNum)
        {
            var summary = string.Equals(item.ChangedSummaryText, "N/A", StringComparison.OrdinalIgnoreCase)
                ? $"stNum={item.StNum} sqNum={item.SqNum}"
                : $"stNum={item.StNum} sqNum={item.SqNum}; {item.ChangedSummaryText}";
            AddEvent("Info", $"Raw GOOSE update: {key} {summary}.");
        }

        state.Item = item;
    }

    private SvStreamState? ResolveSelectedStream()
    {
        if (_selectedSvKey is not null &&
            _svStreams.TryGetValue(_selectedSvKey, out var selected))
            return selected;

        selected = _svStreams.Values
            .OrderBy(x => x.FirstSeenOrder)
            .FirstOrDefault();
        _selectedSvKey = selected?.Key;
        return selected;
    }

    private ProtocolMonitorSnapshot BuildProtocolMonitorSnapshot()
    {
        var now = DateTime.UtcNow;
        var ptp = _ptpTracker.CreateSnapshot(now);

        return new ProtocolMonitorSnapshot
        {
            TotalFrames = _totalFrames,
            SvFrames = _svPackets,
            GooseFrames = _goosePackets,
            PtpFrames = _ptpPackets,
            LiveSvStreams = _svStreams.Values.Count(x => now - x.LastSeenUtc <= TimeSpan.FromSeconds(2)),
            GoosePublishers = _gooseStates.Count,
            LastSvSeenUtc = _lastSvSeenUtc,
            LastGooseSeenUtc = _lastGooseSeenUtc,
            LastPtpSeenUtc = _lastPtpSeenUtc,
            SvStatusText = BuildProtocolStatus("SV", _svPackets, _lastSvSeenUtc, now, _svStreams.Count == 0 ? "no stream" : $"{_svStreams.Count} stream(s)"),
            GooseStatusText = BuildProtocolStatus("GOOSE", _goosePackets, _lastGooseSeenUtc, now, _gooseStates.Count == 0 ? "no publisher" : $"{_gooseStates.Count} publisher(s)"),
            PtpStatusText = ptp.Observed
                ? $"PTP {ptp.StatusText} - {ptp.TransportText} - {ptp.TotalMessages} msg"
                : "PTP not observed",
            TimingConfidenceText = ptp.Observed
                ? "PTP context available; Npcap timestamp remains software-screening."
                : "Arrival-only timing; no PTP context observed."
        };
    }

    private static string BuildProtocolStatus(string name, long count, DateTime? lastSeenUtc, DateTime nowUtc, string detail)
    {
        if (count <= 0 || !lastSeenUtc.HasValue)
            return $"{name} not observed";

        var age = nowUtc - lastSeenUtc.Value;
        var state = age <= TimeSpan.FromSeconds(2) ? "live"
            : age <= TimeSpan.FromSeconds(10) ? "stale"
            : "lost";
        return $"{name} {state} - {detail} - last {age.TotalSeconds:0.0}s ago";
    }

    private SvDiagnosticsSnapshot BuildDiagnostics(SvStreamState? selected)
    {
        var elapsedSeconds = Math.Max(0.001, (DateTime.UtcNow - _startedUtc).TotalSeconds);

        if (selected is null)
        {
            return ApplyPtpDiagnostics(new SvDiagnosticsSnapshot
            {
                IsRunning = true,
                StreamStatusText = _totalFrames == 0
                    ? "Raw engine waiting for process-bus traffic"
                    : $"Raw engine saw {_totalFrames} frame(s), no decoded SV stream yet",
                PacketRatePps = _totalFrames / elapsedSeconds,
                TotalPackets = _totalFrames,
                DecodeErrors = _decodeErrors,
                DecodeStatusText = _decodeErrors == 0 ? "Waiting for SV" : $"{_decodeErrors} raw frame(s) rejected by parser",
                PacketRateMeaningText = "Raw process-bus frames observed since analyzer start",
                TimebaseStatusText = "Raw monotonic capture clock pending"
            });
        }

        return ApplyPtpDiagnostics(new SvDiagnosticsSnapshot
        {
            IsRunning = true,
            StreamStatusText = "Raw SV stream active",
            PacketRatePps = selected.RecentPacketRatePps ?? selected.PacketCount / elapsedSeconds,
            TotalPackets = selected.PacketCount,
            DecodeErrors = _decodeErrors,
            SequenceErrors = selected.SequenceErrors,
            MissingSamples = selected.MissingSamples,
            LastSampleCount = selected.LastSmpCnt,
            CurrentDeltaMicroseconds = selected.CurrentDeltaMicroseconds,
            AverageDeltaMicroseconds = selected.AverageDeltaMicroseconds,
            ExpectedDeltaMicroseconds = selected.ExpectedDeltaMicroseconds,
            CurrentJitterMicroseconds = selected.CurrentJitterMicroseconds,
            AverageAbsJitterMicroseconds = selected.AverageAbsJitterMicroseconds,
            MaxAbsJitterMicroseconds = selected.MaxAbsJitterMicroseconds,
            JitterOver300MicrosecondsCount = selected.JitterOver300MicrosecondsCount,
            RecentJitterOver300MicrosecondsCount = selected.RecentJitterOver300MicrosecondsCount,
            JitterStatusText = selected.JitterStatusText,
            LastPacketTimestampUtc = selected.LastSeenUtc,
            SmpSynchText = selected.SmpSynchText,
            ValidityText = "Raw payload not quality-mapped yet",
            DecodeStatusText = "Raw SV APDU decoded",
            PacketRateMeaningText = selected.RecentPacketRatePps.HasValue
                ? "Raw SV frames observed in a rolling capture window"
                : "Raw SV frames observed since analyzer start",
            TimebaseStatusText = "Raw capture monotonic clock"
        });
    }


    private SvDiagnosticsSnapshot ApplyPtpDiagnostics(SvDiagnosticsSnapshot diagnostics)
    {
        var ptp = _ptpTracker.CreateSnapshot(DateTime.UtcNow);

        diagnostics.PtpObserved = ptp.Observed;
        diagnostics.PtpStatusText = ptp.StatusText;
        diagnostics.PtpTotalMessages = ptp.TotalMessages;
        diagnostics.PtpDomainNumber = ptp.DomainNumber.HasValue ? (int)ptp.DomainNumber.Value : null;
        diagnostics.PtpGrandmasterIdentity = ptp.GrandmasterIdentity;
        diagnostics.PtpClockClass = ptp.ClockClass.HasValue ? (int)ptp.ClockClass.Value : null;
        diagnostics.PtpClockAccuracyText = ptp.ClockAccuracyText;
        diagnostics.PtpStepsRemoved = ptp.StepsRemoved.HasValue ? (int)ptp.StepsRemoved.Value : null;
        diagnostics.PtpSyncRatePerSecond = ptp.SyncRatePerSecond;
        diagnostics.PtpAnnounceRatePerSecond = ptp.AnnounceRatePerSecond;
        diagnostics.PtpFollowUpRatePerSecond = ptp.FollowUpRatePerSecond;
        diagnostics.PtpGrandmasterChangeCount = ptp.GrandmasterChangeCount;
        diagnostics.LastPtpMessageTimestampUtc = ptp.LastMessageUtc;
        diagnostics.LastPtpSyncTimestampUtc = ptp.LastSyncUtc;
        diagnostics.LastPtpAnnounceTimestampUtc = ptp.LastAnnounceUtc;
        diagnostics.PtpProfileHintText = ptp.ProfileHintText;
        diagnostics.PtpTransportText = ptp.TransportText;
        diagnostics.TimingReferenceText = ptp.Observed
            ? $"Timing Reference: PTP observed over {ptp.TransportText}, domain {ptp.DomainNumber?.ToString() ?? "N/A"}, GM {ptp.GrandmasterIdentity}"
            : "Timing Reference: PTP not observed; SV timing is arrival-only";
        diagnostics.TimestampSourceText = "Timestamp Source: Npcap host/software packet timestamp";
        diagnostics.TimingMetricText = ptp.Observed
            ? "Metric: SV arrival timing variation correlated with observed PTP traffic"
            : "Metric: SV arrival timing variation only; no PTP timing context";

        return diagnostics;
    }

    private void AddEvent(string severity, string message)
    {
        _events.Add(new DiagnosticEventItem
        {
            TimestampUtc = DateTime.UtcNow,
            Severity = severity,
            Message = message
        });

        if (_events.Count > 500)
            _events.RemoveRange(0, _events.Count - 500);
    }

    private void AddAggregatedEvent(
        string key,
        string severity,
        string title,
        string latestDetail,
        TimeSpan minInterval)
    {
        var now = DateTime.UtcNow;
        if (!_aggregatedEvents.TryGetValue(key, out var state))
        {
            state = new AggregatedEventState(key, severity, title, minInterval);
            _aggregatedEvents[key] = state;
        }

        state.Observe(now, latestDetail);

        if (state.ShouldEmit(now))
            EmitAggregatedEvent(state, now);
    }

    private void FlushAggregatedEvents()
    {
        var now = DateTime.UtcNow;
        foreach (var state in _aggregatedEvents.Values)
        {
            if (state.WindowCount > 0 && state.ShouldEmit(now))
                EmitAggregatedEvent(state, now);
        }
    }

    private void EmitAggregatedEvent(AggregatedEventState state, DateTime now)
    {
        var message = $"{state.Title}: {state.WindowCount} event(s) in {state.WindowAge(now).TotalSeconds:0.#}s, total={state.TotalCount}. Latest: {state.LatestDetail}";
        AddEvent(state.Severity, message);
        state.MarkEmitted(now);
    }

    private static GooseMessageItem BuildGooseItem(GoosePacket packet)
    {
        var goCbRef = ValueOrFallback(packet.GoCbRef, "N/A");
        var goId = ValueOrFallback(packet.GoId, "N/A");
        var dataSet = ValueOrFallback(packet.DataSet, "N/A");
        var messageId = goCbRef != "N/A"
            ? goCbRef
            : $"{packet.Frame.AppId:X4}:{goId}:{dataSet}";

        var dataValues = packet.DataValues
            .Select(x => new GooseDatasetValueItem
            {
                Index = x.Index,
                Name = x.Name,
                Type = x.Type,
                Value = x.Value,
                RawHex = x.RawHex
            })
            .ToArray();

        return new GooseMessageItem
        {
            MessageId = messageId,
            GoId = goId,
            GoCbRef = goCbRef,
            DataSet = dataSet,
            AppId = FormatAppId(packet.Frame.AppId),
            SourceMac = packet.Frame.Ethernet.SourceMac,
            DestinationMac = packet.Frame.Ethernet.DestinationMac,
            VlanId = packet.Frame.Ethernet.Vlan.HasValue ? packet.Frame.Ethernet.Vlan.Value.VlanId.ToString() : "N/A",
            VlanPriority = packet.Frame.Ethernet.Vlan.HasValue ? packet.Frame.Ethernet.Vlan.Value.PriorityCodePoint.ToString() : "N/A",
            StNum = packet.StNum ?? 0,
            SqNum = packet.SqNum ?? 0,
            ConfRev = packet.ConfRev ?? 0,
            TimeAllowedToLiveMilliseconds = packet.TimeAllowedToLiveMilliseconds ?? 0,
            IsTest = packet.Test ?? false,
            NeedsCommission = packet.NeedsCommission ?? false,
            LastSeenUtc = packet.Frame.CaptureTimeUtc,
            ValuesText = SummarizeGooseValues(dataValues),
            ChangedSummaryText = "N/A",
            StatusText = dataValues.Length == 0 ? "Header decoded" : "Typed decoded",
            DataValues = dataValues
        };
    }

    private static void AnnotateGooseChanges(GooseMessageItem current, GooseMessageItem? previous)
    {
        if (current.DataValues.Count == 0)
        {
            current.ChangedSummaryText = "N/A";
            return;
        }

        if (previous is null || previous.DataValues.Count == 0)
        {
            foreach (var value in current.DataValues)
                value.IsChanged = true;

            current.ChangedSummaryText = SummarizeGooseValues(current.DataValues);
            return;
        }

        var previousByIndex = previous.DataValues.ToDictionary(x => x.Index);
        foreach (var value in current.DataValues)
        {
            if (!previousByIndex.TryGetValue(value.Index, out var oldValue))
                continue;

            value.PreviousValue = oldValue.Value;
            value.IsChanged = !string.Equals(value.Value, oldValue.Value, StringComparison.Ordinal);
        }

        current.ChangedSummaryText = BuildChangedSummary(current.DataValues);
    }

    private static string BuildChangedSummary(IReadOnlyList<GooseDatasetValueItem> values)
    {
        var changed = values
            .Where(x => x.IsChanged)
            .Take(3)
            .Select(x => string.IsNullOrWhiteSpace(x.PreviousValue)
                ? $"{x.Name}={x.Value}"
                : $"{x.Name}: {x.PreviousValue} → {x.Value}")
            .ToArray();

        if (changed.Length == 0)
            return "No dataset value change";

        var suffix = values.Count(x => x.IsChanged) > changed.Length ? "..." : string.Empty;
        return string.Join(", ", changed) + suffix;
    }

    private static string SummarizeGooseValues(IReadOnlyList<GooseDatasetValueItem> values)
    {
        if (values.Count == 0)
            return "N/A";

        var preview = values
            .Take(3)
            .Select(x => $"{x.Name}={x.Value}");

        var text = string.Join(", ", preview);
        return values.Count > 3 ? $"{text}..." : text;
    }

    private static string BuildSvKey(ProcessBusFrame frame, SampledValueAsdu asdu)
    {
        var svId = ValueOrFallback(asdu.SvId, "unknown-sv");
        var vlan = frame.Ethernet.Vlan.HasValue
            ? $"v{frame.Ethernet.Vlan.Value.VlanId}/p{frame.Ethernet.Vlan.Value.PriorityCodePoint}"
            : "untagged";
        var confRev = asdu.ConfRev?.ToString() ?? "noconf";
        return string.Join("|",
            frame.Ethernet.SourceMac,
            frame.Ethernet.DestinationMac,
            vlan,
            $"APPID={frame.AppId:X4}",
            $"svID={svId}",
            $"confRev={confRev}");
    }

    private static string DescribeSv(ProcessBusFrame frame, SampledValueAsdu asdu)
    {
        return $"{ValueOrFallback(asdu.SvId, "unknown svID")} APPID={FormatAppId(frame.AppId)}";
    }

    private static string FormatAppId(ushort appId)
    {
        return $"0x{appId:X4}";
    }

    private static string ValueOrFallback(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private sealed class SvStreamState
    {
        private const int SampleCounterModulo = 65536;
        private const int ReorderBoundary = SampleCounterModulo / 2;
        private const double JitterAlertThresholdMicroseconds = 300.0;
        private const int JitterWindowCapacity = 4000;
        private const int WaveformSampleCapacity = 1024;
        private int _scopeCycles;
        private const int DefaultSamplesPerCycle = 80;
        private const double WaveformRmsSignatureStep = 0.002;
        private const double WaveformAngleSignatureStepDegrees = 0.2;
        private const double WaveformFrequencySignatureStepHz = 0.01;
        private const double AngleAttackAlpha = 1.0;
        private static readonly TimeSpan PacketRateWindow = TimeSpan.FromSeconds(1.0);
        private static readonly string[] CurrentChannels = ["Ia", "Ib", "Ic"];
        private static readonly string[] VoltageChannels = ["Ua", "Ub", "Uc"];
        private static readonly (string Channel, int ElementIndex)[] OmicronInstMagQualityMap =
        [
            ("Ia", 0),
            ("Ib", 2),
            ("Ic", 4),
            ("In", 6),
            ("Ua", 8),
            ("Ub", 10),
            ("Uc", 12),
            ("Un", 14)
        ];

        private readonly Queue<double> _absJitterWindow = new();
        private readonly Queue<DateTime> _recentJitterExcursions = new();
        private readonly Queue<DateTime> _recentPacketTimes = new();
        private readonly Queue<ScopeSampleFrame> _scopeFrames = new(WaveformSampleCapacity + 8);
        private readonly Dictionary<string, RollingChannelSamples> _channelSamples = CreateChannelBuffers();
        private readonly Dictionary<string, double?> _displayAngles = CreateAngleState();
        private long _scopeFrameIndex;
        private ushort? _lastScopeSampleCounter;
        private ushort _maxSeenSequenceCounter;
        private ushort _maxSeenScopeSampleCounter;
        private WaveformSnapshot? _lastScopeWaveform;
        private AnalogValuesSnapshot? _lastAnalogValues;
        private double _absJitterWindowTotalMicroseconds;
        private double _totalDeltaMicroseconds;
        private long _deltaCount;
        private ushort? _previousSmpCnt;
        private long? _previousCaptureTicks;
        private string? _pendingJitterEvidence;
        private string? _activeMappingSignature;

        public SvStreamState(string key, int firstSeenOrder, int scopeCycles)
        {
            Key = key;
            FirstSeenOrder = firstSeenOrder;
            _scopeCycles = Math.Clamp(scopeCycles, 1, 8);
        }

        public void SetScopeCycles(int cycles)
        {
            var clamped = Math.Clamp(cycles, 1, 8);
            if (_scopeCycles == clamped)
                return;

            _scopeCycles = clamped;
            // Keep captured samples, but force the next snapshot to be generated using
            // the newly requested timebase. This makes 4/8-cycle display real instead
            // of just changing the combo-box label.
            _lastScopeWaveform = null;
        }

        public string Key { get; }
        public int FirstSeenOrder { get; }
        public string SvId { get; private set; } = "N/A";
        public string DataSet { get; private set; } = "N/A";
        public string AppId { get; private set; } = "N/A";
        public string SourceMac { get; private set; } = "N/A";
        public string DestinationMac { get; private set; } = "N/A";
        public string VlanText { get; private set; } = "N/A";
        public uint? ConfRev { get; private set; }
        public ushort? LastSmpCnt { get; private set; }
        public ushort? SmpRate { get; private set; }
        public ushort? SmpMod { get; private set; }
        public byte? SmpSynch { get; private set; }
        public int LastPayloadBytes { get; private set; }
        public long PacketCount { get; private set; }
        public long SequenceErrors { get; private set; }
        public long MissingSamples { get; private set; }
        public double? CurrentDeltaMicroseconds { get; private set; }
        public double? AverageDeltaMicroseconds { get; private set; }
        public double? ExpectedDeltaMicroseconds { get; private set; }
        public double? CurrentJitterMicroseconds { get; private set; }
        public double? AverageAbsJitterMicroseconds { get; private set; }
        public double? MaxAbsJitterMicroseconds { get; private set; }
        public long JitterOver300MicrosecondsCount { get; private set; }
        public long RecentJitterOver300MicrosecondsCount => _recentJitterExcursions.Count;
        public double? RecentPacketRatePps { get; private set; }
        public bool HasIntegrityIssue => SequenceErrors > 0 || MissingSamples > 0;
        public int DecodedElementCount { get; private set; }
        public string MappingProfileName { get; private set; } = "Unmapped";
        public string MappedChannelNamesText { get; private set; } = "Not mapped yet";
        public string RawValuesText { get; private set; } = "[]";
        public string JitterStatusText { get; private set; } = "Arrival variation pending";
        public string SequenceStatusText { get; private set; } = "First sample";
        public DateTime LastSeenUtc { get; private set; }
        public string SmpSynchText => SmpSynch.HasValue ? SmpSynch.Value.ToString() : "N/A";

        public void ResetRenderMappingState()
        {
            _activeMappingSignature = null;
            ClearRenderBuffers();
        }

        public void Observe(ProcessBusFrame frame, SampledValueAsdu asdu, SvChannelMappingProfile? mappingProfile)
        {
            PacketCount++;
            SvId = ValueOrFallback(asdu.SvId, "N/A");
            DataSet = ValueOrFallback(asdu.DataSet, "N/A");
            AppId = FormatAppId(frame.AppId);
            SourceMac = frame.Ethernet.SourceMac;
            DestinationMac = frame.Ethernet.DestinationMac;
            VlanText = frame.Ethernet.Vlan.HasValue
                ? $"{frame.Ethernet.Vlan.Value.VlanId} / Priority {frame.Ethernet.Vlan.Value.PriorityCodePoint}"
                : "Untagged";
            ConfRev = asdu.ConfRev;
            LastSmpCnt = asdu.SmpCnt;
            SmpRate = asdu.SmpRate;
            SmpMod = asdu.SmpMod;
            SmpSynch = asdu.SmpSynch;
            LastPayloadBytes = asdu.SamplePayload.Length;
            LastSeenUtc = frame.CaptureTimeUtc;
            TrackRecentPacketRate(frame.CaptureTimeUtc);

            var acceptForDisplay = true;
            if (asdu.SmpCnt.HasValue)
                acceptForDisplay = ObserveTiming(frame.CaptureTicks, frame.CaptureTimeUtc, asdu.SmpCnt.Value);

            ObserveSamples(asdu.SamplePayload, asdu.SmpCnt, mappingProfile, acceptForDisplay);
        }

        public SvStreamItem ToStreamItem()
        {
            var age = DateTime.UtcNow - LastSeenUtc;
            var isStale = age > TimeSpan.FromSeconds(2);
            var hasWarning = HasIntegrityIssue || RecentJitterOver300MicrosecondsCount > 0;
            var severityRank = isStale ? 2 : hasWarning ? 1 : 0;
            var statusText = isStale ? "Stale" : hasWarning ? "Live with warning" : "Live";
            var displayStatus = isStale ? "STALE" : "LIVE";
            var statusBrush = isStale ? "#F0B533" : hasWarning ? "#F0B533" : "#70D7A7";
            var statusSoftBrush = isStale ? "#3A2B12" : hasWarning ? "#3A2B12" : "#173528";

            return new SvStreamItem
            {
                StreamId = Key,
                StreamName = SvId == "N/A" ? Key : SvId,
                SvId = SvId,
                DataSet = DataSet,
                AppId = AppId,
                ConfRevText = ConfRev?.ToString() ?? "N/A",
                SourceMac = SourceMac,
                DestinationMac = DestinationMac,
                VlanText = VlanText,
                IssueSummaryText = BuildIssueSummary(isStale, age),
                SeverityRank = severityRank,
                StatusText = statusText,
                DisplayStatusText = displayStatus,
                StatusBrush = statusBrush,
                StatusSoftBrush = statusSoftBrush,
                LastSeenUtc = LastSeenUtc,
                IsActive = !isStale,
                FirstSeenOrder = FirstSeenOrder
            };
        }

        private string BuildIssueSummary(bool isStale, TimeSpan age)
        {
            if (isStale)
                return $"No SV update for {age.TotalSeconds:0.0}s";

            if (HasIntegrityIssue)
                return $"Sequence issue: jumps {SequenceErrors}, missing {MissingSamples}";

            if (RecentJitterOver300MicrosecondsCount > 0)
                return $"Arrival excursion: {RecentJitterOver300MicrosecondsCount}/5s, max {MaxAbsJitterMicroseconds?.ToString("0.#") ?? "N/A"} us";

            return LastSmpCnt.HasValue
                ? $"Continuous - smpCnt {LastSmpCnt}"
                : "Continuous - awaiting smpCnt";
        }

        public StreamDetailsModel ToDetails()
        {
            return new StreamDetailsModel
            {
                StreamName = SvId == "N/A" ? Key : SvId,
                SvId = SvId,
                DataSet = DataSet,
                AppId = AppId,
                SourceMac = SourceMac,
                DestinationMac = DestinationMac,
                VlanText = VlanText,
                SmpRateText = SmpRate.HasValue ? $"{SmpRate} ({ResolveSmpModText(SmpMod)})" : "N/A",
                ConfRevText = ConfRev?.ToString() ?? "N/A",
                SampleValueMappingText = MappingProfileName,
                SampleValueCountText = DecodedElementCount > 0 ? $"{DecodedElementCount} element(s)" : "0",
                MappedChannelNamesText = MappedChannelNamesText,
                RawValuesText = RawValuesText,
                PacketEvidenceText = BuildPacketEvidenceText(),
                TimebaseStatusText = "Raw capture monotonic clock",
                RmsDebugText = BuildRmsDebugText(),
                LastSeenText = LastSeenUtc.ToLocalTime().ToString("HH:mm:ss.fff"),
                PhaseOrderText = BuildPhaseOrderText(),
                PhaseOrderDetailText = BuildPhaseOrderDetailText(),
                ChannelAngleSummaryText = BuildChannelAngleSummaryText()
            };
        }

        public AnalogValuesSnapshot ToAnalogValues()
        {
            var samplesPerCycle = ResolveSamplesPerCycle();
            var visibleSamples = Math.Max(samplesPerCycle * 2, DefaultSamplesPerCycle);
            var analogWindow = BuildCoherentScopeWindow(samplesPerCycle, visibleSamples);

            if (analogWindow.Count == 0)
            {
                // Do not publish empty/N/A phasor and RMS frames while the triggered scope
                // is filling or resynchronizing. Returning a fresh empty AnalogValuesSnapshot
                // makes the UI flicker between valid values and N/A; hold the last coherent
                // engineering snapshot until a new complete window is available.
                return _lastAnalogValues ?? new AnalogValuesSnapshot();
            }

            // Match ARSVIN runtime: recompute phasor/RMS from the latest locked two-cycle
            // window on every UI refresh. A cache key here made waveform/phasor updates feel
            // stale when harmonic content changed without a stream/angle selection change.
            var reference = ResolvePhasorReference(analogWindow, samplesPerCycle);

            var snapshot = new AnalogValuesSnapshot
            {
                Ia = CreateChannelValue("Ia", reference, analogWindow, samplesPerCycle),
                Ib = CreateChannelValue("Ib", reference, analogWindow, samplesPerCycle),
                Ic = CreateChannelValue("Ic", reference, analogWindow, samplesPerCycle),
                In = CreateChannelValue("In", reference, analogWindow, samplesPerCycle),
                Ua = CreateChannelValue("Ua", reference, analogWindow, samplesPerCycle),
                Ub = CreateChannelValue("Ub", reference, analogWindow, samplesPerCycle),
                Uc = CreateChannelValue("Uc", reference, analogWindow, samplesPerCycle),
                Un = CreateChannelValue("Un", reference, analogWindow, samplesPerCycle)
            };

            _lastAnalogValues = snapshot;
            return snapshot;
        }

        public WaveformSnapshot ToWaveform()
        {
            var sampleRate = ResolveWaveformSampleRate();
            var frequency = ResolveNominalFrequency();
            var samplesPerCycle = ResolveSamplesPerCycle();
            var visibleSamples = Math.Max(samplesPerCycle * _scopeCycles, DefaultSamplesPerCycle);

            // IMPORTANT: draw from one coherent SV sample-point window, not independent per-channel tails.
            // Waveform, phasor, and RMS must be derived from the same sample-count-aligned snapshot; otherwise
            // the scope/phasor/RMS will jitter even when the publisher is stable. This mirrors ARSVIN Subscriber.
            var scopeWindow = BuildCoherentScopeWindow(samplesPerCycle, visibleSamples);
            if (scopeWindow.Count == 0)
            {
                // Do not publish partial/tail windows while the acquisition buffer is filling
                // or resynchronizing after a counter discontinuity. Showing partial windows is
                // what makes the trace appear to run left at start-up and after injection
                // changes. Hold the last complete oscilloscope frame until a full coherent
                // window is available; if there is no published frame yet, show a pending state.
                if (_lastScopeWaveform is not null)
                    return _lastScopeWaveform;

                return new WaveformSnapshot
                {
                    SampleRateHz = sampleRate,
                    MeasuredFrequencyHz = frequency,
                    SamplesPerCycle = samplesPerCycle,
                    StatusText = "Raw waveform waiting for a complete coherent SV cycle window.",
                    ShapeSeverity = "Unknown",
                    ShapeStatusText = "Shape pending: waiting for stable scope window",
                    HasShapeWarning = false,
                    IsReconstructed = false
                };
            }

            // Match ARSVIN runtime: build a fresh snapshot from the latest locked two-cycle
            // window every refresh. The X phase is stable because BuildCoherentScopeWindow()
            // is slot-locked; caching the object is what made harmonic/clipping changes appear
            // late until the user changed SV/angle selection.
            var voltageSeries = CreateLiveWaveformSeries(VoltageChannels, samplesPerCycle, scopeWindow);
            var currentSeries = CreateLiveWaveformSeries(CurrentChannels, samplesPerCycle, scopeWindow);

            var allSeries = voltageSeries.Concat(currentSeries).ToArray();
            var sampleCount = allSeries
                .Select(series => series.Samples.Count)
                .DefaultIfEmpty(0)
                .Max();
            var shape = BuildWaveformShapeSummary(allSeries);
            var isReconstructed = allSeries.Length > 0 && allSeries.All(series => string.Equals(series.ShapeSeverity, "Unavailable", StringComparison.OrdinalIgnoreCase));

            var waveformSnapshot = new WaveformSnapshot
            {
                VoltageSeries = voltageSeries,
                CurrentSeries = currentSeries,
                SampleRateHz = sampleRate,
                MeasuredFrequencyHz = frequency,
                SamplesPerCycle = samplesPerCycle,
                WindowDurationMilliseconds = sampleRate > 0 && sampleCount > 0
                    ? sampleCount * 1000.0 / sampleRate
                    : 0,
                StatusText = sampleCount > 0
                    ? $"Raw scope drawn from decoded SV sample window ({sampleCount} point(s)). {shape.StatusText}"
                    : "Raw waveform waiting for mapped channel samples.",
                ShapeSeverity = shape.Severity,
                ShapeStatusText = shape.StatusText,
                HasShapeWarning = shape.HasWarning,
                IsReconstructed = isReconstructed
            };

            if (sampleCount > 0)
            {
                _lastScopeWaveform = waveformSnapshot;
            }

            return waveformSnapshot;
        }

        private void ObserveSamples(ReadOnlyMemory<byte> samplePayload, ushort? smpCnt, SvChannelMappingProfile? mappingProfile, bool acceptForDisplay)
        {
            var decoded = DecodeInt32Elements(samplePayload.Span);
            DecodedElementCount = decoded.Count;
            RawValuesText = BuildRawValuesText(decoded);
            ApplyMappingSignature(BuildMappingSignature(mappingProfile, decoded.Count));
            var displayValues = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            if (mappingProfile?.HasRenderableChannels == true)
            {
                var sclMapped = new List<string>(mappingProfile.Elements.Count);
                var sclUsed = new List<int>(mappingProfile.Elements.Count);

                foreach (var element in mappingProfile.Elements)
                {
                    if (element.ElementIndex < 0 || element.ElementIndex >= decoded.Count)
                        continue;
                    if (!_channelSamples.TryGetValue(element.ChannelName, out var samples))
                        continue;

                    if (acceptForDisplay)
                    {
                        samples.Add(smpCnt, decoded[element.ElementIndex].Value, ResolveSamplesPerCycle());
                        displayValues[element.ChannelName] = decoded[element.ElementIndex].Value;
                    }

                    sclMapped.Add($"{element.ChannelName}<-element[{element.ElementIndex}]");
                    sclUsed.Add(element.ElementIndex);
                }

                if (sclMapped.Count > 0)
                {
                    if (acceptForDisplay)
                        AddScopeFrame(smpCnt, displayValues);

                    MappingProfileName = $"{mappingProfile.SourceText} / {mappingProfile.ControlBlockReference}";
                    var displayGateText = acceptForDisplay ? string.Empty : " | display held by SV sequence gate";
                    MappedChannelNamesText = $"{string.Join(", ", sclMapped)} | SCL DataSet {mappingProfile.DataSetReference} | Used elements: {string.Join(", ", sclUsed)}{displayGateText}";
                    return;
                }
            }

            if (decoded.Count < 15)
            {
                MappingProfileName = "Raw SV payload evidence";
                MappedChannelNamesText = "Not mapped yet";
                return;
            }

            var mapped = new List<string>(OmicronInstMagQualityMap.Length);
            var used = new List<int>(OmicronInstMagQualityMap.Length);

            foreach (var (channel, elementIndex) in OmicronInstMagQualityMap)
            {
                if (elementIndex >= decoded.Count)
                    continue;

                if (acceptForDisplay)
                {
                    _channelSamples[channel].Add(smpCnt, decoded[elementIndex].Value, ResolveSamplesPerCycle());
                    displayValues[channel] = decoded[elementIndex].Value;
                }

                mapped.Add(channel);
                used.Add(elementIndex);
            }

            if (mapped.Count > 0 && acceptForDisplay)
                AddScopeFrame(smpCnt, displayValues);

            MappingProfileName = IsOmicronStream(SvId)
                ? $"OMICRON raw 4I4V instMag/q profile / {SvId}"
                : "Raw 4I4V instMag/q candidate profile";
            MappedChannelNamesText = mapped.Count == 0
                ? "Not mapped yet"
                : $"{string.Join(", ", mapped)} | Used elements: {string.Join(", ", used)}";
        }

        private static string BuildMappingSignature(SvChannelMappingProfile? mappingProfile, int decodedElementCount)
        {
            if (mappingProfile?.HasRenderableChannels == true)
            {
                var elementSignature = string.Join(",", mappingProfile.Elements
                    .OrderBy(x => x.ChannelName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.ElementIndex)
                    .Select(x => $"{x.ChannelName}:{x.ElementIndex}"));
                return $"scl|{mappingProfile.ProfileKey}|{elementSignature}";
            }

            return decodedElementCount >= 15
                ? "raw|4i4v-instmag-q"
                : "raw|unmapped";
        }

        private void ApplyMappingSignature(string signature)
        {
            if (string.Equals(_activeMappingSignature, signature, StringComparison.Ordinal))
                return;

            _activeMappingSignature = signature;
            ClearRenderBuffers();
        }

        private void ClearRenderBuffers()
        {
            foreach (var samples in _channelSamples.Values)
                samples.Clear();

            ClearScopeAcquisition(clearPublishedSnapshot: true);

            foreach (var channel in _displayAngles.Keys.ToArray())
                _displayAngles[channel] = null;

        }

        private void ClearScopeAcquisition(bool clearPublishedSnapshot)
        {
            _scopeFrames.Clear();
            _scopeFrameIndex = 0;
            _lastScopeSampleCounter = null;
            _maxSeenScopeSampleCounter = 0;
            if (clearPublishedSnapshot)
            {
                _lastScopeWaveform = null;
                _lastAnalogValues = null;
            }
        }

        private ChannelValueModel CreateChannelValue(
            string channel,
            PhasorReference? reference,
            IReadOnlyList<ScopeSampleFrame> coherentWindow,
            int samplesPerCycle)
        {
            var unit = channel.StartsWith("U", StringComparison.OrdinalIgnoreCase) ? "V" : "A";
            var instant = GetLatestScopeValue(channel);
            var rawAngle = GetFundamentalAngleDegrees(coherentWindow, channel, samplesPerCycle);
            var rms = GetRms(coherentWindow, channel);
            var angle = ResolveDisplayAngle(channel, rawAngle, reference);

            return new ChannelValueModel(channel)
            {
                InstantValue = instant,
                RmsValue = rms,
                AngleDegrees = angle,
                Unit = unit
            };
        }

        private double? ResolveDisplayAngle(string channel, double? rawAngle, PhasorReference? reference)
        {
            // Match ARSVIN Subscriber behavior: phasors are a live calculation from the
            // current locked sample window. Sequence/jitter issues are diagnostics; they must
            // not freeze the vector display. The old display-hold path kept returning the
            // previous angle after transient sequence warnings, so the phasor diagram only
            // appeared to refresh after a user clicked SV Explorer and rebuilt the workspace.
            if (!rawAngle.HasValue || reference is null)
                return _displayAngles[channel] ?? DefaultAngleDegrees(channel);

            var referenceValue = reference.Value;
            var relativeAngle = string.Equals(channel, referenceValue.Channel, StringComparison.OrdinalIgnoreCase)
                ? 0.0
                : NormalizeAngleDegrees(rawAngle.Value - referenceValue.AngleDegrees);

            _displayAngles[channel] = relativeAngle;
            return relativeAngle;
        }

        private PhasorReference? ResolvePhasorReference(IReadOnlyList<ScopeSampleFrame> coherentWindow, int samplesPerCycle)
        {
            foreach (var channel in new[] { "Ua", "Ia" })
            {
                var angle = GetFundamentalAngleDegrees(coherentWindow, channel, samplesPerCycle);
                if (angle.HasValue)
                    return new PhasorReference(channel, angle.Value);
            }

            return null;
        }

        private IReadOnlyList<WaveformSeriesModel> CreateLiveWaveformSeries(
            IReadOnlyList<string> channels,
            int samplesPerCycle,
            IReadOnlyList<ScopeSampleFrame> coherentWindow)
        {
            var result = new List<WaveformSeriesModel>(channels.Count);

            foreach (var channel in channels)
            {
                var scopeWindow = GetChannelSamples(coherentWindow, channel);
                if (scopeWindow.Count < 2)
                    continue;

                var lockedShape = AnalyzeWaveformShape(scopeWindow, samplesPerCycle);
                var fastShape = AnalyzeFastWaveformShape(channel, samplesPerCycle);
                var shape = PickMoreSevereWaveformShape(lockedShape, fastShape);
                result.Add(new WaveformSeriesModel
                {
                    Name = channel,
                    Unit = channel.StartsWith("U", StringComparison.OrdinalIgnoreCase) ? "V" : "A",
                    Samples = scopeWindow,
                    ShapeSeverity = shape.Severity,
                    ShapeStatusText = shape.StatusText,
                    ShapeResidualPercent = shape.ResidualPercent,
                    CrestFactor = shape.CrestFactor,
                    HasShapeDistortion = shape.HasDistortion
                });
            }

            return result;
        }

        private WaveformShapeAnalysis AnalyzeFastWaveformShape(string channel, int samplesPerCycle)
        {
            if (!_channelSamples.TryGetValue(channel, out var samples))
                return new WaveformShapeAnalysis("Unknown", "Shape pending: channel samples not available", 0, 0, false);

            // Fast detector: use the latest chronological raw channel tail, independent of
            // the phase-locked visual window. The visual scope must stay stable, but the
            // harmonic/clipping verdict must follow the publisher as soon as one complete
            // cycle has arrived. This mirrors ARSVIN's live runtime philosophy: every UI
            // snapshot recomputes measurements from the current stream data, not from a
            // stale visual/cache object.
            var analysisSamples = samples.GetLatestSamples(samplesPerCycle);
            if (analysisSamples.Count < samplesPerCycle)
                return new WaveformShapeAnalysis("Unknown", "Shape pending: waiting for fast shape cycle", 0, 0, false);

            return AnalyzeWaveformShape(analysisSamples, samplesPerCycle, minimumCycles: 1);
        }

        private static WaveformShapeAnalysis PickMoreSevereWaveformShape(WaveformShapeAnalysis lockedShape, WaveformShapeAnalysis fastShape)
        {
            // Shape verdict should describe the latest publisher waveform, not a stale visual
            // hold. The phase-locked window remains the drawing source, but the fast one-cycle
            // chronological tail is authoritative as soon as it has a valid answer. This makes
            // harmonic/clipping ON and OFF follow the live publisher without needing SV Explorer
            // clicks. Unknown/pending fast results fall back to the locked visual window.
            if (!string.Equals(fastShape.Severity, "Unknown", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(fastShape.Severity, "Unavailable", StringComparison.OrdinalIgnoreCase))
                return fastShape;

            return lockedShape;
        }

        private void AddScopeFrame(ushort? smpCnt, IReadOnlyDictionary<string, double> displayValues)
        {
            if (displayValues.Count == 0)
                return;

            // Unwrap smpCnt into a monotonic 64-bit sample index. SV counters wrap either at
            // the sample rate (e.g. 3999 -> 0 for 50 Hz / 80 spc) or at 65536 depending on the
            // publisher; the adaptive wrap base handles both. Without unwrapping, the scope
            // window phase jumps at every counter rollover (a hard, periodic flicker).
            if (smpCnt.HasValue)
            {
                if (smpCnt.Value > _maxSeenScopeSampleCounter)
                    _maxSeenScopeSampleCounter = smpCnt.Value;

                if (_lastScopeSampleCounter.HasValue)
                {
                    long delta;
                    if (smpCnt.Value >= _lastScopeSampleCounter.Value)
                    {
                        delta = smpCnt.Value - _lastScopeSampleCounter.Value;
                    }
                    else
                    {
                        var wrapBase = (long)_maxSeenScopeSampleCounter + 1;
                        delta = smpCnt.Value + wrapBase - _lastScopeSampleCounter.Value;
                    }

                    if (delta <= 0)
                        return;

                    if (delta > WaveformSampleCapacity)
                    {
                        // Counter reset / large discontinuity. Start acquiring a new stable
                        // window but keep the last published snapshot on screen until the new
                        // window is complete. Never stitch old and new publisher states.
                        ClearScopeAcquisition(clearPublishedSnapshot: false);
                        _scopeFrameIndex = 1;
                    }
                    else
                    {
                        _scopeFrameIndex += delta;
                    }
                }
                else
                {
                    _scopeFrameIndex++;
                }

                _lastScopeSampleCounter = smpCnt.Value;
            }
            else
            {
                _scopeFrameIndex++;
                _lastScopeSampleCounter = null;
            }

            _scopeFrames.Enqueue(new ScopeSampleFrame(
                Index: _scopeFrameIndex,
                SampleCounter: smpCnt,
                Ia: TryGetDisplayValue(displayValues, "Ia"),
                Ib: TryGetDisplayValue(displayValues, "Ib"),
                Ic: TryGetDisplayValue(displayValues, "Ic"),
                In: TryGetDisplayValue(displayValues, "In"),
                Ua: TryGetDisplayValue(displayValues, "Ua"),
                Ub: TryGetDisplayValue(displayValues, "Ub"),
                Uc: TryGetDisplayValue(displayValues, "Uc"),
                Un: TryGetDisplayValue(displayValues, "Un")));

            while (_scopeFrames.Count > WaveformSampleCapacity)
                _scopeFrames.Dequeue();
        }

        private static double? TryGetDisplayValue(IReadOnlyDictionary<string, double> values, string channel)
        {
            return values.TryGetValue(channel, out var value) ? value : null;
        }

        private IReadOnlyList<ScopeSampleFrame> BuildCoherentScopeWindow(int samplesPerCycle, int visibleSamples)
        {
            // ARSVIN's OscilloscopePlot has an X index per WaveformPoint, so a partially-filled
            // locked window can still be drawn without horizontal stretching. DigSub's existing
            // WaveformSeriesModel only carries Y samples and the WPF control spreads whatever
            // count it receives across the whole plot width. Therefore, a 14/29-point startup
            // window is visually stretched into a long, slow line. Keep the ARSVIN slot-locking
            // rule, but publish only when the whole two-cycle slot window is available. Until
            // then ToWaveform()/ToAnalogValues() hold the last published snapshot.
            var frames = _scopeFrames.ToArray();
            if (frames.Length == 0)
                return Array.Empty<ScopeSampleFrame>();

            var pointsPerCycle = ResolveArsvinPointsPerCycle(samplesPerCycle);
            if (pointsPerCycle <= 0)
                return Array.Empty<ScopeSampleFrame>();

            var window = Math.Clamp(pointsPerCycle * _scopeCycles, 32, 640);
            if (frames.Length < window)
                return Array.Empty<ScopeSampleFrame>();

            var slots = new ScopeSampleFrame?[window];
            foreach (var frame in frames)
            {
                var slot = frame.SampleCounter.HasValue
                    ? frame.SampleCounter.Value % window
                    : (int)(frame.Index % window);
                slots[slot] = frame;
            }

            var result = new List<ScopeSampleFrame>(window);
            for (var slot = 0; slot < slots.Length; slot++)
            {
                if (slots[slot] is not { } frame)
                    return Array.Empty<ScopeSampleFrame>();

                result.Add(frame with { Index = slot });
            }

            return result.ToArray();
        }

        private static int ResolveArsvinPointsPerCycle(int samplesPerCycle)
        {
            var candidate = samplesPerCycle > 0 ? samplesPerCycle : DefaultSamplesPerCycle;
            return Math.Clamp(candidate, 16, 256);
        }

        private static IReadOnlyList<double> GetChannelSamples(IReadOnlyList<ScopeSampleFrame> frames, string channel)
        {
            var result = new List<double>(frames.Count);
            foreach (var frame in frames)
            {
                var value = frame.GetValue(channel);
                if (value.HasValue)
                    result.Add(value.Value);
            }

            return result;
        }

        private double? GetLatestScopeValue(string channel)
        {
            foreach (var frame in _scopeFrames.Reverse())
            {
                var value = frame.GetValue(channel);
                if (value.HasValue)
                    return value.Value;
            }

            return _channelSamples[channel].LastValue;
        }

        private static double? GetRms(IReadOnlyList<ScopeSampleFrame> frames, string channel)
        {
            var values = GetChannelSamples(frames, channel);
            if (values.Count == 0)
                return null;

            var sumSquares = 0.0;
            var count = 0;
            foreach (var value in values)
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                    continue;

                sumSquares += value * value;
                count++;
            }

            return count == 0 ? null : Math.Sqrt(Math.Max(0.0, sumSquares / count));
        }

        private static double? GetFundamentalAngleDegrees(IReadOnlyList<ScopeSampleFrame> frames, string channel, int samplesPerCycle)
        {
            var values = GetChannelSamples(frames, channel);
            if (samplesPerCycle <= 1 || values.Count < samplesPerCycle)
                return null;

            var sine = 0.0;
            var cosine = 0.0;
            var phaseStep = 2.0 * Math.PI / samplesPerCycle;
            for (var index = 0; index < values.Count; index++)
            {
                var angle = phaseStep * index;
                sine += values[index] * Math.Sin(angle);
                cosine += values[index] * Math.Cos(angle);
            }

            if (Math.Abs(sine) < 1e-9 && Math.Abs(cosine) < 1e-9)
                return null;

            return NormalizeAngleDegrees(Math.Atan2(cosine, sine) * 180.0 / Math.PI);
        }

        private static IReadOnlyList<WaveformSeriesModel> CreateReconstructedWaveformSeries(
            AnalogValuesSnapshot analog,
            IReadOnlyList<string> channels,
            double frequencyHz,
            double sampleRateHz,
            int visibleSamples)
        {
            var result = new List<WaveformSeriesModel>(channels.Count);
            if (frequencyHz <= 0 || sampleRateHz <= 0 || visibleSamples <= 1)
                return result;

            foreach (var channel in channels)
            {
                var value = GetAnalogChannel(analog, channel);
                if (!value.RmsValue.HasValue)
                    continue;

                result.Add(new WaveformSeriesModel
                {
                    Name = channel,
                    Unit = channel.StartsWith("U", StringComparison.OrdinalIgnoreCase) ? "V" : "A",
                    Samples = CreateSineSamples(
                        value.RmsValue.Value,
                        value.AngleDegrees ?? DefaultAngleDegrees(channel),
                        frequencyHz,
                        sampleRateHz,
                        visibleSamples),
                    ShapeSeverity = "Unavailable",
                    ShapeStatusText = "Shape unavailable: reconstructed sine fallback",
                    ShapeResidualPercent = 0,
                    CrestFactor = Math.Sqrt(2.0),
                    HasShapeDistortion = false
                });
            }

            return result;
        }

        private bool ObserveTiming(long captureTicks, DateTime captureTimeUtc, ushort smpCnt)
        {
            TrimRecentJitterExcursions(captureTimeUtc);
            var acceptForDisplay = true;

            if (_previousSmpCnt.HasValue && _previousCaptureTicks.HasValue)
            {
                var sequence = AnalyzeSequence(_previousSmpCnt.Value, smpCnt, ResolveSequenceWrapBase());
                SequenceStatusText = sequence.StatusText;
                SequenceErrors += sequence.IsError ? 1 : 0;
                MissingSamples += sequence.MissingSamples;
                acceptForDisplay = !sequence.IsError;
                if (sequence.IsError)
                {
                    ClearScopeAcquisition(clearPublishedSnapshot: false);
                }

                var actualUs = (captureTicks - _previousCaptureTicks.Value) * 1_000_000.0 / Stopwatch.Frequency;
                CurrentDeltaMicroseconds = actualUs;
                _totalDeltaMicroseconds += actualUs;
                _deltaCount++;
                AverageDeltaMicroseconds = _totalDeltaMicroseconds / _deltaCount;

                var expectedUs = ResolveExpectedDeltaUs(sequence.SampleStep);
                ExpectedDeltaMicroseconds = expectedUs;

                if (expectedUs.HasValue)
                {
                    var signedJitter = actualUs - expectedUs.Value;
                    var absJitter = Math.Abs(signedJitter);
                    CurrentJitterMicroseconds = signedJitter;
                    AddJitter(absJitter);
                    AverageAbsJitterMicroseconds = _absJitterWindow.Count == 0
                        ? null
                        : _absJitterWindowTotalMicroseconds / _absJitterWindow.Count;
                    MaxAbsJitterMicroseconds = _absJitterWindow.Count == 0 ? null : _absJitterWindow.Max();

                    if (absJitter >= JitterAlertThresholdMicroseconds)
                    {
                        JitterOver300MicrosecondsCount++;
                        TrackRecentJitterExcursion(captureTimeUtc);
                        _pendingJitterEvidence = BuildJitterEvidence(
                            previousSmpCnt: _previousSmpCnt.Value,
                            currentSmpCnt: smpCnt,
                            actualUs: actualUs,
                            expectedUs: expectedUs.Value,
                            signedJitterUs: signedJitter,
                            absJitterUs: absJitter,
                            sampleStep: sequence.SampleStep);
                    }

                    JitterStatusText = absJitter >= JitterAlertThresholdMicroseconds
                        ? $"Arrival variation alert: {absJitter:0.#} us >= {JitterAlertThresholdMicroseconds:0} us"
                        : $"Arrival variation nominal: {absJitter:0.#} us";
                }
                else
                {
                    JitterStatusText = "Arrival variation pending: sample rate unknown";
                }
            }

            if (smpCnt > _maxSeenSequenceCounter)
                _maxSeenSequenceCounter = smpCnt;

            _previousSmpCnt = smpCnt;
            _previousCaptureTicks = captureTicks;
            return acceptForDisplay;
        }

        public string? ConsumeJitterEvidence()
        {
            var evidence = _pendingJitterEvidence;
            _pendingJitterEvidence = null;
            return evidence;
        }

        private double? ResolveExpectedDeltaUs(int sampleStep)
        {
            if (SmpRate is > 0 && SmpMod == 1)
                return sampleStep * 1_000_000.0 / SmpRate.Value;

            return null;
        }

        private void AddJitter(double absJitterUs)
        {
            _absJitterWindow.Enqueue(absJitterUs);
            _absJitterWindowTotalMicroseconds += absJitterUs;

            while (_absJitterWindow.Count > JitterWindowCapacity)
                _absJitterWindowTotalMicroseconds -= _absJitterWindow.Dequeue();
        }

        private void TrackRecentJitterExcursion(DateTime nowUtc)
        {
            _recentJitterExcursions.Enqueue(nowUtc);
            TrimRecentJitterExcursions(nowUtc);
        }

        private void TrackRecentPacketRate(DateTime nowUtc)
        {
            _recentPacketTimes.Enqueue(nowUtc);

            while (_recentPacketTimes.Count > 0 &&
                   nowUtc - _recentPacketTimes.Peek() > PacketRateWindow)
            {
                _recentPacketTimes.Dequeue();
            }

            if (_recentPacketTimes.Count < 2)
            {
                RecentPacketRatePps = null;
                return;
            }

            var windowSeconds = Math.Max(0.001, (nowUtc - _recentPacketTimes.Peek()).TotalSeconds);
            RecentPacketRatePps = (_recentPacketTimes.Count - 1) / windowSeconds;
        }

        private void TrimRecentJitterExcursions(DateTime nowUtc)
        {
            while (_recentJitterExcursions.Count > 0 &&
                   (nowUtc - _recentJitterExcursions.Peek()).TotalSeconds > 5)
            {
                _recentJitterExcursions.Dequeue();
            }
        }

        private string BuildPacketEvidenceText()
        {
            return string.Join("; ", new[]
            {
                $"APPID={AppId}",
                $"svID={SvId}",
                $"datSet={DataSet}",
                $"confRev={ConfRev?.ToString() ?? "N/A"}",
                $"smpCnt={LastSmpCnt?.ToString() ?? "N/A"}",
                $"smpMod={ResolveSmpModText(SmpMod)}",
                $"smpRate={SmpRate?.ToString() ?? "N/A"}",
                $"payloadBytes={LastPayloadBytes}",
                $"mapped={MappedChannelNamesText}",
                $"sequence={SequenceStatusText}",
                $"timing={JitterStatusText}"
            });
        }

        private string BuildJitterEvidence(
            ushort previousSmpCnt,
            ushort currentSmpCnt,
            double actualUs,
            double expectedUs,
            double signedJitterUs,
            double absJitterUs,
            int sampleStep)
        {
            var interpretation = sampleStep == 1 && SequenceErrors == 0 && MissingSamples == 0
                ? "sequence OK; verify USB/NIC buffering or publisher scheduling"
                : "sequence anomaly or sample gap present";

            return string.Join("; ", new[]
            {
                $"svID={SvId}",
                $"APPID={AppId}",
                $"smpCnt={previousSmpCnt}->{currentSmpCnt}",
                $"variation={signedJitterUs:0.#} us",
                $"absVariation={absJitterUs:0.#} us",
                $"delta={actualUs:0.#}/{expectedUs:0.#} us",
                $"step={sampleStep}",
                $"source={SourceMac}",
                $"vlan={VlanText}",
                $"rate={SmpRate?.ToString() ?? "N/A"}",
                $"note={interpretation}",
                $"time={LastSeenUtc:HH:mm:ss.fff}"
            });
        }

        private string BuildPhaseOrderText()
        {
            var samplesPerCycle = ResolveSamplesPerCycle();
            var ub = _displayAngles["Ub"] ?? _channelSamples["Ub"].GetFundamentalAngleDegrees(samplesPerCycle);
            var uc = _displayAngles["Uc"] ?? _channelSamples["Uc"].GetFundamentalAngleDegrees(samplesPerCycle);

            if (!ub.HasValue || !uc.HasValue)
                return "Phase order: pending";

            var ubNorm = NormalizeAngleDegrees(ub.Value);
            var ucNorm = NormalizeAngleDegrees(uc.Value);
            // IEC/utility display convention used by ARSVIN: positive-sequence R/S/T is
            // R = 0°, S = -120°, T = +120°. The previous logic was inverted and showed
            // phase-S at +120°, which is wrong for an engineering phasor view.
            var abcLike = IsNear(ubNorm, -120.0, 35.0) && IsNear(ucNorm, 120.0, 35.0);
            var acbLike = IsNear(ubNorm, 120.0, 35.0) && IsNear(ucNorm, -120.0, 35.0);

            if (abcLike)
                return "Phase order: ABC candidate";

            if (acbLike)
                return "Phase order: ACB / S-T swap suspect";

            return "Phase order: review required";
        }

        private string BuildPhaseOrderDetailText()
        {
            var samplesPerCycle = ResolveSamplesPerCycle();
            var ua = _displayAngles["Ua"] ?? _channelSamples["Ua"].GetFundamentalAngleDegrees(samplesPerCycle);
            var ub = _displayAngles["Ub"] ?? _channelSamples["Ub"].GetFundamentalAngleDegrees(samplesPerCycle);
            var uc = _displayAngles["Uc"] ?? _channelSamples["Uc"].GetFundamentalAngleDegrees(samplesPerCycle);

            if (!ua.HasValue || !ub.HasValue || !uc.HasValue)
                return "Need stable Ua/Ub/Uc samples before phase order can be judged.";

            return $"Ua {NormalizeAngleDegrees(ua.Value):0.#}°, Ub {NormalizeAngleDegrees(ub.Value):0.#}°, Uc {NormalizeAngleDegrees(uc.Value):0.#}°. Mapping is per-stream; confirm with SCL for final semantics.";
        }

        private string BuildChannelAngleSummaryText()
        {
            var samplesPerCycle = ResolveSamplesPerCycle();
            var parts = new List<string>();
            foreach (var channel in new[] { "Ua", "Ub", "Uc", "Ia", "Ib", "Ic" })
            {
                var angle = _displayAngles[channel] ?? _channelSamples[channel].GetFundamentalAngleDegrees(samplesPerCycle);
                var rms = _channelSamples[channel].GetDisplayRms();
                if (angle.HasValue || rms.HasValue)
                    parts.Add($"{channel}: {rms?.ToString("0.###") ?? "rms?"} @ {angle?.ToString("0.#") ?? "ang?"}°");
            }

            return parts.Count == 0 ? "Angles pending" : string.Join(", ", parts);
        }

        private static bool IsNear(double value, double target, double toleranceDegrees)
        {
            return Math.Abs(NormalizeAngleDegrees(value - target)) <= toleranceDegrees;
        }

        private string BuildRmsDebugText()
        {
            var parts = new List<string>();

            foreach (var channel in new[] { "Ia", "Ib", "Ic", "Ua", "Ub", "Uc" })
            {
                var rms = _channelSamples[channel].GetRawRms();
                var displayRms = _channelSamples[channel].GetDisplayRms();
                var angle = _displayAngles[channel];
                if (rms.HasValue)
                    parts.Add(displayRms.HasValue
                        ? $"{channel}Rms={rms.Value:0.###}->{displayRms.Value:0.###}, {channel}Angle={angle?.ToString("0.##") ?? "pending"}"
                        : $"{channel}Rms={rms.Value:0.###}->pending");
            }

            return parts.Count == 0
                ? "Raw RMS pending"
                : $"Buffer={_channelSamples["Ia"].Count}, {string.Join(", ", parts)}";
        }

        private double ResolveWaveformSampleRate()
        {
            if (SmpRate is > 0 && SmpMod == 1)
                return SmpRate.Value;

            return 0;
        }

        private double ResolveNominalFrequency()
        {
            var sampleRate = ResolveWaveformSampleRate();
            return sampleRate >= 4000 ? 50.0 : 0;
        }

        private int ResolveSamplesPerCycle()
        {
            var sampleRate = ResolveWaveformSampleRate();
            var nominalFrequency = ResolveNominalFrequency();

            if (sampleRate > 0 && nominalFrequency > 0)
                return Math.Max(1, (int)Math.Round(sampleRate / nominalFrequency));

            return DefaultSamplesPerCycle;
        }

        private static WaveformShapeSummary BuildWaveformShapeSummary(IReadOnlyList<WaveformSeriesModel> series)
        {
            if (series.Count == 0)
                return new WaveformShapeSummary("Unknown", "Shape pending", 0, false);

            if (series.All(item => string.Equals(item.ShapeSeverity, "Unavailable", StringComparison.OrdinalIgnoreCase)))
                return new WaveformShapeSummary("Unavailable", "Shape unavailable: reconstructed sine fallback", 0, false);

            var ranked = series
                .Select(item => (Item: item, Rank: ShapeSeverityRank(item.ShapeSeverity)))
                .OrderByDescending(item => item.Rank)
                .ThenByDescending(item => item.Item.ShapeResidualPercent)
                .First();

            var selected = ranked.Item;
            var hasWarning = ranked.Rank >= ShapeSeverityRank("Warning");
            var statusText = selected.ShapeSeverity switch
            {
                "Distorted" => $"Waveform distortion suspected: {selected.Name} residual {selected.ShapeResidualPercent:0.#}%.",
                "Warning" => $"Waveform shape watch: {selected.Name} residual {selected.ShapeResidualPercent:0.#}%.",
                "OK" => "Waveform shape OK.",
                "Low" => "Waveform shape pending: signal too low or flat.",
                _ => "Waveform shape pending."
            };

            return new WaveformShapeSummary(selected.ShapeSeverity, statusText, selected.ShapeResidualPercent, hasWarning);
        }

        private static int ShapeSeverityRank(string? severity)
        {
            return severity switch
            {
                "Distorted" => 4,
                "Warning" => 3,
                "Unknown" => 2,
                "Unavailable" => 2,
                "Low" => 1,
                "OK" => 0,
                _ => 2
            };
        }

        private static WaveformShapeAnalysis AnalyzeWaveformShape(IReadOnlyList<double> samples, int samplesPerCycle, int minimumCycles = 2)
        {
            minimumCycles = Math.Clamp(minimumCycles, 1, 4);
            var minimumSamples = Math.Max(16, samplesPerCycle * minimumCycles);
            if (samplesPerCycle <= 1 || samples.Count < minimumSamples)
                return new WaveformShapeAnalysis("Unknown", "Shape pending: insufficient raw samples", 0, 0, false);

            var cycles = Math.Min(4, samples.Count / samplesPerCycle);
            var usableCount = cycles * samplesPerCycle;
            if (usableCount < samplesPerCycle * minimumCycles)
                return new WaveformShapeAnalysis("Unknown", "Shape pending: insufficient coherent cycles", 0, 0, false);

            var start = samples.Count - usableCount;
            var window = new double[usableCount];
            var mean = 0.0;
            for (var i = 0; i < usableCount; i++)
            {
                var value = samples[start + i];
                if (double.IsNaN(value) || double.IsInfinity(value))
                    value = 0.0;
                window[i] = value;
                mean += value;
            }

            mean /= usableCount;

            var totalSquare = 0.0;
            var peak = 0.0;
            for (var i = 0; i < window.Length; i++)
            {
                window[i] -= mean;
                totalSquare += window[i] * window[i];
                peak = Math.Max(peak, Math.Abs(window[i]));
            }

            var totalRms = Math.Sqrt(Math.Max(0.0, totalSquare / usableCount));
            if (totalRms <= 1e-9 || peak <= 1e-9)
                return new WaveformShapeAnalysis("Low", "Shape pending: signal too low or flat", 0, 0, false);

            var real = 0.0;
            var imaginary = 0.0;
            var phaseStep = 2.0 * Math.PI / samplesPerCycle;
            for (var i = 0; i < window.Length; i++)
            {
                var angle = phaseStep * i;
                real += window[i] * Math.Cos(angle);
                imaginary += window[i] * Math.Sin(angle);
            }

            var coefficientScale = 2.0 / usableCount;
            var cosineCoefficient = coefficientScale * real;
            var sineCoefficient = coefficientScale * imaginary;
            var residualSquare = 0.0;
            var fundamentalSquare = 0.0;

            for (var i = 0; i < window.Length; i++)
            {
                var angle = phaseStep * i;
                var fundamental = (cosineCoefficient * Math.Cos(angle)) + (sineCoefficient * Math.Sin(angle));
                var residual = window[i] - fundamental;
                residualSquare += residual * residual;
                fundamentalSquare += fundamental * fundamental;
            }

            var fundamentalRms = Math.Sqrt(Math.Max(0.0, fundamentalSquare / usableCount));
            if (fundamentalRms <= 1e-9)
                return new WaveformShapeAnalysis("Low", "Shape pending: fundamental component too low", 0, peak / totalRms, false);

            var residualRms = Math.Sqrt(Math.Max(0.0, residualSquare / usableCount));
            var residualPercent = residualRms * 100.0 / fundamentalRms;
            var crestFactor = peak / totalRms;
            var plateauRatio = ResolvePlateauRatio(window, peak);

            var distorted = residualPercent >= 12.0 || plateauRatio >= 0.05 || crestFactor <= 1.15 || crestFactor >= 2.70;
            var warning = distorted || residualPercent >= 5.0 || plateauRatio >= 0.02 || crestFactor <= 1.24 || crestFactor >= 2.20;
            if (distorted)
            {
                var reason = plateauRatio >= 0.05
                    ? "flat-top/clipping suspected"
                    : crestFactor <= 1.15 || crestFactor >= 2.70
                        ? "abnormal crest factor"
                        : "harmonic/non-sinus residual high";
                return new WaveformShapeAnalysis(
                    "Distorted",
                    $"Distorted: {reason}, residual {residualPercent:0.#}%",
                    residualPercent,
                    crestFactor,
                    true);
            }

            if (warning)
            {
                var reason = plateauRatio >= 0.02
                    ? "flat-top watch"
                    : crestFactor <= 1.24 || crestFactor >= 2.20
                        ? "crest factor watch"
                        : "harmonic residual watch";
                return new WaveformShapeAnalysis(
                    "Warning",
                    $"Shape watch: {reason}, residual {residualPercent:0.#}%",
                    residualPercent,
                    crestFactor,
                    true);
            }

            return new WaveformShapeAnalysis(
                "OK",
                $"Shape OK: residual {residualPercent:0.#}%",
                residualPercent,
                crestFactor,
                false);
        }

        private static double ResolvePlateauRatio(IReadOnlyList<double> samples, double peak)
        {
            if (samples.Count < 5 || peak <= 0)
                return 0.0;

            // A sine wave naturally spends time near its peak. Counting every sample above
            // 90% peak creates false "flat-top" warnings. Only count points that are both
            // near the peak AND locally flat, which is the clipping signature we care about.
            var highThreshold = peak * 0.96;
            var flatSlopeThreshold = peak * 0.004;
            var plateauSamples = 0;

            for (var i = 1; i < samples.Count - 1; i++)
            {
                var current = samples[i];
                if (Math.Abs(current) < highThreshold)
                    continue;

                var previousSlope = Math.Abs(current - samples[i - 1]);
                var nextSlope = Math.Abs(samples[i + 1] - current);
                if (previousSlope <= flatSlopeThreshold && nextSlope <= flatSlopeThreshold)
                    plateauSamples++;
            }

            return plateauSamples / (double)samples.Count;
        }

        private static double[] CreateSineSamples(
            double rms,
            double angleDegrees,
            double frequencyHz,
            double sampleRateHz,
            int count)
        {
            var result = new double[count];
            var peak = rms * Math.Sqrt(2.0);
            var angleRadians = angleDegrees * Math.PI / 180.0;
            var phaseStep = 2.0 * Math.PI * frequencyHz / sampleRateHz;

            for (var i = 0; i < result.Length; i++)
                result[i] = peak * Math.Cos((i * phaseStep) + angleRadians);

            return result;
        }

        private static ChannelValueModel GetAnalogChannel(AnalogValuesSnapshot analog, string channel)
        {
            return channel switch
            {
                "Ia" => analog.Ia,
                "Ib" => analog.Ib,
                "Ic" => analog.Ic,
                "In" => analog.In,
                "Ua" => analog.Ua,
                "Ub" => analog.Ub,
                "Uc" => analog.Uc,
                "Un" => analog.Un,
                _ => new ChannelValueModel(channel)
            };
        }

        private static string BuildWaveformSignature(AnalogValuesSnapshot analog, double frequencyHz, int visibleSamples)
        {
            return string.Join("|",
                Quantize(frequencyHz, WaveformFrequencySignatureStepHz),
                visibleSamples,
                ChannelSignature(analog.Ua),
                ChannelSignature(analog.Ub),
                ChannelSignature(analog.Uc),
                ChannelSignature(analog.Ia),
                ChannelSignature(analog.Ib),
                ChannelSignature(analog.Ic));
        }

        private static string ChannelSignature(ChannelValueModel channel)
        {
            var rms = channel.RmsValue ?? 0.0;
            var rmsStep = Math.Max(0.05, Math.Abs(rms) * WaveformRmsSignatureStep);
            return $"{channel.Name}:{Quantize(rms, rmsStep)}:{Quantize(channel.AngleDegrees ?? 0.0, WaveformAngleSignatureStepDegrees)}";
        }

        private static long Quantize(double value, double step)
        {
            if (step <= 0 || double.IsNaN(value) || double.IsInfinity(value))
                return 0;

            return (long)Math.Round(value / step);
        }

        private static List<DecodedElement> DecodeInt32Elements(ReadOnlySpan<byte> payload)
        {
            var result = new List<DecodedElement>(payload.Length / 4);

            for (var offset = 0; offset + 4 <= payload.Length; offset += 4)
            {
                result.Add(new DecodedElement(
                    Index: offset / 4,
                    ByteOffset: offset,
                    Value: BinaryPrimitives.ReadInt32BigEndian(payload.Slice(offset, 4))));
            }

            return result;
        }

        private static string BuildRawValuesText(IReadOnlyList<DecodedElement> decoded)
        {
            if (decoded.Count == 0)
                return "[]";

            return "[" + string.Join(", ", decoded.Take(16)
                .Select(x => $"[{x.Index}]@{x.ByteOffset}: i32={x.Value}")) + "]";
        }

        private static Dictionary<string, RollingChannelSamples> CreateChannelBuffers()
        {
            return new Dictionary<string, RollingChannelSamples>(StringComparer.OrdinalIgnoreCase)
            {
                ["Ia"] = new(WaveformSampleCapacity),
                ["Ib"] = new(WaveformSampleCapacity),
                ["Ic"] = new(WaveformSampleCapacity),
                ["In"] = new(WaveformSampleCapacity),
                ["Ua"] = new(WaveformSampleCapacity),
                ["Ub"] = new(WaveformSampleCapacity),
                ["Uc"] = new(WaveformSampleCapacity),
                ["Un"] = new(WaveformSampleCapacity)
            };
        }

        private static Dictionary<string, double?> CreateAngleState()
        {
            return new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Ia"] = null,
                ["Ib"] = null,
                ["Ic"] = null,
                ["In"] = null,
                ["Ua"] = null,
                ["Ub"] = null,
                ["Uc"] = null,
                ["Un"] = null
            };
        }

        private static bool IsOmicronStream(string svId)
        {
            return svId.StartsWith("OMICRON_CMC_SV", StringComparison.OrdinalIgnoreCase);
        }

        private static double DefaultAngleDegrees(string channel)
        {
            return channel switch
            {
                "Ua" => 0.0,
                "Ub" => -120.0,
                "Uc" => 120.0,
                "Ia" => -30.0,
                "Ib" => -150.0,
                "Ic" => 90.0,
                _ => 0.0
            };
        }

        private static double NormalizeAngleDegrees(double angle)
        {
            while (angle <= -180.0)
                angle += 360.0;
            while (angle > 180.0)
                angle -= 360.0;
            return angle;
        }

        /// <summary>
        /// Resolves the smpCnt rollover base. IEC 61850-9-2LE publishers wrap the counter at
        /// the sample rate (e.g. 3999 -> 0 for 50 Hz / 80 spc), NOT at 65536. Treating the
        /// rate rollover as "out-of-order" flagged a healthy publisher as erroring once per
        /// second and held the scope display each time - a visible 1 Hz flicker.
        /// </summary>
        private int ResolveSequenceWrapBase()
        {
            if (SmpRate is > 0)
                return SmpRate.Value;

            // Adaptive fallback: after one full second of traffic the highest counter seen
            // is rate-1. Require a plausible floor so early traffic cannot fake a tiny base.
            return _maxSeenSequenceCounter >= 799 ? _maxSeenSequenceCounter + 1 : 0;
        }

        private static SequenceObservation AnalyzeSequence(ushort previous, ushort current, int rateWrapBase)
        {
            var expected = (ushort)((previous + 1) % SampleCounterModulo);

            if (current == expected)
                return new SequenceObservation(1, 0, false, "Contiguous");

            if (rateWrapBase > 1 && current == (previous + 1) % rateWrapBase)
                return new SequenceObservation(1, 0, false, "Contiguous (rate rollover)");

            if (current == previous)
                return new SequenceObservation(1, 0, true, "Duplicate sample counter");

            var modulo = rateWrapBase > 1 ? rateWrapBase : SampleCounterModulo;
            var boundary = modulo / 2;
            var forwardStep = (current - previous + modulo) % modulo;
            if (forwardStep > 0 && forwardStep < boundary)
                return new SequenceObservation(forwardStep, forwardStep - 1, true, $"Forward gap: {forwardStep - 1} missing");

            return new SequenceObservation(1, 0, true, "Out-of-order sample counter");
        }

        private static string ResolveSmpModText(ushort? smpMod)
        {
            return smpMod switch
            {
                1 => "SAMPLES_PER_SECOND",
                2 => "SAMPLES_PER_PERIOD",
                _ => smpMod?.ToString() ?? "N/A"
            };
        }

        private readonly record struct ScopeSampleFrame(
            long Index,
            ushort? SampleCounter,
            double? Ia,
            double? Ib,
            double? Ic,
            double? In,
            double? Ua,
            double? Ub,
            double? Uc,
            double? Un)
        {
            public double? GetValue(string channel)
            {
                return channel switch
                {
                    "Ia" => Ia,
                    "Ib" => Ib,
                    "Ic" => Ic,
                    "In" => In,
                    "Ua" => Ua,
                    "Ub" => Ub,
                    "Uc" => Uc,
                    "Un" => Un,
                    _ => null
                };
            }
        }

        private readonly record struct WaveformShapeAnalysis(
            string Severity,
            string StatusText,
            double ResidualPercent,
            double CrestFactor,
            bool HasDistortion);

        private readonly record struct WaveformShapeSummary(
            string Severity,
            string StatusText,
            double ResidualPercent,
            bool HasWarning);

        private readonly record struct SequenceObservation(
            int SampleStep,
            long MissingSamples,
            bool IsError,
            string StatusText);

        private readonly record struct DecodedElement(int Index, int ByteOffset, int Value);

        private readonly record struct PhasorReference(string Channel, double AngleDegrees);
    }

    private sealed class RollingChannelSamples
    {
        private const int SampleCounterModulo = 65536;
        private const double RmsAttackAlpha = 0.02;
        private const double RmsReleaseAlpha = 0.006;
        private readonly Queue<double> _samples = new();
        private readonly Queue<ushort> _sampleCounters = new();
        private readonly Dictionary<ushort, double> _samplesByCounter = new();
        private readonly int _capacity;
        private double _sumSquares;
        private double? _displayRms;

        public RollingChannelSamples(int capacity)
        {
            _capacity = capacity;
        }

        public int Count => _samples.Count;
        public double? LastValue { get; private set; }

        public void Add(ushort? sampleCounter, double value, int minSamplesForRms)
        {
            _samples.Enqueue(value);
            _sumSquares += value * value;
            LastValue = value;

            if (sampleCounter.HasValue)
            {
                _sampleCounters.Enqueue(sampleCounter.Value);
                _samplesByCounter[sampleCounter.Value] = value;
            }

            while (_samples.Count > _capacity)
            {
                var removed = _samples.Dequeue();
                _sumSquares -= removed * removed;
            }

            while (_sampleCounters.Count > _capacity)
            {
                var removedCounter = _sampleCounters.Dequeue();
                if (!_sampleCounters.Contains(removedCounter))
                    _samplesByCounter.Remove(removedCounter);
            }

            UpdateDisplayRms(Math.Max(1, minSamplesForRms));
        }

        public double? GetRawRms()
        {
            return _samples.Count == 0
                ? null
                : Math.Sqrt(Math.Max(0.0, _sumSquares / _samples.Count));
        }

        public double? GetDisplayRms()
        {
            return _displayRms;
        }

        private void UpdateDisplayRms(int minSamplesForRms)
        {
            if (_samples.Count < minSamplesForRms)
                return;

            var rawRms = GetRawRms();
            if (!rawRms.HasValue)
                return;

            if (!_displayRms.HasValue)
            {
                _displayRms = rawRms;
                return;
            }

            var alpha = rawRms.Value >= _displayRms.Value ? RmsAttackAlpha : RmsReleaseAlpha;
            _displayRms += (rawRms.Value - _displayRms.Value) * alpha;
        }

        public IReadOnlyList<double> GetSamples()
        {
            return _samples.ToArray();
        }

        public IReadOnlyList<double> GetLatestSamples(int maxSamples)
        {
            var all = _samples.ToArray();
            if (maxSamples <= 0 || all.Length <= maxSamples)
                return all;

            return all.Skip(all.Length - maxSamples).ToArray();
        }

        public void Clear()
        {
            _samples.Clear();
            _sampleCounters.Clear();
            _samplesByCounter.Clear();
            _sumSquares = 0;
            _displayRms = null;
            LastValue = null;
        }

        public double? GetFundamentalAngleDegrees(int samplesPerCycle)
        {
            if (samplesPerCycle <= 1 || _samples.Count < samplesPerCycle)
                return null;

            var samples = _samples
                .Skip(Math.Max(0, _samples.Count - samplesPerCycle))
                .Take(samplesPerCycle)
                .ToArray();
            if (samples.Length < samplesPerCycle)
                return null;

            // Match ARSVIN Subscriber phasor convention. For a positive sequence waveform,
            // relative to Ua/R = 0°, Ub/S must draw at -120° and Uc/T at +120°.
            // The previous atan2(imaginary, real) - 90° formula inverted the sign and made
            // phase-S appear at +120°, which is fatal for electrical phase interpretation.
            var sine = 0.0;
            var cosine = 0.0;
            var phaseStep = 2.0 * Math.PI / samplesPerCycle;

            for (var index = 0; index < samples.Length; index++)
            {
                var angle = phaseStep * index;
                sine += samples[index] * Math.Sin(angle);
                cosine += samples[index] * Math.Cos(angle);
            }

            if (Math.Abs(sine) < 1e-9 && Math.Abs(cosine) < 1e-9)
                return null;

            return NormalizeAngleDegrees(Math.Atan2(cosine, sine) * 180.0 / Math.PI);
        }

        private static double NormalizeAngleDegrees(double angle)
        {
            while (angle <= -180.0)
                angle += 360.0;
            while (angle > 180.0)
                angle -= 360.0;
            return angle;
        }

        public IReadOnlyList<double> GetScopeWindow(ushort? lastSampleCounter, int samplesPerCycle, int visibleSamples)
        {
            if (!lastSampleCounter.HasValue || samplesPerCycle <= 0 || visibleSamples <= 0 || _sampleCounters.Count == 0)
                return GetTail(visibleSamples);

            // ARSVIN-style phase-locked scope:
            // - Do NOT draw a sliding tail as the primary oscilloscope view. A sliding tail moves the
            //   waveform horizontally at every refresh and looks glitchy even when SV data is stable.
            // - Each smpCnt maps to a fixed modulo slot in the visible window, so new samples update
            //   the same phase position instead of shifting the whole trace.
            var slotValues = new double?[visibleSamples];
            var filled = 0;

            foreach (var counter in _sampleCounters)
            {
                if (!_samplesByCounter.TryGetValue(counter, out var value))
                    continue;

                var slot = counter % visibleSamples;
                if (!slotValues[slot].HasValue)
                    filled++;

                slotValues[slot] = value;
            }

            // Do not forward-fill missing modulo slots. Forward-fill creates artificial flat
            // segments and visible flicker while the UI samples a partially refreshed ring. ARSVIN
            // keeps only real decoded SV points in slot order; use a locked window only after it is
            // mostly populated, otherwise fall back to the latest contiguous tail.
            var minimumUsefulFill = Math.Max(samplesPerCycle * 2, (int)Math.Ceiling(visibleSamples * 0.85));
            if (filled < minimumUsefulFill)
                return GetTail(visibleSamples);

            return BuildStableSlotWindow(slotValues);
        }

        private static IReadOnlyList<double> BuildStableSlotWindow(IReadOnlyList<double?> slotValues)
        {
            var result = new List<double>(slotValues.Count);

            for (var i = 0; i < slotValues.Count; i++)
            {
                if (slotValues[i].HasValue)
                    result.Add(slotValues[i]!.Value);
            }

            return result.Count == 0 ? Array.Empty<double>() : result.ToArray();
        }

        private IReadOnlyList<double> GetTail(int visibleSamples)
        {
            var all = _samples.ToArray();
            if (visibleSamples <= 0 || all.Length <= visibleSamples)
                return all;

            return all.Skip(all.Length - visibleSamples).ToArray();
        }
    }

    private sealed class GooseState
    {
        public GooseState(GooseMessageItem item, int firstSeenOrder)
        {
            Item = item;
            FirstSeenOrder = firstSeenOrder;
        }

        public GooseMessageItem Item { get; set; }
        public int FirstSeenOrder { get; }

        public GooseMessageItem Clone()
        {
            return new GooseMessageItem
            {
                MessageId = Item.MessageId,
                GoId = Item.GoId,
                GoCbRef = Item.GoCbRef,
                DataSet = Item.DataSet,
                AppId = Item.AppId,
                SourceMac = Item.SourceMac,
                DestinationMac = Item.DestinationMac,
                VlanId = Item.VlanId,
                VlanPriority = Item.VlanPriority,
                StNum = Item.StNum,
                SqNum = Item.SqNum,
                ConfRev = Item.ConfRev,
                TimeAllowedToLiveMilliseconds = Item.TimeAllowedToLiveMilliseconds,
                IsTest = Item.IsTest,
                NeedsCommission = Item.NeedsCommission,
                LastSeenUtc = Item.LastSeenUtc,
                ValuesText = Item.ValuesText,
                ChangedSummaryText = Item.ChangedSummaryText,
                StatusText = Item.StatusText,
                DataValues = Item.DataValues
                    .Select(x => new GooseDatasetValueItem
                    {
                        Index = x.Index,
                        Name = x.Name,
                        Type = x.Type,
                        Value = x.Value,
                        RawHex = x.RawHex,
                        IsChanged = x.IsChanged,
                        PreviousValue = x.PreviousValue
                    })
                    .ToArray()
            };
        }
    }

    private sealed class AggregatedEventState
    {
        public AggregatedEventState(string key, string severity, string title, TimeSpan minInterval)
        {
            Key = key;
            Severity = severity;
            Title = title;
            MinInterval = minInterval;
        }

        public string Key { get; }
        public string Severity { get; }
        public string Title { get; }
        public TimeSpan MinInterval { get; }
        public long TotalCount { get; private set; }
        public long WindowCount { get; private set; }
        public DateTime WindowStartedUtc { get; private set; } = DateTime.MinValue;
        public DateTime LastEmittedUtc { get; private set; } = DateTime.MinValue;
        public string LatestDetail { get; private set; } = "N/A";

        public void Observe(DateTime now, string latestDetail)
        {
            if (WindowCount == 0)
                WindowStartedUtc = now;

            TotalCount++;
            WindowCount++;
            LatestDetail = latestDetail;
        }

        public bool ShouldEmit(DateTime now)
        {
            if (WindowCount == 0)
                return false;

            return LastEmittedUtc == DateTime.MinValue ||
                   now - LastEmittedUtc >= MinInterval;
        }

        public TimeSpan WindowAge(DateTime now)
        {
            return WindowStartedUtc == DateTime.MinValue
                ? TimeSpan.Zero
                : now - WindowStartedUtc;
        }

        public void MarkEmitted(DateTime now)
        {
            WindowCount = 0;
            WindowStartedUtc = DateTime.MinValue;
            LastEmittedUtc = now;
        }
    }
}
