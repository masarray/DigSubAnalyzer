using System.Collections.Concurrent;
using ProcessBus.Core.Models;
using ProcessBus.Core.Services;
using ProcessBus.Iec61850.Raw.Analysis;
using ProcessBus.Iec61850.Raw.Capture;

namespace ProcessBus.Iec61850.Raw.Live;

public sealed class RawAnalyzerDataSource : IRawCaptureDataSource, IDisposable
{
    private readonly object _sync = new();
    private readonly RawProcessBusAnalyzer _analyzer = new();
    private readonly ConcurrentQueue<DiagnosticEventItem> _events = new();
    private CancellationTokenSource? _captureCts;
    private Task? _captureTask;
    private IRawFrameSource? _frameSource;
    private string _adapterId = string.Empty;
    private string _adapterRawDeviceName = string.Empty;
    private bool _isRunning;
    private bool _isDisposed;
    private long _capturedFrames;

    public string Name => "Raw Passive";

    public bool IsRunning
    {
        get
        {
            lock (_sync)
                return _isRunning;
        }
    }

    public void SelectAdapter(string adapterId, string rawDeviceName)
    {
        lock (_sync)
        {
            _adapterId = adapterId;
            _adapterRawDeviceName = rawDeviceName;
        }

        EnqueueEvent("Info", $"Raw adapter selected: {rawDeviceName}");
    }

    public void SelectStream(string? streamId)
    {
        _analyzer.SelectStream(streamId);
    }

    public void SetSvChannelMappings(IReadOnlyList<SvChannelMappingProfile> profiles)
    {
        _analyzer.SetSvChannelMappings(profiles);
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_isDisposed || _isRunning)
                return Task.CompletedTask;

            if (string.IsNullOrWhiteSpace(_adapterRawDeviceName) ||
                _adapterRawDeviceName.StartsWith("index:", StringComparison.OrdinalIgnoreCase))
            {
                EnqueueEvent("Error", "Raw capture cannot start: select a real Npcap Ethernet adapter. The current adapter is empty, fallback, or loopback-style index only.");
                return Task.CompletedTask;
            }

            _captureCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _frameSource = new NpcapRawFrameSource(_adapterRawDeviceName, EnqueueEvent);
            _captureTask = Task.Run(() => PumpAsync(_frameSource, _captureCts.Token), CancellationToken.None);
            _isRunning = true;
        }

        EnqueueEvent("Info", $"Raw process-bus capture started. Adapter={_adapterId}");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? captureTask;
        IRawFrameSource? frameSource;
        CancellationTokenSource? cts;

        lock (_sync)
        {
            if (!_isRunning && _captureTask is null)
                return;

            _isRunning = false;
            captureTask = _captureTask;
            frameSource = _frameSource;
            cts = _captureCts;
            _captureTask = null;
            _frameSource = null;
            _captureCts = null;
        }

        cts?.Cancel();

        if (captureTask is not null)
        {
            try
            {
                await captureTask.WaitAsync(TimeSpan.FromMilliseconds(750), cancellationToken);
            }
            catch (TimeoutException)
            {
                EnqueueEvent("Warning", "Raw capture stop timed out; forcing Npcap reader disposal.");
                if (frameSource is not null)
                    await frameSource.DisposeAsync();

                try
                {
                    await captureTask.WaitAsync(TimeSpan.FromMilliseconds(750), CancellationToken.None);
                }
                catch
                {
                    // Do not keep app shutdown blocked by a capture reader.
                }

                frameSource = null;
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (frameSource is not null)
            await frameSource.DisposeAsync();

        cts?.Dispose();
        EnqueueEvent("Info", "Raw process-bus capture stopped.");
    }

    public void ClearRuntimeState()
    {
        _analyzer.Reset();

        while (_events.TryDequeue(out _))
        {
        }

        EnqueueEvent("Info", "Raw process-bus runtime state cleared.");
    }

    public Task<AnalyzerSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = _analyzer.GetAnalyzerSnapshot();
        var events = DrainEvents()
            .Concat(snapshot.Events)
            .OrderByDescending(x => x.TimestampUtc)
            .Take(100)
            .ToArray();
        var diagnostics = snapshot.Diagnostics;
        var selectedStreamDetails = snapshot.SelectedStreamDetails;

        if (snapshot.Streams.Count == 0)
        {
            diagnostics.IsRunning = IsRunning;
            diagnostics.TotalPackets = Math.Max(diagnostics.TotalPackets, Interlocked.Read(ref _capturedFrames));
            diagnostics.StreamStatusText = _isRunning
                ? $"Raw capture running: {diagnostics.TotalPackets} frame(s), no decoded SV stream yet"
                : diagnostics.StreamStatusText;
            diagnostics.DecodeStatusText = diagnostics.TotalPackets == 0
                ? "Raw capture has not received frames yet"
                : "Raw capture received frames, but none decoded as SV";
            diagnostics.FrequencyRejectReason = BuildRawRejectReason(diagnostics, events);
            selectedStreamDetails = BuildRawWaitingDetails(diagnostics, events);
        }

        return Task.FromResult(new AnalyzerSnapshot
        {
            Streams = snapshot.Streams,
            SelectedStreamDetails = selectedStreamDetails,
            AnalogValues = snapshot.AnalogValues,
            Waveform = snapshot.Waveform,
            Diagnostics = diagnostics,
            Events = events,
            ProtocolMonitor = snapshot.ProtocolMonitor,
            PtpEvents = snapshot.PtpEvents
        });
    }

    public GooseMonitorSnapshot GetGooseSnapshot()
    {
        return _analyzer.GetGooseSnapshot();
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        StopAsync().GetAwaiter().GetResult();
    }

    private async Task PumpAsync(IRawFrameSource frameSource, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in frameSource.ReadFramesAsync(cancellationToken))
            {
                Interlocked.Increment(ref _capturedFrames);
                _analyzer.ObserveFrame(frame.Bytes, frame.CaptureTimeUtc, frame.CaptureTicks);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            EnqueueEvent("Error", $"Raw capture failed: {ex.Message}");
        }
        finally
        {
            lock (_sync)
                _isRunning = false;
        }
    }

    private void EnqueueEvent(string severity, string message)
    {
        _events.Enqueue(new DiagnosticEventItem
        {
            TimestampUtc = DateTime.UtcNow,
            Severity = severity,
            Message = message
        });

        while (_events.Count > 100 && _events.TryDequeue(out _))
        {
        }
    }

    private IReadOnlyList<DiagnosticEventItem> DrainEvents()
    {
        var result = new List<DiagnosticEventItem>();

        while (_events.TryDequeue(out var item))
            result.Add(item);

        return result;
    }

    private static StreamDetailsModel BuildRawWaitingDetails(
        SvDiagnosticsSnapshot diagnostics,
        IReadOnlyList<DiagnosticEventItem> events)
    {
        var latestEvent = events.FirstOrDefault();
        var eventText = latestEvent is null
            ? "No raw capture event yet"
            : $"{latestEvent.Severity}: {latestEvent.Message}";

        return new StreamDetailsModel
        {
            StreamName = "Raw passive capture",
            SvId = "N/A",
            DataSet = "N/A",
            AppId = "N/A",
            SourceMac = "N/A",
            DestinationMac = "N/A",
            VlanText = "N/A",
            SmpRateText = "N/A",
            ConfRevText = "N/A",
            SampleValueMappingText = diagnostics.StreamStatusText,
            SampleValueCountText = diagnostics.TotalPackets.ToString(),
            MappedChannelNamesText = "No decoded SV ASDU yet",
            RawValuesText = eventText,
            PacketEvidenceText = string.Join("; ", new[]
            {
                $"running={diagnostics.IsRunning}",
                $"capturedFrames={diagnostics.TotalPackets}",
                $"decodeErrors={diagnostics.DecodeErrors}",
                $"decode={diagnostics.DecodeStatusText}"
            }),
            TimebaseStatusText = diagnostics.TimebaseStatusText,
            RmsDebugText = diagnostics.PacketRatePps.HasValue
                ? $"Raw packet rate={diagnostics.PacketRatePps.Value:0.###} fps"
                : "Raw packet rate pending",
            LastSeenText = diagnostics.LastPacketTimestampUtc?.ToLocalTime().ToString("HH:mm:ss.fff") ?? "No decoded packets"
        };
    }

    private static string BuildRawRejectReason(
        SvDiagnosticsSnapshot diagnostics,
        IReadOnlyList<DiagnosticEventItem> events)
    {
        var latestEvent = events.FirstOrDefault();
        var eventText = latestEvent is null ? "no event" : latestEvent.Message;
        return $"{diagnostics.DecodeStatusText}; frames={diagnostics.TotalPackets}; errors={diagnostics.DecodeErrors}; last={eventText}";
    }
}
