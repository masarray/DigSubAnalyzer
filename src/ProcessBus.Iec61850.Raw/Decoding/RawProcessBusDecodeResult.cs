using ProcessBus.Iec61850.Raw.Goose;
using ProcessBus.Iec61850.Raw.Protocol;
using ProcessBus.Iec61850.Raw.Ptp;
using ProcessBus.Iec61850.Raw.Sv;

namespace ProcessBus.Iec61850.Raw.Decoding;

public sealed class RawProcessBusDecodeResult
{
    public RawProcessBusDecodeResult(ProcessBusFrame frame, SampledValuePacket? sampledValues, GoosePacket? goose, PtpMessage? ptp)
    {
        Frame = frame;
        SampledValues = sampledValues;
        Goose = goose;
        Ptp = ptp;
    }

    public ProcessBusFrame Frame { get; }
    public SampledValuePacket? SampledValues { get; }
    public GoosePacket? Goose { get; }
    public PtpMessage? Ptp { get; }
    public bool HasDecodedApdu => SampledValues is not null || Goose is not null || Ptp is not null;
}
