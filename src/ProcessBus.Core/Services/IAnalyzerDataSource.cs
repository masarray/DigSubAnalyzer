using ProcessBus.Core.Models;

namespace ProcessBus.Core.Services;

public interface IAnalyzerDataSource
{
    string Name { get; }
    Task<AnalyzerSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
