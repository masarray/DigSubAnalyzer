namespace ProcessBus.Iec61850.Raw.Sv;

public sealed class SampledValueAsdu
{
    public string? SvId { get; init; }
    public string? DataSet { get; init; }
    public ushort? SmpCnt { get; init; }
    public uint? ConfRev { get; init; }
    public ReadOnlyMemory<byte> RefrTm { get; init; }
    public byte? SmpSynch { get; init; }
    public ushort? SmpRate { get; init; }
    public ushort? SmpMod { get; init; }
    public ReadOnlyMemory<byte> SamplePayload { get; init; }
}
