using ProcessBus.Core.Models;

namespace ProcessBus.Core.Services;

public interface IRawCaptureDataSource : IAnalyzerDataSource
{
    bool IsRunning { get; }
    void SelectAdapter(string adapterId, string rawDeviceName);
    void SelectStream(string? streamId);
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    void ClearRuntimeState();
    GooseMonitorSnapshot GetGooseSnapshot();
}
