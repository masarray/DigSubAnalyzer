using ProcessBus.Core.Models;
using ProcessBus.Iec61850.Raw.Analysis;
using ProcessBus.Iec61850.Raw.Runtime;
using System.Diagnostics;

namespace ProcessBus.Iec61850.Raw.Replay;

/// <summary>
/// Deterministic offline replay that uses the same RawProcessBusAnalyzer frame entry
/// point as live Npcap capture. Replay is therefore a reproducibility path, not a
/// second protocol implementation.
/// </summary>
public sealed class ProcessBusReplaySession
{
    private readonly PcapReplayReader _reader;
    private readonly SvRuntimeSnapshotPublisher _snapshotPublisher = new();

    public ProcessBusReplaySession(
        RawProcessBusAnalyzer? analyzer = null,
        PcapReplayReader? reader = null)
    {
        Analyzer = analyzer ?? new RawProcessBusAnalyzer();
        _reader = reader ?? new PcapReplayReader();
    }

    public RawProcessBusAnalyzer Analyzer { get; }
    public SvRuntimeSnapshotPublisher Snapshots => _snapshotPublisher;

    public ProcessBusReplayResult Replay(
        Stream pcapStream,
        ProcessBusReplayOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ProcessBusReplayOptions();
        if (options.MaximumFrames <= 0)
            throw new ArgumentOutOfRangeException(nameof(options.MaximumFrames));

        if (options.ResetAnalyzer)
        {
            Analyzer.Reset();
            _snapshotPublisher.Reset();
        }

        var startedUtc = DateTime.UtcNow;
        DateTime? firstCaptureUtc = null;
        DateTime? lastCaptureUtc = null;
        var framesRead = 0L;

        foreach (var frame in _reader.Read(pcapStream, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (framesRead >= options.MaximumFrames)
                break;

            firstCaptureUtc ??= frame.CaptureTimeUtc;
            lastCaptureUtc = frame.CaptureTimeUtc;

            var elapsed = frame.CaptureTimeUtc - firstCaptureUtc.Value;
            var replayTicks = checked((long)Math.Round(elapsed.TotalSeconds * Stopwatch.Frequency));

            Analyzer.ObserveOwnedFrame(
                frame.FrameBytes.ToArray(),
                frame.CaptureTimeUtc,
                replayTicks);

            framesRead++;
        }

        var analyzerSnapshot = Analyzer.GetAnalyzerSnapshot();
        var runtimeSnapshot = _snapshotPublisher.Publish(analyzerSnapshot);
        var monitor = analyzerSnapshot.ProtocolMonitor;

        return new ProcessBusReplayResult(
            framesRead,
            startedUtc,
            DateTime.UtcNow,
            firstCaptureUtc,
            lastCaptureUtc,
            monitor.TotalFrames,
            monitor.SvFrames,
            monitor.GooseFrames,
            monitor.PtpFrames,
            analyzerSnapshot.Diagnostics.DecodeErrors,
            analyzerSnapshot,
            runtimeSnapshot);
    }

    public SvRuntimeSnapshot PublishSelectedStreamSnapshot(DateTime? createdUtc = null)
        => _snapshotPublisher.Publish(Analyzer.GetAnalyzerSnapshot(), createdUtc);
}

public sealed class ProcessBusReplayOptions
{
    public bool ResetAnalyzer { get; init; } = true;
    public int MaximumFrames { get; init; } = int.MaxValue;
}

public sealed record ProcessBusReplayResult(
    long FramesRead,
    DateTime StartedUtc,
    DateTime CompletedUtc,
    DateTime? FirstCaptureUtc,
    DateTime? LastCaptureUtc,
    long TotalDecodedFrames,
    long SvFrames,
    long GooseFrames,
    long PtpFrames,
    long DecodeErrors,
    AnalyzerSnapshot AnalyzerSnapshot,
    SvRuntimeSnapshot RuntimeSnapshot)
{
    public TimeSpan ReplayDuration => CompletedUtc - StartedUtc;
    public TimeSpan? CaptureDuration => FirstCaptureUtc.HasValue && LastCaptureUtc.HasValue
        ? LastCaptureUtc.Value - FirstCaptureUtc.Value
        : null;
}
