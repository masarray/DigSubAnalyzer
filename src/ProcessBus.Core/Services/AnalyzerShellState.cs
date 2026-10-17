using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ProcessBus.Core.Models;

namespace ProcessBus.Core.Services;

public sealed class AnalyzerShellState : INotifyPropertyChanged
{
    private StreamDetailsModel? _selectedStreamDetails;
    private AnalogValuesSnapshot _analogValues = new();
    private WaveformSnapshot _waveform = new();
    private SvDiagnosticsSnapshot _diagnostics = new();
    private ProtocolMonitorSnapshot _protocolMonitor = new();
    private string _dataSourceName = "Raw Passive";
    private string _waveformStatusText = "Waiting for SV samples.";
    // Preserve bounded deduplication indexes across UI ticks rather than
    // rebuilding hundreds of formatted keys on every 8 FPS refresh.
    private readonly HashSet<DiagnosticEventKey> _diagnosticEventKeys = new();
    private readonly HashSet<PtpEventKey> _ptpEventKeys = new();
    private readonly record struct DiagnosticEventKey(DateTime TimestampUtc, string Severity, string Message);
    private readonly record struct PtpEventKey(DateTime TimestampUtc, string Transport, string MessageType,
        string SequenceId, string ClockIdentity);

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SvStreamItem> Streams { get; } = new();
    public ObservableCollection<DiagnosticEventItem> Events { get; } = new();
    public ObservableCollection<PtpEventItem> PtpEvents { get; } = new();

    public StreamDetailsModel? SelectedStreamDetails
    {
        get => _selectedStreamDetails;
        set => SetField(ref _selectedStreamDetails, value);
    }

    public SvDiagnosticsSnapshot Diagnostics
    {
        get => _diagnostics;
        set => SetField(ref _diagnostics, value);
    }

    public WaveformSnapshot Waveform
    {
        get => _waveform;
        set => SetField(ref _waveform, value);
    }

    public AnalogValuesSnapshot AnalogValues
    {
        get => _analogValues;
        set => SetField(ref _analogValues, value);
    }

    public ProtocolMonitorSnapshot ProtocolMonitor
    {
        get => _protocolMonitor;
        set => SetField(ref _protocolMonitor, value);
    }

    public string DataSourceName
    {
        get => _dataSourceName;
        set => SetField(ref _dataSourceName, value);
    }

    public string WaveformStatusText
    {
        get => _waveformStatusText;
        set => SetField(ref _waveformStatusText, value);
    }

    public void ApplySnapshot(AnalyzerSnapshot snapshot, bool mergeEvents = true)
    {
        var incomingById = snapshot.Streams.ToDictionary(stream => stream.StreamId, StringComparer.OrdinalIgnoreCase);
        var existingById = Streams.ToDictionary(stream => stream.StreamId, StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < Streams.Count;)
        {
            var existing = Streams[index];
            if (incomingById.ContainsKey(existing.StreamId))
            {
                index++;
                continue;
            }

            Streams.RemoveAt(index);
        }

        foreach (var incoming in snapshot.Streams
                     .OrderByDescending(stream => stream.IsActive)
                     .ThenBy(stream => string.IsNullOrWhiteSpace(stream.SvId) ? stream.StreamName : stream.SvId, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(stream => stream.StreamId, StringComparer.OrdinalIgnoreCase))
        {
            existingById.TryGetValue(incoming.StreamId, out var existing);
            if (existing is null)
            {
                Streams.Add(incoming);
                continue;
            }

            existing.StreamName = incoming.StreamName;
            existing.SvId = incoming.SvId;
            existing.DataSet = incoming.DataSet;
            existing.AppId = incoming.AppId;
            existing.ConfRevText = incoming.ConfRevText;
            existing.SourceMac = incoming.SourceMac;
            existing.DestinationMac = incoming.DestinationMac;
            existing.VlanText = incoming.VlanText;
            existing.IssueSummaryText = incoming.IssueSummaryText;
            existing.SeverityRank = incoming.SeverityRank;
            existing.StatusText = incoming.StatusText;
            existing.DisplayStatusText = incoming.DisplayStatusText;
            existing.StatusBrush = incoming.StatusBrush;
            existing.StatusSoftBrush = incoming.StatusSoftBrush;
            existing.IsActive = incoming.IsActive;
            existing.LastSeenUtc = incoming.LastSeenUtc;
            existing.FirstSeenOrder = incoming.FirstSeenOrder;
        }

        SortStreamsForExplorer();

        if (mergeEvents)
            MergeEvents(snapshot.Events);

        SelectedStreamDetails = snapshot.SelectedStreamDetails;
        AnalogValues = snapshot.AnalogValues;
        Waveform = snapshot.Waveform;
        Diagnostics = snapshot.Diagnostics;
        ProtocolMonitor = snapshot.ProtocolMonitor;
        MergePtpEvents(snapshot.PtpEvents);
        WaveformStatusText = snapshot.Waveform.StatusText;
    }


    private void SortStreamsForExplorer()
    {
        var sorted = Streams
            .Select((stream, index) => new { Stream = stream, Index = index })
            .OrderByDescending(item => item.Stream.IsActive)
            .ThenBy(item => string.IsNullOrWhiteSpace(item.Stream.SvId) ? item.Stream.StreamName : item.Stream.SvId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Stream.StreamId, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Stream)
            .ToList();

        for (var targetIndex = 0; targetIndex < sorted.Count; targetIndex++)
        {
            var currentIndex = Streams.IndexOf(sorted[targetIndex]);
            if (currentIndex >= 0 && currentIndex != targetIndex)
                Streams.Move(currentIndex, targetIndex);
        }
    }

    public void ClearRuntimeData()
    {
        Streams.Clear();
        Events.Clear();
        PtpEvents.Clear();
        _diagnosticEventKeys.Clear();
        _ptpEventKeys.Clear();
        SelectedStreamDetails = null;
        AnalogValues = new AnalogValuesSnapshot();
        Waveform = new WaveformSnapshot { StatusText = "Cleared. Waiting for new SV packets." };
        Diagnostics = new SvDiagnosticsSnapshot { StreamStatusText = "Cleared", DecodeStatusText = "Cleared" };
        ProtocolMonitor = new ProtocolMonitorSnapshot();
        WaveformStatusText = Waveform.StatusText;
    }

    public void MergeEvents(IReadOnlyList<DiagnosticEventItem> incomingItems)
    {
        foreach (var item in incomingItems.OrderBy(x => x.TimestampUtc))
        {
            var key = new DiagnosticEventKey(item.TimestampUtc, item.Severity, item.Message);
            if (!_diagnosticEventKeys.Add(key))
                continue;

            Events.Insert(0, item);
            while (Events.Count > 200)
            {
                var removed = Events[^1];
                _diagnosticEventKeys.Remove(new DiagnosticEventKey(
                    removed.TimestampUtc, removed.Severity, removed.Message));
                Events.RemoveAt(Events.Count - 1);
            }
        }
    }

    private void MergePtpEvents(IReadOnlyList<PtpEventItem> incomingItems)
    {
        foreach (var item in incomingItems.OrderBy(x => x.TimestampUtc))
        {
            var key = new PtpEventKey(item.TimestampUtc, item.Transport, item.MessageType,
                item.SequenceIdText, item.ClockIdentity);
            if (!_ptpEventKeys.Add(key))
                continue;

            PtpEvents.Insert(0, item);
            while (PtpEvents.Count > 200)
            {
                var removed = PtpEvents[^1];
                _ptpEventKeys.Remove(new PtpEventKey(removed.TimestampUtc, removed.Transport,
                    removed.MessageType, removed.SequenceIdText, removed.ClockIdentity));
                PtpEvents.RemoveAt(PtpEvents.Count - 1);
            }
        }
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
