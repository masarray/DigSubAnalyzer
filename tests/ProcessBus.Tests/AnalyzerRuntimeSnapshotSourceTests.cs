using ProcessBus.Core.Models;
using ProcessBus.Core.Services;
using ProcessBus.Iec61850.Raw.Runtime;
using Xunit;

namespace ProcessBus.Tests;

public sealed class AnalyzerRuntimeSnapshotSourceTests
{
    [Fact]
    [Trait("Category", "RuntimeArchitecture")]
    public async Task Adapter_PublishesAtomicGenerationsFromAnyAnalyzerSource()
    {
        var source = new SequenceDataSource(
            Snapshot("MU_LIVE_ADAPTER", 1000),
            Snapshot("MU_LIVE_ADAPTER", 2000));
        var adapter = new AnalyzerRuntimeSnapshotSource(source);

        var first = await adapter.GetSnapshotAsync();
        var second = await adapter.GetSnapshotAsync();

        Assert.Equal("test-live-source", adapter.Name);
        Assert.Equal(1, first.Generation);
        Assert.Equal(2, second.Generation);
        Assert.Same(second, adapter.Latest);
        Assert.InRange(Channel(first, "Ia").Samples.Max(), 999, 1001);
        Assert.InRange(Channel(second, "Ia").Samples.Max(), 1999, 2001);
        Assert.InRange(Channel(first, "Ia").Samples.Max(), 999, 1001);
    }

    [Fact]
    [Trait("Category", "RuntimeArchitecture")]
    public async Task Adapter_Reset_ClearsPublishedGeneration()
    {
        var adapter = new AnalyzerRuntimeSnapshotSource(new SequenceDataSource(Snapshot("MU_RESET", 500)));

        var published = await adapter.GetSnapshotAsync();
        Assert.Equal(1, published.Generation);

        adapter.Reset();

        Assert.Same(SvRuntimeSnapshot.Empty, adapter.Latest);
        Assert.Equal(0, adapter.Latest.Generation);
    }

    private static AnalyzerSnapshot Snapshot(string svId, double peak)
    {
        var samples = Array.AsReadOnly(new[] { 0.0, peak, 0.0, -peak });
        return new AnalyzerSnapshot
        {
            SelectedStreamId = $"stream:{svId}",
            SelectedStreamDetails = new StreamDetailsModel
            {
                StreamName = svId,
                SvId = svId,
                DataSet = "LD0/LLN0$Dataset1",
                AppId = "0x4000",
                SourceMac = "00:00:00:00:00:01",
                DestinationMac = "01:0C:CD:04:00:01",
                VlanText = "100 / Priority 4",
                SmpRateText = "4000",
                ConfRevText = "1",
                SampleValueMappingText = "4I4V"
            },
            AnalogValues = new AnalogValuesSnapshot
            {
                Ia = new ChannelValueModel("Ia")
                {
                    InstantValue = peak,
                    RmsValue = peak / Math.Sqrt(2),
                    AngleDegrees = 0,
                    Unit = "A"
                }
            },
            Waveform = new WaveformSnapshot
            {
                CurrentSeries = new[]
                {
                    new WaveformSeriesModel
                    {
                        Name = "Ia",
                        Unit = "A",
                        Samples = samples,
                        ShapeSeverity = "Normal",
                        ShapeStatusText = "Sinusoidal",
                        CrestFactor = Math.Sqrt(2)
                    }
                },
                SampleRateHz = 4000,
                MeasuredFrequencyHz = 50,
                SamplesPerCycle = 80,
                WindowDurationMilliseconds = 40,
                StatusText = "Coherent window",
                ShapeSeverity = "Normal",
                ShapeStatusText = "Sinusoidal"
            },
            Diagnostics = new SvDiagnosticsSnapshot
            {
                IsRunning = true,
                StreamStatusText = "Raw SV stream active",
                TotalPackets = 160,
                LastSampleCount = 159
            }
        };
    }

    private static SvRuntimeChannelSnapshot Channel(SvRuntimeSnapshot snapshot, string name)
        => snapshot.Channels.Single(channel => string.Equals(channel.Name, name, StringComparison.OrdinalIgnoreCase));

    private sealed class SequenceDataSource : IAnalyzerDataSource
    {
        private readonly Queue<AnalyzerSnapshot> _snapshots;
        private AnalyzerSnapshot? _last;

        public SequenceDataSource(params AnalyzerSnapshot[] snapshots)
        {
            _snapshots = new Queue<AnalyzerSnapshot>(snapshots);
        }

        public string Name => "test-live-source";

        public Task<AnalyzerSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_snapshots.Count > 0)
                _last = _snapshots.Dequeue();

            return Task.FromResult(_last ?? new AnalyzerSnapshot());
        }
    }
}
