namespace ProcessBus.Iec61850.Raw.Capture;

public interface IRawFrameSource : IAsyncDisposable
{
    IAsyncEnumerable<RawCapturedFrame> ReadFramesAsync(CancellationToken cancellationToken = default);
}
