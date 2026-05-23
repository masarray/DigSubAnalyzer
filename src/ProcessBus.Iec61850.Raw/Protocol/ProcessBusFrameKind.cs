namespace ProcessBus.Iec61850.Raw.Protocol;

public enum ProcessBusFrameKind
{
    Unknown = 0,
    SampledValues,
    Goose,
    Ptp
}
