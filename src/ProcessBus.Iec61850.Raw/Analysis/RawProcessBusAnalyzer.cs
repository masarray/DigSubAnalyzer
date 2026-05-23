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
    private long _totalFrames;
    private long _svPackets;
    private long _goosePackets;
    private long _ptpPackets;
    private long _decodeErrors;
    private string? _selectedSvKey;
    private int _svFirstSeenSequence;
    private DateTime _startedUtc = DateTime.UtcNow;
    private DateTime? _lastSvSeenUtc;
    private DateTime? _lastGooseSeenUtc;
    private DateTime? _lastPtpSeenUtc;

    public void ObserveFrame(ReadOnlyMemory<byte> frameBytes, DateTime? captureTimeUtc = null, long? captureTicks = null)
    {
        var stableBytes = frameBytes.ToArray();

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
                    .OrderByDescending(x => x.Item.LastSeenUtc)
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
                state = new SvStreamState(key, ++_svFirstSeenSequence);
                _svStreams[key] = state;
                AddEvent("Info", $"Raw SV stream detected: {DescribeSv(packet.Frame, asdu)}.");
            }

            state.Observe(packet.Frame, asdu);
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
            _gooseStates[key] = new GooseState(item);
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
                ? $"PTP {ptp.StatusText} · {ptp.TransportText} · {ptp.TotalMessages} msg"
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
        return $"{name} {state} · {detail} · last {age.TotalSeconds:0.0}s ago";
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
        private const int WaveformSampleCapacity = 640;
        private const int ScopeCycles = 4;
        private const int DefaultSamplesPerCycle = 80;
        private const double WaveformRmsSignatureStep = 0.002;
        private const double WaveformAngleSignatureStepDegrees = 0.2;
        private const double WaveformFrequencySignatureStepHz = 0.01;
        private const double AngleAttackAlpha = 0.18;
        private static readonly TimeSpan DisplayHoldAfterSequenceIssue = TimeSpan.FromMilliseconds(650);
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
        private readonly Dictionary<string, RollingChannelSamples> _channelSamples = CreateChannelBuffers();
        private readonly Dictionary<string, double?> _displayAngles = CreateAngleState();
        private double _absJitterWindowTotalMicroseconds;
        private double _totalDeltaMicroseconds;
        private long _deltaCount;
        private ushort? _previousSmpCnt;
        private long? _previousCaptureTicks;
        private string? _pendingJitterEvidence;
        private string? _lastWaveformSignature;
        private WaveformSnapshot? _lastReconstructedWaveform;
        private DateTime _lastSequenceIssueUtc = DateTime.MinValue;

        public SvStreamState(string key, int firstSeenOrder)
        {
            Key = key;
            FirstSeenOrder = firstSeenOrder;
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

        public void Observe(ProcessBusFrame frame, SampledValueAsdu asdu)
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

            if (acceptForDisplay)
                ObserveSamples(asdu.SamplePayload, asdu.SmpCnt);
        }

        public SvStreamItem ToStreamItem()
        {
            var age = DateTime.UtcNow - LastSeenUtc;
            var isStale = age > TimeSpan.FromSeconds(2);
            var hasWarning = HasIntegrityIssue || RecentJitterOver300MicrosecondsCount > 0;
            var severityRank = isStale ? 2 : hasWarning ? 1 : 0;
            var statusText = isStale ? "Stale" : hasWarning ? "Warning" : "Running";
            var displayStatus = isStale ? "STALE" : hasWarning ? "WARN" : "RAW";
            var statusBrush = isStale ? "#F0B533" : hasWarning ? "#F0B533" : "#70D7A7";
            var statusSoftBrush = isStale ? "#3A2B12" : hasWarning ? "#3A2B12" : "#173528";

            return new SvStreamItem
            {
                StreamId = Key,
                StreamName = SvId == "N/A" ? Key : SvId,
                SvId = SvId,
                AppId = AppId,
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
                ? $"Continuous · smpCnt {LastSmpCnt}"
                : "Continuous · awaiting smpCnt";
        }

        public StreamDetailsModel ToDetails()
        {
            return new StreamDetailsModel
            {
                StreamName = SvId == "N/A" ? Key : SvId,
                SvId = SvId,
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
            var reference = ResolvePhasorReference();

            return new AnalogValuesSnapshot
            {
                Ia = CreateChannelValue("Ia", reference),
                Ib = CreateChannelValue("Ib", reference),
                Ic = CreateChannelValue("Ic", reference),
                In = CreateChannelValue("In", reference),
                Ua = CreateChannelValue("Ua", reference),
                Ub = CreateChannelValue("Ub", reference),
                Uc = CreateChannelValue("Uc", reference),
                Un = CreateChannelValue("Un", reference)
            };
        }

        public WaveformSnapshot ToWaveform()
        {
            var sampleRate = ResolveWaveformSampleRate();
            var frequency = ResolveNominalFrequency();
            var samplesPerCycle = ResolveSamplesPerCycle();
            var visibleSamples = Math.Max(samplesPerCycle * ScopeCycles, DefaultSamplesPerCycle);
            var analog = ToAnalogValues();
            var signature = BuildWaveformSignature(analog, frequency, visibleSamples);

            if (string.Equals(_lastWaveformSignature, signature, StringComparison.Ordinal) &&
                _lastReconstructedWaveform is not null)
                return _lastReconstructedWaveform;

            var voltageSeries = CreateReconstructedWaveformSeries(analog, VoltageChannels, frequency, sampleRate, visibleSamples);
            var currentSeries = CreateReconstructedWaveformSeries(analog, CurrentChannels, frequency, sampleRate, visibleSamples);
            var sampleCount = voltageSeries.Concat(currentSeries)
                .Select(series => series.Samples.Count)
                .DefaultIfEmpty(0)
                .Max();

            var snapshot = new WaveformSnapshot
            {
                VoltageSeries = voltageSeries,
                CurrentSeries = currentSeries,
                SampleRateHz = sampleRate,
                MeasuredFrequencyHz = frequency,
                WindowDurationMilliseconds = sampleRate > 0 && sampleCount > 0
                    ? sampleCount * 1000.0 / sampleRate
                    : 0,
                StatusText = sampleCount > 0
                    ? $"Raw scope reconstructed from RMS + smpCnt timing ({sampleCount} point(s))."
                    : "Raw waveform waiting for mapped channel samples."
            };

            if (sampleCount > 0)
            {
                _lastWaveformSignature = signature;
                _lastReconstructedWaveform = snapshot;
            }

            return snapshot;
        }

        private void ObserveSamples(ReadOnlyMemory<byte> samplePayload, ushort? smpCnt)
        {
            var decoded = DecodeInt32Elements(samplePayload.Span);
            DecodedElementCount = decoded.Count;
            RawValuesText = BuildRawValuesText(decoded);

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

                _channelSamples[channel].Add(smpCnt, decoded[elementIndex].Value, ResolveSamplesPerCycle());
                mapped.Add(channel);
                used.Add(elementIndex);
            }

            MappingProfileName = IsOmicronStream(SvId)
                ? $"OMICRON raw 4I4V instMag/q profile / {SvId}"
                : "Raw 4I4V instMag/q candidate profile";
            MappedChannelNamesText = mapped.Count == 0
                ? "Not mapped yet"
                : $"{string.Join(", ", mapped)} | Used elements: {string.Join(", ", used)}";
        }

        private ChannelValueModel CreateChannelValue(string channel, PhasorReference? reference)
        {
            var samples = _channelSamples[channel];
            var instant = samples.LastValue;
            var unit = channel.StartsWith("U", StringComparison.OrdinalIgnoreCase) ? "V" : "A";
            var angle = ResolveDisplayAngle(channel, samples, reference);

            return new ChannelValueModel(channel)
            {
                InstantValue = instant,
                RmsValue = samples.GetDisplayRms(),
                AngleDegrees = angle,
                Unit = unit
            };
        }

        private double? ResolveDisplayAngle(string channel, RollingChannelSamples samples, PhasorReference? reference)
        {
            if (IsDisplayHoldActive())
                return _displayAngles[channel] ?? DefaultAngleDegrees(channel);

            var samplesPerCycle = ResolveSamplesPerCycle();
            var rawAngle = samples.GetFundamentalAngleDegrees(samplesPerCycle);
            if (!rawAngle.HasValue || reference is null)
                return _displayAngles[channel] ?? DefaultAngleDegrees(channel);

            var referenceValue = reference.Value;
            var relativeAngle = string.Equals(channel, referenceValue.Channel, StringComparison.OrdinalIgnoreCase)
                ? 0.0
                : NormalizeAngleDegrees(rawAngle.Value - referenceValue.AngleDegrees);
            var current = _displayAngles[channel];
            if (!current.HasValue)
            {
                _displayAngles[channel] = relativeAngle;
                return relativeAngle;
            }

            var delta = NormalizeAngleDegrees(relativeAngle - current.Value);
            var smoothed = NormalizeAngleDegrees(current.Value + (delta * AngleAttackAlpha));
            _displayAngles[channel] = smoothed;
            return smoothed;
        }

        private bool IsDisplayHoldActive()
        {
            return _lastSequenceIssueUtc != DateTime.MinValue &&
                   LastSeenUtc - _lastSequenceIssueUtc <= DisplayHoldAfterSequenceIssue;
        }

        private PhasorReference? ResolvePhasorReference()
        {
            var samplesPerCycle = ResolveSamplesPerCycle();

            foreach (var channel in new[] { "Ua", "Ia" })
            {
                var angle = _channelSamples[channel].GetFundamentalAngleDegrees(samplesPerCycle);
                if (angle.HasValue)
                    return new PhasorReference(channel, angle.Value);
            }

            return null;
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
                        visibleSamples)
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
                var sequence = AnalyzeSequence(_previousSmpCnt.Value, smpCnt);
                SequenceStatusText = sequence.StatusText;
                SequenceErrors += sequence.IsError ? 1 : 0;
                MissingSamples += sequence.MissingSamples;
                acceptForDisplay = !sequence.IsError;
                if (sequence.IsError)
                    _lastSequenceIssueUtc = captureTimeUtc;

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
            var abcLike = IsNear(ubNorm, 120.0, 35.0) && IsNear(ucNorm, -120.0, 35.0);
            var acbLike = IsNear(ubNorm, -120.0, 35.0) && IsNear(ucNorm, 120.0, 35.0);

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
                "Ub" => 120.0,
                "Uc" => -120.0,
                "Ia" => -30.0,
                "Ib" => 90.0,
                "Ic" => -150.0,
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

        private static SequenceObservation AnalyzeSequence(ushort previous, ushort current)
        {
            var expected = (ushort)((previous + 1) % SampleCounterModulo);

            if (current == expected)
                return new SequenceObservation(1, 0, false, "Contiguous");

            if (current == previous)
                return new SequenceObservation(1, 0, true, "Duplicate sample counter");

            var forwardStep = (current - previous + SampleCounterModulo) % SampleCounterModulo;
            if (forwardStep > 0 && forwardStep < ReorderBoundary)
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

            var real = 0.0;
            var imaginary = 0.0;
            var phaseStep = 2.0 * Math.PI / samplesPerCycle;

            for (var index = 0; index < samples.Length; index++)
            {
                var angle = phaseStep * index;
                real += samples[index] * Math.Cos(angle);
                imaginary += samples[index] * Math.Sin(angle);
            }

            if (Math.Abs(real) < 1e-9 && Math.Abs(imaginary) < 1e-9)
                return null;

            return NormalizeAngleDegrees((Math.Atan2(imaginary, real) * 180.0 / Math.PI) - 90.0);
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
            if (!lastSampleCounter.HasValue || samplesPerCycle <= 0 || visibleSamples <= 0)
                return GetTail(visibleSamples);

            var latest = lastSampleCounter.Value;
            var latestPhase = latest % samplesPerCycle;
            var end = (latest - latestPhase + SampleCounterModulo) % SampleCounterModulo;
            var start = (end - visibleSamples + 1 + SampleCounterModulo) % SampleCounterModulo;
            var result = new List<double>(visibleSamples);

            for (var i = 0; i < visibleSamples; i++)
            {
                var counter = (ushort)((start + i) % SampleCounterModulo);
                if (_samplesByCounter.TryGetValue(counter, out var value))
                    result.Add(value);
            }

            return result.Count >= Math.Min(samplesPerCycle, visibleSamples / 2)
                ? result
                : GetTail(visibleSamples);
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
        public GooseState(GooseMessageItem item)
        {
            Item = item;
        }

        public GooseMessageItem Item { get; set; }

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
