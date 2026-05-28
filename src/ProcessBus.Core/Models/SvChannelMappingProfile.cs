namespace ProcessBus.Core.Models;

public sealed class SvChannelMappingProfile
{
    public string ProfileKey { get; init; } = string.Empty;
    public string SourceText { get; init; } = "SCL DataSet entry order";
    public string ControlBlockReference { get; init; } = string.Empty;
    public string DataSetReference { get; init; } = string.Empty;
    public string SvId { get; init; } = string.Empty;
    public string AppId { get; init; } = string.Empty;
    public string DestinationMac { get; init; } = string.Empty;
    public string VlanId { get; init; } = string.Empty;
    public string ConfRevText { get; init; } = string.Empty;
    public IReadOnlyList<SvChannelElementMapping> Elements { get; init; } = Array.Empty<SvChannelElementMapping>();

    public bool HasRenderableChannels => Elements.Count > 0;
}

public sealed class SvChannelElementMapping
{
    public string ChannelName { get; init; } = string.Empty;
    public int ElementIndex { get; init; }
    public string SignalReference { get; init; } = string.Empty;
    public string TypeText { get; init; } = string.Empty;
}
