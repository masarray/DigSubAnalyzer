namespace ProcessBus.Core.Models;

public sealed class GooseMessageItem
{
    public string MessageId { get; set; } = string.Empty;
    public string GoId { get; set; } = "N/A";
    public string GoCbRef { get; set; } = "N/A";
    public string DataSet { get; set; } = "N/A";
    public string AppId { get; set; } = "N/A";
    public string SourceMac { get; set; } = "N/A";
    public string DestinationMac { get; set; } = "N/A";
    public string VlanId { get; set; } = "N/A";
    public string VlanPriority { get; set; } = "N/A";
    public uint StNum { get; set; }
    public uint SqNum { get; set; }
    public uint ConfRev { get; set; }
    public uint TimeAllowedToLiveMilliseconds { get; set; }
    public bool IsTest { get; set; }
    public bool NeedsCommission { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public string ValuesText { get; set; } = "N/A";
    public string ChangedSummaryText { get; set; } = "N/A";
    public string StatusText { get; set; } = "Detected";
    public IReadOnlyList<GooseDatasetValueItem> DataValues { get; set; } = Array.Empty<GooseDatasetValueItem>();
}

public sealed class GooseDatasetValueItem
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Unknown";
    public string Value { get; set; } = "-";
    public string RawHex { get; set; } = string.Empty;
    public bool IsChanged { get; set; }
    public string PreviousValue { get; set; } = string.Empty;
}
