namespace ProcessBus.Core.Models;

public sealed class StreamDetailsModel
{
    public string StreamName { get; set; } = string.Empty;
    public string SvId { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string SourceMac { get; set; } = string.Empty;
    public string DestinationMac { get; set; } = string.Empty;
    public string VlanText { get; set; } = "N/A";
    public string SmpRateText { get; set; } = "N/A";
    public string ConfRevText { get; set; } = "N/A";
    public string SampleValueMappingText { get; set; } = "Unmapped";
    public string SampleValueCountText { get; set; } = "0";
    public string MappedChannelNamesText { get; set; } = "None";
    public string RawValuesText { get; set; } = "[]";
    public string PacketEvidenceText { get; set; } = "No packet evidence";
    public string TimebaseStatusText { get; set; } = "Timebase pending";
    public string RmsDebugText { get; set; } = "RMS pending";
    public string LastSeenText { get; set; } = "No packets";
    public string PhaseOrderText { get; set; } = "Phase order: pending";
    public string PhaseOrderDetailText { get; set; } = "Need stable Ua/Ub/Uc angles.";
    public string ChannelAngleSummaryText { get; set; } = "Angles pending";
}
