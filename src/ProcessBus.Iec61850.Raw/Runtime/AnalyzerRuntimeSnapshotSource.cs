using ProcessBus.Core.Services;

namespace ProcessBus.Iec61850.Raw.Runtime;

/// <summary>
/// Adapts any analyzer data source, including the live Npcap-backed source, to the
/// immutable runtime publication boundary. This enables staged consumer migration
/// without changing the existing capture contract or introducing a second decoder.
/// </summary>
public sealed class AnalyzerRuntimeSnapshotSource
{
    private readonly IAnalyzerDataSource _source;
    private readonly SvRuntimeSnapshotPublisher _publisher = new();

    public AnalyzerRuntimeSnapshotSource(IAnalyzerDataSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public string Name => _source.Name;
    public SvRuntimeSnapshot Latest => _publisher.Latest;

    public async Task<SvRuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var analyzerSnapshot = await _source.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return _publisher.Publish(analyzerSnapshot);
    }

    public void Reset() => _publisher.Reset();
}
