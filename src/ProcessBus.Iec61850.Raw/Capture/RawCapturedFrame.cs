namespace ProcessBus.Iec61850.Raw.Capture;

public sealed class RawCapturedFrame
{
    public RawCapturedFrame(ReadOnlyMemory<byte> bytes, DateTime captureTimeUtc, long captureTicks)
    {
        Bytes = bytes;
        CaptureTimeUtc = captureTimeUtc;
        CaptureTicks = captureTicks;
    }

    public ReadOnlyMemory<byte> Bytes { get; }
    public DateTime CaptureTimeUtc { get; }
    public long CaptureTicks { get; }
}
