using ProcessBus.Core.Models;
using ProcessBus.Core.Models.Scl;
using ProcessBus.Core.Services;
using ProcessBus.Core.Services.Scl;
using Microsoft.Win32;
using ProcessBus.Iec61850.Raw.Live;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Data;

namespace ProcessBus.App.Wpf.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private const int LiveWaveformUiFps = 6;
    private const int PassiveUiRefreshMs = 500;
    private const int GlobalStatusRefreshMs = 500;
    private const int GooseHistoryLimit = 400;
    private const int GooseBufferedFlushLimit = 8;
    private const int DebugTextLimit = 360;
    private static readonly TimeSpan GooseInteractionQuietPeriod = TimeSpan.FromMilliseconds(750);
    private static readonly string LastInterfacePath = Path.Combine(AppContext.BaseDirectory, "raw_capture.last_interface.txt");
    private readonly AnalyzerShellState _state = new();
    private readonly IRawCaptureDataSource _rawDataSource = new RawAnalyzerDataSource();
    private readonly DispatcherTimer _timer;
    private IAnalyzerDataSource _dataSource;
    private bool _isLiveMode = true;
    private bool _relaxDestinationCheck = true;
    private string _selectedAdapterId = string.Empty;
    private SvStreamItem? _selectedStream;
    private GooseMessageItem? _selectedGooseMessage;
    private GooseTrafficRow? _selectedGooseTrafficRow;
    private IReadOnlyList<GooseMessageItem> _gooseMessages = Array.Empty<GooseMessageItem>();
    private GooseMonitorSnapshot _gooseSnapshot = new();
    private IReadOnlyList<PhasorDisplayItem> _phasors = Array.Empty<PhasorDisplayItem>();
    private WaveformSnapshot _displayedWaveform = new();
    private string _waveformShowMode = "Both";
    private string _waveformLayoutMode = "Overlay";
    private string _waveformTimebase = "4 cycles";
    private string _waveformScopeMode = "Locked";
    private string _waveformVoltageScale = "Auto";
    private string _waveformCurrentScale = "Auto";
    private string _debugSvIdText = "N/A";
    private string _debugMappingProfileText = "N/A";
    private string _debugMappedChannelsText = "None";
    private string _debugSampleRateText = "N/A";
    private string _debugTimebaseText = "Timebase pending";
    private string _debugEstimatorText = "N/A";
    private string _debugRawValuesText = "[]";
    private string _debugPacketEvidenceText = "No packet evidence";
    private string _debugRmsText = "RMS pending";
    private string _evidenceCopyStatusText = "Ready to copy engineering snapshot";
    private int _currentWorkspaceTabIndex;
    private bool _refreshInFlight;
    private DateTime _lastRefreshUtc = DateTime.MinValue;
    private long _lastObservedPacketCount = -1;
    private DateTime _lastPacketAdvanceUtc = DateTime.MinValue;
    private DateTime _lastGlobalRaiseUtc = DateTime.MinValue;
    private DateTime _lastGooseRaiseUtc = DateTime.MinValue;
    private DateTime _lastDiagnosticsRaiseUtc = DateTime.MinValue;
    private DateTime _lastDebugRaiseUtc = DateTime.MinValue;
    private bool _streamStale;
    private int _skippedRefreshCount;
    private double _lastUiRefreshMilliseconds;
    private long _managedMemoryMegabytes;
    private const double StreamStaleTimeoutSeconds = 2.0;
    // ===== DIAGNOSTIC ENGINE =====
    private double _packetLossPercent;
    private double _jitterUs;
    private double _latencyUs;
    private int _outOfSequenceCount;
    private bool _smcCntOk = true;
    private bool _frequencyStable = true;
    private bool _isRunning;

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (_isRunning == value) return;
            _isRunning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanSwitchCaptureMode));
        }
    }

    public bool IsRawMode => true;
    public bool CanSwitchCaptureMode => !IsRunning;

    private readonly ObservableCollection<GooseTrafficRow> _gooseHistory = new();
    private readonly Dictionary<string, GooseMessageItem> _lastGooseState = new();
    private readonly Queue<GooseTrafficRow> _pendingGooseRows = new();
    private readonly Queue<DiagnosticEventItem> _pendingDiagnosticEvents = new();
    private DateTime? _lastGooseHistoryTimeUtc;
    private DateTime _gooseInteractionUntilUtc = DateTime.MinValue;
    private bool _isGooseInteractionActive;
    private bool _isSelectionOverlayVisible;
    private bool _isShuttingDown;
    private bool _includeGooseRetransmission;
    private readonly List<SclProjectModel> _sclProjects = new();
    private SclProjectModel _sclProject = SclProjectModel.Empty;
    private string _sclLoadStatusText = "No SCL loaded";
    private readonly ObservableCollection<SclDocumentCardRow> _sclDocuments = new();
    private readonly ObservableCollection<SclIedCardRow> _sclIedCards = new();
    private readonly ObservableCollection<SclStreamCatalogRow> _sclStreamCatalog = new();
    private readonly ObservableCollection<SclBindingMatrixRow> _sclBindingMatrix = new();
    private SclIedCardRow? _selectedSclIedCard;
    private SclStreamCatalogRow? _selectedSclStreamCatalog;
    private SclBindingMatrixRow? _selectedSclBindingMatrixRow;
    private ValidationFindingRow? _selectedValidationFindingRow;
    private string _lastSclBindingSignature = string.Empty;
    private string _goosePublisherFilterText = string.Empty;
    private string _selectedGooseIdFilter = "All";
    private readonly ICollectionView _gooseHistoryView;

    private readonly ObservableCollection<TrafficHealthTargetRow> _diagnosticTargets = new();
    private TrafficHealthTargetRow? _selectedDiagnosticTarget;

    public ObservableCollection<GooseTrafficRow> GooseHistory => _gooseHistory;
    public ICollectionView GooseHistoryView => _gooseHistoryView;
    public ObservableCollection<TrafficHealthTargetRow> DiagnosticTargets => _diagnosticTargets;

    public TrafficHealthTargetRow? SelectedDiagnosticTarget
    {
        get => _selectedDiagnosticTarget;
        set
        {
            if (ReferenceEquals(_selectedDiagnosticTarget, value))
                return;

            // Live target lists refresh continuously. WPF ListBox can briefly push null
            // when its view is refreshed or containers are recycled; do not let that
            // transient null blank the Advanced/Diagnostics inspector.
            if (value is null && _diagnosticTargets.Count > 0)
            {
                OnPropertyChanged();
                return;
            }

            BeginWorkspaceTargetTransition();
            _selectedDiagnosticTarget = value;
            ApplyDiagnosticTargetSelection(value);
            OnPropertyChanged();
            RaiseDiagnosticScopeProperties();
            RaiseAdvancedProperties();
        }
    }

    public bool IncludeGooseRetransmission
    {
        get => _includeGooseRetransmission;
        set
        {
            if (_includeGooseRetransmission == value)
                return;

            _includeGooseRetransmission = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(GooseRetransmissionModeText));
            OnPropertyChanged(nameof(GooseFilterSummaryText));
            _gooseHistoryView.Refresh();
        }
    }

    public string GoosePublisherFilterText
    {
        get => _goosePublisherFilterText;
        set
        {
            if (string.Equals(_goosePublisherFilterText, value, StringComparison.Ordinal))
                return;

            _goosePublisherFilterText = value ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(GooseFilterSummaryText));
            _gooseHistoryView.Refresh();
        }
    }

    public IReadOnlyList<string> GooseIdFilterOptions
    {
        get
        {
            var values = _gooseMessages
                .Select(m => string.IsNullOrWhiteSpace(m.GoId) ? m.GoCbRef : m.GoId)
                .Where(v => !string.IsNullOrWhiteSpace(v) && !string.Equals(v, "N/A", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();

            values.Insert(0, "All");
            return values;
        }
    }

    public string SelectedGooseIdFilter
    {
        get => _selectedGooseIdFilter;
        set
        {
            var next = string.IsNullOrWhiteSpace(value) ? "All" : value;
            if (string.Equals(_selectedGooseIdFilter, next, StringComparison.Ordinal))
                return;

            _selectedGooseIdFilter = next;
            OnPropertyChanged();
            OnPropertyChanged(nameof(GooseFilterSummaryText));
            _gooseHistoryView.Refresh();
        }
    }

    public string GooseRetransmissionModeText => IncludeGooseRetransmission ? "Retrans ON" : "Retrans OFF";
    public string GoosePublisherCountText => $"{_gooseMessages.Count} publisher{(_gooseMessages.Count == 1 ? string.Empty : "s")}";
    public string GooseFilterSummaryText
    {
        get
        {
            var filter = string.Equals(SelectedGooseIdFilter, "All", StringComparison.OrdinalIgnoreCase) ? "All GOOSE IDs" : SelectedGooseIdFilter;
            var retrans = IncludeGooseRetransmission ? "with retransmission" : "state changes only";
            return $"{filter}  -  {retrans}";
        }
    }

    public IReadOnlyList<GooseDatasetValueDisplayItem> SelectedGooseDatasetValues =>
        BuildGooseDatasetValues(SelectedGooseMessage);

    public string SelectedGooseSemanticText
    {
        get
        {
            if (SelectedGooseMessage is null)
                return "Select a GOOSE publisher to inspect semantic DataSet values.";

            var match = ResolveSclGooseStream(SelectedGooseMessage);
            if (match is null)
                return HasSclProject
                    ? "Semantic source: generic typed decode; no matching SCL GOOSE DataSet found."
                    : "Semantic source: generic typed decode; load SCL to resolve signal names.";

            return $"Semantic source: SCL DataSet order  -  {match.ControlBlockReference}  -  {match.DataSetReference}";
        }
    }

    public string DiagnosticScopeTitle => SelectedDiagnosticTarget is null
        ? "All Traffic Overview"
        : $"{SelectedDiagnosticTarget.Protocol}  -  {SelectedDiagnosticTarget.DisplayName}";

    public string DiagnosticScopeSubtitle => SelectedDiagnosticTarget?.Subtitle
        ?? "Select an SV stream, GOOSE publisher, or PTP source from the health navigator.";

    public string DiagnosticScopeIssueText => SelectedDiagnosticTarget?.IssueSummaryText
        ?? "No diagnostic target selected.";

    public string DiagnosticTargetHealthText => SelectedDiagnosticTarget?.StatusText
        ?? HealthText;

    public string DiagnosticAffectedSummaryText
    {
        get
        {
            var warnings = _diagnosticTargets.Count(x => x.SeverityRank > 0);
            if (_diagnosticTargets.Count == 0)
                return "No decoded traffic yet";

            return warnings == 0
                ? $"{_diagnosticTargets.Count} target(s) observed  -  no active warning"
                : $"{warnings} affected target(s)  -  select an item to inspect";
        }
    }

    public string DiagnosticFindingsHeaderText => SelectedDiagnosticTarget is null
        ? "Recent Findings"
        : $"Findings for {SelectedDiagnosticTarget.Protocol} target";


    public string AdvancedTargetTitle => SelectedDiagnosticTarget is null
        ? "Raw Target Inspector"
        : $"{SelectedDiagnosticTarget.Protocol}  -  {SelectedDiagnosticTarget.DisplayName}";

    public string AdvancedTargetSubtitle => SelectedDiagnosticTarget?.Subtitle
        ?? "Select an SV, GOOSE, or PTP source from the Advanced explorer.";

    public string AdvancedPrimaryDetailsText
    {
        get
        {
            var target = SelectedDiagnosticTarget;
            if (target is null)
                return "No raw target selected.";

            if (string.Equals(target.Protocol, "SV", StringComparison.OrdinalIgnoreCase))
            {
                var details = SelectedStreamDetails;
                if (details is null)
                    return "SV target selected, waiting for raw stream details.";

                return string.Join(Environment.NewLine, new[]
                {
                    $"svID                 {details.SvId}",
                    $"DataSet              {details.DataSet}",
                    $"APPID                {details.AppId}",
                    $"Source MAC           {details.SourceMac}",
                    $"Destination MAC      {details.DestinationMac}",
                    $"VLAN                 {details.VlanText}",
                    $"Sample rate          {details.SmpRateText}",
                    $"ConfRev              {details.ConfRevText}",
                    SvMappingSourceText,
                    $"Last seen            {details.LastSeenText}",
                    $"Phase order          {details.PhaseOrderText}"
                });
            }

            if (string.Equals(target.Protocol, "GOOSE", StringComparison.OrdinalIgnoreCase))
            {
                var goose = SelectedGooseMessage;
                if (goose is null)
                    return "GOOSE target selected, waiting for publisher details.";

                return string.Join(Environment.NewLine, new[]
                {
                    $"GOOSE ID             {goose.GoId}",
                    $"GoCBRef              {goose.GoCbRef}",
                    $"APPID                {goose.AppId}",
                    $"Source MAC           {goose.SourceMac}",
                    $"Destination MAC      {goose.DestinationMac}",
                    $"VLAN                 {goose.VlanId} / Priority {goose.VlanPriority}",
                    $"DataSet              {goose.DataSet}",
                    $"stNum / sqNum        {goose.StNum} / {goose.SqNum}",
                    $"TTL                  {goose.TimeAllowedToLiveMilliseconds} ms"
                });
            }

            return string.Join(Environment.NewLine, new[]
            {
                PtpStatusText,
                PtpTransportText,
                PtpDomainText,
                PtpGrandmasterText,
                PtpClockQualityText,
                PtpRateText,
                $"Recent PTP events     {PtpEvents.Count} buffered"
            });
        }
    }

    public string AdvancedEvidenceTitle => SelectedDiagnosticTarget?.Protocol switch
    {
        "GOOSE" => "GOOSE packet evidence",
        "PTP" => "PTP message evidence",
        _ => "SV packet evidence"
    };

    public string AdvancedPacketEvidenceText
    {
        get
        {
            var target = SelectedDiagnosticTarget;
            if (target is null)
                return "Select a raw target to inspect packet evidence.";

            if (string.Equals(target.Protocol, "SV", StringComparison.OrdinalIgnoreCase))
                return DebugPacketEvidenceText;

            if (string.Equals(target.Protocol, "GOOSE", StringComparison.OrdinalIgnoreCase))
            {
                var goose = SelectedGooseMessage;
                return goose is null
                    ? "No GOOSE message selected."
                    : $"{goose.GoCbRef}; APPID={goose.AppId}; DataSet={goose.DataSet}; stNum={goose.StNum}; sqNum={goose.SqNum}; values={goose.ValuesText}; changed={BuildSemanticGooseChangedSummary(goose)}";
            }

            var last = PtpEvents.FirstOrDefault();
            return last is null
                ? "No raw PTP event buffered yet."
                : $"Latest PTP {last.MessageType}; {last.Transport}; {last.Source} -> {last.Destination}; {last.DomainText}; seq={last.SequenceIdText}; clock={last.ClockIdentity}";
        }
    }

    public string AdvancedDecodedTitle => SelectedDiagnosticTarget?.Protocol switch
    {
        "GOOSE" => "Typed DataSet values",
        "PTP" => "Recent PTP messages",
        _ => "Raw decoded elements [0..15]"
    };

    public string AdvancedRawValuesText
    {
        get
        {
            var target = SelectedDiagnosticTarget;
            if (target is null)
                return "[]";

            if (string.Equals(target.Protocol, "SV", StringComparison.OrdinalIgnoreCase))
                return DebugRawValuesText;

            if (string.Equals(target.Protocol, "GOOSE", StringComparison.OrdinalIgnoreCase))
            {
                var values = SelectedGooseMessage?.DataValues;
                if (values is null || values.Count == 0)
                    return "No typed DataSet values decoded.";

                var displayValues = BuildGooseDatasetValues(SelectedGooseMessage);
                return string.Join(Environment.NewLine, displayValues.Select(v => $"[{v.Index}] {v.NameText} - {v.TypeText} = {v.DisplayValueText}"));
            }

            return PtpEvents.Count == 0
                ? "No PTP event list yet."
                : string.Join(Environment.NewLine, PtpEvents.Take(12).Select(x => $"{x.TimestampUtc:HH:mm:ss.fff}  -  {x.Transport}  -  {x.MessageType}  -  {x.Source} -> {x.Destination}  -  {x.DomainText}  -  seq {x.SequenceIdText}"));
        }
    }

    public string AdvancedEngineeringTitle => SelectedDiagnosticTarget?.Protocol switch
    {
        "GOOSE" => "GOOSE engineering interpretation",
        "PTP" => "PTP timing interpretation",
        _ => "Engineering channels and phase order"
    };

    public string AdvancedEngineeringText
    {
        get
        {
            var target = SelectedDiagnosticTarget;
            if (target is null)
                return "Select a target to inspect engineering details.";

            if (string.Equals(target.Protocol, "SV", StringComparison.OrdinalIgnoreCase))
            {
                var details = SelectedStreamDetails;
                if (details is null)
                    return "SV engineering details pending.";

                return string.Join(Environment.NewLine + Environment.NewLine, new[]
                {
                    string.Join(Environment.NewLine, new[]
                    {
                        SvMappingSourceText,
                        SvSemanticChannelSummaryText,
                        details.MappedChannelNamesText,
                        details.ChannelAngleSummaryText,
                        details.PhaseOrderText,
                        details.PhaseOrderDetailText,
                        DebugRmsText
                    }),
                    BuildSclSvSemanticText(details)
                });
            }

            if (string.Equals(target.Protocol, "GOOSE", StringComparison.OrdinalIgnoreCase))
                return SelectedGooseMessage is null
                    ? "GOOSE engineering details pending."
                    : BuildSclGooseSemanticText(SelectedGooseMessage);

            return $"{TimingReferenceText}{Environment.NewLine}{TimingMetricText}{Environment.NewLine}{TimingConfidenceText}";
        }
    }


    private string BuildSclSvSemanticText(StreamDetailsModel details)
    {
        if (!HasSclProject)
            return "SCL semantic map: not loaded. Load SCD/CID/ICD to resolve SV dataset order and signal names.";

        var match = ResolveSclSvMapping(details);

        if (match is null)
            return $"SCL semantic map: no matching SV stream for svID={details.SvId}, APPID={details.AppId}, dst={details.DestinationMac}.";

        return BuildSclSvChannelMapText(match, details);
    }

    private SclSvStreamModel? ResolveSclSvStream(StreamDetailsModel details)
        => ResolveSclSvMapping(details)?.Stream;

    private SclSvMappingCandidate? ResolveSclSvMapping(StreamDetailsModel details)
    {
        if (!HasSclProject)
            return null;

        return _sclProject.SvStreams
            .Select(stream => new
            {
                Stream = stream,
                Score =
                    (TextMatches(stream.SvId, details.SvId) ? 45 : 0) +
                    (AppIdMatches(stream.AppId, details.AppId) ? 30 : 0) +
                    (TextMatches(stream.DestinationMac, details.DestinationMac) ? 20 : 0) +
                    (TextMatches(stream.VlanId, details.VlanText) ? 10 : 0) +
                    (stream.ConfRev > 0 && string.Equals(stream.ConfRev.ToString(), details.ConfRevText, StringComparison.OrdinalIgnoreCase) ? 15 : 0),
                MismatchCount =
                    CountMismatch("svID", stream.SvId, details.SvId) +
                    CountMismatch("APPID", stream.AppId, details.AppId, appId: true) +
                    CountMismatch("Dst MAC", stream.DestinationMac, details.DestinationMac, mac: true) +
                    CountMismatch("VLAN", stream.VlanId, details.VlanText, vlan: true) +
                    CountMismatch("confRev", stream.ConfRev > 0 ? stream.ConfRev.ToString() : "N/A", details.ConfRevText)
            })
            .Where(candidate => candidate.Score >= 35)
            .OrderByDescending(candidate => candidate.Score)
            .Select(candidate => new SclSvMappingCandidate(candidate.Stream, candidate.Score, candidate.MismatchCount))
            .FirstOrDefault();
    }

    private static string BuildSclSvChannelMapText(SclSvMappingCandidate candidate, StreamDetailsModel details)
    {
        var stream = candidate.Stream;
        var lines = new List<string>
        {
            $"SCL SV semantic channel map: {(candidate.IsConfirmed ? "CONFIRMED" : "CANDIDATE")}",
            $"Control block   {stream.ControlBlockReference}",
            $"DataSet         {stream.DataSetReference}",
            $"Transport       {stream.TransportText}",
            $"confRev         {stream.ConfRev}",
            $"Binding score   {candidate.Score}%",
            $"Mapping source  SCL DataSet entry order",
            $"Display source  {details.MappedChannelNamesText}",
            "Note            If channel elements are resolved, Analyzer rendering uses this SCL-bound profile; scaling remains sample-value engineering-unit screening until vendor scale factors are modeled."
        };

        foreach (var entry in stream.Entries.Take(16))
        {
            var payloadIndex = entry.Index - 1;
            var role = InferSvSignalRole(entry);
            lines.Add($"element[{payloadIndex:00}] -> {entry.DisplayName}  -  {entry.TypeText}  -  {role}");
        }

        if (stream.Entries.Count > 16)
            lines.Add($"... {stream.Entries.Count - 16} more SCL DataSet entries");

        return string.Join(Environment.NewLine, lines);
    }

    private static string InferSvSignalRole(SclDataSetEntryModel entry)
    {
        var text = $"{entry.SignalReference}.{entry.DoName}.{entry.DaName}".ToLowerInvariant();
        if (entry.IsQuality)
            return "quality";
        if (entry.IsTimestamp)
            return "timestamp";
        if (text.Contains("phsa") || text.Contains(".ia") || text.Contains(" ia") || text.Contains("instia"))
            return "candidate Ia";
        if (text.Contains("phsb") || text.Contains(".ib") || text.Contains(" ib") || text.Contains("instib"))
            return "candidate Ib";
        if (text.Contains("phsc") || text.Contains(".ic") || text.Contains(" ic") || text.Contains("instic"))
            return "candidate Ic";
        if (text.Contains("phsn") || text.Contains(".in") || text.Contains(" in") || text.Contains("neutral"))
            return "candidate In";
        if (text.Contains("phv") || text.Contains("vol") || text.Contains("voltage") || text.Contains("instu") || text.Contains(".u") || text.Contains(" u"))
            return "candidate voltage";
        if (text.Contains("amp") || text.Contains("current") || text.Contains("insta"))
            return "candidate current";
        return "semantic signal";
    }

    private string BuildSclGooseSemanticText(GooseMessageItem goose)
    {
        if (!HasSclProject)
            return "SCL semantic map: not loaded. Typed allData is decoded generically; load SCD/CID/ICD to resolve GOOSE DataSet signal names, FC, CDC, and types.";

        var match = ResolveSclGooseStream(goose);

        if (match is null)
            return $"SCL semantic map: no matching GOOSE stream for goID={goose.GoId}, APPID={goose.AppId}, DataSet={goose.DataSet}.";

        return BuildSclStreamText("SCL GOOSE semantic map", match.ControlBlockReference, match.DataSetReference, match.TransportText, match.Entries);
    }

    private SclGooseStreamModel? ResolveSclGooseStream(GooseMessageItem goose)
    {
        if (!HasSclProject)
            return null;

        return _sclProject.GooseStreams
            .Select(stream => new
            {
                Stream = stream,
                Score =
                    (TextMatches(stream.ControlBlockReference, goose.GoCbRef) ? 45 : 0) +
                    (TextMatches(stream.GoId, goose.GoId) ? 35 : 0) +
                    (AppIdMatches(stream.AppId, goose.AppId) ? 30 : 0) +
                    (TextMatches(stream.DataSetReference, goose.DataSet) ? 25 : 0) +
                    (TextMatches(stream.DestinationMac, goose.DestinationMac) ? 15 : 0) +
                    (TextMatches(stream.VlanId, goose.VlanId) ? 10 : 0) +
                    (stream.ConfRev > 0 && goose.ConfRev > 0 && stream.ConfRev == goose.ConfRev ? 15 : 0)
            })
            .Where(candidate => candidate.Score >= 35)
            .OrderByDescending(candidate => candidate.Score)
            .Select(candidate => candidate.Stream)
            .FirstOrDefault();
    }

    private static string BuildSclStreamText(string title, string controlReference, string dataSetReference, string transportText, IReadOnlyList<SclDataSetEntryModel> entries)
    {
        var lines = new List<string>
        {
            $"{title}: MATCHED",
            $"Control block   {controlReference}",
            $"DataSet         {dataSetReference}",
            $"Transport       {transportText}",
            $"Entries         {entries.Count}"
        };

        foreach (var entry in entries.Take(12))
            lines.Add($"[{entry.Index:00}] {entry.DisplayName}  -  {entry.TypeText}");

        if (entries.Count > 12)
            lines.Add($"... {entries.Count - 12} more DataSet entries");

        return string.Join(Environment.NewLine, lines);
    }

    private static bool TextMatches(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        var a = NormalizeMatchText(left);
        var b = NormalizeMatchText(right);
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase) || a.Contains(b, StringComparison.OrdinalIgnoreCase) || b.Contains(a, StringComparison.OrdinalIgnoreCase);
    }

    private static int MatchScore(string left, string right)
        => TextMatches(left, right) ? 1 : 0;

    private static int CountMismatch(string name, string expected, string observed, bool appId = false, bool mac = false, bool vlan = false)
    {
        var comparison = CompareField(name, expected, observed, required: true, useAppId: appId, useMac: mac, useVlan: vlan);
        return comparison.IsMismatch ? 1 : 0;
    }

    private static string NormalizeMatchText(string value)
        => (value ?? string.Empty).Trim().Replace("-", ":", StringComparison.Ordinal).Replace("0X", "0x", StringComparison.OrdinalIgnoreCase);

    public MainWindowViewModel()
    {
        _dataSource = _rawDataSource;
        _state.DataSourceName = _dataSource.Name;

        Adapters = new ObservableCollection<NetworkAdapterInfo>(PcapAdapterCatalog.GetAdapters());
        if (Adapters.Count > 0)
            SelectedAdapterId = ResolveInitialAdapterId(Adapters, LoadLastAdapterId());

        Streams = _state.Streams;
        Events = _state.Events;

        _gooseHistoryView = CollectionViewSource.GetDefaultView(_gooseHistory);
        _gooseHistoryView.Filter = FilterGooseHistory;

        StartCommand = new AsyncRelayCommand(StartAsync);
        StopCommand = new AsyncRelayCommand(StopAsync);
        ClearCommand = new AsyncRelayCommand(ClearAsync);
        CopyEvidenceCommand = new RelayCommand(CopyEvidenceSnapshot);
        LoadSclCommand = new RelayCommand(LoadSclProject);
        ClearSclCommand = new RelayCommand(ClearSclProject);

        SwitchToRawCommand = new RelayCommand(() =>
        {
            SwitchToRaw();
            RaiseModeStateChanged();
        });
        GooseInteractionCommand = new RelayCommand(() => DeferGooseUiFlush());

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };

        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();

        _ = RefreshAsync();
    }

    private void BeginWorkspaceTargetTransition()
    {
        // The previous transient overlay was visually noisy in live capture and could flicker
        // when stream/diagnostic target state refreshed. Keep target switching immediate and stable.
        if (_isSelectionOverlayVisible)
        {
            _isSelectionOverlayVisible = false;
            OnPropertyChanged(nameof(SelectionOverlayVisibility));
        }
    }

    private void RaiseModeStateChanged()
    {
        OnPropertyChanged(nameof(IsRawMode));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(CanSwitchCaptureMode));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<NetworkAdapterInfo> Adapters { get; }
    public ObservableCollection<SvStreamItem> Streams { get; }
    public ObservableCollection<DiagnosticEventItem> Events { get; }
    public SvStreamItem? SelectedStream
    {
        get => _selectedStream;
        set
        {
            if (string.Equals(_selectedStream?.StreamId, value?.StreamId, StringComparison.OrdinalIgnoreCase))
                return;

            BeginWorkspaceTargetTransition();
            _selectedStream = value;
            _rawDataSource.SelectStream(value?.StreamId);
            _phasors = Array.Empty<PhasorDisplayItem>();
            _displayedWaveform = new WaveformSnapshot { StatusText = value is null ? "No SV stream selected." : $"Switching to {value.StreamName}." };
            OnPropertyChanged();
            OnPropertyChanged(nameof(Phasors));
            OnPropertyChanged(nameof(Waveform));
            RaiseDiagnosticScopeProperties();
            RaiseAdvancedProperties();
        }
    }

    public IReadOnlyList<GooseMessageItem> GooseMessages => _gooseMessages;

    public GooseMessageItem? SelectedGooseMessage
    {
        get => _selectedGooseMessage;
        set
        {
            if (ReferenceEquals(_selectedGooseMessage, value))
                return;

            BeginWorkspaceTargetTransition();
            _selectedGooseMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedGooseDatasetValues));
            OnPropertyChanged(nameof(SelectedGooseSemanticText));
            RaiseDiagnosticScopeProperties();
            RaiseAdvancedProperties();
        }
    }

    public GooseTrafficRow? SelectedGooseTrafficRow
    {
        get => _selectedGooseTrafficRow;
        set
        {
            if (ReferenceEquals(_selectedGooseTrafficRow, value))
                return;

            BeginWorkspaceTargetTransition();
            _selectedGooseTrafficRow = value;
            _selectedGooseMessage = value?.Source;

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedGooseMessage));
            OnPropertyChanged(nameof(SelectedGooseDatasetValues));
            OnPropertyChanged(nameof(SelectedGooseSemanticText));
            RaiseAdvancedProperties();
        }
    }

    public GooseMonitorSnapshot GooseSnapshot => _gooseSnapshot;
    public ProtocolMonitorSnapshot ProtocolMonitor => _state.ProtocolMonitor;
    public ObservableCollection<PtpEventItem> PtpEvents => _state.PtpEvents;
    public Visibility SelectionOverlayVisibility => Visibility.Collapsed;
    public string ProtocolSvStatusText => _state.ProtocolMonitor.SvStatusText;
    public string ProtocolGooseStatusText => _state.ProtocolMonitor.GooseStatusText;
    public string ProtocolPtpStatusText => _state.ProtocolMonitor.PtpStatusText;
    public string ProtocolSummaryText => _state.ProtocolMonitor.SummaryText;
    public string GooseStatusText => _gooseSnapshot.StatusText;
    public string GooseTotalMessagesText => $"{_gooseSnapshot.TotalMessages} messages";
    public string GooseDetectedCountText => $"{_gooseMessages.Count} detected";
    public string UiRefreshDurationText => $"{_lastUiRefreshMilliseconds:0.0} ms UI tick";
    public string UiQueueDepthText => $"GOOSE queue: {_pendingGooseRows.Count}";
    public string ManagedMemoryText => $"GC memory: {_managedMemoryMegabytes} MB";
    public string SkippedRefreshText => $"Skipped refresh: {_skippedRefreshCount}";
    public string DebugSvIdText => _debugSvIdText;
    public string DebugMappingProfileText => _debugMappingProfileText;
    public string DebugMappedChannelsText => _debugMappedChannelsText;
    public string DebugSampleRateText => _debugSampleRateText;
    public string DebugTimebaseText => _debugTimebaseText;
    public string DebugEstimatorText => _debugEstimatorText;
    public string DebugRawValuesText => _debugRawValuesText;
    public string DebugPacketEvidenceText => _debugPacketEvidenceText;
    public string DebugRmsText => _debugRmsText;

    public int CurrentWorkspaceTabIndex
    {
        get => _currentWorkspaceTabIndex;
        set
        {
            if (_currentWorkspaceTabIndex == value)
                return;

            _currentWorkspaceTabIndex = value;
            UpdateRefreshCadence();
            if (IsGooseTabActive)
            {
                FlushBufferedGooseRows();
                if (SelectedGooseTrafficRow is null && _gooseHistory.Count > 0)
                    SelectedGooseTrafficRow = _gooseHistory[^1];
            }
            if (IsDebugTabActive)
                UpdateDebugDisplaySnapshot();

            OnPropertyChanged();
            OnPropertyChanged(nameof(WorkspaceFooterLeftText));
            OnPropertyChanged(nameof(WorkspaceFooterRightText));
            RaiseAll(DateTime.UtcNow, force: true);
        }
    }

    private bool IsAnalyzerTabActive => _currentWorkspaceTabIndex == 0;
    private bool IsDiagnosticsTabActive => _currentWorkspaceTabIndex == 1;
    private bool IsGooseTabActive => _currentWorkspaceTabIndex == 2;
    private bool IsTimingTabActive => _currentWorkspaceTabIndex == 3;
    private bool IsValidationTabActive => _currentWorkspaceTabIndex == 4;
    private bool IsSclTabActive => _currentWorkspaceTabIndex == 5;
    private bool IsDebugTabActive => _currentWorkspaceTabIndex == 6;

    public IReadOnlyList<string> WaveformShowOptions { get; } = ["Voltage", "Current", "Both"];
    public IReadOnlyList<string> WaveformLayoutOptions { get; } = ["Overlay", "Stacked"];
    public IReadOnlyList<string> WaveformTimebaseOptions { get; } = ["1 cycle", "2 cycles", "4 cycles", "8 cycles"];
    public IReadOnlyList<string> WaveformModeOptions { get; } = ["Locked"];
    public IReadOnlyList<string> WaveformMarkerOptions { get; } = ["On", "Off"];
    public IReadOnlyList<string> WaveformVoltageScaleOptions { get; } =
    [
        "Auto",
    "Auto Peak Hold",
    "110 V",
    "220 V",
    "20 kV",
    "75 kV",
    "150 kV",
    "275 kV",
    "500 kV"
    ];

    public IReadOnlyList<string> WaveformCurrentScaleOptions { get; } =
    [
        "Auto",
    "Auto Peak Hold",
    "1 A",
    "5 A",
    "2 kA",
    "5 kA"
    ];
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand SwitchToRawCommand { get; }
    public ICommand GooseInteractionCommand { get; }
    public ICommand CopyEvidenceCommand { get; }
    public ICommand LoadSclCommand { get; }
    public ICommand ClearSclCommand { get; }

    public SclProjectModel SclProject => _sclProject;
    public bool HasSclProject => _sclProjects.Count > 0 || (!ReferenceEquals(_sclProject, SclProjectModel.Empty) && !string.IsNullOrWhiteSpace(_sclProject.FilePath));
    public string SclLoadStatusText => _sclLoadStatusText;
    public string SclFileNameText => HasSclProject ? _sclProject.FileName : "No SCL loaded";
    public string SclSummaryText => _sclProject.SummaryText;
    public IReadOnlyList<SclSvStreamModel> SclSvStreams => _sclProject.SvStreams;
    public IReadOnlyList<SclGooseStreamModel> SclGooseStreams => _sclProject.GooseStreams;
    public IReadOnlyList<SclDataSetModel> SclDataSets => _sclProject.DataSets;
    public IReadOnlyList<string> SclWarnings => _sclProject.Warnings;
    public ObservableCollection<SclDocumentCardRow> SclDocuments => _sclDocuments;
    public ObservableCollection<SclIedCardRow> SclIedCards => _sclIedCards;
    public ObservableCollection<SclStreamCatalogRow> SclStreamCatalog => _sclStreamCatalog;
    public IReadOnlyList<SclStreamCatalogRow> SclFilteredStreamCatalog => SelectedSclIedCard is null
        ? _sclStreamCatalog.ToList()
        : _sclStreamCatalog
            .Where(x => string.Equals(x.IedName, SelectedSclIedCard.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.SourceFileName, SelectedSclIedCard.SourceFileName, StringComparison.OrdinalIgnoreCase))
            .ToList();

    public ObservableCollection<SclBindingMatrixRow> SclBindingMatrix => _sclBindingMatrix;
    public IReadOnlyList<SclBindingMatrixRow> SclFilteredBindingMatrix => SelectedSclIedCard is null
        ? _sclBindingMatrix.ToList()
        : _sclBindingMatrix
            .Where(x => (string.Equals(x.IedName, SelectedSclIedCard.Name, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.SourceFileName, SelectedSclIedCard.SourceFileName, StringComparison.OrdinalIgnoreCase))
                || string.Equals(x.StatusText, "UNEXPECTED", StringComparison.OrdinalIgnoreCase))
            .ToList();

    public SclBindingMatrixRow? SelectedSclBindingMatrixRow
    {
        get => _selectedSclBindingMatrixRow;
        set
        {
            if (ReferenceEquals(_selectedSclBindingMatrixRow, value))
                return;

            _selectedSclBindingMatrixRow = value;
            if (value?.ExpectedStream is not null)
                _selectedSclStreamCatalog = value.ExpectedStream;

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedSclStreamCatalog));
            RaiseSclSelectionProperties();
        }
    }

    public string SclBindingSummaryText
    {
        get
        {
            if (!HasSclProject)
                return "Load SCL to build binding matrix";

            var scope = SclFilteredBindingMatrix;
            var matched = scope.Count(x => x.IsMatched);
            var missing = scope.Count(x => string.Equals(x.StatusText, "MISSING", StringComparison.OrdinalIgnoreCase));
            var unexpected = scope.Count(x => string.Equals(x.StatusText, "UNEXPECTED", StringComparison.OrdinalIgnoreCase));
            var weak = scope.Count(x => string.Equals(x.StatusText, "WEAK", StringComparison.OrdinalIgnoreCase));
            var mismatch = scope.Count(x => string.Equals(x.StatusText, "MISMATCH", StringComparison.OrdinalIgnoreCase));
            var conflict = scope.Count(x => string.Equals(x.StatusText, "CONFLICT", StringComparison.OrdinalIgnoreCase));
            var ambiguous = scope.Count(x => string.Equals(x.StatusText, "AMBIGUOUS", StringComparison.OrdinalIgnoreCase));
            return $"Binding: {matched} matched  -  {mismatch} mismatch  -  {conflict} conflict  -  {ambiguous} ambiguous  -  {missing} missing  -  {unexpected} unexpected  -  {weak} weak";
        }
    }

    public string ValidationOverallStatusText => GetValidationOverallStatus();
    public string ValidationOverallBrush => ValidationOverallStatusText switch
    {
        "PASS" => "#70D7A7",
        "WARNING" => "#F6D781",
        "FAIL" => "#FF6B6B",
        _ => "#8FA8BF"
    };
    public string ValidationOverallBackgroundBrush => ValidationOverallStatusText switch
    {
        "PASS" => "#17382C",
        "WARNING" => "#3A3218",
        "FAIL" => "#3D1E25",
        _ => "#142235"
    };
    public string ValidationSummaryText
    {
        get
        {
            if (!HasSclProject)
                return $"Engineering context missing. Live traffic can be observed, but expected-vs-observed validation is UNKNOWN until SCL is loaded. {_state.ProtocolMonitor.SummaryText}.";

            var expected = _sclBindingMatrix.Count(x => x.ExpectedStream is not null);
            var observed = _sclBindingMatrix.Count(x => !string.IsNullOrWhiteSpace(x.LiveKey));
            return $"{_sclDocuments.Count} SCL document(s), {expected} expected stream(s), {observed} observed binding target(s). Timing remains packet-arrival screening unless validated hardware timestamps are present.";
        }
    }
    public string ValidationSummaryCompactText
    {
        get
        {
            if (!HasSclProject)
                return "Load SCL to validate expected vs observed traffic.";

            var expected = _sclBindingMatrix.Count(x => x.ExpectedStream is not null);
            var observed = _sclBindingMatrix.Count(x => !string.IsNullOrWhiteSpace(x.LiveKey));
            return $"{_sclDocuments.Count} SCL - {_sclIedCards.Count} IED - {expected} expected - {observed} observed";
        }
    }
    public string ValidationTimingCompactText => Diagnostics.PtpObserved
        ? $"{Diagnostics.PtpTransportText} - {TimingConfidenceBadgeText}"
        : $"PTP not observed - {TimingConfidenceBadgeText}";
    public string ValidationSvSummaryText => BuildValidationProtocolSummary("SV");
    public string ValidationGooseSummaryText => BuildValidationProtocolSummary("GOOSE");
    public string ValidationPtpSummaryText => Diagnostics.PtpObserved
        ? $"PTP observed - {Diagnostics.PtpTransportText} - {PtpDomainText}"
        : (_state.ProtocolMonitor.PtpFrames > 0 ? "PTP stale - previously observed on capture path" : "PTP not observed");
    public string ValidationTimingConfidenceText => $"{TimingConfidenceText} - {TimestampSourceText}";
    public IReadOnlyList<ValidationFindingRow> ValidationFindings => BuildValidationFindings();
    public ValidationFindingRow? SelectedValidationFindingRow
    {
        get => _selectedValidationFindingRow ?? ValidationFindings.FirstOrDefault(x => string.Equals(x.StatusText, "FAIL", StringComparison.OrdinalIgnoreCase))
            ?? ValidationFindings.FirstOrDefault(x => string.Equals(x.StatusText, "WARNING", StringComparison.OrdinalIgnoreCase))
            ?? ValidationFindings.FirstOrDefault();
        set
        {
            if (ReferenceEquals(_selectedValidationFindingRow, value))
                return;

            _selectedValidationFindingRow = value;
            OnPropertyChanged();
            RaiseValidationSelectionProperties();
        }
    }
    public string ValidationDetailTitle => SelectedValidationFindingRow is null
        ? "No validation finding selected"
        : $"{SelectedValidationFindingRow.StatusText} - {SelectedValidationFindingRow.ObjectText}";
    public string ValidationDetailScopeText => SelectedValidationFindingRow is null
        ? "Select a validation row to inspect its evidence."
        : $"{SelectedValidationFindingRow.IedName} - {SelectedValidationFindingRow.StatusText}";
    public string ValidationDetailExpectedText => SelectedValidationFindingRow?.ExpectedText ?? "No expected evidence selected.";
    public string ValidationDetailObservedText => SelectedValidationFindingRow?.ObservedText ?? "No observed evidence selected.";
    public string ValidationDetailEvidenceText => SelectedValidationFindingRow?.EvidenceText ?? "No evidence selected.";
    public string ValidationDetailStatusBrush => SelectedValidationFindingRow?.StatusBrush ?? "#8FA8BF";
    public string ValidationDetailStatusBackgroundBrush => SelectedValidationFindingRow?.StatusBackgroundBrush ?? "#142235";

    private string GetValidationOverallStatus()
    {
        if (!HasSclProject)
            return "UNKNOWN";

        if (_sclBindingMatrix.Count == 0)
            return "UNKNOWN";

        if (_sclBindingMatrix.Any(x => StatusIs(x, "CONFLICT", "MISMATCH", "MISSING")))
            return "FAIL";

        if (_sclBindingMatrix.Any(x => StatusIs(x, "AMBIGUOUS", "WEAK", "UNEXPECTED")))
            return "WARNING";

        return _sclBindingMatrix.Any(x => x.ExpectedStream is not null && x.IsMatched)
            ? "PASS"
            : "UNKNOWN";
    }

    private string BuildValidationProtocolSummary(string protocol)
    {
        var rows = _sclBindingMatrix
            .Where(x => string.Equals(x.Protocol, protocol, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var expected = rows.Count(x => x.ExpectedStream is not null);
        var matched = rows.Count(x => StatusIs(x, "MATCHED"));
        var missing = rows.Count(x => StatusIs(x, "MISSING"));
        var mismatch = rows.Count(x => StatusIs(x, "MISMATCH"));
        var conflict = rows.Count(x => StatusIs(x, "CONFLICT", "AMBIGUOUS"));
        var unexpected = rows.Count(x => StatusIs(x, "UNEXPECTED"));
        var weak = rows.Count(x => StatusIs(x, "WEAK"));

        if (!HasSclProject)
            return $"{protocol}: SCL not loaded - validation UNKNOWN";

        return $"{protocol}: expected {expected} - matched {matched} - missing {missing} - mismatch {mismatch} - conflict/ambiguous {conflict} - unexpected {unexpected} - weak {weak}";
    }

    private IReadOnlyList<ValidationFindingRow> BuildValidationFindings()
    {
        var findings = new List<ValidationFindingRow>();

        if (!HasSclProject)
        {
            findings.Add(ValidationFindingRow.Create(
                "Engineering context",
                "Project",
                "SCL document",
                "No SCL loaded",
                "UNKNOWN",
                "Expected-vs-observed validation requires SCD/CID/ICD/IID engineering context."));
        }
        else
        {
            findings.AddRange(_sclBindingMatrix
                .OrderBy(x => x.SortRank)
                .ThenBy(x => x.Protocol, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.ExpectedName, StringComparer.OrdinalIgnoreCase)
                .Select(row => ValidationFindingRow.Create(
                    $"{row.Protocol} - {(row.ExpectedStream is null ? row.ObservedName : row.ExpectedName)}",
                    string.IsNullOrWhiteSpace(row.IedName) ? "Live traffic" : row.IedName,
                    row.ExpectedStream is null ? row.ExpectedDetailText : $"{row.ExpectedName}\n{row.ExpectedMetaText}",
                    string.IsNullOrWhiteSpace(row.LiveKey) ? "Not observed" : $"{row.ObservedName}\n{row.ObservedMetaText}",
                    MapValidationStatus(row.StatusText),
                    row.StatusSummaryText)));

            if (_sclBindingMatrix.Count == 0)
            {
                findings.Add(ValidationFindingRow.Create(
                    "SCL binding matrix",
                    "Project",
                    "Expected SV/GOOSE streams",
                    "No rows built",
                    "UNKNOWN",
                    "Imported SCL context did not produce expected stream rows yet."));
            }
        }

        findings.Add(ValidationFindingRow.Create(
            "PTP timing context",
            "Timing",
            "Passive timing context",
            ValidationPtpSummaryText,
            Diagnostics.PtpObserved ? "INFO" : "UNKNOWN",
            "PTP affects timing confidence only; it does not enter SV buffers, sequence logic, or waveform rendering."));

        findings.Add(ValidationFindingRow.Create(
            "Capture timestamp source",
            "Capture path",
            "Hardware timestamp validation",
            TimestampSourceText,
            "INFO",
            "Arrival timing evidence is screening-level until validated with hardware timestamping or trusted timing equipment/TAP."));

        return findings;
    }

    private static string MapValidationStatus(string bindingStatus)
        => bindingStatus switch
        {
            "MATCHED" => "PASS",
            "MISSING" or "MISMATCH" or "CONFLICT" => "FAIL",
            "WEAK" or "UNEXPECTED" or "AMBIGUOUS" => "WARNING",
            _ => "UNKNOWN"
        };

    private static bool StatusIs(SclBindingMatrixRow row, params string[] statuses)
        => statuses.Any(status => string.Equals(row.StatusText, status, StringComparison.OrdinalIgnoreCase));

    public SclIedCardRow? SelectedSclIedCard
    {
        get => _selectedSclIedCard;
        set
        {
            if (ReferenceEquals(_selectedSclIedCard, value))
                return;

            _selectedSclIedCard = value;
            if (value is not null)
            {
                var stream = _sclStreamCatalog.FirstOrDefault(x => string.Equals(x.IedName, value.Name, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.SourceFileName, value.SourceFileName, StringComparison.OrdinalIgnoreCase));
                if (stream is not null)
                    _selectedSclStreamCatalog = stream;
            }

            var binding = value is null
                ? _sclBindingMatrix.FirstOrDefault()
                : _sclBindingMatrix.FirstOrDefault(x => string.Equals(x.IedName, value.Name, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.SourceFileName, value.SourceFileName, StringComparison.OrdinalIgnoreCase));
            if (binding is not null)
                _selectedSclBindingMatrixRow = binding;

            OnPropertyChanged();
            OnPropertyChanged(nameof(SclFilteredStreamCatalog));
            OnPropertyChanged(nameof(SclFilteredBindingMatrix));
            OnPropertyChanged(nameof(SelectedSclStreamCatalog));
            OnPropertyChanged(nameof(SelectedSclBindingMatrixRow));
            OnPropertyChanged(nameof(SclBindingSummaryText));
            RaiseSclSelectionProperties();
        }
    }

    public SclStreamCatalogRow? SelectedSclStreamCatalog
    {
        get => _selectedSclStreamCatalog;
        set
        {
            if (ReferenceEquals(_selectedSclStreamCatalog, value))
                return;

            _selectedSclStreamCatalog = value;
            if (value is not null)
            {
                var ied = _sclIedCards.FirstOrDefault(x => string.Equals(x.Name, value.IedName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.SourceFileName, value.SourceFileName, StringComparison.OrdinalIgnoreCase));
                if (ied is not null)
                    _selectedSclIedCard = ied;
            }

            var binding = value is null
                ? null
                : _sclBindingMatrix.FirstOrDefault(x => ReferenceEquals(x.ExpectedStream, value)
                    || string.Equals(x.ExpectedKey, value.ExpectedKey, StringComparison.OrdinalIgnoreCase));
            if (binding is not null)
                _selectedSclBindingMatrixRow = binding;

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedSclIedCard));
            OnPropertyChanged(nameof(SclFilteredStreamCatalog));
            OnPropertyChanged(nameof(SclFilteredBindingMatrix));
            OnPropertyChanged(nameof(SelectedSclBindingMatrixRow));
            OnPropertyChanged(nameof(SclBindingSummaryText));
            RaiseSclSelectionProperties();
        }
    }

    public string SclSemanticStatusText => HasSclProject
        ? $"SCL semantic map loaded  -  {_sclProject.SvStreams.Count} SV  -  {_sclProject.GooseStreams.Count} GOOSE  -  {_sclProject.EditionText}"
        : "Load SCL/CID/ICD to enable semantic stream mapping.";

    public string SclWorkspaceSummaryText => HasSclProject
        ? $"{_sclDocuments.Count} document(s)  -  {_sclIedCards.Count} IED(s)  -  {_sclStreamCatalog.Count} mapped stream(s)  -  {_sclProject.DataSets.Count} DataSet(s)"
        : "Import SCD / CID / ICD / IID files to build the engineering context.";

    public string SclWorkspaceStatusText => HasSclProject
        ? $"Semantic catalog ready  -  {_sclProject.EditionText}"
        : "No engineering context loaded";

    public string SclSelectedDetailTitle => SelectedSclBindingMatrixRow is { ExpectedStream: null } binding
        ? $"{binding.StatusText} {binding.Protocol}  -  {binding.ObservedName}"
        : SelectedSclStreamCatalog is not null
            ? $"{SelectedSclStreamCatalog.Protocol} stream  -  {SelectedSclStreamCatalog.DisplayName}"
            : SelectedSclIedCard is not null
                ? $"IED  -  {SelectedSclIedCard.Name}"
                : "No SCL object selected";

    public string SclSelectedDetailSubtitle => SelectedSclBindingMatrixRow is { ExpectedStream: null } binding
        ? binding.EvidenceText
        : SelectedSclStreamCatalog is not null
            ? $"{SelectedSclStreamCatalog.ControlBlockReference}  -  {SelectedSclStreamCatalog.TransportText}"
            : SelectedSclIedCard is not null
                ? $"{SelectedSclIedCard.SourceFileName}  -  {SelectedSclIedCard.SummaryText}"
                : "Select an imported IED or mapped stream.";

    public IReadOnlyList<SclDataSetEntryModel> SclSelectedEntries => SelectedSclBindingMatrixRow is { ExpectedStream: null }
        ? Array.Empty<SclDataSetEntryModel>()
        : SelectedSclStreamCatalog?.Entries ?? Array.Empty<SclDataSetEntryModel>();

    public string SclSelectedTransportText => SelectedSclStreamCatalog?.TransportText ?? "No stream selected";
    public string SclSelectedDatasetText => SelectedSclBindingMatrixRow is { ExpectedStream: null } binding
        ? $"Observed {binding.Protocol}  -  APPID {binding.AppIdText}  -  VLAN {binding.VlanText}"
        : SelectedSclStreamCatalog?.DataSetReference ?? "No DataSet selected";
    public string SclSelectedBindingText => SelectedSclBindingMatrixRow?.StatusSummaryText
        ?? SelectedSclStreamCatalog?.LiveStatusText
        ?? "Binding pending";
    public string SclSelectedExpectedText => SelectedSclBindingMatrixRow?.ExpectedDetailText
        ?? SelectedSclStreamCatalog?.TransportText
        ?? "No expected stream selected";
    public string SclSelectedObservedText => SelectedSclBindingMatrixRow?.ObservedDetailText
        ?? "No observed stream selected";

    public bool RelaxDestinationCheck
    {
        get => _relaxDestinationCheck;
        set
        {
            if (_relaxDestinationCheck == value) return;
            _relaxDestinationCheck = value;
            OnPropertyChanged();
        }
    }

    public StreamDetailsModel? SelectedStreamDetails => _state.SelectedStreamDetails;
    public string SvMappingSourceText => BuildSvMappingSourceText(SelectedStreamDetails);
    public string SvMappingSourceCompactText => BuildSvMappingSourceCompactText(SelectedStreamDetails);
    public string SvSemanticChannelSummaryText => BuildSvSemanticChannelSummaryText(SelectedStreamDetails);
    public string SvSemanticChannelCompactText => BuildSvSemanticChannelCompactText(SelectedStreamDetails);
    public AnalogValuesSnapshot AnalogValues => _state.AnalogValues;
    public string TotalActivePowerText => FormatPower(ComputeThreePhasePower().P, "W");
    public string TotalReactivePowerText => FormatPower(ComputeThreePhasePower().Q, "var");
    public string TotalApparentPowerText => FormatPower(ComputeThreePhasePower().S, "VA");
    public string PowerFactorText
    {
        get
        {
            var power = ComputeThreePhasePower();
            if (power.S <= 0)
                return "N/A";

            return $"{Math.Clamp(power.P / power.S, -1.0, 1.0):0.000}";
        }
    }
    public WaveformSnapshot Waveform => _displayedWaveform;
    public SvDiagnosticsSnapshot Diagnostics => _state.Diagnostics;
    public string DataSourceName => _state.DataSourceName;
    public string WaveformStatusText => _state.WaveformStatusText;
    public IReadOnlyList<PhasorDisplayItem> Phasors => _phasors;
    public string WaveformHeaderModeText => _dataSource switch
    {
        _ => "Mode: Raw Passive"
    };
    public string WaveformHeaderFrequencyText => Diagnostics.MeasuredFrequencyHz.HasValue
        ? $"{Diagnostics.MeasuredFrequencyHz.Value:0.###} Hz"
        : "N/A";
    public string WaveformHeaderPacketRateText => Diagnostics.PacketRatePps.HasValue
        ? $"{Diagnostics.PacketRatePps.Value:0} pps"
        : "Packet rate pending";
    public string WaveformHeaderSamplesPerCycleText => Diagnostics.SamplesPerCycleEstimate.HasValue
        ? $"{Diagnostics.SamplesPerCycleEstimate.Value:0.##} samples/cycle"
        : "Samples/cycle pending";
    public string WaveformHeaderWindowText => $"Window: {WaveformTimebase}";
    public string WaveformHeaderStatusText => string.IsNullOrWhiteSpace(WaveformStatusText)
        ? "Software timestamp timing  -  reconstructed scope"
        : WaveformStatusText.Replace("Raw scope reconstructed from RMS + smpCnt timing", "Software timestamp timing  -  reconstructed scope", StringComparison.OrdinalIgnoreCase);
    private string BuildSvMappingSourceText(StreamDetailsModel? details)
    {
        if (details is null)
            return "Mapping source: no SV stream selected";

        if (!HasSclProject)
            return $"Mapping source: auto inferred / raw candidate. {details.SampleValueMappingText}";

        var candidate = ResolveSclSvMapping(details);
        if (candidate is null)
            return $"Mapping source: auto inferred / no SCL binding. {details.SampleValueMappingText}";

        var elementCount = BuildSclSvChannelElements(candidate.Stream).Count;
        var source = candidate.IsConfirmed ? "SCL-bound rendering profile" : "SCL candidate rendering profile";
        return $"Mapping source: {source} ({candidate.Score}% binding, {elementCount} channel element(s)). {candidate.Stream.ControlBlockReference}";
    }

    private string BuildSvMappingSourceCompactText(StreamDetailsModel? details)
    {
        if (details is null)
            return "Mapping: no SV selected";

        if (!HasSclProject)
            return "Mapping: inferred raw profile";

        var candidate = ResolveSclSvMapping(details);
        if (candidate is null)
            return "Mapping: no SCL binding";

        var elementCount = BuildSclSvChannelElements(candidate.Stream).Count;
        return candidate.IsConfirmed
            ? $"Mapping: SCL-bound ({candidate.Score}%, {elementCount} ch)"
            : $"Mapping: SCL candidate ({candidate.Score}%, {elementCount} ch)";
    }

    private string BuildSvSemanticChannelSummaryText(StreamDetailsModel? details)
    {
        if (details is null)
            return "No selected SV stream.";

        var candidate = ResolveSclSvMapping(details);
        if (candidate is null)
            return details.MappedChannelNamesText;

        var roles = candidate.Stream.Entries
            .Select(entry => new { Entry = entry, Role = InferSvSignalRole(entry) })
            .Where(x => !string.Equals(x.Role, "quality", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(x.Role, "timestamp", StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .Select(x => $"element[{x.Entry.Index - 1:00}] {x.Role} -> {x.Entry.DisplayName}");

        var summary = string.Join("; ", roles);
        return string.IsNullOrWhiteSpace(summary)
            ? "SCL DataSet loaded, signal roles unresolved; inspect Advanced for full entry order."
            : summary;
    }

    private string BuildSvSemanticChannelCompactText(StreamDetailsModel? details)
    {
        if (details is null)
            return "Select an SV stream";

        var candidate = ResolveSclSvMapping(details);
        if (candidate is null)
            return details.MappedChannelNamesText;

        var entries = candidate.Stream.Entries
            .Where(x => !string.IsNullOrWhiteSpace(x.DisplayName))
            .Take(4)
            .Select(x => x.DisplayName)
            .ToArray();
        var suffix = candidate.Stream.Entries.Count > entries.Length
            ? $" + {candidate.Stream.Entries.Count - entries.Length} more"
            : string.Empty;

        return entries.Length == 0
            ? candidate.Stream.DataSetReference
            : $"{string.Join(", ", entries)}{suffix}";
    }

    public string WaveformShowMode
    {
        get => _waveformShowMode;
        set
        {
            if (_waveformShowMode == value) return;
            _waveformShowMode = value;
            OnPropertyChanged();
        }
    }
    public string WaveformLayoutMode
    {
        get => _waveformLayoutMode;
        set
        {
            if (_waveformLayoutMode == value) return;
            _waveformLayoutMode = value;
            OnPropertyChanged();
        }
    }
    public string WaveformTimebase
    {
        get => _waveformTimebase;
        set
        {
            if (_waveformTimebase == value) return;
            _waveformTimebase = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(WaveformHeaderWindowText));
        }
    }
    public string WaveformScopeMode
    {
        get => _waveformScopeMode;
        set
        {
            const string lockedMode = "Locked";
            if (_waveformScopeMode == lockedMode) return;
            _waveformScopeMode = lockedMode;
            OnPropertyChanged();
        }
    }
    public string WaveformVoltageScale
    {
        get => _waveformVoltageScale;
        set
        {
            if (_waveformVoltageScale == value) return;
            _waveformVoltageScale = value;
            OnPropertyChanged();
        }
    }
    public string WaveformCurrentScale
    {
        get => _waveformCurrentScale;
        set
        {
            if (_waveformCurrentScale == value) return;
            _waveformCurrentScale = value;
            OnPropertyChanged();
        }
    }
    
    public string ModeBannerText => _dataSource switch
    {
        _ => "Raw Passive SV/GOOSE/PTP Engine"
    };

    // ===== HEALTH =====
    private bool HasDiagnosticWarning =>
        Diagnostics.DecodeErrors > 0 ||
        Diagnostics.SequenceErrors > 0 ||
        Diagnostics.MissingSamples > 0 ||
        Diagnostics.RecentJitterOver300MicrosecondsCount > 0 ||
        _packetLossPercent >= 0.1 ||
        _jitterUs >= 300;

    public bool IsHealthy =>
        !_streamStale &&
        !HasDiagnosticWarning;

    public string HealthText =>
        _streamStale ? "NO LIVE STREAM" : IsHealthy ? "GOOD" : "WARNING";

    public string StreamStatusText =>
        !IsRunning ? "STOPPED"
        : _streamStale ? "STALE"
        : "LIVE";

    public bool HasLivePackets => !_streamStale;

    public string StreamAliveText =>
        _streamStale ? "NO SV PACKETS" : "SV PACKETS LIVE";

    public string HealthIcon => IsHealthy ? "OK" : "!";

    public Brush HealthBackgroundBrush =>
        IsHealthy
            ? new SolidColorBrush(Color.FromRgb(18, 64, 42))
            : new SolidColorBrush(Color.FromRgb(80, 45, 20));

    public string HealthDetails =>
        _streamStale
            ? "No SV packet update detected. Check publisher / NIC / APPID / VLAN."
            : HasDiagnosticWarning
                ? BuildHealthWarningText()
                : $"Loss {_packetLossPercent:0.00}% \u2022 Arrival variation {_jitterUs:0}us \u2022 {Diagnostics.JitterStatusText}";

    // ===== TIMING =====
    public string FrequencyText =>
        Diagnostics.MeasuredFrequencyHz.HasValue && Diagnostics.FrequencyEstimateValid
            ? $"{Diagnostics.MeasuredFrequencyHz.Value:0.##} Hz"
            : Diagnostics.MeasuredFrequencyHz.HasValue
                ? "PENDING"
                : "PENDING";
    public string JitterText => $"Variation: {_jitterUs:0} us";
    public string LatencyText => Diagnostics.CurrentDeltaMicroseconds.HasValue
        ? $"Delta: {Diagnostics.CurrentDeltaMicroseconds.Value:0} us"
        : $"Delta: {_latencyUs:0} us";
    public string SvTimingStandardText => "9-2LE protection: 80 samples/cycle @ 50 Hz = 4000 fps, ideal 250 us";
    public string ExpectedDeltaText => Diagnostics.ExpectedDeltaMicroseconds.HasValue
        ? $"Expected: {Diagnostics.ExpectedDeltaMicroseconds.Value:0.###} us"
        : "Expected: pending";

    // ===== INTEGRITY =====
    public string SmcCntStatus =>
        _streamStale ? "NO UPDATE"
        : !_smcCntOk ? "BROKEN"
        : _outOfSequenceCount > 0 ? "JUMP"
        : "CONTINUOUS";

    public string PacketLossText =>
        _streamStale
            ? "Loss: stream stale"
            : _packetLossPercent < 0.01
                ? "Loss: OK"
                : $"Loss: {_packetLossPercent:0.00}%";

    public string OutOfSequenceText =>
        _outOfSequenceCount == 0
            ? "Sequence OK"
            : $"Out-of-seq: {_outOfSequenceCount}";

    public string SequenceStatusText => OutOfSequenceText;

    public string NetworkHealthText =>
        _streamStale && Diagnostics.TotalPackets > 0 ? "SV STALE"
        : _streamStale ? "NO LIVE STREAM"
        : Diagnostics.RecentJitterOver300MicrosecondsCount > 0 && Diagnostics.SequenceErrors == 0 && Diagnostics.MissingSamples == 0 ? "TIMING PATH SUSPECT"
        : !HasDiagnosticWarning ? "STABLE"
        : "UNSTABLE";

    public string SignalHealthText =>
        _streamStale ? "NO LIVE DATA"
        : Diagnostics.FrequencyEstimateValid ? "FREQUENCY OK"
        : "SIGNAL PENDING";

    public string StreamStateText =>
        !IsRunning ? "STOPPED"
        : _streamStale ? "STALE"
        : "LIVE";

    public Brush StreamStateBrush =>
        !IsRunning ? new SolidColorBrush(Color.FromRgb(143, 168, 191))
        : _streamStale ? new SolidColorBrush(Color.FromRgb(240, 181, 51))
        : new SolidColorBrush(Color.FromRgb(55, 214, 122));

    public Brush StreamStateSoftBrush =>
        !IsRunning ? new SolidColorBrush(Color.FromRgb(28, 42, 56))
        : _streamStale ? new SolidColorBrush(Color.FromRgb(58, 43, 18))
        : new SolidColorBrush(Color.FromRgb(12, 58, 40));

    // ===== NETWORK =====
    public string BandwidthText => "Throughput: not measured";

    public string PacketRateText => Diagnostics.PacketRatePps.HasValue
        ? $"{Diagnostics.PacketRatePps.Value:0} fps"
        : "N/A";
    public string CaptureTimingCautionText =>
        Diagnostics.RecentJitterOver300MicrosecondsCount > 0 && Diagnostics.SequenceErrors == 0 && Diagnostics.MissingSamples == 0
            ? "Arrival variation with sequence OK: timing path, publisher scheduling, or capture path suspected"
            : "Arrival timestamp from Npcap/software clock; use PTP context plus TAP/hardware timestamp for measurement-grade proof";

    public string AdapterTimingRiskText
    {
        get
        {
            var selected = GetSelectedAdapter();
            var adapterText = BuildAdapterText(selected);

            if (ContainsAny(adapterText, "Loopback", "Npcap Loopback", "Virtual", "Wi-Fi Direct", "Wireless", "Hyper-V", "VMware"))
                return "Adapter note: virtual/loopback/wireless path is not suitable for jitter validation; use physical Ethernet/TAP.";

            if (ContainsAny(adapterText, "USB"))
                return "Adapter note: USB Ethernet may batch arrivals; prefer TAP/PCIe/hardware timestamp for proof.";

            return "Adapter note: physical Ethernet path selected; software timestamp remains screening-level until externally validated.";
        }
    }

    public string TimingConfidenceText
    {
        get
        {
            var selected = GetSelectedAdapter();
            if (selected is null)
                return "Timing confidence: UNKNOWN - select a capture adapter.";

            var adapterText = BuildAdapterText(selected);
            if (ContainsAny(adapterText, "Loopback", "Npcap Loopback", "Virtual", "Wi-Fi Direct", "Wireless", "Hyper-V", "VMware"))
                return "Timing confidence: LOW - virtual/loopback/wireless capture path; arrival variation is not valid for 300 us proof.";

            if (!Diagnostics.PtpObserved)
                return "Timing confidence: LOW - no PTP timing reference observed; SV timing is arrival-only screening.";

            if (ContainsAny(adapterText, "USB"))
                return "Timing confidence: LOW/MEDIUM - PTP observed, but USB capture may batch arrivals; validate externally.";

            return "Timing confidence: SCREENING - PTP observed with software timestamp on physical Ethernet; hardware timestamp required for proof.";
        }
    }


    public string PtpStatusText => Diagnostics.PtpObserved
        ? $"PTP: {Diagnostics.PtpStatusText}  -  {Diagnostics.PtpTransportText}"
        : $"PTP: {Diagnostics.PtpStatusText}";

    public string PtpStatusCompactText => Diagnostics.PtpObserved
        ? $"PTP: {Diagnostics.PtpTransportText}"
        : (_state.ProtocolMonitor.PtpFrames > 0 ? "PTP: stale" : "PTP: not observed");

    public string TimingConfidenceBadgeText
    {
        get
        {
            var selected = GetSelectedAdapter();
            if (selected is null)
                return "Timing: unknown";

            var adapterText = BuildAdapterText(selected);
            if (ContainsAny(adapterText, "Loopback", "Npcap Loopback", "Virtual", "Wi-Fi Direct", "Wireless", "Hyper-V", "VMware"))
                return "Timing: low";

            if (!Diagnostics.PtpObserved)
                return "Timing: arrival-only";

            if (ContainsAny(adapterText, "USB"))
                return "Timing: screening";

            return "Timing: screening";
        }
    }

    public string WorkspaceFooterLeftText => CurrentWorkspaceTabIndex switch
    {
        0 => $"SV  -  {SelectedStreamDetails?.MappedChannelNamesText ?? "No SV stream selected"}",
        1 => $"Diagnostics  -  {HealthText}  -  {NetworkHealthText}",
        2 => $"GOOSE  -  {_gooseMessages.Count} publisher(s)  -  {_gooseHistory.Count} event(s)",
        3 => $"Timing/PTP  -  {PtpStatusCompactText}  -  {PtpDomainText}",
        4 => $"Validation  -  {ValidationOverallStatusText}  -  expected-vs-observed evidence",
        5 => $"SCL  -  {_sclDocuments.Count} document(s)  -  {_sclIedCards.Count} IED(s)",
        6 => "Advanced  -  raw decode, estimator, and performance",
        _ => "Process Bus Insight"
    };

    public string WorkspaceFooterRightText => CurrentWorkspaceTabIndex switch
    {
        0 => WaveformHeaderStatusText,
        1 => TimingConfidenceBadgeText,
        2 => GooseFilterSummaryText,
        3 => TimingConfidenceBadgeText,
        4 => ValidationTimingConfidenceText,
        5 => SclWorkspaceStatusText,
        6 => UiRefreshDurationText,
        _ => StreamStatusText
    };

    public string PtpGrandmasterText => Diagnostics.PtpObserved
        ? $"GM: {Diagnostics.PtpGrandmasterIdentity}"
        : "GM: N/A";

    public string PtpDomainText => Diagnostics.PtpDomainNumber.HasValue
        ? $"Domain: {Diagnostics.PtpDomainNumber}"
        : "Domain: N/A";

    public string PtpClockQualityText => Diagnostics.PtpObserved
        ? $"ClockClass {Diagnostics.PtpClockClass?.ToString() ?? "N/A"}  -  Accuracy {Diagnostics.PtpClockAccuracyText}  -  Steps {Diagnostics.PtpStepsRemoved?.ToString() ?? "N/A"}"
        : "Clock quality: N/A";

    public string PtpRateText
    {
        get
        {
            var sync = Diagnostics.PtpSyncRatePerSecond.HasValue ? $"{Diagnostics.PtpSyncRatePerSecond.Value:0.##}/s" : "N/A";
            var announce = Diagnostics.PtpAnnounceRatePerSecond.HasValue ? $"{Diagnostics.PtpAnnounceRatePerSecond.Value:0.##}/s" : "N/A";
            var follow = Diagnostics.PtpFollowUpRatePerSecond.HasValue ? $"{Diagnostics.PtpFollowUpRatePerSecond.Value:0.##}/s" : "N/A";
            return $"Sync {sync}  -  Announce {announce}  -  Follow_Up {follow}";
        }
    }

    public string PtpTransportText => Diagnostics.PtpObserved
        ? $"Transport: {Diagnostics.PtpTransportText}"
        : "Transport: N/A";

    public string PtpProfileText => Diagnostics.PtpProfileHintText;
    public string TimingReferenceText => Diagnostics.TimingReferenceText;
    public string TimestampSourceText => Diagnostics.TimestampSourceText;
    public string TimingMetricText => Diagnostics.TimingMetricText;

    private NetworkAdapterInfo? GetSelectedAdapter() =>
        Adapters.FirstOrDefault(adapter =>
            string.Equals(adapter.Id, SelectedAdapterId, StringComparison.OrdinalIgnoreCase));

    private static string BuildAdapterText(NetworkAdapterInfo? adapter) =>
        adapter is null
            ? string.Empty
            : $"{adapter.Name} {adapter.Description} {adapter.RawDeviceName}";

    private static bool ContainsAny(string text, params string[] tokens) =>
        tokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));

    // ===== ENGINEERING SNAPSHOT =====
    public string EvidenceVerdictText
    {
        get
        {
            if (_streamStale)
                return "WAITING FOR LIVE SV";

            if (Diagnostics.SequenceErrors == 0 &&
                Diagnostics.MissingSamples == 0 &&
                Diagnostics.RecentJitterOver300MicrosecondsCount > 0)
            {
                return "SV CONTINUOUS; TIMING PATH SUSPECT";
            }

            return IsHealthy
                ? "PASSIVE SV DECODER NOMINAL"
                : "PASSIVE SV DECODER NEEDS REVIEW";
        }
    }

    public string EvidenceStandardText =>
        $"Reference: IEC 61850-9-2LE protection, 80 samples/cycle @ 50 Hz = 4000 fps, ideal interval 250 us. Observed: {PacketRateText}, {ExpectedDeltaText}.";

    public string EvidenceIntegrityText =>
        $"Integrity: {SmcCntStatus}; {OutOfSequenceText}; {PacketLossText}; smpCnt={Diagnostics.LastSampleCount?.ToString() ?? "N/A"}; missing={Diagnostics.MissingSamples}.";

    public string EvidenceTimingText
    {
        get
        {
            var recent = Diagnostics.RecentJitterOver300MicrosecondsCount;
            var max = Diagnostics.MaxAbsJitterMicroseconds.HasValue
                ? $"{Diagnostics.MaxAbsJitterMicroseconds.Value:0.#} us"
                : "N/A";
            return $"Timing: {LatencyText}; {JitterText}; arrival excursions >=300 us={recent}/5s; max abs variation={max}; {Diagnostics.TimingMetricText}; {Diagnostics.PacketRateMeaningText}.";
        }
    }

    public string EvidencePtpText =>
        $"PTP: {Diagnostics.PtpStatusText}; {PtpTransportText}; {PtpDomainText}; {PtpGrandmasterText}; {PtpRateText}; {Diagnostics.PtpProfileHintText}.";

    public string EvidenceDecodeText =>
        $"Decode: {Diagnostics.DecodeStatusText}; rejected frames={Diagnostics.DecodeErrors}; raw passive engine; no external IEC 61850 runtime dependency.";

    public string EvidenceCapturePathText
    {
        get
        {
            var selected = GetSelectedAdapter();
            var adapter = selected is null ? "N/A" : selected.ToString();
            return $"Capture path: {adapter}; {TimingConfidenceText}; {CaptureTimingCautionText}; {AdapterTimingRiskText}";
        }
    }

    public string EvidenceSnapshotText => BuildEvidenceSnapshotText();
    public string EvidenceCopyStatusText => _evidenceCopyStatusText;

    // ===== SIGNAL =====
    public string VoltageBalanceText => "Balance: not mapped";
    public string FrequencyStabilityText =>
        Diagnostics.MeasuredFrequencyHz.HasValue && Diagnostics.FrequencyEstimateValid
            ? $"Measured frequency: {Diagnostics.MeasuredFrequencyHz.Value:0.##} Hz"
            : Diagnostics.MeasuredFrequencyHz.HasValue
                ? $"Measured frequency pending: {Diagnostics.MeasuredFrequencyHz.Value:0.##} Hz"
                : "Measured frequency: pending";

    private string BuildHealthWarningText()
    {
        var parts = new List<string>(5);
        if (Diagnostics.DecodeErrors > 0)
            parts.Add($"decode rejects {Diagnostics.DecodeErrors}");
        if (Diagnostics.SequenceErrors > 0)
            parts.Add($"sequence jumps {Diagnostics.SequenceErrors}");
        if (Diagnostics.MissingSamples > 0)
            parts.Add($"missing samples {Diagnostics.MissingSamples}");
        if (Diagnostics.RecentJitterOver300MicrosecondsCount > 0)
            parts.Add($"recent arrival excursion >=300us {Diagnostics.RecentJitterOver300MicrosecondsCount}/5s");
        if (_packetLossPercent >= 0.1 && Diagnostics.MissingSamples == 0)
            parts.Add($"loss {_packetLossPercent:0.00}%");
        if (_jitterUs >= 300 && Diagnostics.RecentJitterOver300MicrosecondsCount == 0)
            parts.Add($"arrival variation {_jitterUs:0}us");

        return parts.Count == 0
            ? $"Loss {_packetLossPercent:0.00}% \u2022 Arrival variation {_jitterUs:0}us \u2022 {Diagnostics.JitterStatusText}"
            : string.Join(" \u2022 ", parts);
    }

    public bool IsLiveMode
    {
        get => _isLiveMode;
        set
        {
            if (_isLiveMode == value) return;
            _isLiveMode = value;
            OnPropertyChanged();
        }
    }

    public string SelectedAdapterId
    {
        get => _selectedAdapterId;
        set
        {
            if (_selectedAdapterId == value) return;
            _selectedAdapterId = value;
            var selected = Adapters.FirstOrDefault(adapter => adapter.Id == value);
            _rawDataSource.SelectAdapter(value, selected?.RawDeviceName ?? string.Empty);
            SaveLastAdapterId(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(AdapterTimingRiskText));
            OnPropertyChanged(nameof(TimingConfidenceText));
            OnPropertyChanged(nameof(TimingConfidenceBadgeText));
            OnPropertyChanged(nameof(WorkspaceFooterLeftText));
            OnPropertyChanged(nameof(WorkspaceFooterRightText));
            OnPropertyChanged(nameof(TimingReferenceText));
            OnPropertyChanged(nameof(TimestampSourceText));
            OnPropertyChanged(nameof(TimingMetricText));
            OnPropertyChanged(nameof(EvidenceCapturePathText));
            OnPropertyChanged(nameof(EvidenceSnapshotText));
        }
    }

    private static string? LoadLastAdapterId()
    {
        try
        {
            if (!File.Exists(LastInterfacePath))
                return null;

            var id = File.ReadAllText(LastInterfacePath).Trim();
            return string.IsNullOrWhiteSpace(id) ? null : id;
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveInitialAdapterId(IReadOnlyList<NetworkAdapterInfo> adapters, string? savedAdapterId)
    {
        if (!string.IsNullOrWhiteSpace(savedAdapterId) &&
            adapters.Any(adapter => string.Equals(adapter.Id, savedAdapterId, StringComparison.OrdinalIgnoreCase)))
        {
            return savedAdapterId;
        }

        return adapters[0].Id;
    }

    private static void SaveLastAdapterId(string adapterId)
    {
        try
        {
            File.WriteAllText(LastInterfacePath, adapterId ?? string.Empty);
        }
        catch
        {
            // Best-effort persistence only.
        }
    }

    private async Task RefreshAsync()
    {
        if (_refreshInFlight)
        {
            _skippedRefreshCount++;
            return;
        }

        var now = DateTime.UtcNow;
        if ((now - _lastRefreshUtc).TotalMilliseconds < 90)
        {
            _skippedRefreshCount++;
            return;
        }

        _refreshInFlight = true;
        var refreshWatch = Stopwatch.StartNew();
        try
        {
            var (snapshot, gooseSnapshot) = await CaptureSnapshotsAsync();
            if (ReferenceEquals(_dataSource, _rawDataSource) && _rawDataSource is not null && IsRunning != _rawDataSource.IsRunning)
                IsRunning = _rawDataSource.IsRunning;

            _gooseSnapshot = gooseSnapshot;
            _gooseMessages = _gooseSnapshot.Messages;
            if (_selectedGooseMessage is not null)
            {
                _selectedGooseMessage = _gooseMessages.FirstOrDefault(x => string.Equals(x.MessageId, _selectedGooseMessage.MessageId, StringComparison.OrdinalIgnoreCase))
                    ?? _selectedGooseMessage;
            }

            foreach (var msg in _gooseSnapshot.Messages)
            {
                var key = $"{msg.GoId}|{msg.GoCbRef}|{msg.DataSet}";

                if (!_lastGooseState.TryGetValue(key, out var last))
                {
                    _lastGooseState[key] = msg;
                    BufferGooseHistoryRow(msg, "New");
                    continue;
                }

                bool isStateChange =
                    msg.StNum != last.StNum ||
                    msg.ConfRev != last.ConfRev;

                bool isRetransmit =
                    msg.StNum == last.StNum &&
                    msg.SqNum != last.SqNum;

                if (isStateChange)
                {
                    _lastGooseState[key] = msg;
                    BufferGooseHistoryRow(msg, "State Change");
                }
                else if (IncludeGooseRetransmission && isRetransmit)
                {
                    BufferGooseHistoryRow(msg, "Retrans");
                }
            }

            if (IsGooseTabActive)
                TryFlushBufferedGooseRows();

            if (IsDiagnosticsTabActive)
                FlushBufferedDiagnosticEvents(snapshot.Events);
            else
                BufferDiagnosticEvents(snapshot.Events);

            _state.ApplySnapshot(snapshot, mergeEvents: IsDiagnosticsTabActive);
            UpdateStreamStaleState();
            RebuildDiagnosticTargets();
            RebuildSclBindingMatrix();
            if (IsDebugTabActive)
                UpdateDebugDisplaySnapshot();

            // ===== DIAGNOSTIC COMPUTE ENGINE =====
            var observedPackets = Math.Max(0, Diagnostics.TotalPackets);
            var missingSamples = Math.Max(0, Diagnostics.MissingSamples);
            _packetLossPercent = observedPackets + missingSamples > 0
                ? missingSamples * 100.0 / (observedPackets + missingSamples)
                : 0;

            _jitterUs = Diagnostics.CurrentJitterMicroseconds.HasValue
                ? Math.Abs(Diagnostics.CurrentJitterMicroseconds.Value)
                : Diagnostics.AverageAbsJitterMicroseconds ?? 0;
            _latencyUs = Diagnostics.CurrentDeltaMicroseconds ?? 0;

            _outOfSequenceCount = Diagnostics.SequenceErrors > int.MaxValue
                ? int.MaxValue
                : (int)Diagnostics.SequenceErrors;

            _smcCntOk = Diagnostics.LastSampleCount > 0;
            _frequencyStable = Diagnostics.FrequencyEstimateValid;

            if (SelectedStream is null)
                SelectedStream = Streams.FirstOrDefault();
            else
                _selectedStream = Streams.FirstOrDefault(stream => string.Equals(stream.StreamId, SelectedStream.StreamId, StringComparison.OrdinalIgnoreCase)) ?? _selectedStream;
            if (IsGooseTabActive && SelectedGooseTrafficRow is null && _gooseHistory.Count > 0)
            {
                SelectedGooseTrafficRow = _gooseHistory[^1];
            }
            if (IsAnalyzerTabActive)
            {
                _phasors = BuildPhasorItems(_state.AnalogValues);
                _displayedWaveform = _state.Waveform;
            }

            _lastRefreshUtc = now;
            refreshWatch.Stop();
            _lastUiRefreshMilliseconds = refreshWatch.Elapsed.TotalMilliseconds;
            _managedMemoryMegabytes = GC.GetTotalMemory(false) / (1024 * 1024);
            RaiseAll(now);
        }
        finally
        {
            _refreshInFlight = false;
        }
    }

    private async Task<(AnalyzerSnapshot Snapshot, GooseMonitorSnapshot GooseSnapshot)> CaptureSnapshotsAsync()
    {
        return await Task.Run(() =>
        {
            var snapshot = _rawDataSource.GetSnapshotAsync().GetAwaiter().GetResult();
            var gooseSnapshot = _rawDataSource.GetGooseSnapshot();
            return (snapshot, gooseSnapshot);
        });
    }

    private void RebuildDiagnosticTargets()
    {
        var previousKey = SelectedDiagnosticTarget?.TargetKey;
        var rows = new List<TrafficHealthTargetRow>();

        foreach (var stream in Streams.OrderBy(x => x.FirstSeenOrder))
        {
            rows.Add(new TrafficHealthTargetRow
            {
                Protocol = "SV",
                TargetKey = $"SV|{stream.StreamId}",
                DisplayName = string.IsNullOrWhiteSpace(stream.SvId) ? stream.StreamName : stream.SvId,
                Subtitle = $"{stream.AppId}  -  {stream.VlanText}  -  {ShortenMac(stream.SourceMac)}",
                StatusText = stream.DisplayStatusText,
                StatusBrush = stream.StatusBrush,
                StatusSoftBrush = stream.StatusSoftBrush,
                IssueSummaryText = stream.IssueSummaryText,
                LastSeenUtc = stream.LastSeenUtc,
                SeverityRank = stream.SeverityRank,
                SourceId = stream.StreamId
            });
        }

        foreach (var goose in _gooseMessages)
        {
            var age = DateTime.UtcNow - goose.LastSeenUtc;
            var stale = age > TimeSpan.FromSeconds(5);
            rows.Add(new TrafficHealthTargetRow
            {
                Protocol = "GOOSE",
                TargetKey = $"GOOSE|{goose.MessageId}",
                DisplayName = string.IsNullOrWhiteSpace(goose.GoId) || goose.GoId == "N/A" ? goose.GoCbRef : goose.GoId,
                Subtitle = $"{goose.AppId}  -  VLAN {goose.VlanId}  -  st/sq {goose.StNum}/{goose.SqNum}",
                StatusText = stale ? "STALE" : "LIVE",
                StatusBrush = stale ? "#F0B533" : "#70D7A7",
                StatusSoftBrush = stale ? "#3A2B12" : "#173528",
                IssueSummaryText = string.IsNullOrWhiteSpace(goose.ChangedSummaryText) || goose.ChangedSummaryText == "N/A"
                    ? $"State tracked  -  st/sq {goose.StNum}/{goose.SqNum}"
                    : goose.ChangedSummaryText,
                LastSeenUtc = goose.LastSeenUtc,
                SeverityRank = stale ? 1 : 0,
                SourceId = goose.MessageId
            });
        }

        var ptpObserved = Diagnostics.PtpObserved || _state.ProtocolMonitor.PtpFrames > 0;
        rows.Add(new TrafficHealthTargetRow
        {
            Protocol = "PTP",
            TargetKey = "PTP|reference",
            DisplayName = ptpObserved ? Diagnostics.PtpGrandmasterIdentity : "Timing reference",
            Subtitle = ptpObserved ? $"{Diagnostics.PtpTransportText}  -  {PtpDomainText}" : "No PTP observed",
            StatusText = ptpObserved ? "OBSERVED" : "WAIT",
            StatusBrush = ptpObserved ? "#70D7A7" : "#8FA8BF",
            StatusSoftBrush = ptpObserved ? "#173528" : "#1C2A38",
            IssueSummaryText = ptpObserved ? PtpRateText : "No PTP packets detected in current capture",
            LastSeenUtc = Diagnostics.LastPtpMessageTimestampUtc,
            SeverityRank = ptpObserved ? 0 : 1,
            SourceId = "PTP"
        });

        SyncDiagnosticTargetRows(rows);

        var next = !string.IsNullOrWhiteSpace(previousKey)
            ? _diagnosticTargets.FirstOrDefault(x => string.Equals(x.TargetKey, previousKey, StringComparison.OrdinalIgnoreCase))
            : null;

        next ??= _diagnosticTargets.FirstOrDefault(x => x.SeverityRank > 0);
        next ??= SelectedStream is null
            ? null
            : _diagnosticTargets.FirstOrDefault(x => string.Equals(x.TargetKey, $"SV|{SelectedStream.StreamId}", StringComparison.OrdinalIgnoreCase));
        next ??= _diagnosticTargets.FirstOrDefault();

        if (!ReferenceEquals(_selectedDiagnosticTarget, next))
        {
            _selectedDiagnosticTarget = next;
            ApplyDiagnosticTargetSelection(next);
            OnPropertyChanged(nameof(SelectedDiagnosticTarget));
        }

        RaiseDiagnosticScopeProperties();
        RaiseAdvancedProperties();
        OnPropertyChanged(nameof(DiagnosticTargets));
    }

    private void SyncDiagnosticTargetRows(IReadOnlyList<TrafficHealthTargetRow> rows)
    {
        var desiredKeys = rows.Select(x => x.TargetKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var index = _diagnosticTargets.Count - 1; index >= 0; index--)
        {
            if (!desiredKeys.Contains(_diagnosticTargets[index].TargetKey))
                _diagnosticTargets.RemoveAt(index);
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var existingIndex = -1;
            for (var i = 0; i < _diagnosticTargets.Count; i++)
            {
                if (string.Equals(_diagnosticTargets[i].TargetKey, row.TargetKey, StringComparison.OrdinalIgnoreCase))
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex < 0)
            {
                _diagnosticTargets.Add(row);
                continue;
            }

            var existing = _diagnosticTargets[existingIndex];
            existing.UpdateFrom(row);
            if (existingIndex != rowIndex && rowIndex < _diagnosticTargets.Count)
                _diagnosticTargets.Move(existingIndex, rowIndex);
        }
    }

    private void ApplyDiagnosticTargetSelection(TrafficHealthTargetRow? target)
    {
        if (target is null)
            return;

        if (string.Equals(target.Protocol, "SV", StringComparison.OrdinalIgnoreCase))
        {
            var stream = Streams.FirstOrDefault(x => string.Equals(x.StreamId, target.SourceId, StringComparison.OrdinalIgnoreCase));
            if (stream is not null)
                SelectedStream = stream;
        }
        else if (string.Equals(target.Protocol, "GOOSE", StringComparison.OrdinalIgnoreCase))
        {
            var goose = _gooseMessages.FirstOrDefault(x => string.Equals(x.MessageId, target.SourceId, StringComparison.OrdinalIgnoreCase));
            if (goose is not null)
                SelectedGooseMessage = goose;
        }
    }

    private void RaiseDiagnosticScopeProperties()
    {
        OnPropertyChanged(nameof(DiagnosticScopeTitle));
        OnPropertyChanged(nameof(DiagnosticScopeSubtitle));
        OnPropertyChanged(nameof(DiagnosticScopeIssueText));
        OnPropertyChanged(nameof(DiagnosticTargetHealthText));
        OnPropertyChanged(nameof(DiagnosticAffectedSummaryText));
        OnPropertyChanged(nameof(DiagnosticFindingsHeaderText));
    }

    private static string ShortenMac(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= 8)
            return value;

        return value.Replace(":", string.Empty).Length >= 6
            ? value
            : value;
    }

    private void UpdateStreamStaleState()
    {
        var now = DateTime.UtcNow;
        var packets = Diagnostics.TotalPackets;

        if (packets != _lastObservedPacketCount)
        {
            _lastObservedPacketCount = packets;
            _lastPacketAdvanceUtc = now;
            _streamStale = false;
            return;
        }

        if (_lastPacketAdvanceUtc == DateTime.MinValue)
        {
            _lastPacketAdvanceUtc = now;
            _streamStale = true;
            return;
        }

        _streamStale = (now - _lastPacketAdvanceUtc).TotalSeconds > StreamStaleTimeoutSeconds;
    }

    private void RaiseAll(DateTime now, bool force = false)
    {
        if (force || (now - _lastGlobalRaiseUtc).TotalMilliseconds >= GlobalStatusRefreshMs)
        {
            _lastGlobalRaiseUtc = now;
            OnPropertyChanged(nameof(StreamStatusText));
            OnPropertyChanged(nameof(DataSourceName));
            OnPropertyChanged(nameof(IsLiveMode));
            OnPropertyChanged(nameof(ModeBannerText));
            OnPropertyChanged(nameof(SelectedStream));
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsRawMode));
            OnPropertyChanged(nameof(PtpStatusCompactText));
            OnPropertyChanged(nameof(TimingConfidenceBadgeText));
            OnPropertyChanged(nameof(ProtocolMonitor));
            OnPropertyChanged(nameof(ProtocolSummaryText));
            OnPropertyChanged(nameof(ProtocolSvStatusText));
            OnPropertyChanged(nameof(ProtocolGooseStatusText));
            OnPropertyChanged(nameof(ProtocolPtpStatusText));
            OnPropertyChanged(nameof(WorkspaceFooterLeftText));
            OnPropertyChanged(nameof(WorkspaceFooterRightText));
            OnPropertyChanged(nameof(SelectionOverlayVisibility));
            RaiseDiagnosticScopeProperties();
        }

        if (IsAnalyzerTabActive)
            RaiseAnalyzerRenderProperties();
        if (IsDiagnosticsTabActive && (force || (now - _lastDiagnosticsRaiseUtc).TotalMilliseconds >= PassiveUiRefreshMs))
        {
            _lastDiagnosticsRaiseUtc = now;
            RaiseDiagnosticsProperties();
        }
        if (IsGooseTabActive && (force || (now - _lastGooseRaiseUtc).TotalMilliseconds >= PassiveUiRefreshMs))
        {
            _lastGooseRaiseUtc = now;
            RaiseGooseProperties();
        }
        if (IsTimingTabActive && (force || (now - _lastDiagnosticsRaiseUtc).TotalMilliseconds >= PassiveUiRefreshMs))
        {
            _lastDiagnosticsRaiseUtc = now;
            RaiseTimingProperties();
        }
        if (IsValidationTabActive && (force || (now - _lastDiagnosticsRaiseUtc).TotalMilliseconds >= PassiveUiRefreshMs))
        {
            _lastDiagnosticsRaiseUtc = now;
            RaiseValidationProperties();
        }
        if (IsDebugTabActive && (force || (now - _lastDebugRaiseUtc).TotalMilliseconds >= PassiveUiRefreshMs))
        {
            _lastDebugRaiseUtc = now;
            RaiseDebugProperties();
        }
    }

    private void RaiseAnalyzerRenderProperties()
    {
        OnPropertyChanged(nameof(SelectedStreamDetails));
        OnPropertyChanged(nameof(SvMappingSourceText));
        OnPropertyChanged(nameof(SvMappingSourceCompactText));
        OnPropertyChanged(nameof(SvSemanticChannelSummaryText));
        OnPropertyChanged(nameof(SvSemanticChannelCompactText));
        OnPropertyChanged(nameof(AnalogValues));
        OnPropertyChanged(nameof(TotalActivePowerText));
        OnPropertyChanged(nameof(TotalReactivePowerText));
        OnPropertyChanged(nameof(TotalApparentPowerText));
        OnPropertyChanged(nameof(PowerFactorText));
        OnPropertyChanged(nameof(Waveform));
        OnPropertyChanged(nameof(WaveformStatusText));
        OnPropertyChanged(nameof(WaveformHeaderModeText));
        OnPropertyChanged(nameof(WaveformHeaderFrequencyText));
        OnPropertyChanged(nameof(WaveformHeaderPacketRateText));
        OnPropertyChanged(nameof(WaveformHeaderSamplesPerCycleText));
        OnPropertyChanged(nameof(WaveformHeaderWindowText));
        OnPropertyChanged(nameof(WaveformHeaderStatusText));
        OnPropertyChanged(nameof(Phasors));
        OnPropertyChanged(nameof(WaveformVoltageScale));
        OnPropertyChanged(nameof(WaveformCurrentScale));
    }

    private void RaiseDiagnosticsProperties()
    {
        OnPropertyChanged(nameof(HealthIcon));
        OnPropertyChanged(nameof(HealthBackgroundBrush));
        OnPropertyChanged(nameof(IsHealthy));
        OnPropertyChanged(nameof(HealthText));
        OnPropertyChanged(nameof(HealthDetails));
        RaiseDiagnosticScopeProperties();
        OnPropertyChanged(nameof(NetworkHealthText));
        OnPropertyChanged(nameof(SignalHealthText));
        OnPropertyChanged(nameof(FrequencyText));
        OnPropertyChanged(nameof(JitterText));
        OnPropertyChanged(nameof(LatencyText));
        OnPropertyChanged(nameof(SvTimingStandardText));
        OnPropertyChanged(nameof(ExpectedDeltaText));
        OnPropertyChanged(nameof(SmcCntStatus));
        OnPropertyChanged(nameof(PacketLossText));
        OnPropertyChanged(nameof(OutOfSequenceText));
        OnPropertyChanged(nameof(BandwidthText));
        OnPropertyChanged(nameof(PacketRateText));
        OnPropertyChanged(nameof(CaptureTimingCautionText));
        OnPropertyChanged(nameof(AdapterTimingRiskText));
        OnPropertyChanged(nameof(EvidenceVerdictText));
        OnPropertyChanged(nameof(EvidenceStandardText));
        OnPropertyChanged(nameof(EvidenceIntegrityText));
        OnPropertyChanged(nameof(EvidenceTimingText));
        OnPropertyChanged(nameof(EvidencePtpText));
        OnPropertyChanged(nameof(EvidenceDecodeText));
        OnPropertyChanged(nameof(EvidenceCapturePathText));
        OnPropertyChanged(nameof(PtpStatusText));
        OnPropertyChanged(nameof(PtpStatusCompactText));
        OnPropertyChanged(nameof(PtpGrandmasterText));
        OnPropertyChanged(nameof(PtpDomainText));
        OnPropertyChanged(nameof(PtpClockQualityText));
        OnPropertyChanged(nameof(PtpRateText));
        OnPropertyChanged(nameof(PtpProfileText));
        OnPropertyChanged(nameof(PtpTransportText));
        OnPropertyChanged(nameof(TimingReferenceText));
        OnPropertyChanged(nameof(TimestampSourceText));
        OnPropertyChanged(nameof(TimingMetricText));
        OnPropertyChanged(nameof(TimingConfidenceText));
        OnPropertyChanged(nameof(EvidenceSnapshotText));
        OnPropertyChanged(nameof(EvidenceCopyStatusText));
        OnPropertyChanged(nameof(VoltageBalanceText));
        OnPropertyChanged(nameof(FrequencyStabilityText));
    }

    private void RaiseGooseProperties()
    {
        OnPropertyChanged(nameof(GooseSnapshot));
        OnPropertyChanged(nameof(GooseMessages));
        OnPropertyChanged(nameof(GooseStatusText));
        OnPropertyChanged(nameof(GooseTotalMessagesText));
        OnPropertyChanged(nameof(GooseDetectedCountText));
        OnPropertyChanged(nameof(GoosePublisherCountText));
        OnPropertyChanged(nameof(GooseIdFilterOptions));
        OnPropertyChanged(nameof(GooseFilterSummaryText));
        OnPropertyChanged(nameof(SelectedGooseDatasetValues));
        OnPropertyChanged(nameof(SelectedGooseSemanticText));
        _gooseHistoryView.Refresh();
    }

    private void RaiseTimingProperties()
    {
        OnPropertyChanged(nameof(ProtocolMonitor));
        OnPropertyChanged(nameof(PtpEvents));
        OnPropertyChanged(nameof(ProtocolSummaryText));
        OnPropertyChanged(nameof(ProtocolSvStatusText));
        OnPropertyChanged(nameof(ProtocolGooseStatusText));
        OnPropertyChanged(nameof(ProtocolPtpStatusText));
        OnPropertyChanged(nameof(PtpStatusText));
        OnPropertyChanged(nameof(PtpStatusCompactText));
        OnPropertyChanged(nameof(PtpGrandmasterText));
        OnPropertyChanged(nameof(PtpDomainText));
        OnPropertyChanged(nameof(PtpClockQualityText));
        OnPropertyChanged(nameof(PtpRateText));
        OnPropertyChanged(nameof(PtpProfileText));
        OnPropertyChanged(nameof(PtpTransportText));
        OnPropertyChanged(nameof(TimingReferenceText));
        OnPropertyChanged(nameof(TimestampSourceText));
        OnPropertyChanged(nameof(TimingMetricText));
        OnPropertyChanged(nameof(TimingConfidenceText));
        OnPropertyChanged(nameof(JitterText));
        OnPropertyChanged(nameof(LatencyText));
        OnPropertyChanged(nameof(PacketLossText));
        OnPropertyChanged(nameof(SequenceStatusText));
    }

    private void RaiseValidationProperties()
    {
        OnPropertyChanged(nameof(ValidationOverallStatusText));
        OnPropertyChanged(nameof(ValidationOverallBrush));
        OnPropertyChanged(nameof(ValidationOverallBackgroundBrush));
        OnPropertyChanged(nameof(ValidationSummaryText));
        OnPropertyChanged(nameof(ValidationSummaryCompactText));
        OnPropertyChanged(nameof(ValidationTimingCompactText));
        OnPropertyChanged(nameof(ValidationSvSummaryText));
        OnPropertyChanged(nameof(ValidationGooseSummaryText));
        OnPropertyChanged(nameof(ValidationPtpSummaryText));
        OnPropertyChanged(nameof(ValidationTimingConfidenceText));
        OnPropertyChanged(nameof(ValidationFindings));
        OnPropertyChanged(nameof(SelectedValidationFindingRow));
        RaiseValidationSelectionProperties();
    }

    private void RaiseValidationSelectionProperties()
    {
        OnPropertyChanged(nameof(ValidationDetailTitle));
        OnPropertyChanged(nameof(ValidationDetailScopeText));
        OnPropertyChanged(nameof(ValidationDetailExpectedText));
        OnPropertyChanged(nameof(ValidationDetailObservedText));
        OnPropertyChanged(nameof(ValidationDetailEvidenceText));
        OnPropertyChanged(nameof(ValidationDetailStatusBrush));
        OnPropertyChanged(nameof(ValidationDetailStatusBackgroundBrush));
    }

    private void RaiseDebugProperties()
    {
        OnPropertyChanged(nameof(Diagnostics));
        OnPropertyChanged(nameof(HasLivePackets));
        OnPropertyChanged(nameof(StreamAliveText));
        OnPropertyChanged(nameof(StreamStateText));
        OnPropertyChanged(nameof(StreamStateBrush));
        OnPropertyChanged(nameof(StreamStateSoftBrush));
        OnPropertyChanged(nameof(DebugSvIdText));
        OnPropertyChanged(nameof(DebugMappingProfileText));
        OnPropertyChanged(nameof(DebugMappedChannelsText));
        OnPropertyChanged(nameof(DebugSampleRateText));
        OnPropertyChanged(nameof(DebugTimebaseText));
        OnPropertyChanged(nameof(DebugEstimatorText));
        OnPropertyChanged(nameof(DebugRawValuesText));
        OnPropertyChanged(nameof(DebugPacketEvidenceText));
        OnPropertyChanged(nameof(DebugRmsText));
        OnPropertyChanged(nameof(UiRefreshDurationText));
        OnPropertyChanged(nameof(UiQueueDepthText));
        OnPropertyChanged(nameof(ManagedMemoryText));
        OnPropertyChanged(nameof(SkippedRefreshText));
        RaiseAdvancedProperties();
    }

    private void RaiseAdvancedProperties()
    {
        OnPropertyChanged(nameof(AdvancedTargetTitle));
        OnPropertyChanged(nameof(AdvancedTargetSubtitle));
        OnPropertyChanged(nameof(AdvancedPrimaryDetailsText));
        OnPropertyChanged(nameof(AdvancedEvidenceTitle));
        OnPropertyChanged(nameof(AdvancedPacketEvidenceText));
        OnPropertyChanged(nameof(AdvancedDecodedTitle));
        OnPropertyChanged(nameof(AdvancedRawValuesText));
        OnPropertyChanged(nameof(AdvancedEngineeringTitle));
        OnPropertyChanged(nameof(AdvancedEngineeringText));
    }

    private void UpdateDebugDisplaySnapshot()
    {
        var details = SelectedStreamDetails;
        _debugSvIdText = CompactDebugText(details?.SvId, 120);
        _debugMappingProfileText = CompactDebugText(details?.SampleValueMappingText, 180);
        _debugMappedChannelsText = CompactDebugText(details?.MappedChannelNamesText, DebugTextLimit);
        _debugSampleRateText = CompactDebugText(details?.SmpRateText, 120);
        _debugTimebaseText = CompactDebugText(details?.TimebaseStatusText, DebugTextLimit);
        _debugEstimatorText = CompactDebugText(Diagnostics.FrequencyRejectReason, DebugTextLimit);
        _debugRawValuesText = CompactDebugText(details?.RawValuesText, DebugTextLimit);
        _debugPacketEvidenceText = CompactDebugText(details?.PacketEvidenceText, DebugTextLimit);
        _debugRmsText = CompactDebugText(details?.RmsDebugText, DebugTextLimit);
    }

    private static string CompactDebugText(string? value, int limit)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "N/A";

        var trimmed = value.Trim();
        if (trimmed.Length <= limit)
            return trimmed;

        return $"{trimmed[..Math.Max(0, limit - 16)]} ... ({trimmed.Length} chars)";
    }


    private void LoadSclProject()
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Import IEC 61850 SCL / CID / ICD / SCD / IID",
                Filter = "IEC 61850 SCL files (*.scd;*.cid;*.icd;*.iid)|*.scd;*.cid;*.icd;*.iid|XML files (*.xml)|*.xml|All files (*.*)|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0)
                return;

            var parser = new SclSemanticParser();
            var loaded = 0;
            var errors = new List<string>();

            foreach (var fileName in dialog.FileNames)
            {
                try
                {
                    if (_sclProjects.Any(x => string.Equals(x.FilePath, fileName, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    _sclProjects.Add(parser.Load(fileName));
                    loaded++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{Path.GetFileName(fileName)}: {ex.Message}");
                }
            }

            RebuildSclProjectAggregate();
            RebuildSclWorkspaceRows();
            PushSclSvMappingsToRawEngine();
            _sclLoadStatusText = loaded > 0
                ? $"Imported {loaded} SCL document(s). {SclWorkspaceSummaryText}"
                : errors.Count > 0
                    ? $"SCL import failed: {string.Join(" | ", errors.Take(2))}"
                    : "SCL document already imported.";

            if (errors.Count > 0 && loaded > 0)
                _sclLoadStatusText += $"  -  {errors.Count} file(s) skipped";

            RaiseSclProperties();
            RaiseDiagnosticScopeProperties();
            RaiseAdvancedProperties();
        }
        catch (Exception ex)
        {
            _sclLoadStatusText = $"SCL load failed: {ex.Message}";
            OnPropertyChanged(nameof(SclLoadStatusText));
        }
    }

    private void ClearSclProject()
    {
        _sclProjects.Clear();
        _sclProject = SclProjectModel.Empty;
        _sclDocuments.Clear();
        _sclIedCards.Clear();
        _sclStreamCatalog.Clear();
        _sclBindingMatrix.Clear();
        _selectedSclIedCard = null;
        _selectedSclStreamCatalog = null;
        _selectedSclBindingMatrixRow = null;
        _lastSclBindingSignature = string.Empty;
        _sclLoadStatusText = "No SCL loaded";
        _rawDataSource.SetSvChannelMappings(Array.Empty<SvChannelMappingProfile>());
        RaiseSclProperties();
    }

    private void RebuildSclProjectAggregate()
    {
        if (_sclProjects.Count == 0)
        {
            _sclProject = SclProjectModel.Empty;
            return;
        }

        if (_sclProjects.Count == 1)
        {
            _sclProject = _sclProjects[0];
            return;
        }

        var warnings = _sclProjects.SelectMany(x => x.Warnings.Select(w => $"{x.FileName}: {w}")).ToList();
        var editionText = _sclProjects.Select(x => x.EditionText).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1
            ? _sclProjects[0].EditionText
            : "Mixed SCL editions / vendor exports";

        _sclProject = new SclProjectModel
        {
            FilePath = "multi-scl-project",
            FileName = $"{_sclProjects.Count} imported SCL documents",
            NamespaceUri = string.Join("; ", _sclProjects.Select(x => x.NamespaceUri).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Take(3)),
            HeaderId = "multi-document",
            HeaderVersion = string.Join(", ", _sclProjects.Select(x => x.HeaderVersion).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Take(3)),
            HeaderRevision = string.Join(", ", _sclProjects.Select(x => x.HeaderRevision).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Take(3)),
            Edition = _sclProjects.Select(x => x.Edition).Distinct().Count() == 1 ? _sclProjects[0].Edition : SclEditionKind.Unknown,
            EditionText = editionText,
            Ieds = _sclProjects.SelectMany(x => x.Ieds).ToList(),
            DataSets = _sclProjects.SelectMany(x => x.DataSets).ToList(),
            GooseStreams = _sclProjects.SelectMany(x => x.GooseStreams).ToList(),
            SvStreams = _sclProjects.SelectMany(x => x.SvStreams).ToList(),
            TypeSummaries = _sclProjects.SelectMany(x => x.TypeSummaries).ToList(),
            Warnings = warnings
        };
    }

    private void RebuildSclWorkspaceRows()
    {
        _sclDocuments.Clear();
        _sclIedCards.Clear();
        _sclStreamCatalog.Clear();

        foreach (var project in _sclProjects)
        {
            _sclDocuments.Add(new SclDocumentCardRow
            {
                FileName = project.FileName,
                FilePath = project.FilePath,
                EditionText = project.EditionText,
                StatusText = project.Warnings.Count == 0 ? "Parsed" : $"Parsed  -  {project.Warnings.Count} warning(s)",
                SummaryText = $"IED {project.Ieds.Count}  -  SV {project.SvStreams.Count}  -  GOOSE {project.GooseStreams.Count}  -  DataSet {project.DataSets.Count}",
                WarningText = project.Warnings.Count == 0 ? "No parser warnings" : string.Join(Environment.NewLine, project.Warnings.Take(3))
            });

            foreach (var ied in project.Ieds.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                var gooseCount = project.GooseStreams.Count(x => string.Equals(x.IedName, ied.Name, StringComparison.OrdinalIgnoreCase));
                var svCount = project.SvStreams.Count(x => string.Equals(x.IedName, ied.Name, StringComparison.OrdinalIgnoreCase));
                var dsCount = project.DataSets.Count(x => string.Equals(x.IedName, ied.Name, StringComparison.OrdinalIgnoreCase));
                _sclIedCards.Add(new SclIedCardRow
                {
                    Name = string.IsNullOrWhiteSpace(ied.Name) ? "Unnamed IED" : ied.Name,
                    SourceFileName = project.FileName,
                    Manufacturer = ied.Manufacturer,
                    Type = ied.Type,
                    ConfigVersion = ied.ConfigVersion,
                    GooseCount = gooseCount,
                    SvCount = svCount,
                    DataSetCount = dsCount,
                    SummaryText = $"SV {svCount}  -  GOOSE {gooseCount}  -  DataSet {dsCount}",
                    StatusText = (gooseCount + svCount) == 0 ? "No streams" : "Engineering model"
                });
            }

            foreach (var sv in project.SvStreams.OrderBy(x => x.IedName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.ControlName, StringComparer.OrdinalIgnoreCase))
            {
                _sclStreamCatalog.Add(SclStreamCatalogRow.FromSv(project.FileName, sv, ComputeSvLiveStatus(sv)));
            }

            foreach (var goose in project.GooseStreams.OrderBy(x => x.IedName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.ControlName, StringComparer.OrdinalIgnoreCase))
            {
                _sclStreamCatalog.Add(SclStreamCatalogRow.FromGoose(project.FileName, goose, ComputeGooseLiveStatus(goose)));
            }
        }

        _selectedSclIedCard = _sclIedCards.FirstOrDefault();
        _selectedSclStreamCatalog = _sclStreamCatalog.FirstOrDefault(x => _selectedSclIedCard is null || string.Equals(x.IedName, _selectedSclIedCard.Name, StringComparison.OrdinalIgnoreCase))
            ?? _sclStreamCatalog.FirstOrDefault();
        RebuildSclBindingMatrix(force: true);
        PushSclSvMappingsToRawEngine();
    }

    private void PushSclSvMappingsToRawEngine()
    {
        if (!HasSclProject)
        {
            _rawDataSource.SetSvChannelMappings(Array.Empty<SvChannelMappingProfile>());
            return;
        }

        var profiles = _sclProject.SvStreams
            .Select(BuildSclSvChannelMappingProfile)
            .Where(profile => profile.HasRenderableChannels)
            .ToArray();

        _rawDataSource.SetSvChannelMappings(profiles);
    }

    private static SvChannelMappingProfile BuildSclSvChannelMappingProfile(SclSvStreamModel stream)
    {
        var elements = BuildSclSvChannelElements(stream);
        return new SvChannelMappingProfile
        {
            ProfileKey = $"{stream.IedName}|{stream.ControlBlockReference}|{stream.SvId}|{stream.AppId}|{stream.ConfRev}",
            SourceText = "SCL DataSet entry order",
            ControlBlockReference = stream.ControlBlockReference,
            DataSetReference = stream.DataSetReference,
            SvId = stream.SvId,
            AppId = stream.AppId,
            DestinationMac = stream.DestinationMac,
            VlanId = stream.VlanId,
            ConfRevText = stream.ConfRev > 0 ? stream.ConfRev.ToString() : "N/A",
            Elements = elements
        };
    }

    private static IReadOnlyList<SvChannelElementMapping> BuildSclSvChannelElements(SclSvStreamModel stream)
    {
        var direct = stream.Entries
            .Select(entry => (Entry: entry, Channel: ResolveExplicitSvChannel(entry)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Channel))
            .Select(x => new SvChannelElementMapping
            {
                ChannelName = x.Channel!,
                ElementIndex = Math.Max(0, x.Entry.Index - 1),
                SignalReference = x.Entry.DisplayName,
                TypeText = x.Entry.TypeText
            })
            .GroupBy(x => x.ChannelName, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.OrderBy(e => e.ElementIndex).First())
            .ToArray();

        var result = direct.ToList();
        AddSequentialClassMappings(result, stream.Entries, "TCTR", ["Ia", "Ib", "Ic", "In"]);
        AddSequentialClassMappings(result, stream.Entries, "TVTR", ["Ua", "Ub", "Uc", "Un"]);
        return result
            .GroupBy(x => x.ChannelName, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.OrderBy(e => e.ElementIndex).First())
            .ToArray();
    }

    private static void AddSequentialClassMappings(List<SvChannelElementMapping> result, IReadOnlyList<SclDataSetEntryModel> entries, string lnClass, IReadOnlyList<string> channels)
    {
        var candidates = entries
            .Where(entry => !entry.IsQuality &&
                !entry.IsTimestamp &&
                string.Equals(entry.LnClass, lnClass, StringComparison.OrdinalIgnoreCase) &&
                LooksLikeSampleMagnitude(entry))
            .OrderBy(entry => ParseLnInst(entry.LnInst))
            .ThenBy(entry => entry.Index)
            .ToArray();

        for (var i = 0; i < candidates.Length && i < channels.Count; i++)
        {
            if (result.Any(existing => string.Equals(existing.ChannelName, channels[i], StringComparison.OrdinalIgnoreCase)))
                continue;

            result.Add(new SvChannelElementMapping
            {
                ChannelName = channels[i],
                ElementIndex = Math.Max(0, candidates[i].Index - 1),
                SignalReference = candidates[i].DisplayName,
                TypeText = candidates[i].TypeText
            });
        }
    }

    private static string? ResolveExplicitSvChannel(SclDataSetEntryModel entry)
    {
        if (entry.IsQuality || entry.IsTimestamp || !LooksLikeSampleMagnitude(entry))
            return null;

        var text = $"{entry.SignalReference}.{entry.DoName}.{entry.DaName}".ToLowerInvariant();
        var tokenText = BuildTokenText(text);
        var compactText = new string(text.Where(char.IsLetterOrDigit).ToArray());
        var isVoltageNode = string.Equals(entry.LnClass, "TVTR", StringComparison.OrdinalIgnoreCase) || ContainsAny(text, "vol", "voltage");
        var isCurrentNode = string.Equals(entry.LnClass, "TCTR", StringComparison.OrdinalIgnoreCase) || ContainsAny(text, "amp", "current");

        if (isVoltageNode && (HasToken(tokenText, "phsa", "ua") || compactText.Contains("instua", StringComparison.Ordinal))) return "Ua";
        if (isVoltageNode && (HasToken(tokenText, "phsb", "ub") || compactText.Contains("instub", StringComparison.Ordinal))) return "Ub";
        if (isVoltageNode && (HasToken(tokenText, "phsc", "uc") || compactText.Contains("instuc", StringComparison.Ordinal))) return "Uc";
        if (isVoltageNode && (HasToken(tokenText, "phsn", "un", "neutral", "neut") || compactText.Contains("instun", StringComparison.Ordinal))) return "Un";
        if (isCurrentNode && (HasToken(tokenText, "phsa", "ia") || compactText.Contains("instia", StringComparison.Ordinal))) return "Ia";
        if (isCurrentNode && (HasToken(tokenText, "phsb", "ib") || compactText.Contains("instib", StringComparison.Ordinal))) return "Ib";
        if (isCurrentNode && (HasToken(tokenText, "phsc", "ic") || compactText.Contains("instic", StringComparison.Ordinal))) return "Ic";
        if (isCurrentNode && (HasToken(tokenText, "phsn", "in", "neutral", "neut") || compactText.Contains("instin", StringComparison.Ordinal))) return "In";
        return null;
    }

    private static string BuildTokenText(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append(' ');
        foreach (var c in value)
            builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
        builder.Append(' ');
        return builder.ToString();
    }

    private static bool HasToken(string tokenText, params string[] tokens)
        => tokens.Any(token => tokenText.Contains($" {token} ", StringComparison.Ordinal));

    private static bool LooksLikeSampleMagnitude(SclDataSetEntryModel entry)
    {
        var text = $"{entry.SignalReference}.{entry.DoName}.{entry.DaName}.{entry.BType}".ToLowerInvariant();
        return text.Contains("instmag") ||
               text.Contains("samples") ||
               text.Contains("ampsv") ||
               text.Contains("volsv") ||
               string.Equals(entry.BType, "INT32", StringComparison.OrdinalIgnoreCase);
    }

    private static int ParseLnInst(string value)
        => int.TryParse(value, out var parsed) ? parsed : int.MaxValue;

    private void RebuildSclBindingMatrix(bool force = false)
    {
        var previousKey = _selectedSclBindingMatrixRow?.BindingKey;
        var rows = BuildSclBindingRows();
        var signature = string.Join("|", rows.Select(x => x.SignatureText));
        if (!force && string.Equals(signature, _lastSclBindingSignature, StringComparison.Ordinal))
            return;

        _lastSclBindingSignature = signature;
        SyncSclBindingRows(rows);

        _selectedSclBindingMatrixRow = !string.IsNullOrWhiteSpace(previousKey)
            ? _sclBindingMatrix.FirstOrDefault(x => string.Equals(x.BindingKey, previousKey, StringComparison.OrdinalIgnoreCase))
            : null;

        _selectedSclBindingMatrixRow ??= _sclBindingMatrix.FirstOrDefault(x => !x.IsMatched);
        _selectedSclBindingMatrixRow ??= _sclBindingMatrix.FirstOrDefault(x => x.ExpectedStream is not null && (_selectedSclIedCard is null || string.Equals(x.IedName, _selectedSclIedCard.Name, StringComparison.OrdinalIgnoreCase)));
        _selectedSclBindingMatrixRow ??= _sclBindingMatrix.FirstOrDefault();

        if (_selectedSclBindingMatrixRow?.ExpectedStream is not null)
            _selectedSclStreamCatalog = _selectedSclBindingMatrixRow.ExpectedStream;

        OnPropertyChanged(nameof(SclBindingMatrix));
        OnPropertyChanged(nameof(SclFilteredBindingMatrix));
        OnPropertyChanged(nameof(SelectedSclBindingMatrixRow));
        OnPropertyChanged(nameof(SclBindingSummaryText));
        RaiseValidationProperties();
        RaiseSclSelectionProperties();
    }

    private void SyncSclBindingRows(IReadOnlyList<SclBindingMatrixRow> rows)
    {
        var desiredKeys = rows.Select(x => x.BindingKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var index = _sclBindingMatrix.Count - 1; index >= 0; index--)
        {
            if (!desiredKeys.Contains(_sclBindingMatrix[index].BindingKey))
                _sclBindingMatrix.RemoveAt(index);
        }

        foreach (var row in rows)
        {
            var existing = _sclBindingMatrix.FirstOrDefault(x => string.Equals(x.BindingKey, row.BindingKey, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                _sclBindingMatrix.Add(row);
                continue;
            }

            existing.UpdateFrom(row);
        }
    }

    private IReadOnlyList<SclBindingMatrixRow> BuildSclBindingRows()
    {
        var rows = new List<SclBindingMatrixRow>();
        if (!HasSclProject)
            return rows;

        var expectedRows = _sclStreamCatalog.ToList();
        var expectedConflictReasons = BuildExpectedConflictReasons(expectedRows);
        var ambiguityReasons = BuildBindingAmbiguityReasons(expectedRows);
        var matchedLiveSvKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matchedLiveGooseKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var expected in expectedRows)
        {
            if (string.Equals(expected.Protocol, "SV", StringComparison.OrdinalIgnoreCase))
            {
                var candidates = Streams
                    .Select(live => BuildSvBindingCandidate(expected, live))
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.MatchCount)
                    .ToList();
                var best = candidates.FirstOrDefault();

                if (best is not null && best.Score >= 35)
                {
                    var candidate = ApplyAmbiguityEvidence(best, candidates, ambiguityReasons.GetValueOrDefault(expected.ExpectedKey));
                    var liveAlreadyMatched = matchedLiveSvKeys.Contains(candidate.LiveKey);
                    var status = ClassifyBindingCandidate(candidate, expectedConflictReasons.ContainsKey(expected.ExpectedKey), liveAlreadyMatched);
                    matchedLiveSvKeys.Add(candidate.LiveKey);
                    var evidence = AppendEvidence(candidate.EvidenceText, expectedConflictReasons.GetValueOrDefault(expected.ExpectedKey));
                    if (liveAlreadyMatched)
                        evidence = AppendEvidence(evidence, $"SCL conflict: live SV {candidate.ObservedName} is already bound to another expected stream.");
                    rows.Add(SclBindingMatrixRow.FromExpected(expected, candidate.ObservedName, candidate.ObservedAppId, candidate.ObservedVlan, status, candidate.Score, evidence, candidate.LiveKey, candidate.ExpectedDetailText, candidate.ObservedDetailText));
                }
                else
                {
                    var hasConflict = expectedConflictReasons.ContainsKey(expected.ExpectedKey);
                    var status = hasConflict ? "CONFLICT" : "MISSING";
                    var evidence = status == "CONFLICT"
                        ? expectedConflictReasons[expected.ExpectedKey]
                        : "Expected SV stream from SCL has no live candidate on selected adapter.";
                    rows.Add(SclBindingMatrixRow.FromExpected(expected, "Not observed", expected.AppId, expected.VlanId, status, 0, evidence, string.Empty));
                }
            }
            else if (string.Equals(expected.Protocol, "GOOSE", StringComparison.OrdinalIgnoreCase))
            {
                var candidates = _gooseMessages
                    .Select(live => BuildGooseBindingCandidate(expected, live))
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.MatchCount)
                    .ToList();
                var best = candidates.FirstOrDefault();

                if (best is not null && best.Score >= 35)
                {
                    var candidate = ApplyAmbiguityEvidence(best, candidates, ambiguityReasons.GetValueOrDefault(expected.ExpectedKey));
                    var liveAlreadyMatched = matchedLiveGooseKeys.Contains(candidate.LiveKey);
                    var status = ClassifyBindingCandidate(candidate, expectedConflictReasons.ContainsKey(expected.ExpectedKey), liveAlreadyMatched);
                    matchedLiveGooseKeys.Add(candidate.LiveKey);
                    var evidence = AppendEvidence(candidate.EvidenceText, expectedConflictReasons.GetValueOrDefault(expected.ExpectedKey));
                    if (liveAlreadyMatched)
                        evidence = AppendEvidence(evidence, $"SCL conflict: live GOOSE {candidate.ObservedName} is already bound to another expected publisher.");
                    rows.Add(SclBindingMatrixRow.FromExpected(expected, candidate.ObservedName, candidate.ObservedAppId, candidate.ObservedVlan, status, candidate.Score, evidence, candidate.LiveKey, candidate.ExpectedDetailText, candidate.ObservedDetailText));
                }
                else
                {
                    var hasConflict = expectedConflictReasons.ContainsKey(expected.ExpectedKey);
                    var status = hasConflict ? "CONFLICT" : "MISSING";
                    var evidence = status == "CONFLICT"
                        ? expectedConflictReasons[expected.ExpectedKey]
                        : "Expected GOOSE publisher from SCL has no live candidate on selected adapter.";
                    rows.Add(SclBindingMatrixRow.FromExpected(expected, "Not observed", expected.AppId, expected.VlanId, status, 0, evidence, string.Empty));
                }
            }
        }

        foreach (var live in Streams.OrderBy(x => x.FirstSeenOrder))
        {
            if (matchedLiveSvKeys.Contains(live.StreamId))
                continue;

            var bestScore = expectedRows
                .Where(x => string.Equals(x.Protocol, "SV", StringComparison.OrdinalIgnoreCase))
                .Select(expected => ScoreSvBinding(expected, live))
                .DefaultIfEmpty(0)
                .Max();

            if (bestScore < 35)
            {
                var observedDetail = string.Join(Environment.NewLine, new[]
                {
                    $"Observed SV: {live.SvId}",
                    $"DataSet: {live.DataSet}",
                    $"APPID: {live.AppId}",
                    $"Destination MAC: {live.DestinationMac}",
                    $"VLAN: {live.VlanText}",
                    $"confRev: {live.ConfRevText}"
                });
                rows.Add(SclBindingMatrixRow.FromUnexpected("SV", live.SvId, live.AppId, live.VlanText, "Live SV stream is not described by imported SCL context.", live.StreamId, observedDetail));
            }
        }

        foreach (var live in _gooseMessages)
        {
            if (matchedLiveGooseKeys.Contains(live.MessageId))
                continue;

            var bestScore = expectedRows
                .Where(x => string.Equals(x.Protocol, "GOOSE", StringComparison.OrdinalIgnoreCase))
                .Select(expected => ScoreGooseBinding(expected, live))
                .DefaultIfEmpty(0)
                .Max();

            if (bestScore < 35)
            {
                var observedName = string.IsNullOrWhiteSpace(live.GoId) || live.GoId == "N/A" ? live.GoCbRef : live.GoId;
                var observedDetail = string.Join(Environment.NewLine, new[]
                {
                    $"Observed GOOSE: {observedName}",
                    $"GoCBRef: {live.GoCbRef}",
                    $"DataSet: {live.DataSet}",
                    $"APPID: {live.AppId}",
                    $"Destination MAC: {live.DestinationMac}",
                    $"VLAN: {live.VlanId} / Priority {live.VlanPriority}",
                    $"confRev: {live.ConfRev}"
                });
                rows.Add(SclBindingMatrixRow.FromUnexpected("GOOSE", observedName, live.AppId, live.VlanId, "Live GOOSE publisher is not described by imported SCL context.", live.MessageId, observedDetail));
            }
        }

        return rows
            .OrderBy(x => x.SortRank)
            .ThenBy(x => x.Protocol, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.IedName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ExpectedName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private Dictionary<string, string> BuildBindingAmbiguityReasons(IReadOnlyList<SclStreamCatalogRow> expectedRows)
    {
        const int strongScoreThreshold = 65;
        var reasons = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        void AddReason(string expectedKey, string reason)
        {
            if (!reasons.TryGetValue(expectedKey, out var list))
            {
                list = new List<string>();
                reasons[expectedKey] = list;
            }

            if (!list.Contains(reason, StringComparer.OrdinalIgnoreCase))
                list.Add(reason);
        }

        var strongCandidates = new List<(SclStreamCatalogRow Expected, BindingCandidate Candidate)>();
        foreach (var expected in expectedRows)
        {
            IEnumerable<BindingCandidate> candidates = expected.Protocol switch
            {
                "SV" => Streams.Select(live => BuildSvBindingCandidate(expected, live)),
                "GOOSE" => _gooseMessages.Select(live => BuildGooseBindingCandidate(expected, live)),
                _ => Array.Empty<BindingCandidate>()
            };

            strongCandidates.AddRange(candidates
                .Where(candidate => candidate.Score >= strongScoreThreshold)
                .Select(candidate => (expected, candidate)));
        }

        foreach (var group in strongCandidates.GroupBy(x => x.Expected.ExpectedKey, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            var names = group
                .OrderByDescending(x => x.Candidate.Score)
                .Take(3)
                .Select(x => $"{x.Candidate.ObservedName} ({x.Candidate.Score}%)");
            AddReason(group.Key, $"Ambiguous binding: expected stream has multiple strong live candidates: {string.Join(", ", names)}.");
        }

        foreach (var group in strongCandidates.GroupBy(x => x.Candidate.LiveKey, StringComparer.OrdinalIgnoreCase).Where(g => g.Select(x => x.Expected.ExpectedKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1))
        {
            var names = group
                .OrderByDescending(x => x.Candidate.Score)
                .Take(3)
                .Select(x => $"{x.Expected.DisplayName} ({x.Candidate.Score}%)");
            var reason = $"Ambiguous binding: live stream {group.First().Candidate.ObservedName} strongly matches multiple SCL expectations: {string.Join(", ", names)}.";
            foreach (var item in group)
                AddReason(item.Expected.ExpectedKey, reason);
        }

        return reasons.ToDictionary(
            pair => pair.Key,
            pair => string.Join("; ", pair.Value),
            StringComparer.OrdinalIgnoreCase);
    }

    private static BindingCandidate ApplyAmbiguityEvidence(BindingCandidate best, IReadOnlyList<BindingCandidate> candidates, string? existingAmbiguityReason)
    {
        var tieCandidates = candidates
            .Where(candidate => candidate.Score >= 35 && candidate.LiveKey != best.LiveKey && Math.Abs(candidate.Score - best.Score) <= 5)
            .OrderByDescending(candidate => candidate.Score)
            .Take(3)
            .Select(candidate => $"{candidate.ObservedName} ({candidate.Score}%)")
            .ToArray();

        var reason = existingAmbiguityReason;
        if (tieCandidates.Length > 0)
            reason = AppendEvidence(reason, $"Ambiguous binding: near-tie live candidate(s): {string.Join(", ", tieCandidates)}.");

        return string.IsNullOrWhiteSpace(reason)
            ? best
            : best with
            {
                Ambiguous = true,
                EvidenceText = AppendEvidence(best.EvidenceText, reason)
            };
    }

    private static string AppendEvidence(string? primary, string? extra)
    {
        if (string.IsNullOrWhiteSpace(primary))
            return extra ?? string.Empty;
        if (string.IsNullOrWhiteSpace(extra))
            return primary;

        return primary.TrimEnd('.', ';', ' ') + "; " + extra.Trim();
    }

    private static Dictionary<string, string> BuildExpectedConflictReasons(IReadOnlyList<SclStreamCatalogRow> expectedRows)
    {
        var conflictReasons = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        void AddReason(IEnumerable<SclStreamCatalogRow> rows, string reason)
        {
            foreach (var row in rows)
            {
                if (!conflictReasons.TryGetValue(row.ExpectedKey, out var reasons))
                {
                    reasons = new List<string>();
                    conflictReasons[row.ExpectedKey] = reasons;
                }

                if (!reasons.Contains(reason, StringComparer.OrdinalIgnoreCase))
                    reasons.Add(reason);
            }
        }

        foreach (var group in expectedRows
            .Where(x => !string.IsNullOrWhiteSpace(NormalizeAppId(x.AppId)))
            .GroupBy(x => $"{x.Protocol}|APPID|{NormalizeAppId(x.AppId)}", StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1))
        {
            AddReason(group, $"SCL conflict: duplicate {group.First().Protocol} APPID {ValueOrNa(group.First().AppId)} across {group.Count()} expected streams.");
        }

        foreach (var group in expectedRows
            .Where(x => !string.IsNullOrWhiteSpace(NormalizeAppId(x.AppId)) &&
                !string.IsNullOrWhiteSpace(NormalizeComparable(x.DestinationMac, appId: false, mac: true, vlan: false)) &&
                !string.IsNullOrWhiteSpace(NormalizeVlanValue(x.VlanId)))
            .GroupBy(x => $"{x.Protocol}|TRANSPORT|{NormalizeAppId(x.AppId)}|{NormalizeComparable(x.DestinationMac, appId: false, mac: true, vlan: false)}|{NormalizeVlanValue(x.VlanId)}", StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1))
        {
            AddReason(group, $"SCL conflict: duplicate transport tuple APPID {ValueOrNa(group.First().AppId)}, destination {ValueOrNa(group.First().DestinationMac)}, VLAN {ValueOrNa(group.First().VlanId)}.");
        }

        foreach (var group in expectedRows
            .GroupBy(x => $"{x.Protocol}|CTRL|{NormalizeMatchText(x.IedName)}|{NormalizeMatchText(x.ControlBlockReference)}", StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1))
        {
            var dataSetCount = group.Select(x => NormalizeMatchText(x.DataSetReference)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var confRevCount = group.Select(x => x.ExpectedConfRevText).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var entrySignatureCount = group.Select(BuildEntryOrderSignature).Distinct(StringComparer.OrdinalIgnoreCase).Count();

            if (dataSetCount > 1)
                AddReason(group, $"SCL conflict: control block {ValueOrNa(group.First().ControlBlockReference)} has contradictory DataSet references.");
            if (confRevCount > 1)
                AddReason(group, $"SCL conflict: control block {ValueOrNa(group.First().ControlBlockReference)} has conflicting confRev values.");
            if (entrySignatureCount > 1)
                AddReason(group, $"SCL conflict: control block {ValueOrNa(group.First().ControlBlockReference)} has different DataSet entry order.");
        }

        return conflictReasons.ToDictionary(
            pair => pair.Key,
            pair => string.Join("; ", pair.Value),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildEntryOrderSignature(SclStreamCatalogRow row)
        => row.Entries.Count == 0
            ? "NO_ENTRIES"
            : string.Join("|", row.Entries.Select(entry => $"{entry.Index}:{NormalizeMatchText(entry.SignalReference)}:{NormalizeMatchText(entry.Fc)}:{NormalizeMatchText(entry.Cdc)}:{NormalizeMatchText(entry.BType)}"));

    private static string ClassifyBindingCandidate(BindingCandidate candidate, bool expectedConflict, bool liveAlreadyMatched)
    {
        if (expectedConflict || liveAlreadyMatched)
            return "CONFLICT";

        if (candidate.Ambiguous)
            return "AMBIGUOUS";

        if (candidate.MismatchCount > 0)
            return "MISMATCH";

        return candidate.Score >= 65 ? "MATCHED" : "WEAK";
    }

    private static BindingCandidate BuildSvBindingCandidate(SclStreamCatalogRow expected, SvStreamItem live)
    {
        var comparisons = new List<BindingFieldComparison>
        {
            CompareField("svID", expected.DisplayName, live.SvId, required: true, aliases: new[] { live.StreamName }),
            CompareField("DataSet", expected.DataSetReference, live.DataSet, required: false),
            CompareField("APPID", expected.AppId, live.AppId, required: true, useAppId: true),
            CompareField("Dst MAC", expected.DestinationMac, live.DestinationMac, required: true, useMac: true),
            CompareField("VLAN", expected.VlanId, live.VlanText, required: true, useVlan: true),
            CompareField("confRev", expected.ExpectedConfRevText, live.ConfRevText, required: true)
        };

        return BuildBindingCandidate(
            live,
            live.StreamId,
            string.IsNullOrWhiteSpace(live.SvId) ? live.StreamName : live.SvId,
            live.AppId,
            live.VlanText,
            expected,
            comparisons,
            BuildExpectedDetail(expected),
            string.Join(Environment.NewLine, new[]
            {
                $"Observed SV: {live.SvId}",
                $"DataSet: {live.DataSet}",
                $"APPID: {live.AppId}",
                $"Destination MAC: {live.DestinationMac}",
                $"VLAN: {live.VlanText}",
                $"confRev: {live.ConfRevText}"
            }));
    }

    private static BindingCandidate BuildGooseBindingCandidate(SclStreamCatalogRow expected, GooseMessageItem live)
    {
        var observedName = string.IsNullOrWhiteSpace(live.GoId) || live.GoId == "N/A" ? live.GoCbRef : live.GoId;
        var comparisons = new List<BindingFieldComparison>
        {
            CompareField("goID", expected.DisplayName, live.GoId, required: false),
            CompareField("GoCBRef", expected.ControlBlockReference, live.GoCbRef, required: true),
            CompareField("DataSet", expected.DataSetReference, live.DataSet, required: true),
            CompareField("APPID", expected.AppId, live.AppId, required: true, useAppId: true),
            CompareField("Dst MAC", expected.DestinationMac, live.DestinationMac, required: true, useMac: true),
            CompareField("VLAN", expected.VlanId, live.VlanId, required: true, useVlan: true),
            CompareField("Priority", expected.VlanPriority, live.VlanPriority, required: true, useVlan: true),
            CompareField("confRev", expected.ExpectedConfRevText, live.ConfRev.ToString(), required: true)
        };

        return BuildBindingCandidate(
            live,
            live.MessageId,
            observedName,
            live.AppId,
            live.VlanId,
            expected,
            comparisons,
            BuildExpectedDetail(expected),
            string.Join(Environment.NewLine, new[]
            {
                $"Observed GOOSE: {observedName}",
                $"GoCBRef: {live.GoCbRef}",
                $"DataSet: {live.DataSet}",
                $"APPID: {live.AppId}",
                $"Destination MAC: {live.DestinationMac}",
                $"VLAN: {live.VlanId} / Priority {live.VlanPriority}",
                $"confRev: {live.ConfRev}"
            }));
    }

    private static BindingCandidate BuildBindingCandidate<TLive>(
        TLive live,
        string liveKey,
        string observedName,
        string observedAppId,
        string observedVlan,
        SclStreamCatalogRow expected,
        IReadOnlyList<BindingFieldComparison> comparisons,
        string expectedDetail,
        string observedDetail)
        where TLive : notnull
    {
        var score = ScoreComparisons(comparisons);
        var matches = comparisons.Where(x => x.IsMatch).Select(x => x.Name).ToArray();
        var mismatches = comparisons.Where(x => x.IsMismatch).Select(x => $"{x.Name} expected {x.ExpectedValue}, observed {x.ObservedValue}").ToArray();
        var missingObserved = comparisons.Where(x => x.Required && x.HasExpected && !x.HasObserved).Select(x => x.Name).ToArray();
        var missingExpected = comparisons.Where(x => x.Required && !x.HasExpected && x.HasObserved).Select(x => x.Name).ToArray();

        var evidenceParts = new List<string>();
        evidenceParts.Add($"Score {score}%");
        if (matches.Length > 0)
            evidenceParts.Add($"Matched {string.Join(" + ", matches)}");
        if (mismatches.Length > 0)
            evidenceParts.Add($"Mismatch: {string.Join("; ", mismatches)}");
        if (missingObserved.Length > 0)
            evidenceParts.Add($"Missing observed field(s): {string.Join(", ", missingObserved)}");
        if (missingExpected.Length > 0)
            evidenceParts.Add($"Missing SCL expected field(s): {string.Join(", ", missingExpected)}");
        if (evidenceParts.Count == 1)
            evidenceParts.Add("Candidate selected by weak similarity.");

        return new BindingCandidate(
            live,
            liveKey,
            observedName,
            observedAppId,
            observedVlan,
            score,
            matches.Length,
            mismatches.Length,
            false,
            $"{string.Join(". ", evidenceParts)}.",
            expectedDetail,
            observedDetail);
    }

    private static int ScoreComparisons(IReadOnlyList<BindingFieldComparison> comparisons)
    {
        var score = 0;
        foreach (var comparison in comparisons.Where(x => x.IsMatch))
        {
            score += comparison.Name switch
            {
                "svID" or "goID" or "GoCBRef" => 45,
                "APPID" => 35,
                "DataSet" => 25,
                "Dst MAC" => 20,
                "VLAN" => 10,
                "Priority" => 5,
                "confRev" => 20,
                _ => 5
            };
        }

        return Math.Min(score, 100);
    }

    private static BindingFieldComparison CompareField(
        string name,
        string expected,
        string observed,
        bool required,
        bool useAppId = false,
        bool useMac = false,
        bool useVlan = false,
        IReadOnlyList<string>? aliases = null)
    {
        var normalizedExpected = NormalizeComparable(expected, useAppId, useMac, useVlan);
        var normalizedObserved = NormalizeComparable(observed, useAppId, useMac, useVlan);
        var aliasMatch = aliases?.Any(alias => string.Equals(normalizedExpected, NormalizeComparable(alias, useAppId, useMac, useVlan), StringComparison.OrdinalIgnoreCase)) == true;
        var hasExpected = !string.IsNullOrWhiteSpace(normalizedExpected) && normalizedExpected != "N/A";
        var hasObserved = !string.IsNullOrWhiteSpace(normalizedObserved) && normalizedObserved != "N/A";
        var isMatch = hasExpected && hasObserved && (string.Equals(normalizedExpected, normalizedObserved, StringComparison.OrdinalIgnoreCase) || aliasMatch);
        var isMismatch = required && hasExpected && hasObserved && !isMatch;

        return new BindingFieldComparison(name, ValueOrNa(expected), ValueOrNa(observed), required, hasExpected, hasObserved, isMatch, isMismatch);
    }

    private static string NormalizeComparable(string value, bool appId, bool mac, bool vlan)
    {
        if (appId)
            return NormalizeAppId(value);
        if (mac)
            return NormalizeMatchText(value).Replace("-", ":", StringComparison.Ordinal).ToUpperInvariant();
        if (vlan)
            return NormalizeVlanValue(value);
        return NormalizeMatchText(value);
    }

    private static string NormalizeVlanValue(string value)
    {
        var text = NormalizeMatchText(value);
        if (string.IsNullOrWhiteSpace(text) || text == "N/A")
            return string.Empty;

        var slashIndex = text.IndexOf('/', StringComparison.Ordinal);
        if (slashIndex >= 0)
            text = text[..slashIndex];

        var digits = new string(text.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits))
            return text;

        var normalizedDigits = digits.TrimStart('0');
        return string.IsNullOrWhiteSpace(normalizedDigits) ? "0" : normalizedDigits;
    }

    private static string BuildExpectedDetail(SclStreamCatalogRow expected)
        => string.Join(Environment.NewLine, new[]
        {
            $"Expected {expected.Protocol}: {expected.DisplayName}",
            $"Control: {expected.ControlBlockReference}",
            $"DataSet: {expected.DataSetReference}",
            $"APPID: {ValueOrNa(expected.AppId)}",
            $"Destination MAC: {ValueOrNa(expected.DestinationMac)}",
            $"VLAN: {ValueOrNa(expected.VlanId)} / Priority {ValueOrNa(expected.VlanPriority)}",
            $"confRev: {expected.ExpectedConfRevText}"
        });

    private static string ValueOrNa(string value)
        => string.IsNullOrWhiteSpace(value) ? "N/A" : value;

    private static int ScoreSvBinding(SclStreamCatalogRow expected, SvStreamItem live)
    {
        var score = 0;
        if (TextMatches(expected.DisplayName, live.SvId) || TextMatches(expected.DisplayName, live.StreamName)) score += 45;
        if (TextMatches(expected.AppId, live.AppId) || AppIdMatches(expected.AppId, live.AppId)) score += 35;
        if (TextMatches(expected.DestinationMac, live.DestinationMac)) score += 20;
        if (TextMatches(expected.VlanId, live.VlanText)) score += 10;
        return score;
    }

    private static int ScoreGooseBinding(SclStreamCatalogRow expected, GooseMessageItem live)
    {
        var score = 0;
        if (TextMatches(expected.DisplayName, live.GoId) || TextMatches(expected.ControlBlockReference, live.GoCbRef)) score += 45;
        if (TextMatches(expected.AppId, live.AppId) || AppIdMatches(expected.AppId, live.AppId)) score += 35;
        if (TextMatches(expected.DataSetReference, live.DataSet)) score += 25;
        if (TextMatches(expected.DestinationMac, live.DestinationMac)) score += 15;
        if (TextMatches(expected.VlanId, live.VlanId)) score += 10;
        return score;
    }

    private static string DescribeSvBindingEvidence(SclStreamCatalogRow expected, SvStreamItem live)
    {
        var parts = new List<string>();
        if (TextMatches(expected.DisplayName, live.SvId) || TextMatches(expected.DisplayName, live.StreamName)) parts.Add("svID");
        if (TextMatches(expected.AppId, live.AppId) || AppIdMatches(expected.AppId, live.AppId)) parts.Add("APPID");
        if (TextMatches(expected.DestinationMac, live.DestinationMac)) parts.Add("Dst MAC");
        if (TextMatches(expected.VlanId, live.VlanText)) parts.Add("VLAN");
        return parts.Count == 0 ? "Candidate selected by weak similarity." : $"Matched by {string.Join(" + ", parts)}.";
    }

    private static string DescribeGooseBindingEvidence(SclStreamCatalogRow expected, GooseMessageItem live)
    {
        var parts = new List<string>();
        if (TextMatches(expected.DisplayName, live.GoId)) parts.Add("goID");
        if (TextMatches(expected.ControlBlockReference, live.GoCbRef)) parts.Add("GoCBRef");
        if (TextMatches(expected.AppId, live.AppId) || AppIdMatches(expected.AppId, live.AppId)) parts.Add("APPID");
        if (TextMatches(expected.DataSetReference, live.DataSet)) parts.Add("DataSet");
        if (TextMatches(expected.DestinationMac, live.DestinationMac)) parts.Add("Dst MAC");
        if (TextMatches(expected.VlanId, live.VlanId)) parts.Add("VLAN");
        return parts.Count == 0 ? "Candidate selected by weak similarity." : $"Matched by {string.Join(" + ", parts)}.";
    }

    private static bool AppIdMatches(string expected, string observed)
    {
        var a = NormalizeAppId(expected);
        var b = NormalizeAppId(observed);
        return !string.IsNullOrWhiteSpace(a) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeAppId(string value)
    {
        var text = NormalizeMatchText(value).Replace("APPID", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            text = text[2..];
        text = new string(text.Where(Uri.IsHexDigit).ToArray()).TrimStart('0');
        return string.IsNullOrWhiteSpace(text) ? "0" : text;
    }

    private string ComputeSvLiveStatus(SclSvStreamModel stream)
    {
        var match = Streams.Any(s => TextMatches(s.SvId, stream.SvId) || TextMatches(s.AppId, stream.AppId) || TextMatches(s.DestinationMac, stream.DestinationMac));
        return match ? "Live candidate" : "Expected";
    }

    private string ComputeGooseLiveStatus(SclGooseStreamModel stream)
    {
        var match = _gooseMessages.Any(g => TextMatches(g.GoId, stream.GoId) || TextMatches(g.AppId, stream.AppId) || TextMatches(g.DataSet, stream.DataSetReference));
        return match ? "Live candidate" : "Expected";
    }

    private void RaiseSclSelectionProperties()
    {
        OnPropertyChanged(nameof(SclSelectedDetailTitle));
        OnPropertyChanged(nameof(SclSelectedDetailSubtitle));
        OnPropertyChanged(nameof(SclSelectedEntries));
        OnPropertyChanged(nameof(SclSelectedTransportText));
        OnPropertyChanged(nameof(SclSelectedDatasetText));
        OnPropertyChanged(nameof(SclSelectedBindingText));
        OnPropertyChanged(nameof(SclSelectedExpectedText));
        OnPropertyChanged(nameof(SclSelectedObservedText));
        OnPropertyChanged(nameof(SelectedSclBindingMatrixRow));
    }

    private void RaiseSclProperties()
    {
        OnPropertyChanged(nameof(SclProject));
        OnPropertyChanged(nameof(HasSclProject));
        OnPropertyChanged(nameof(SclLoadStatusText));
        OnPropertyChanged(nameof(SclFileNameText));
        OnPropertyChanged(nameof(SclSummaryText));
        OnPropertyChanged(nameof(SclSvStreams));
        OnPropertyChanged(nameof(SclGooseStreams));
        OnPropertyChanged(nameof(SclDataSets));
        OnPropertyChanged(nameof(SclWarnings));
        OnPropertyChanged(nameof(SclDocuments));
        OnPropertyChanged(nameof(SclIedCards));
        OnPropertyChanged(nameof(SclStreamCatalog));
        OnPropertyChanged(nameof(SclFilteredStreamCatalog));
        OnPropertyChanged(nameof(SclBindingMatrix));
        OnPropertyChanged(nameof(SclFilteredBindingMatrix));
        OnPropertyChanged(nameof(SelectedSclBindingMatrixRow));
        OnPropertyChanged(nameof(SclBindingSummaryText));
        OnPropertyChanged(nameof(SelectedSclIedCard));
        OnPropertyChanged(nameof(SelectedSclStreamCatalog));
        OnPropertyChanged(nameof(SclSemanticStatusText));
        OnPropertyChanged(nameof(SclWorkspaceSummaryText));
        OnPropertyChanged(nameof(SclWorkspaceStatusText));
        OnPropertyChanged(nameof(SelectedGooseDatasetValues));
        OnPropertyChanged(nameof(SelectedGooseSemanticText));
        RaiseSclSelectionProperties();
        OnPropertyChanged(nameof(WorkspaceFooterLeftText));
        OnPropertyChanged(nameof(WorkspaceFooterRightText));
    }

    private void CopyEvidenceSnapshot()
    {
        try
        {
            Clipboard.SetText(BuildEvidenceSnapshotText());
            _evidenceCopyStatusText = $"Copied {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            _evidenceCopyStatusText = $"Copy failed: {ex.Message}";
        }

        OnPropertyChanged(nameof(EvidenceCopyStatusText));
    }

    private string BuildEvidenceSnapshotText()
    {
        var selectedAdapter = Adapters.FirstOrDefault(adapter =>
            string.Equals(adapter.Id, SelectedAdapterId, StringComparison.OrdinalIgnoreCase));
        var selectedStream = SelectedStreamDetails;
        var builder = new StringBuilder();

        builder.AppendLine("Process Bus Insight - Raw Passive SV/GOOSE/PTP Engineering Snapshot");
        builder.AppendLine($"Generated local: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        builder.AppendLine("Engine: Raw Passive SV/GOOSE/PTP decoder; product WPF app does not reference, load, or call external IEC 61850 subscriber stacks");
        builder.AppendLine($"Verdict: {EvidenceVerdictText}");
        builder.AppendLine();
        builder.AppendLine("[Standard]");
        builder.AppendLine("IEC 61850-9-2LE protection profile: 80 samples/cycle @ 50 Hz = 4000 fps, ideal interval 250 us");
        builder.AppendLine();
        builder.AppendLine("[Observed SV]");
        builder.AppendLine($"Stream status: {Diagnostics.StreamStatusText}");
        builder.AppendLine($"svID: {selectedStream?.SvId ?? "N/A"}");
        builder.AppendLine($"APPID: {selectedStream?.AppId ?? "N/A"}");
        builder.AppendLine($"Source MAC: {selectedStream?.SourceMac ?? "N/A"}");
        builder.AppendLine($"Destination MAC: {selectedStream?.DestinationMac ?? "N/A"}");
        builder.AppendLine($"VLAN: {selectedStream?.VlanText ?? "N/A"}");
        builder.AppendLine($"smpCnt: {Diagnostics.LastSampleCount?.ToString() ?? "N/A"}");
        builder.AppendLine($"Packet rate: {PacketRateText}");
        builder.AppendLine($"Packet rate meaning: {Diagnostics.PacketRateMeaningText}");
        builder.AppendLine();
        builder.AppendLine("[Integrity]");
        builder.AppendLine($"Sequence: {OutOfSequenceText}");
        builder.AppendLine($"Missing samples: {Diagnostics.MissingSamples}");
        builder.AppendLine($"Loss: {PacketLossText}");
        builder.AppendLine($"Decode rejects: {Diagnostics.DecodeErrors}");
        builder.AppendLine();
        builder.AppendLine("[Timing]");
        builder.AppendLine($"Expected interval: {FormatMicroseconds(Diagnostics.ExpectedDeltaMicroseconds)}");
        builder.AppendLine($"Current arrival delta: {FormatMicroseconds(Diagnostics.CurrentDeltaMicroseconds)}");
        builder.AppendLine($"Current arrival variation: {FormatMicroseconds(Diagnostics.CurrentJitterMicroseconds)}");
        builder.AppendLine($"Average abs arrival variation: {FormatMicroseconds(Diagnostics.AverageAbsJitterMicroseconds)}");
        builder.AppendLine($"Max abs arrival variation: {FormatMicroseconds(Diagnostics.MaxAbsJitterMicroseconds)}");
        builder.AppendLine($"Recent arrival excursions >=300 us: {Diagnostics.RecentJitterOver300MicrosecondsCount}/5s");
        builder.AppendLine($"Total arrival excursions >=300 us: {Diagnostics.JitterOver300MicrosecondsCount}");
        builder.AppendLine($"Packet rate: {PacketRateText}");
        builder.AppendLine($"Packet rate meaning: {Diagnostics.PacketRateMeaningText}");
        builder.AppendLine(TimingReferenceText);
        builder.AppendLine(TimestampSourceText);
        builder.AppendLine(TimingMetricText);
        builder.AppendLine(TimingConfidenceText);
        builder.AppendLine(CaptureTimingCautionText);
        builder.AppendLine(AdapterTimingRiskText);
        builder.AppendLine();
        builder.AppendLine("[PTP]");
        builder.AppendLine(PtpStatusText);
        builder.AppendLine(PtpDomainText);
        builder.AppendLine(PtpGrandmasterText);
        builder.AppendLine(PtpClockQualityText);
        builder.AppendLine(PtpRateText);
        builder.AppendLine($"Profile hint: {PtpProfileText}");
        builder.AppendLine($"GM changes: {Diagnostics.PtpGrandmasterChangeCount}");
        builder.AppendLine();
        builder.AppendLine("[Capture Path]");
        builder.AppendLine($"Adapter: {selectedAdapter?.ToString() ?? "N/A"}");
        builder.AppendLine($"Raw device: {selectedAdapter?.RawDeviceName ?? "N/A"}");
        builder.AppendLine(TimingConfidenceText);
        builder.AppendLine(CaptureTimingCautionText);
        builder.AppendLine(AdapterTimingRiskText);
        builder.AppendLine();
        builder.AppendLine("[Interpretation]");
        builder.AppendLine(ResolveEvidenceInterpretation());

        return builder.ToString();
    }

    private string ResolveEvidenceInterpretation()
    {
        if (_streamStale)
            return "No active SV evidence yet; verify publisher, selected NIC, VLAN, and Npcap capture path.";

        if (Diagnostics.SequenceErrors == 0 &&
            Diagnostics.MissingSamples == 0 &&
            Diagnostics.RecentJitterOver300MicrosecondsCount > 0)
        {
            return "smpCnt sequence is continuous while arrival variation is high; investigate PTP status, publisher scheduling, USB/NIC batching, Windows/Npcap timestamping, or capture hardware before blaming SV payload continuity.";
        }

        if (Diagnostics.SequenceErrors > 0 || Diagnostics.MissingSamples > 0)
            return "smpCnt continuity is not clean; investigate network loss, publisher sequence, VLAN path, or raw decoder mapping path.";

        if (Diagnostics.DecodeErrors > 0)
            return "SV stream is present but some matched process-bus frames were rejected by the raw decoder; inspect malformed event details and frame source.";

        return "Passive raw SV decoder snapshot is nominal for the current receive-only engineering check.";
    }

    private static string FormatMicroseconds(double? value)
    {
        return value.HasValue ? $"{value.Value:0.###} us" : "N/A";
    }

    public async Task ShutdownAsync()
    {
        if (_isShuttingDown)
            return;

        _isShuttingDown = true;
        _timer.Stop();

        try
        {
            await _rawDataSource.StopAsync().ConfigureAwait(false);
        }
        catch
        {
            // Best-effort shutdown only. The application is closing.
        }

        try
        {
            if (_rawDataSource is IDisposable disposable)
                disposable.Dispose();
        }
        catch
        {
            // Do not keep the WPF process alive because an unmanaged capture adapter failed to close cleanly.
        }
    }

    public void Dispose()
    {
        ShutdownAsync().GetAwaiter().GetResult();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SwitchToRaw()
    {
        _rawDataSource.StopAsync().GetAwaiter().GetResult();
        IsRunning = false;

        IsLiveMode = true;
        _dataSource = _rawDataSource;
        _state.DataSourceName = _dataSource.Name;

        UpdateRefreshCadence();
        RaiseAll(DateTime.UtcNow, force: true);
    }

    private async Task StartAsync()
    {
        await _rawDataSource.StartAsync();
        IsRunning = _rawDataSource.IsRunning;

        UpdateRefreshCadence();
        await RefreshAsync();
    }

    private async Task StopAsync()
    {
        await _rawDataSource.StopAsync();
        IsRunning = false;

        UpdateRefreshCadence();
        await RefreshAsync();

        // === FORCE UI RESET ===

        foreach (var s in Streams)
        {
            s.IsActive = false;
            s.StatusText = "STOPPED";
            s.DisplayStatusText = "STOPPED";
            s.StatusBrush = "#8FA8BF";
            s.StatusSoftBrush = "#1C2A38";
        }

        _gooseMessages = Array.Empty<GooseMessageItem>();
        _gooseSnapshot = new GooseMonitorSnapshot();

        OnPropertyChanged(nameof(Streams));
        OnPropertyChanged(nameof(SelectedStream));
        OnPropertyChanged(nameof(GooseMessages));
        OnPropertyChanged(nameof(GooseSnapshot));
    }

    private async Task ClearAsync()
    {
        for (var i = 0; i < 25 && _refreshInFlight; i++)
            await Task.Delay(20);

        _rawDataSource.ClearRuntimeState();

        _state.ClearRuntimeData();
        _gooseSnapshot = new GooseMonitorSnapshot { StatusText = "Cleared" };
        _gooseMessages = Array.Empty<GooseMessageItem>();
        _gooseHistory.Clear();
        _lastGooseState.Clear();
        _pendingGooseRows.Clear();
        _pendingDiagnosticEvents.Clear();
        _lastGooseHistoryTimeUtc = null;
        _selectedGooseMessage = null;
        _selectedGooseTrafficRow = null;
        _selectedStream = null;
        _phasors = Array.Empty<PhasorDisplayItem>();
        _displayedWaveform = _state.Waveform;
        _streamStale = true;
        _lastObservedPacketCount = -1;
        _lastPacketAdvanceUtc = DateTime.MinValue;
        _packetLossPercent = 0;
        _jitterUs = 0;
        _latencyUs = 0;
        _outOfSequenceCount = 0;
        _smcCntOk = true;
        _frequencyStable = true;
        UpdateDebugDisplaySnapshot();
        RaiseAll(DateTime.UtcNow, force: true);
        OnPropertyChanged(nameof(SelectedGooseMessage));
        OnPropertyChanged(nameof(SelectedGooseTrafficRow));
        OnPropertyChanged(nameof(SelectedGooseDatasetValues));
        OnPropertyChanged(nameof(SelectedGooseSemanticText));
    }

    private void UpdateRefreshCadence()
    {
        var fastInterval = TimeSpan.FromMilliseconds(1000.0 / LiveWaveformUiFps);
        if (!IsAnalyzerTabActive)
        {
            _timer.Interval = TimeSpan.FromMilliseconds(PassiveUiRefreshMs);
            return;
        }

        _timer.Interval = _dataSource switch
        {
            _ => fastInterval
        };
    }

    private static IReadOnlyList<PhasorDisplayItem> BuildPhasorItems(AnalogValuesSnapshot analogValues)
    {
        var referenceName = ResolvePhasorReferenceName(analogValues);

        return
        [
            CreatePhasor("Ua", PhasorFamily.Voltage, analogValues.Ua, forceAngleZero: string.Equals(referenceName, "Ua", StringComparison.OrdinalIgnoreCase)),
            CreatePhasor("Ub", PhasorFamily.Voltage, analogValues.Ub),
            CreatePhasor("Uc", PhasorFamily.Voltage, analogValues.Uc),
            CreatePhasor("Ia", PhasorFamily.Current, analogValues.Ia, forceAngleZero: string.Equals(referenceName, "Ia", StringComparison.OrdinalIgnoreCase)),
            CreatePhasor("Ib", PhasorFamily.Current, analogValues.Ib),
            CreatePhasor("Ic", PhasorFamily.Current, analogValues.Ic)
        ];
    }

    private (double P, double Q, double S) ComputeThreePhasePower()
    {
        var phases = new[]
        {
            (U: AnalogValues.Ua, I: AnalogValues.Ia),
            (U: AnalogValues.Ub, I: AnalogValues.Ib),
            (U: AnalogValues.Uc, I: AnalogValues.Ic)
        };

        var p = 0.0;
        var q = 0.0;
        var s = 0.0;

        foreach (var phase in phases)
        {
            if (!phase.U.RmsValue.HasValue || !phase.I.RmsValue.HasValue)
                continue;

            var apparent = Math.Abs(phase.U.RmsValue.Value * phase.I.RmsValue.Value);
            s += apparent;

            if (!phase.U.AngleDegrees.HasValue || !phase.I.AngleDegrees.HasValue)
                continue;

            var angle = (phase.U.AngleDegrees.Value - phase.I.AngleDegrees.Value) * Math.PI / 180.0;
            p += apparent * Math.Cos(angle);
            q += apparent * Math.Sin(angle);
        }

        return (p, q, s);
    }

    private static string FormatPower(double value, string unit)
    {
        var abs = Math.Abs(value);
        if (abs >= 1_000_000.0)
            return $"{value / 1_000_000.0:0.00} M{unit}";
        if (abs >= 1_000.0)
            return $"{value / 1_000.0:0.00} k{unit}";
        return $"{value:0.00} {unit}";
    }

    private static string? ResolvePhasorReferenceName(AnalogValuesSnapshot analogValues)
    {
        if (analogValues.Ua.RmsValue.HasValue)
            return "Ua";

        if (analogValues.Ia.RmsValue.HasValue)
            return "Ia";

        return null;
    }

    private static PhasorDisplayItem CreatePhasor(string name, PhasorFamily family, ChannelValueModel channel, bool forceAngleZero = false)
    {
        return new PhasorDisplayItem
        {
            Name = name,
            Family = family,
            Magnitude = channel.RmsValue,
            AngleDegrees = forceAngleZero && channel.RmsValue.HasValue ? 0.0 : channel.AngleDegrees,
            Unit = channel.Unit
        };
    }

    private bool FilterGooseHistory(object item)
    {
        if (item is not GooseTrafficRow row)
            return false;

        if (!IncludeGooseRetransmission && string.Equals(row.EventType, "Retrans", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(SelectedGooseIdFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            var matchesSelected = string.Equals(row.GoId, SelectedGooseIdFilter, StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(row.GoCbRef, SelectedGooseIdFilter, StringComparison.OrdinalIgnoreCase);
            if (!matchesSelected)
                return false;
        }

        if (!string.IsNullOrWhiteSpace(GoosePublisherFilterText))
        {
            var filter = GoosePublisherFilterText.Trim();
            var matchesText = Contains(row.GoId, filter) ||
                              Contains(row.GoCbRef, filter) ||
                              Contains(row.AppId, filter) ||
                              Contains(row.DataSet, filter) ||
                              Contains(row.SourceMac, filter) ||
                              Contains(row.DestinationMac, filter);
            if (!matchesText)
                return false;
        }

        return true;

        static bool Contains(string? text, string filter) =>
            !string.IsNullOrWhiteSpace(text) &&
            text.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<GooseDatasetValueDisplayItem> BuildGooseDatasetValues(GooseMessageItem? goose)
    {
        var values = goose?.DataValues;
        if (values is null || values.Count == 0)
            return Array.Empty<GooseDatasetValueDisplayItem>();

        var semanticEntries = goose is null
            ? Array.Empty<SclDataSetEntryModel>()
            : ResolveSclGooseStream(goose)?.Entries ?? Array.Empty<SclDataSetEntryModel>();
        var semanticByIndex = semanticEntries.ToDictionary(entry => entry.Index);

        return values
            .Select(value =>
            {
                semanticByIndex.TryGetValue(value.Index, out var semantic);
                return new GooseDatasetValueDisplayItem
                {
                    Index = value.Index,
                    NameText = semantic?.DisplayName ?? (string.IsNullOrWhiteSpace(value.Name) ? $"Entry {value.Index}" : value.Name),
                    TypeText = BuildSemanticGooseTypeText(value, semantic),
                    SemanticText = semantic is null ? "Generic typed decode" : "SCL DataSet entry",
                    ValueText = BuildSemanticGooseValueText(value, semantic),
                    RawHexText = value.RawHex,
                    IsChanged = value.IsChanged,
                    PreviousValueText = string.IsNullOrWhiteSpace(value.PreviousValue)
                        ? value.PreviousValue
                        : BuildSemanticGooseValueText(
                            new GooseDatasetValueItem
                            {
                                Index = value.Index,
                                Name = value.Name,
                                Type = value.Type,
                                Value = value.PreviousValue,
                                RawHex = value.RawHex
                            },
                            semantic)
                };
            })
            .ToArray();
    }

    private string BuildSemanticGooseChangedSummary(GooseMessageItem goose)
    {
        var values = BuildGooseDatasetValues(goose);
        var changed = values
            .Where(value => value.IsChanged)
            .Take(3)
            .Select(value => string.IsNullOrWhiteSpace(value.PreviousValueText)
                ? $"{value.NameText}={value.DisplayValueText}"
                : $"{value.NameText}: {value.PreviousValueText} -> {value.DisplayValueText}")
            .ToArray();

        if (changed.Length > 0)
        {
            var suffix = values.Count(value => value.IsChanged) > changed.Length ? "..." : string.Empty;
            return string.Join(", ", changed) + suffix;
        }

        return string.IsNullOrWhiteSpace(goose.ChangedSummaryText) || string.Equals(goose.ChangedSummaryText, "N/A", StringComparison.OrdinalIgnoreCase)
            ? "No dataset value change"
            : goose.ChangedSummaryText;
    }

    private static string BuildSemanticGooseValueText(GooseDatasetValueItem value, SclDataSetEntryModel? semantic)
    {
        var text = value.Value ?? string.Empty;
        if (semantic is null)
            return text;

        if (string.Equals(semantic.Cdc, "DPC", StringComparison.OrdinalIgnoreCase))
        {
            return text switch
            {
                "10" => "CLOSE [10]",
                "01" => "OPEN [01]",
                "00" => "INTERMEDIATE [00]",
                "11" => "BAD_STATE [11]",
                _ => text
            };
        }

        if (string.Equals(semantic.Cdc, "SPS", StringComparison.OrdinalIgnoreCase) &&
            bool.TryParse(text, out var singlePoint))
        {
            return singlePoint ? "true" : "false";
        }

        return text;
    }

    private static string BuildSemanticGooseTypeText(GooseDatasetValueItem value, SclDataSetEntryModel? semantic)
    {
        if (semantic is null)
            return value.Type;

        var parts = new[] { semantic.Fc, semantic.Cdc, semantic.BType }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();
        var semanticType = parts.Length == 0 ? "SCL type unresolved" : string.Join(" / ", parts);
        return $"{semanticType}  -  MMS {value.Type}";
    }

    private void BufferGooseHistoryRow(GooseMessageItem msg, string eventType)
    {
        double? deltaMs = null;

        if (_lastGooseHistoryTimeUtc.HasValue)
            deltaMs = (msg.LastSeenUtc - _lastGooseHistoryTimeUtc.Value).TotalMilliseconds;

        _lastGooseHistoryTimeUtc = msg.LastSeenUtc;

        _pendingGooseRows.Enqueue(new GooseTrafficRow
        {
            Source = msg,
            EventType = eventType,
            DeltaMs = deltaMs,
            SemanticChangedSummaryText = BuildSemanticGooseChangedSummary(msg)
        });

        while (_pendingGooseRows.Count > 500)
            _pendingGooseRows.Dequeue();
    }

    private void DeferGooseUiFlush()
    {
        _isGooseInteractionActive = true;
        _gooseInteractionUntilUtc = DateTime.UtcNow.Add(GooseInteractionQuietPeriod);
    }

    private void BufferDiagnosticEvents(IReadOnlyList<DiagnosticEventItem> events)
    {
        foreach (var item in events)
            _pendingDiagnosticEvents.Enqueue(item);

        while (_pendingDiagnosticEvents.Count > 200)
            _pendingDiagnosticEvents.Dequeue();
    }

    private void FlushBufferedDiagnosticEvents(IReadOnlyList<DiagnosticEventItem> currentEvents)
    {
        if (_pendingDiagnosticEvents.Count == 0)
            return;

        var items = new List<DiagnosticEventItem>(_pendingDiagnosticEvents.Count + currentEvents.Count);
        while (_pendingDiagnosticEvents.Count > 0)
            items.Add(_pendingDiagnosticEvents.Dequeue());

        items.AddRange(currentEvents);
        _state.MergeEvents(items);
    }

    private void FlushBufferedGooseRows(bool flushAll = false)
    {
        var limit = flushAll ? int.MaxValue : GooseBufferedFlushLimit;
        var flushed = 0;

        while (_pendingGooseRows.Count > 0 && flushed < limit)
        {
            _gooseHistory.Add(_pendingGooseRows.Dequeue());
            flushed++;
        }

        if (_gooseHistory.Count > GooseHistoryLimit)
        {
            var removeCount = _gooseHistory.Count - GooseHistoryLimit;
            for (var i = 0; i < removeCount; i++)
                _gooseHistory.RemoveAt(0);
        }

        _gooseHistoryView.Refresh();
    }

    private void TryFlushBufferedGooseRows()
    {
        if (_isGooseInteractionActive)
        {
            if (DateTime.UtcNow < _gooseInteractionUntilUtc)
                return;

            _isGooseInteractionActive = false;
        }

        FlushBufferedGooseRows();
    }
}

public sealed class GooseDatasetValueDisplayItem
{
    public int Index { get; init; }
    public string NameText { get; init; } = string.Empty;
    public string TypeText { get; init; } = "Unknown";
    public string SemanticText { get; init; } = "Generic typed decode";
    public string ValueText { get; init; } = "-";
    public string RawHexText { get; init; } = string.Empty;
    public bool IsChanged { get; init; }
    public string PreviousValueText { get; init; } = string.Empty;

    public string DisplayValueText => ValueText switch
    {
        "10" => "[10] CLOSE",
        "01" => "[01] OPEN",
        _ => ValueText
    };

    public string ChangeText => IsChanged && !string.IsNullOrWhiteSpace(PreviousValueText)
        ? $"changed: {PreviousValueText} -> {ValueText}"
        : IsChanged ? "changed" : string.Empty;

    public Visibility ChangeVisibility => IsChanged ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RawHexVisibility => string.IsNullOrWhiteSpace(RawHexText) ? Visibility.Collapsed : Visibility.Visible;

    public Brush ValueBrush => ResolveBrush(ValueText, IsChanged);

    private static Brush ResolveBrush(string value, bool isChanged)
    {
        if (isChanged)
            return new SolidColorBrush(Color.FromRgb(255, 190, 82));

        if (bool.TryParse(value, out var b))
            return b
                ? new SolidColorBrush(Color.FromRgb(255, 80, 80))
                : new SolidColorBrush(Color.FromRgb(55, 214, 122));

        if (value == "10")
            return new SolidColorBrush(Color.FromRgb(255, 80, 80));

        if (value == "01")
            return new SolidColorBrush(Color.FromRgb(55, 214, 122));

        return new SolidColorBrush(Color.FromRgb(143, 168, 191));
    }
}

public sealed class GooseTrafficRow
{
    public required GooseMessageItem Source { get; init; }

    public string EventType { get; init; } = "STATE";
    public double? DeltaMs { get; init; }
    public string SemanticChangedSummaryText { get; init; } = string.Empty;

    public string TimeText => Source.LastSeenUtc.ToString("HH:mm:ss.fff");
    public string DeltaText => DeltaMs.HasValue ? $"{DeltaMs.Value:0.0}" : "-";

    public string GoId => Source.GoId;
    public string GoCbRef => Source.GoCbRef;
    public string DataSet => Source.DataSet;
    public uint StNum => Source.StNum;
    public uint SqNum => Source.SqNum;
    public string StatusText => Source.StatusText;
    public string ValuesText => Source.ValuesText;
    public string DisplayStatusText =>
        string.IsNullOrWhiteSpace(Source.StatusText) || string.Equals(Source.StatusText, "N/A", StringComparison.OrdinalIgnoreCase)
            ? EventType
            : Source.StatusText;

    public string DisplayValuesText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SemanticChangedSummaryText) &&
                !string.Equals(SemanticChangedSummaryText, "N/A", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(SemanticChangedSummaryText, "No dataset value change", StringComparison.OrdinalIgnoreCase))
            {
                return SemanticChangedSummaryText;
            }

            if (!string.IsNullOrWhiteSpace(Source.ChangedSummaryText) &&
                !string.Equals(Source.ChangedSummaryText, "N/A", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Source.ChangedSummaryText, "No dataset value change", StringComparison.OrdinalIgnoreCase))
            {
                return Source.ChangedSummaryText;
            }

            if (Source.DataValues.Count == 0)
                return "View details";

            var preview = Source.DataValues
                .Take(2)
                .Select(x => $"{x.Name}={FormatValuePreview(x.Value)}");

            var text = string.Join(", ", preview);
            return Source.DataValues.Count > 2 ? $"{text}..." : text;
        }
    }
    public string AppId => Source.AppId;
    public string EntryTimeText => Source.LastSeenUtc.ToLocalTime().ToString("M/d/yyyy h:mm:ss.fff tt");
    public string NeedsCommissionText => Source.NeedsCommission ? "true" : "false";
    public string TestText => Source.IsTest ? "true" : "false";
    public int DatasetEntryCount => Source.DataValues.Count;
    public string DatasetEntryCountText => DatasetEntryCount.ToString();
    public string DestinationMac => Source.DestinationMac;
    public string SourceMac => Source.SourceMac;
    public string VlanId => Source.VlanId;
    public string VlanPriority => Source.VlanPriority;

    public Brush EventBrush => EventType switch
    {
        "New" => new SolidColorBrush(Color.FromRgb(77, 181, 255)),
        "State Change" => new SolidColorBrush(Color.FromRgb(255, 190, 82)),
        "Retrans" => new SolidColorBrush(Color.FromRgb(143, 168, 191)),
        _ => new SolidColorBrush(Color.FromRgb(220, 235, 255))
    };

    private static IReadOnlyList<string> SplitGooseValues(string? valuesText)
    {
        if (string.IsNullOrWhiteSpace(valuesText) || valuesText == "N/A")
            return Array.Empty<string>();

        var trimmed = valuesText.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
            trimmed = trimmed[1..^1];

        return trimmed
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }

    private static string FormatValuePreview(string value)
    {
        return value switch
        {
            "10" => "CLOSE",
            "01" => "OPEN",
            _ => value
        };
    }
}

public sealed class TrafficHealthTargetRow : INotifyPropertyChanged
{
    private string _protocol = "N/A";
    private string _targetKey = string.Empty;
    private string _displayName = "N/A";
    private string _subtitle = string.Empty;
    private string _statusText = "N/A";
    private string _statusBrush = "#8FA8BF";
    private string _statusSoftBrush = "#1C2A38";
    private string _issueSummaryText = "No issue";
    private DateTime? _lastSeenUtc;
    private int _severityRank;
    private string _sourceId = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Protocol
    {
        get => _protocol;
        set => SetField(ref _protocol, value);
    }

    public string TargetKey
    {
        get => _targetKey;
        set => SetField(ref _targetKey, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetField(ref _displayName, value);
    }

    public string Subtitle
    {
        get => _subtitle;
        set => SetField(ref _subtitle, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public string StatusBrush
    {
        get => _statusBrush;
        set => SetField(ref _statusBrush, value);
    }

    public string StatusSoftBrush
    {
        get => _statusSoftBrush;
        set => SetField(ref _statusSoftBrush, value);
    }

    public string IssueSummaryText
    {
        get => _issueSummaryText;
        set => SetField(ref _issueSummaryText, value);
    }

    public DateTime? LastSeenUtc
    {
        get => _lastSeenUtc;
        set
        {
            if (SetField(ref _lastSeenUtc, value))
                OnPropertyChanged(nameof(LastSeenText));
        }
    }

    public int SeverityRank
    {
        get => _severityRank;
        set => SetField(ref _severityRank, value);
    }

    public string SourceId
    {
        get => _sourceId;
        set => SetField(ref _sourceId, value);
    }

    public string LastSeenText
    {
        get
        {
            if (!LastSeenUtc.HasValue)
                return "last: n/a";

            var age = DateTime.UtcNow - LastSeenUtc.Value;
            return age.TotalSeconds < 60
                ? $"last {age.TotalSeconds:0.0}s"
                : LastSeenUtc.Value.ToLocalTime().ToString("HH:mm:ss");
        }
    }

    public void UpdateFrom(TrafficHealthTargetRow source)
    {
        Protocol = source.Protocol;
        TargetKey = source.TargetKey;
        DisplayName = source.DisplayName;
        Subtitle = source.Subtitle;
        StatusText = source.StatusText;
        StatusBrush = source.StatusBrush;
        StatusSoftBrush = source.StatusSoftBrush;
        IssueSummaryText = source.IssueSummaryText;
        LastSeenUtc = source.LastSeenUtc;
        SeverityRank = source.SeverityRank;
        SourceId = source.SourceId;
        OnPropertyChanged(nameof(LastSeenText));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}


public sealed class SclDocumentCardRow
{
    public string FileName { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public string EditionText { get; init; } = string.Empty;
    public string StatusText { get; init; } = string.Empty;
    public string SummaryText { get; init; } = string.Empty;
    public string WarningText { get; init; } = string.Empty;
}

public sealed class SclIedCardRow
{
    public string Name { get; init; } = string.Empty;
    public string SourceFileName { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string ConfigVersion { get; init; } = string.Empty;
    public int GooseCount { get; init; }
    public int SvCount { get; init; }
    public int DataSetCount { get; init; }
    public string SummaryText { get; init; } = string.Empty;
    public string StatusText { get; init; } = string.Empty;
    public string VendorText => string.Join("  -  ", new[] { Manufacturer, Type, ConfigVersion }.Where(x => !string.IsNullOrWhiteSpace(x)));
}

public sealed class SclStreamCatalogRow
{
    public string Protocol { get; init; } = string.Empty;
    public string IedName { get; init; } = string.Empty;
    public string SourceFileName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ControlBlockReference { get; init; } = string.Empty;
    public string DataSetReference { get; init; } = string.Empty;
    public string AppId { get; init; } = string.Empty;
    public string DestinationMac { get; init; } = string.Empty;
    public string VlanId { get; init; } = string.Empty;
    public string VlanPriority { get; init; } = string.Empty;
    public int ConfRev { get; init; }
    public string TransportText { get; init; } = string.Empty;
    public string LiveStatusText { get; init; } = string.Empty;
    public IReadOnlyList<SclDataSetEntryModel> Entries { get; init; } = Array.Empty<SclDataSetEntryModel>();
    public string EntryCountText => $"{Entries.Count} entry(s)";
    public string SummaryText => $"{IedName}  -  {TransportText}";
    public string ExpectedKey => $"{Protocol}|{SourceFileName}|{IedName}|{ControlBlockReference}|{DisplayName}";
    public string ExpectedConfRevText => ConfRev > 0 ? ConfRev.ToString() : "N/A";

    public static SclStreamCatalogRow FromSv(string sourceFileName, SclSvStreamModel stream, string liveStatus)
        => new()
        {
            Protocol = "SV",
            IedName = stream.IedName,
            SourceFileName = sourceFileName,
            DisplayName = string.IsNullOrWhiteSpace(stream.SvId) ? stream.ControlName : stream.SvId,
            ControlBlockReference = stream.ControlBlockReference,
            DataSetReference = stream.DataSetReference,
            AppId = stream.AppId,
            DestinationMac = stream.DestinationMac,
            VlanId = stream.VlanId,
            VlanPriority = stream.VlanPriority,
            ConfRev = stream.ConfRev,
            TransportText = stream.TransportText,
            LiveStatusText = liveStatus,
            Entries = stream.Entries
        };

    public static SclStreamCatalogRow FromGoose(string sourceFileName, SclGooseStreamModel stream, string liveStatus)
        => new()
        {
            Protocol = "GOOSE",
            IedName = stream.IedName,
            SourceFileName = sourceFileName,
            DisplayName = string.IsNullOrWhiteSpace(stream.GoId) ? stream.ControlName : stream.GoId,
            ControlBlockReference = stream.ControlBlockReference,
            DataSetReference = stream.DataSetReference,
            AppId = stream.AppId,
            DestinationMac = stream.DestinationMac,
            VlanId = stream.VlanId,
            VlanPriority = stream.VlanPriority,
            ConfRev = stream.ConfRev,
            TransportText = stream.TransportText,
            LiveStatusText = liveStatus,
            Entries = stream.Entries
        };
}

public sealed class ValidationFindingRow
{
    public string ObjectText { get; init; } = string.Empty;
    public string IedName { get; init; } = string.Empty;
    public string ExpectedText { get; init; } = string.Empty;
    public string ObservedText { get; init; } = string.Empty;
    public string StatusText { get; init; } = string.Empty;
    public string EvidenceText { get; init; } = string.Empty;
    public string StatusBrush => StatusText switch
    {
        "PASS" => "#70D7A7",
        "WARNING" => "#F6D781",
        "FAIL" => "#FF6B6B",
        "INFO" => "#8FCBFF",
        _ => "#8FA8BF"
    };
    public string StatusBackgroundBrush => StatusText switch
    {
        "PASS" => "#17382C",
        "WARNING" => "#3A3218",
        "FAIL" => "#3D1E25",
        "INFO" => "#112E46",
        _ => "#142235"
    };

    public static ValidationFindingRow Create(string objectText, string iedName, string expectedText, string observedText, string statusText, string evidenceText)
        => new()
        {
            ObjectText = objectText,
            IedName = iedName,
            ExpectedText = expectedText,
            ObservedText = observedText,
            StatusText = statusText,
            EvidenceText = evidenceText
        };
}

public sealed class SclBindingMatrixRow : INotifyPropertyChanged
{
    public string BindingKey { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public string IedName { get; set; } = string.Empty;
    public string SourceFileName { get; set; } = string.Empty;
    public string ExpectedName { get; set; } = string.Empty;
    public string ObservedName { get; set; } = string.Empty;
    public string AppIdText { get; set; } = string.Empty;
    public string VlanText { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public int Score { get; set; }
    public string EvidenceText { get; set; } = string.Empty;
    public string ExpectedDetailText { get; set; } = string.Empty;
    public string ObservedDetailText { get; set; } = string.Empty;
    public string LiveKey { get; set; } = string.Empty;
    public string ExpectedKey { get; set; } = string.Empty;
    public SclStreamCatalogRow? ExpectedStream { get; set; }

    public bool IsMatched => string.Equals(StatusText, "MATCHED", StringComparison.OrdinalIgnoreCase);
    public int SortRank => StatusText switch
    {
        "CONFLICT" => 0,
        "AMBIGUOUS" => 1,
        "MISMATCH" => 2,
        "MISSING" => 3,
        "UNEXPECTED" => 4,
        "WEAK" => 5,
        "MATCHED" => 6,
        _ => 6
    };

    public string StatusBrush => StatusText switch
    {
        "MATCHED" => "#70D7A7",
        "WEAK" => "#F6D781",
        "MISSING" => "#F0B533",
        "UNEXPECTED" => "#FF8A6A",
        "MISMATCH" => "#FF6B6B",
        "CONFLICT" => "#DDA0FF",
        "AMBIGUOUS" => "#B9A4FF",
        _ => "#8FA8BF"
    };

    public string StatusSummaryText => $"{StatusText}  -  confidence {Score}%  -  {EvidenceText}";
    public string StatusBackgroundBrush => StatusText switch
    {
        "MATCHED" => "#17382C",
        "WEAK" => "#3A3218",
        "MISSING" => "#3D2D12",
        "UNEXPECTED" => "#3D231B",
        "MISMATCH" => "#3D1E25",
        "CONFLICT" => "#33254D",
        "AMBIGUOUS" => "#2B2850",
        _ => "#142235"
    };

    public string ScoreText => Score > 0 ? $"{Score}% confidence" : "No score";
    public string ExpectedMetaText => string.Join(" - ", new[] { IedName, AppIdText, VlanText }.Where(x => !string.IsNullOrWhiteSpace(x)));
    public string ObservedMetaText => string.IsNullOrWhiteSpace(LiveKey) ? "Not seen on selected adapter" : string.Join(" - ", new[] { AppIdText, VlanText }.Where(x => !string.IsNullOrWhiteSpace(x)));
    public string EvidencePrimaryText => SplitEvidence(EvidenceText).Primary;
    public string EvidenceSecondaryText => SplitEvidence(EvidenceText).Secondary;
    public string SignatureText => $"{BindingKey}:{StatusText}:{ObservedName}:{Score}:{EvidenceText}";

    public event PropertyChangedEventHandler? PropertyChanged;

    public void UpdateFrom(SclBindingMatrixRow source)
    {
        Protocol = source.Protocol;
        IedName = source.IedName;
        SourceFileName = source.SourceFileName;
        ExpectedName = source.ExpectedName;
        ObservedName = source.ObservedName;
        AppIdText = source.AppIdText;
        VlanText = source.VlanText;
        StatusText = source.StatusText;
        Score = source.Score;
        EvidenceText = source.EvidenceText;
        ExpectedDetailText = source.ExpectedDetailText;
        ObservedDetailText = source.ObservedDetailText;
        LiveKey = source.LiveKey;
        ExpectedKey = source.ExpectedKey;
        ExpectedStream = source.ExpectedStream;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    public static SclBindingMatrixRow FromExpected(
        SclStreamCatalogRow expected,
        string observedName,
        string observedAppId,
        string observedVlan,
        string status,
        int score,
        string evidence,
        string liveKey,
        string? expectedDetail = null,
        string? observedDetail = null)
        => new()
        {
            BindingKey = $"EXPECTED|{expected.ExpectedKey}",
            Protocol = expected.Protocol,
            IedName = expected.IedName,
            SourceFileName = expected.SourceFileName,
            ExpectedName = expected.DisplayName,
            ObservedName = string.IsNullOrWhiteSpace(observedName) ? "Not observed" : observedName,
            AppIdText = string.IsNullOrWhiteSpace(observedAppId) ? expected.AppId : $"SCL {expected.AppId} / Live {observedAppId}",
            VlanText = string.IsNullOrWhiteSpace(observedVlan) ? expected.VlanId : $"SCL {expected.VlanId} / Live {observedVlan}",
            StatusText = status,
            Score = score,
            EvidenceText = evidence,
            ExpectedDetailText = expectedDetail ?? BuildDefaultExpectedDetail(expected),
            ObservedDetailText = observedDetail ?? (string.IsNullOrWhiteSpace(liveKey) ? "Observed: not seen on selected adapter." : $"Observed: {observedName}"),
            LiveKey = liveKey,
            ExpectedKey = expected.ExpectedKey,
            ExpectedStream = expected
        };

    public static SclBindingMatrixRow FromUnexpected(string protocol, string observedName, string appId, string vlan, string evidence, string liveKey, string? observedDetail = null)
        => new()
        {
            BindingKey = $"UNEXPECTED|{protocol}|{liveKey}",
            Protocol = protocol,
            IedName = "Live traffic",
            SourceFileName = "Observed network",
            ExpectedName = "No SCL expected stream",
            ObservedName = string.IsNullOrWhiteSpace(observedName) ? "Unnamed live stream" : observedName,
            AppIdText = appId,
            VlanText = vlan,
            StatusText = "UNEXPECTED",
            Score = 0,
            EvidenceText = evidence,
            ExpectedDetailText = "Expected: no matching stream in imported SCL context.",
            ObservedDetailText = observedDetail ?? $"Observed {protocol}: {observedName}\nAPPID: {appId}\nVLAN: {vlan}",
            LiveKey = liveKey,
            ExpectedKey = string.Empty,
            ExpectedStream = null
        };

    private static string BuildDefaultExpectedDetail(SclStreamCatalogRow expected)
        => $"Expected {expected.Protocol}: {expected.DisplayName}\nControl: {expected.ControlBlockReference}\nDataSet: {expected.DataSetReference}\nAPPID: {expected.AppId}\nVLAN: {expected.VlanId}\nconfRev: {expected.ExpectedConfRevText}";

    private static (string Primary, string Secondary) SplitEvidence(string evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence))
            return ("No evidence captured yet", string.Empty);

        var parts = evidence
            .Split(new[] { "; " }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .ToArray();

        if (parts.Length == 0)
            return (evidence, string.Empty);

        return (parts[0], string.Join(Environment.NewLine, parts.Skip(1)));
    }
}

internal sealed record BindingCandidate(
    object Live,
    string LiveKey,
    string ObservedName,
    string ObservedAppId,
    string ObservedVlan,
    int Score,
    int MatchCount,
    int MismatchCount,
    bool Ambiguous,
    string EvidenceText,
    string ExpectedDetailText,
    string ObservedDetailText);

internal sealed record SclSvMappingCandidate(SclSvStreamModel Stream, int Score, int MismatchCount)
{
    public bool IsConfirmed => Score >= 100 && MismatchCount == 0;
}

internal sealed record BindingFieldComparison(
    string Name,
    string ExpectedValue,
    string ObservedValue,
    bool Required,
    bool HasExpected,
    bool HasObserved,
    bool IsMatch,
    bool IsMismatch);

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    public RelayCommand(Action execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
}

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private bool _isExecuting;
    public AsyncRelayCommand(Func<Task> execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !_isExecuting;
    public async void Execute(object? parameter)
    {
        if (_isExecuting) return;
        _isExecuting = true;
        try { await _execute(); }
        finally
        {
            _isExecuting = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
