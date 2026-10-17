using System.Globalization;
using ProcessBus.Core.Models.Scl;
using ProcessBus.Iec61850.Raw.Decoding;
using ProcessBus.Iec61850.Raw.Ptp;
using ProcessBus.Iec61850.Raw.Replay;

namespace ProcessBus.Iec61850.Raw.Interop;

/// <summary>
/// Offline, read-only capture acceptance screening. Uses the production decoder and
/// PCAP reader; never synthesizes missing VLAN tags, counter policy or PTP lock.
/// No IED subscriber acceptance / physical capture-path claim is possible here.
/// </summary>
public sealed class PcapInteropInspector
{
    private const int MaximumIdentities = 128;
    private readonly PcapReplayReader _reader = new(maximumFrameBytes: 65536);

    public PcapInteropReport Inspect(
        Stream pcap,
        IReadOnlyList<SclSvStreamModel>? expectations = null,
        int maximumFrames = 1_000_000,
        CancellationToken cancellationToken = default,
        IProgress<PcapInspectionProgress>? progress = null)
    {
        if (maximumFrames < 1 || maximumFrames > 50_000_000)
            throw new ArgumentOutOfRangeException(nameof(maximumFrames));

        // Value-key identity prevents 4k/4.8k FPS per-ASDU joined-string
        // allocations. Only new publisher identities need a display string.
        var observed = new Dictionary<SvWireKey, SvEvidenceBuilder>();
        var ptp = new PtpHealthTracker();
        long frames = 0, sv = 0, ptpCount = 0, goose = 0, rejected = 0, capped = 0;
        var frameLimitReached = false;
        DateTime? first = null, last = null;
        foreach (var record in _reader.Read(pcap, cancellationToken))
        {
            if (frames >= maximumFrames)
            {
                frameLimitReached = true; // More records exist but were not evaluated.
                break;
            }
            frames++;
            if ((frames & 0x3FFF) == 0)
            {
                // One progress notification per 16k frames; not per sample.
                // The caller owns UI scheduling and may cancel independently.
                progress?.Report(new PcapInspectionProgress(frames, pcap.CanSeek ? pcap.Position : null,
                    pcap.CanSeek ? pcap.Length : null));
            }
            first ??= record.CaptureTimeUtc;
            last = record.CaptureTimeUtc;

            if (!RawProcessBusDecoder.TryDecode(record.FrameBytes, out var decoded,
                    record.CaptureTimeUtc) ||
                (decoded.SampledValues is null && decoded.Ptp is null && decoded.Goose is null))
            {
                rejected++;
                continue;
            }

            if (decoded.Ptp is { } message)
            {
                ptpCount++;
                ptp.Observe(message);
            }

            if (decoded.Goose is not null)
                goose++;

            if (decoded.SampledValues is not { } packet)
                continue;
            sv++;
            foreach (var asdu in packet.Asdus)
            {
                var vlan = packet.Frame.Ethernet.Vlan;
                // This is the SAME strong wire identity domain as the live analyzer.
                var key = new SvWireKey(packet.Frame.Ethernet.SourceMac,
                    packet.Frame.Ethernet.DestinationMac, packet.Frame.AppId,
                    asdu.SvId ?? "unknown-sv", asdu.ConfRev,
                    vlan?.VlanId, vlan?.PriorityCodePoint);

                if (!observed.TryGetValue(key, out var state))
                {
                    if (observed.Count >= MaximumIdentities)
                    {
                        capped++;
                        continue;
                    }
                    state = new SvEvidenceBuilder(key.DisplayText, packet.Frame.Ethernet.SourceMac,
                        packet.Frame.Ethernet.DestinationMac, packet.Frame.AppId,
                        asdu.SvId ?? string.Empty, asdu.ConfRev,
                        vlan?.VlanId, vlan?.PriorityCodePoint);
                    observed.Add(key, state);
                }

                state.Observe(asdu.SmpCnt, packet.Asdus.Count, asdu.SamplePayload.Length,
                    asdu.SmpSynch, record.CaptureTimeUtc);
            }
        }

        var facts = observed.Values.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => x.Finish()).ToArray();
        var expected = expectations ?? Array.Empty<SclSvStreamModel>();
        var comparison = expected.Select(x => Compare(x, facts)).ToArray();
        if (capped > 0 || frameLimitReached)
        {
            // The reader did not examine every identity/frame. A partial
            // capture must not be reported as a complete positive comparison.
            comparison = comparison.Select(x => x with
            {
                Status = "CAPTURE_INCOMPLETE",
                Explanation = $"{x.Status}: {x.Explanation}. " +
                    (frameLimitReached ? "Frame limit reached. " : "") +
                    (capped > 0 ? "Stream catalog saturated. " : "")
            }).ToArray();
        }
        var status = ptp.CreateSnapshot(last ?? DateTime.UtcNow);
        progress?.Report(new PcapInspectionProgress(frames, pcap.CanSeek ? pcap.Position : null,
            pcap.CanSeek ? pcap.Length : null));

        return new PcapInteropReport(
            frames, sv, goose, ptpCount, rejected, capped,
            first, last, facts, comparison,
            status.ObservedSourceCount, status.MatchedTwoStepCount,
            status.UnmatchedFollowUpCount,
            "Capture-file screening only. VLAN visibility depends on capture hardware; " +
            "PTP visibility is not clock lock. Independent TAP/SPAN, hardware timing " +
            "and subscriber/IED acceptance remain unverified.", frameLimitReached);
    }

    private static SclInteropComparison Compare(SclSvStreamModel expectation, IReadOnlyList<SvWireEvidence> observed)
    {
        var id = !string.IsNullOrWhiteSpace(expectation.SvId)
            ? expectation.SvId : expectation.SmvId;

        if (string.IsNullOrWhiteSpace(id))
            return new(expectation.ControlBlockReference, "UNRESOLVED", "SCL svID is missing");

        var candidates = observed.Where(x => string.Equals(x.SvId, id, StringComparison.Ordinal)).ToArray();
        if (candidates.Length == 0)
        {
            var mirrorOnly = observed.Any(x => x.IsArstackDiagnosticMirror);
            return new(expectation.ControlBlockReference,
                mirrorOnly ? "MIRROR_ONLY" : "NOT_OBSERVED",
                mirrorOnly
                    ? "Only ARStack broadcast diagnostic mirror observed; canonical SCL stream not proven"
                    : "Expected svID not observed on this capture path");
        }

        var candidatesWithMismatches = candidates
            .Select(x => (Stream: x, Errors: CompareFields(expectation, x))).ToArray();
        var matching = candidatesWithMismatches.Where(x => x.Errors.Count == 0).ToArray();
        if (matching.Length > 1)
            return new(expectation.ControlBlockReference, "AMBIGUOUS_WIRE",
                $"{matching.Length} independent observed publisher identities match the same SCL stream; select and investigate the true source");

        var exact = matching.FirstOrDefault();
        if (exact.Stream is not null)
        {
            // Missing SCL fields cannot be turned into verified equality.
            var incomplete = string.IsNullOrWhiteSpace(expectation.AppId) ||
                             string.IsNullOrWhiteSpace(expectation.DestinationMac) ||
                             expectation.ConfRev <= 0 ||
                             expectation.NofAsdu <= 0 ||
                             (string.IsNullOrWhiteSpace(expectation.VlanId) && exact.Stream.VlanId.HasValue);
            if (incomplete)
                return new(expectation.ControlBlockReference, "INCOMPLETE_INTENT",
                    "Matching observed fields, but mandatory SCL intent is incomplete", exact.Stream.Key);

            if (exact.Stream.AsduCount < 2)
                return new(expectation.ControlBlockReference, "LIMITED_EVIDENCE",
                    "One ASDU cannot establish sequence progression; record more than one sample", exact.Stream.Key);

            if (exact.Stream.DuplicateCounters > 0 || exact.Stream.NonUnitCounterSteps > 0)
                return new(expectation.ControlBlockReference, "SEQUENCE_WARNING",
                    $"Observed duplicates={exact.Stream.DuplicateCounters}, non-unit steps={exact.Stream.NonUnitCounterSteps}. " +
                    "Do not silently accept interrupted/ambiguous sample continuity", exact.Stream.Key);

            if (exact.Stream.MinPayloadBytes != exact.Stream.MaxPayloadBytes)
                return new(expectation.ControlBlockReference, "LAYOUT_DRIFT",
                    $"Observed sample payload size changed: {exact.Stream.MinPayloadBytes}–{exact.Stream.MaxPayloadBytes} bytes", exact.Stream.Key);

            if (exact.Stream.Candidate4000Wraps > 0 || exact.Stream.Candidate4800Wraps > 0)
                return new(expectation.ControlBlockReference, "COUNTER_POLICY_UNRESOLVED",
                    $"Observed {exact.Stream.Candidate4000Wraps} × 4000 and {exact.Stream.Candidate4800Wraps} × 4800 smpCnt wrap candidates. " +
                    "Sampling rate alone cannot authorize a counter modulus; validate applicable profile/capture authority",
                    exact.Stream.Key);

            return new(expectation.ControlBlockReference, "WIRE_FIELDS_MATCH",
                "Observed identity fields agree and sequential evidence exists; capture path and IED still unverified",
                exact.Stream.Key);
        }

        var nearest = candidatesWithMismatches.OrderBy(x => x.Errors.Count).First();
        return new(expectation.ControlBlockReference, "CONFLICT",
            string.Join("; ", nearest.Errors), nearest.Stream.Key);
    }

    private static List<string> CompareFields(SclSvStreamModel e, SvWireEvidence a)
    {
        var mismatch = new List<string>(6);
        if (!string.IsNullOrWhiteSpace(e.AppId))
        {
            if (!ushort.TryParse(e.AppId.Trim().Replace("0x", "", StringComparison.OrdinalIgnoreCase),
                    NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var appId))
                mismatch.Add("SCL APPID not parseable");
            else if (appId != a.AppId)
                mismatch.Add($"APPID SCL={e.AppId}, wire=0x{a.AppId:X4}");
        }
        if (!string.IsNullOrWhiteSpace(e.DestinationMac) &&
            !string.Equals(NormalizeMac(e.DestinationMac), NormalizeMac(a.DestinationMac), StringComparison.Ordinal))
            mismatch.Add("Destination MAC differs from SCL");
        if (!string.IsNullOrWhiteSpace(e.VlanId))
        {
            if (!TryParseSclVlan(e.VlanId, out var vid))
                mismatch.Add("SCL VLAN ID not parseable");
            else if (a.VlanId != vid)
                mismatch.Add($"VLAN SCL={vid}, wire={(a.VlanId?.ToString() ?? "untagged")}");
        }
        if (!string.IsNullOrWhiteSpace(e.VlanPriority))
        {
            if (!byte.TryParse(e.VlanPriority, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pcp))
                mismatch.Add("SCL VLAN PCP not parseable");
            else if (a.VlanPriority != pcp)
                mismatch.Add($"PCP SCL={pcp}, wire={(a.VlanPriority?.ToString() ?? "untagged")}");
        }
        if (e.ConfRev > 0 && a.ConfRev != e.ConfRev)
            mismatch.Add($"confRev SCL={e.ConfRev}, wire={(a.ConfRev?.ToString() ?? "missing")}");
        if (e.NofAsdu > 0 && (a.MinAsduPerFrame != e.NofAsdu || a.MaxAsduPerFrame != e.NofAsdu))
            mismatch.Add($"nofASDU SCL={e.NofAsdu}, observed range={a.MinAsduPerFrame}..{a.MaxAsduPerFrame}");
        return mismatch;
    }

    private static bool TryParseSclVlan(string raw, out ushort value)
    {
        // IEC SCL P[type=VLAN-ID] commonly uses a four-digit hex lexical form
        // (e.g. 0064 is VID 100, not decimal 64). Short decimal forms are also
        // accepted, but ambiguous four-digit SCL wire encodings remain hex.
        var text = raw.Trim();
        var hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                  text.Length == 4 || text.Any(c => c is >= 'A' and <= 'F' or >= 'a' and <= 'f');
        if (hex)
            text = text.Replace("0x", "", StringComparison.OrdinalIgnoreCase);
        return ushort.TryParse(text, hex ? NumberStyles.AllowHexSpecifier : NumberStyles.Integer,
                   CultureInfo.InvariantCulture, out value) && value <= 4095;
    }

    private static string NormalizeMac(string mac) =>
        new(mac.Where(c => char.IsAsciiHexDigit(c)).Select(char.ToUpperInvariant).ToArray());

    private readonly record struct SvWireKey(
        string SourceMac, string DestinationMac, ushort AppId, string SvId,
        uint? ConfRev, ushort? VlanId, byte? VlanPriority)
    {
        public string DisplayText => string.Join("|",
            SourceMac, DestinationMac,
            VlanId.HasValue ? $"v{VlanId.Value}/p{VlanPriority}" : "untagged",
            $"APPID={AppId:X4}", $"svID={SvId}",
            $"confRev={ConfRev?.ToString() ?? "noconf"}");
    }

    private sealed class SvEvidenceBuilder
    {
        private ushort? _previous;
        private int _minAsdus = int.MaxValue, _maxAsdus;
        private int _minPayload = int.MaxValue, _maxPayload;
        private long _asdUs, _nonUnitSteps, _duplicates, _rateWrap4000, _rateWrap4800;
        private byte _syncValues; // observed 0/1/2 encoded as bit mask, not lock proof
        private DateTime _lastUtc;

        public SvEvidenceBuilder(string key, string source, string destination,
            ushort appId, string svId, uint? confRev, ushort? vid, byte? pcp)
        {
            Key = key; Source = source; Destination = destination;
            AppId = appId; SvId = svId; ConfRev = confRev; Vid = vid; Pcp = pcp;
        }

        public string Key { get; }
        public string Source { get; }
        public string Destination { get; }
        public ushort AppId { get; }
        public string SvId { get; }
        public uint? ConfRev { get; }
        public ushort? Vid { get; }
        public byte? Pcp { get; }

        public void Observe(ushort? smpCnt, int asdus, int bytes, byte? smpSynch, DateTime timestamp)
        {
            _asdUs++;
            _minAsdus = Math.Min(_minAsdus, asdus);
            _maxAsdus = Math.Max(_maxAsdus, asdus);
            _minPayload = Math.Min(_minPayload, bytes);
            _maxPayload = Math.Max(_maxPayload, bytes);
            _lastUtc = timestamp;
            if (smpSynch is <= 2)
                _syncValues |= (byte)(1 << smpSynch.Value);
            if (smpCnt.HasValue && _previous.HasValue)
            {
                if (smpCnt == _previous)
                    _duplicates++;
                else if ((ushort)(smpCnt.Value - _previous.Value) != 1)
                {
                    if (_previous == 3999 && smpCnt == 0)
                        _rateWrap4000++;
                    else if (_previous == 4799 && smpCnt == 0)
                        _rateWrap4800++;
                    else
                        _nonUnitSteps++;
                }
            }
            _previous = smpCnt;
        }

        public SvWireEvidence Finish()
            => new(Key, Source, Destination, AppId, SvId, ConfRev, Vid, Pcp,
                _asdUs, _minAsdus, _maxAsdus, _minPayload, _maxPayload,
                _duplicates, _nonUnitSteps, _rateWrap4000, _rateWrap4800,
                _syncValues, _lastUtc,
                AppId == 0x4F01 && string.Equals(SvId, "AR_DIAG_SV1", StringComparison.Ordinal) &&
                string.Equals(Destination, "FF:FF:FF:FF:FF:FF", StringComparison.OrdinalIgnoreCase) &&
                !Vid.HasValue);
    }
}

public sealed record SvWireEvidence(
    string Key, string SourceMac, string DestinationMac, ushort AppId,
    string SvId, uint? ConfRev, ushort? VlanId, byte? VlanPriority,
    long AsduCount, int MinAsduPerFrame, int MaxAsduPerFrame,
    int MinPayloadBytes, int MaxPayloadBytes,
    long DuplicateCounters, long NonUnitCounterSteps,
    long Candidate4000Wraps, long Candidate4800Wraps,
    byte ObservedSmpSynchMask, DateTime LastSeenUtc,
    bool IsArstackDiagnosticMirror);

public sealed record SclInteropComparison(string ControlBlockReference, string Status,
    string Explanation, string? ObservedStreamKey = null);

public sealed record PcapInteropReport(
    long FramesRead, long SvFrames, long GooseFrames, long PtpMessages,
    long RejectedOrUnsupportedFrames, long CapacityRejectedAsdus,
    DateTime? FirstCaptureUtc, DateTime? LastCaptureUtc,
    IReadOnlyList<SvWireEvidence> SvStreams,
    IReadOnlyList<SclInteropComparison> Comparisons,
    int PtpSources, long MatchedTwoStep, long UnmatchedFollowUp,
    string EvidenceLimitations, bool FrameLimitReached = false)
{
    public bool IsPhysicallyAccepted => false; // offline software cannot certify this
}

/// <summary>Throttled offline PCAP progress; bytes are nullable on non-seekable streams.</summary>
public readonly record struct PcapInspectionProgress(long FramesRead, long? BytesRead, long? TotalBytes)
{
    public int? Percent => BytesRead.HasValue && TotalBytes > 0
        ? (int)Math.Clamp(BytesRead.Value * 100.0 / TotalBytes.Value, 0, 100)
        : null;
}
