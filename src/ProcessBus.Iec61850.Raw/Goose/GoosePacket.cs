using ProcessBus.Iec61850.Raw.Protocol;

namespace ProcessBus.Iec61850.Raw.Goose;

public sealed class GoosePacket
{
    public GoosePacket(ProcessBusFrame frame)
    {
        Frame = frame;
    }

    public ProcessBusFrame Frame { get; }
    public string? GoCbRef { get; init; }
    public uint? TimeAllowedToLiveMilliseconds { get; init; }
    public string? DataSet { get; init; }
    public string? GoId { get; init; }
    public ReadOnlyMemory<byte> Timestamp { get; init; }
    public uint? StNum { get; init; }
    public uint? SqNum { get; init; }
    public bool? Test { get; init; }
    public uint? ConfRev { get; init; }
    public bool? NeedsCommission { get; init; }
    public uint? NumDataSetEntries { get; init; }
    public ReadOnlyMemory<byte> AllData { get; init; }
    public IReadOnlyList<GooseDataValue> DataValues { get; init; } = Array.Empty<GooseDataValue>();
}
