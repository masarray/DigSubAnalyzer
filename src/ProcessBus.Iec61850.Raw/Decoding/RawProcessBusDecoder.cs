using ProcessBus.Iec61850.Raw.Goose;
using ProcessBus.Iec61850.Raw.Protocol;
using ProcessBus.Iec61850.Raw.Ptp;
using ProcessBus.Iec61850.Raw.Sv;

namespace ProcessBus.Iec61850.Raw.Decoding;

public static class RawProcessBusDecoder
{
    public static bool TryDecode(
        ReadOnlyMemory<byte> frameBytes,
        out RawProcessBusDecodeResult result,
        DateTime? captureTimeUtc = null,
        long? captureTicks = null)
    {
        result = null!;

        if (!ProcessBusFrameParser.TryParse(frameBytes, out var frame, captureTimeUtc, captureTicks))
            return false;

        SampledValuePacket? sampledValues = null;
        GoosePacket? goose = null;
        PtpMessage? ptp = null;

        switch (frame.Kind)
        {
            case ProcessBusFrameKind.SampledValues:
                SampledValueParser.TryParse(frame, out sampledValues);
                break;
            case ProcessBusFrameKind.Goose:
                GooseParser.TryParse(frame, out goose);
                break;
            case ProcessBusFrameKind.Ptp:
                PtpPacketParser.TryParse(frame, out ptp);
                break;
        }

        result = new RawProcessBusDecodeResult(frame, sampledValues, goose, ptp);
        return true;
    }
}
