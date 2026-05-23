using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ProcessBus.Core.Models;

public sealed class SvStreamItem : INotifyPropertyChanged
{
    private string _streamId = string.Empty;
    private string _streamName = string.Empty;
    private string _svId = string.Empty;
    private string _appId = string.Empty;
    private string _sourceMac = string.Empty;
    private string _destinationMac = string.Empty;
    private string _vlanText = "N/A";
    private string _issueSummaryText = "No issue";
    private int _severityRank;
    private string _statusText = "Inactive";
    private string _displayStatusText = "STOPPED";
    private string _statusBrush = "#8FA8BF";
    private string _statusSoftBrush = "#1C2A38";
    private DateTime _lastSeenUtc;
    private bool _isActive;
    private int _firstSeenOrder;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string StreamId
    {
        get => _streamId;
        set => SetField(ref _streamId, value);
    }

    public string StreamName
    {
        get => _streamName;
        set => SetField(ref _streamName, value);
    }

    public string SvId
    {
        get => _svId;
        set => SetField(ref _svId, value);
    }

    public string AppId
    {
        get => _appId;
        set => SetField(ref _appId, value);
    }

    public string SourceMac
    {
        get => _sourceMac;
        set => SetField(ref _sourceMac, value);
    }

    public string DestinationMac
    {
        get => _destinationMac;
        set => SetField(ref _destinationMac, value);
    }

    public string VlanText
    {
        get => _vlanText;
        set => SetField(ref _vlanText, value);
    }

    public string IssueSummaryText
    {
        get => _issueSummaryText;
        set => SetField(ref _issueSummaryText, value);
    }

    public int SeverityRank
    {
        get => _severityRank;
        set => SetField(ref _severityRank, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public string DisplayStatusText
    {
        get => _displayStatusText;
        set => SetField(ref _displayStatusText, value);
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

    public DateTime LastSeenUtc
    {
        get => _lastSeenUtc;
        set => SetField(ref _lastSeenUtc, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetField(ref _isActive, value);
    }

    public int FirstSeenOrder
    {
        get => _firstSeenOrder;
        set => SetField(ref _firstSeenOrder, value);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
