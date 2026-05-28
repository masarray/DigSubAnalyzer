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

        foreach (var incoming in snapshot.Streams.OrderBy(stream => stream.FirstSeenOrder))
        {
            var existing = Streams.FirstOrDefault(stream => string.Equals(stream.StreamId, incoming.StreamId, StringComparison.OrdinalIgnoreCase));
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

    public void ClearRuntimeData()
    {
        Streams.Clear();
        Events.Clear();
        PtpEvents.Clear();
        SelectedStreamDetails = null;
        AnalogValues = new AnalogValuesSnapshot();
        Waveform = new WaveformSnapshot { StatusText = "Cleared. Waiting for new SV packets." };
        Diagnostics = new SvDiagnosticsSnapshot { StreamStatusText = "Cleared", DecodeStatusText = "Cleared" };
        ProtocolMonitor = new ProtocolMonitorSnapshot();
        WaveformStatusText = Waveform.StatusText;
    }

    public void MergeEvents(IReadOnlyList<DiagnosticEventItem> incomingItems)
    {
        if (incomingItems.Count == 0)
            return;

        var existingKeys = Events
            .Select(item => BuildEventKey(item.TimestampUtc, item.Severity, item.Message))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var item in incomingItems.OrderBy(x => x.TimestampUtc))
        {
            var key = BuildEventKey(item.TimestampUtc, item.Severity, item.Message);
            if (existingKeys.Contains(key))
                continue;

            Events.Insert(0, item);
            existingKeys.Add(key);
        }

        while (Events.Count > 200)
            Events.RemoveAt(Events.Count - 1);
    }

    private void MergePtpEvents(IReadOnlyList<PtpEventItem> incomingItems)
    {
        if (incomingItems.Count == 0)
            return;

        var existingKeys = PtpEvents
            .Select(item => $"{item.TimestampUtc.Ticks}|{item.Transport}|{item.MessageType}|{item.SequenceIdText}|{item.ClockIdentity}")
            .ToHashSet(StringComparer.Ordinal);

        foreach (var item in incomingItems.OrderBy(x => x.TimestampUtc))
        {
            var key = $"{item.TimestampUtc.Ticks}|{item.Transport}|{item.MessageType}|{item.SequenceIdText}|{item.ClockIdentity}";
            if (existingKeys.Contains(key))
                continue;

            PtpEvents.Insert(0, item);
            existingKeys.Add(key);
        }

        while (PtpEvents.Count > 200)
            PtpEvents.RemoveAt(PtpEvents.Count - 1);
    }

    private static string BuildEventKey(DateTime timestampUtc, string severity, string message)
    {
        return $"{timestampUtc.Ticks}|{severity}|{message}";
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
