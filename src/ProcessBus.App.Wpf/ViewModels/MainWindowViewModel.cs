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
    private int _selectionOverlayGeneration;
    private bool _isShuttingDown;
    private bool _includeGooseRetransmission;
    private readonly List<SclProjectModel> _sclProjects = new();
    private SclProjectModel _sclProject = SclProjectModel.Empty;
    private string _sclLoadStatusText = "No SCL loaded";
    private readonly ObservableCollection<SclDocumentCardRow> _sclDocuments = new();
    private readonly ObservableCollection<SclIedCardRow> _sclIedCards = new();
    private readonly ObservableCollection<SclStreamCatalogRow> _sclStreamCatalog = new();
    private SclIedCardRow? _selectedSclIedCard;
    private SclStreamCatalogRow? _selectedSclStreamCatalog;
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
            return $"{filter} · {retrans}";
        }
    }

    public IReadOnlyList<GooseDatasetValueDisplayItem> SelectedGooseDatasetValues =>
        BuildGooseDatasetValues(SelectedGooseMessage?.DataValues);

    public string DiagnosticScopeTitle => SelectedDiagnosticTarget is null
        ? "All Traffic Overview"
        : $"{SelectedDiagnosticTarget.Protocol} · {SelectedDiagnosticTarget.DisplayName}";

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
                ? $"{_diagnosticTargets.Count} target(s) observed · no active warning"
                : $"{warnings} affected target(s) · select an item to inspect";
        }
    }

    public string DiagnosticFindingsHeaderText => SelectedDiagnosticTarget is null
        ? "Recent Findings"
        : $"Findings for {SelectedDiagnosticTarget.Protocol} target";


    public string AdvancedTargetTitle => SelectedDiagnosticTarget is null
        ? "Raw Target Inspector"
        : $"{SelectedDiagnosticTarget.Protocol} · {SelectedDiagnosticTarget.DisplayName}";

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
                    $"APPID                {details.AppId}",
                    $"Source MAC           {details.SourceMac}",
                    $"Destination MAC      {details.DestinationMac}",
                    $"VLAN                 {details.VlanText}",
                    $"Sample rate          {details.SmpRateText}",
                    $"ConfRev              {details.ConfRevText}",
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
                    : $"{goose.GoCbRef}; APPID={goose.AppId}; DataSet={goose.DataSet}; stNum={goose.StNum}; sqNum={goose.SqNum}; values={goose.ValuesText}; changed={goose.ChangedSummaryText}";
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

                return string.Join(Environment.NewLine, values.Select(v => $"[{v.Index}] {v.Name} · {v.Type} = {v.Value}"));
            }

            return PtpEvents.Count == 0
                ? "No PTP event list yet."
                : string.Join(Environment.NewLine, PtpEvents.Take(12).Select(x => $"{x.TimestampUtc:HH:mm:ss.fff} · {x.Transport} · {x.MessageType} · {x.Source} -> {x.Destination} · {x.DomainText} · seq {x.SequenceIdText}"));
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

        var match = _sclProject.SvStreams
            .OrderByDescending(s => MatchScore(s.SvId, details.SvId))
            .ThenByDescending(s => MatchScore(s.AppId, details.AppId))
            .ThenByDescending(s => MatchScore(s.DestinationMac, details.DestinationMac))
            .FirstOrDefault(s =>
                TextMatches(s.SvId, details.SvId) ||
                TextMatches(s.AppId, details.AppId) ||
                TextMatches(s.DestinationMac, details.DestinationMac));

        if (match is null)
            return $"SCL semantic map: no matching SV stream for svID={details.SvId}, APPID={details.AppId}, dst={details.DestinationMac}.";

        return BuildSclStreamText("SCL SV semantic map", match.ControlBlockReference, match.DataSetReference, match.TransportText, match.Entries);
    }

    private string BuildSclGooseSemanticText(GooseMessageItem goose)
    {
        if (!HasSclProject)
            return "SCL semantic map: not loaded. Typed allData is decoded generically; load SCD/CID/ICD to resolve GOOSE DataSet signal names, FC, CDC, and types.";

        var match = _sclProject.GooseStreams
            .OrderByDescending(g => MatchScore(g.GoId, goose.GoId))
            .ThenByDescending(g => MatchScore(g.AppId, goose.AppId))
            .ThenByDescending(g => MatchScore(g.DataSetReference, goose.DataSet))
            .FirstOrDefault(g =>
                TextMatches(g.GoId, goose.GoId) ||
                TextMatches(g.AppId, goose.AppId) ||
                TextMatches(g.DataSetReference, goose.DataSet) ||
                TextMatches(g.ControlBlockReference, goose.GoCbRef));

        if (match is null)
            return $"SCL semantic map: no matching GOOSE stream for goID={goose.GoId}, APPID={goose.AppId}, DataSet={goose.DataSet}.";

        return BuildSclStreamText("SCL GOOSE semantic map", match.ControlBlockReference, match.DataSetReference, match.TransportText, match.Entries);
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
            lines.Add($"[{entry.Index:00}] {entry.DisplayName} · {entry.TypeText}");

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
            OnPropertyChanged();
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
    private bool IsSclTabActive => _currentWorkspaceTabIndex == 4;
    private bool IsDebugTabActive => _currentWorkspaceTabIndex == 5;

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
                var stream = _sclStreamCatalog.FirstOrDefault(x => string.Equals(x.IedName, value.Name, StringComparison.OrdinalIgnoreCase));
                if (stream is not null)
                    _selectedSclStreamCatalog = stream;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedSclStreamCatalog));
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
                var ied = _sclIedCards.FirstOrDefault(x => string.Equals(x.Name, value.IedName, StringComparison.OrdinalIgnoreCase));
                if (ied is not null)
                    _selectedSclIedCard = ied;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedSclIedCard));
            RaiseSclSelectionProperties();
        }
    }

    public string SclSemanticStatusText => HasSclProject
        ? $"SCL semantic map loaded · {_sclProject.SvStreams.Count} SV · {_sclProject.GooseStreams.Count} GOOSE · {_sclProject.EditionText}"
        : "Load SCL/CID/ICD to enable semantic stream mapping.";

    public string SclWorkspaceSummaryText => HasSclProject
        ? $"{_sclDocuments.Count} document(s) · {_sclIedCards.Count} IED(s) · {_sclStreamCatalog.Count} mapped stream(s) · {_sclProject.DataSets.Count} DataSet(s)"
        : "Import SCD / CID / ICD / IID files to build the engineering context.";

    public string SclWorkspaceStatusText => HasSclProject
        ? $"Semantic catalog ready · {_sclProject.EditionText}"
        : "No engineering context loaded";

    public string SclSelectedDetailTitle => SelectedSclStreamCatalog is not null
        ? $"{SelectedSclStreamCatalog.Protocol} stream · {SelectedSclStreamCatalog.DisplayName}"
        : SelectedSclIedCard is not null
            ? $"IED · {SelectedSclIedCard.Name}"
            : "No SCL object selected";

    public string SclSelectedDetailSubtitle => SelectedSclStreamCatalog is not null
        ? $"{SelectedSclStreamCatalog.ControlBlockReference} · {SelectedSclStreamCatalog.TransportText}"
        : SelectedSclIedCard is not null
            ? $"{SelectedSclIedCard.SourceFileName} · {SelectedSclIedCard.SummaryText}"
            : "Select an imported IED or mapped stream.";

    public IReadOnlyList<SclDataSetEntryModel> SclSelectedEntries => SelectedSclStreamCatalog?.Entries ?? Array.Empty<SclDataSetEntryModel>();

    public string SclSelectedTransportText => SelectedSclStreamCatalog?.TransportText ?? "No stream selected";
    public string SclSelectedDatasetText => SelectedSclStreamCatalog?.DataSetReference ?? "No DataSet selected";
    public string SclSelectedBindingText => SelectedSclStreamCatalog?.LiveStatusText ?? "Binding pending";

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
    public AnalogValuesSnapshot AnalogValues => _state.AnalogValues;
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
        ? "Software timestamp timing · reconstructed scope"
        : WaveformStatusText.Replace("Raw scope reconstructed from RMS + smpCnt timing", "Software timestamp timing · reconstructed scope", StringComparison.OrdinalIgnoreCase);
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

    public string HealthIcon => IsHealthy ? "✓" : "!";

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
        ? $"PTP: {Diagnostics.PtpStatusText} · {Diagnostics.PtpTransportText}"
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
        0 => $"SV · {SelectedStreamDetails?.MappedChannelNamesText ?? "No SV stream selected"}",
        1 => $"Diagnostics · {HealthText} · {NetworkHealthText}",
        2 => $"GOOSE · {_gooseMessages.Count} publisher(s) · {_gooseHistory.Count} event(s)",
        3 => $"Timing/PTP · {PtpStatusCompactText} · {PtpDomainText}",
        4 => $"SCL · {_sclDocuments.Count} document(s) · {_sclIedCards.Count} IED(s)",
        5 => "Advanced · raw decode, estimator, and performance",
        _ => "Process Bus Insight"
    };

    public string WorkspaceFooterRightText => CurrentWorkspaceTabIndex switch
    {
        0 => WaveformHeaderStatusText,
        1 => TimingConfidenceBadgeText,
        2 => GooseFilterSummaryText,
        3 => TimingConfidenceBadgeText,
        4 => SclWorkspaceStatusText,
        5 => UiRefreshDurationText,
        _ => StreamStatusText
    };

    public string PtpGrandmasterText => Diagnostics.PtpObserved
        ? $"GM: {Diagnostics.PtpGrandmasterIdentity}"
        : "GM: N/A";

    public string PtpDomainText => Diagnostics.PtpDomainNumber.HasValue
        ? $"Domain: {Diagnostics.PtpDomainNumber}"
        : "Domain: N/A";

    public string PtpClockQualityText => Diagnostics.PtpObserved
        ? $"ClockClass {Diagnostics.PtpClockClass?.ToString() ?? "N/A"} · Accuracy {Diagnostics.PtpClockAccuracyText} · Steps {Diagnostics.PtpStepsRemoved?.ToString() ?? "N/A"}"
        : "Clock quality: N/A";

    public string PtpRateText
    {
        get
        {
            var sync = Diagnostics.PtpSyncRatePerSecond.HasValue ? $"{Diagnostics.PtpSyncRatePerSecond.Value:0.##}/s" : "N/A";
            var announce = Diagnostics.PtpAnnounceRatePerSecond.HasValue ? $"{Diagnostics.PtpAnnounceRatePerSecond.Value:0.##}/s" : "N/A";
            var follow = Diagnostics.PtpFollowUpRatePerSecond.HasValue ? $"{Diagnostics.PtpFollowUpRatePerSecond.Value:0.##}/s" : "N/A";
            return $"Sync {sync} · Announce {announce} · Follow_Up {follow}";
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
                Subtitle = $"{stream.AppId} · {stream.VlanText} · {ShortenMac(stream.SourceMac)}",
                StatusText = stream.DisplayStatusText,
                StatusBrush = stream.StatusBrush,
                StatusSoftBrush = stream.StatusSoftBrush,
                IssueSummaryText = stream.IssueSummaryText,
                LastSeenUtc = stream.LastSeenUtc,
                SeverityRank = stream.SeverityRank,
                SourceId = stream.StreamId
            });
        }

        foreach (var goose in _gooseMessages.OrderBy(x => string.IsNullOrWhiteSpace(x.GoId) ? x.GoCbRef : x.GoId, StringComparer.OrdinalIgnoreCase))
        {
            var age = DateTime.UtcNow - goose.LastSeenUtc;
            var stale = age > TimeSpan.FromSeconds(5);
            rows.Add(new TrafficHealthTargetRow
            {
                Protocol = "GOOSE",
                TargetKey = $"GOOSE|{goose.MessageId}",
                DisplayName = string.IsNullOrWhiteSpace(goose.GoId) || goose.GoId == "N/A" ? goose.GoCbRef : goose.GoId,
                Subtitle = $"{goose.AppId} · VLAN {goose.VlanId} · st/sq {goose.StNum}/{goose.SqNum}",
                StatusText = stale ? "STALE" : "LIVE",
                StatusBrush = stale ? "#F0B533" : "#70D7A7",
                StatusSoftBrush = stale ? "#3A2B12" : "#173528",
                IssueSummaryText = string.IsNullOrWhiteSpace(goose.ChangedSummaryText) || goose.ChangedSummaryText == "N/A"
                    ? $"State tracked · st/sq {goose.StNum}/{goose.SqNum}"
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
            Subtitle = ptpObserved ? $"{Diagnostics.PtpTransportText} · {PtpDomainText}" : "No PTP observed",
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
        if (IsDebugTabActive && (force || (now - _lastDebugRaiseUtc).TotalMilliseconds >= PassiveUiRefreshMs))
        {
            _lastDebugRaiseUtc = now;
            RaiseDebugProperties();
        }
    }

    private void RaiseAnalyzerRenderProperties()
    {
        OnPropertyChanged(nameof(SelectedStreamDetails));
        OnPropertyChanged(nameof(AnalogValues));
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
            _sclLoadStatusText = loaded > 0
                ? $"Imported {loaded} SCL document(s). {SclWorkspaceSummaryText}"
                : errors.Count > 0
                    ? $"SCL import failed: {string.Join(" | ", errors.Take(2))}"
                    : "SCL document already imported.";

            if (errors.Count > 0 && loaded > 0)
                _sclLoadStatusText += $" · {errors.Count} file(s) skipped";

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
        _selectedSclIedCard = null;
        _selectedSclStreamCatalog = null;
        _sclLoadStatusText = "No SCL loaded";
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
                StatusText = project.Warnings.Count == 0 ? "Parsed" : $"Parsed · {project.Warnings.Count} warning(s)",
                SummaryText = $"IED {project.Ieds.Count} · SV {project.SvStreams.Count} · GOOSE {project.GooseStreams.Count} · DataSet {project.DataSets.Count}",
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
                    SummaryText = $"SV {svCount} · GOOSE {gooseCount} · DataSet {dsCount}",
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
        OnPropertyChanged(nameof(SelectedSclIedCard));
        OnPropertyChanged(nameof(SelectedSclStreamCatalog));
        OnPropertyChanged(nameof(SclSemanticStatusText));
        OnPropertyChanged(nameof(SclWorkspaceSummaryText));
        OnPropertyChanged(nameof(SclWorkspaceStatusText));
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
        builder.AppendLine("Engine: Raw Passive SV/GOOSE/PTP decoder; product WPF app does not reference, load, or call libiec61850");
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

    private static IReadOnlyList<GooseDatasetValueDisplayItem> BuildGooseDatasetValues(IReadOnlyList<GooseDatasetValueItem>? values)
    {
        if (values is null || values.Count == 0)
            return Array.Empty<GooseDatasetValueDisplayItem>();

        return values
            .Select(value => new GooseDatasetValueDisplayItem
            {
                Index = value.Index,
                NameText = string.IsNullOrWhiteSpace(value.Name) ? $"Entry {value.Index}" : value.Name,
                TypeText = value.Type,
                ValueText = value.Value,
                RawHexText = value.RawHex,
                IsChanged = value.IsChanged,
                PreviousValueText = value.PreviousValue
            })
            .ToArray();
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
            DeltaMs = deltaMs
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
        ? $"changed: {PreviousValueText} → {ValueText}"
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
    public string VendorText => string.Join(" · ", new[] { Manufacturer, Type, ConfigVersion }.Where(x => !string.IsNullOrWhiteSpace(x)));
}

public sealed class SclStreamCatalogRow
{
    public string Protocol { get; init; } = string.Empty;
    public string IedName { get; init; } = string.Empty;
    public string SourceFileName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ControlBlockReference { get; init; } = string.Empty;
    public string DataSetReference { get; init; } = string.Empty;
    public string TransportText { get; init; } = string.Empty;
    public string LiveStatusText { get; init; } = string.Empty;
    public IReadOnlyList<SclDataSetEntryModel> Entries { get; init; } = Array.Empty<SclDataSetEntryModel>();
    public string EntryCountText => $"{Entries.Count} entry(s)";
    public string SummaryText => $"{IedName} · {TransportText}";

    public static SclStreamCatalogRow FromSv(string sourceFileName, SclSvStreamModel stream, string liveStatus)
        => new()
        {
            Protocol = "SV",
            IedName = stream.IedName,
            SourceFileName = sourceFileName,
            DisplayName = string.IsNullOrWhiteSpace(stream.SvId) ? stream.ControlName : stream.SvId,
            ControlBlockReference = stream.ControlBlockReference,
            DataSetReference = stream.DataSetReference,
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
            TransportText = stream.TransportText,
            LiveStatusText = liveStatus,
            Entries = stream.Entries
        };
}

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

