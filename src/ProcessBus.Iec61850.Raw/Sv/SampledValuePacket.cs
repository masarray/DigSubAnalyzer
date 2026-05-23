using ProcessBus.Iec61850.Raw.Protocol;

namespace ProcessBus.Iec61850.Raw.Sv;

public sealed class SampledValuePacket
{
    public SampledValuePacket(ProcessBusFrame frame, IReadOnlyList<SampledValueAsdu> asdus)
    {
        Frame = frame;
        Asdus = asdus;
    }

    public ProcessBusFrame Frame { get; }
    public IReadOnlyList<SampledValueAsdu> Asdus { get; }
}
