using ProcessBus.Core.Models;
using System.Collections.ObjectModel;
using System.Threading;

namespace ProcessBus.Iec61850.Raw.Runtime;

/// <summary>
/// Immutable, per-publication view of one selected SV stream. All mutable analyzer
/// collections are copied before publication so UI, replay, and export consumers can
/// read a coherent generation without holding the analyzer lock.
/// </summary>
public sealed class SvRuntimeSnapshot
{
    internal SvRuntimeSnapshot(
        long generation,
        DateTime createdUtc,
        string? streamId,
        SvRuntimeIdentitySnapshot? identity,
        IReadOnlyList<SvRuntimeChannelSnapshot> channels,
        SvRuntimeDiagnosticsSnapshot diagnostics,
        int samplesPerCycle,
        double sampleRateHz,
        double measuredFrequencyHz,
        double windowDurationMilliseconds,
        string waveformStatus,
        string shapeSeverity,
        string shapeStatus,
        bool hasShapeWarning)
    {
        Generation = generation;
        CreatedUtc = createdUtc;
        StreamId = streamId;
        Identity = identity;
        Channels = channels;
        Diagnostics = diagnostics;
        SamplesPerCycle = samplesPerCycle;
        SampleRateHz = sampleRateHz;
        MeasuredFrequencyHz = measuredFrequencyHz;
        WindowDurationMilliseconds = windowDurationMilliseconds;
        WaveformStatus = waveformStatus;
        ShapeSeverity = shapeSeverity;
        ShapeStatus = shapeStatus;
        HasShapeWarning = hasShapeWarning;
    }

    public long Generation { get; }
    public DateTime CreatedUtc { get; }
    public string? StreamId { get; }
    public SvRuntimeIdentitySnapshot? Identity { get; }
    public IReadOnlyList<SvRuntimeChannelSnapshot> Channels { get; }
    public SvRuntimeDiagnosticsSnapshot Diagnostics { get; }
    public int SamplesPerCycle { get; }
    public double SampleRateHz { get; }
    public double MeasuredFrequencyHz { get; }
    public double WindowDurationMilliseconds { get; }
    public string WaveformStatus { get; }
    public string ShapeSeverity { get; }
    public string ShapeStatus { get; }
    public bool HasShapeWarning { get; }

    public static SvRuntimeSnapshot Empty { get; } = new(
        generation: 0,
        createdUtc: DateTime.UnixEpoch,
        streamId: null,
        identity: null,
        channels: Array.AsReadOnly(Array.Empty<SvRuntimeChannelSnapshot>()),
        diagnostics: SvRuntimeDiagnosticsSnapshot.Empty,
        samplesPerCycle: 0,
        sampleRateHz: 0,
        measuredFrequencyHz: 0,
        windowDurationMilliseconds: 0,
        waveformStatus: "No runtime snapshot published.",
        shapeSeverity: "Unknown",
        shapeStatus: "Shape pending",
        hasShapeWarning: false);
}

public sealed record SvRuntimeIdentitySnapshot(
    string StreamName,
    string SvId,
    string DataSet,
    string AppId,
    string SourceMac,
    string DestinationMac,
    string VlanText,
    string SmpRateText,
    string ConfRevText,
    string MappingProfileName);

public sealed class SvRuntimeChannelSnapshot
{
    internal SvRuntimeChannelSnapshot(
        string name,
        string unit,
        double? instantValue,
        double? rmsValue,
        double? angleDegrees,
        IReadOnlyList<double> samples,
        string shapeSeverity,
        string shapeStatus,
        double shapeResidualPercent,
        double crestFactor,
        bool hasShapeDistortion)
    {
        Name = name;
        Unit = unit;
        InstantValue = instantValue;
        RmsValue = rmsValue;
        AngleDegrees = angleDegrees;
        Samples = samples;
        ShapeSeverity = shapeSeverity;
        ShapeStatus = shapeStatus;
        ShapeResidualPercent = shapeResidualPercent;
        CrestFactor = crestFactor;
        HasShapeDistortion = hasShapeDistortion;
    }

    public string Name { get; }
    public string Unit { get; }
    public double? InstantValue { get; }
    public double? RmsValue { get; }
    public double? AngleDegrees { get; }
    public IReadOnlyList<double> Samples { get; }
    public string ShapeSeverity { get; }
    public string ShapeStatus { get; }
    public double ShapeResidualPercent { get; }
    public double CrestFactor { get; }
    public bool HasShapeDistortion { get; }
}

public sealed record SvRuntimeDiagnosticsSnapshot(
    bool IsRunning,
    string StreamStatus,
    long TotalPackets,
    long DecodeErrors,
    long SequenceErrors,
    long MissingSamples,
    int? LastSampleCount,
    double? PacketRatePps,
    double? CurrentDeltaMicroseconds,
    double? AverageDeltaMicroseconds,
    double? ExpectedDeltaMicroseconds,
    double? CurrentJitterMicroseconds,
    double? MaxAbsJitterMicroseconds,
    DateTime? LastPacketTimestampUtc)
{
    public static SvRuntimeDiagnosticsSnapshot Empty { get; } = new(
        false,
        "No stream selected",
        0,
        0,
        0,
        0,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null);
}

public static class SvRuntimeSnapshotFactory
{
    public static SvRuntimeSnapshot Create(AnalyzerSnapshot source, long generation, DateTime? createdUtc = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (generation < 0)
            throw new ArgumentOutOfRangeException(nameof(generation));

        var waveformByName = source.Waveform.VoltageSeries
            .Concat(source.Waveform.CurrentSeries)
            .GroupBy(series => series.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var analogChannels = new[]
        {
            source.AnalogValues.Ia,
            source.AnalogValues.Ib,
            source.AnalogValues.Ic,
            source.AnalogValues.In,
            source.AnalogValues.Ua,
            source.AnalogValues.Ub,
            source.AnalogValues.Uc,
            source.AnalogValues.Un
        };
        var analogByName = analogChannels.ToDictionary(channel => channel.Name, StringComparer.OrdinalIgnoreCase);

        var orderedNames = source.Waveform.VoltageSeries
            .Concat(source.Waveform.CurrentSeries)
            .Select(series => series.Name)
            .Concat(analogChannels.Select(channel => channel.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var channelCopies = orderedNames.Select(name =>
        {
            waveformByName.TryGetValue(name, out var series);
            analogByName.TryGetValue(name, out var analog);

            var sampleCopy = series?.Samples.ToArray() ?? Array.Empty<double>();
            var samples = Array.AsReadOnly(sampleCopy);

            return new SvRuntimeChannelSnapshot(
                name,
                analog?.Unit ?? series?.Unit ?? string.Empty,
                analog?.InstantValue,
                analog?.RmsValue,
                analog?.AngleDegrees,
                samples,
                series?.ShapeSeverity ?? "Unknown",
                series?.ShapeStatusText ?? "Shape pending",
                series?.ShapeResidualPercent ?? 0,
                series?.CrestFactor ?? 0,
                series?.HasShapeDistortion ?? false);
        }).ToArray();

        var details = source.SelectedStreamDetails;
        var identity = details is null
            ? null
            : new SvRuntimeIdentitySnapshot(
                details.StreamName,
                details.SvId,
                details.DataSet,
                details.AppId,
                details.SourceMac,
                details.DestinationMac,
                details.VlanText,
                details.SmpRateText,
                details.ConfRevText,
                details.SampleValueMappingText);

        var diagnostics = source.Diagnostics;
        var diagnosticCopy = new SvRuntimeDiagnosticsSnapshot(
            diagnostics.IsRunning,
            diagnostics.StreamStatusText,
            diagnostics.TotalPackets,
            diagnostics.DecodeErrors,
            diagnostics.SequenceErrors,
            diagnostics.MissingSamples,
            diagnostics.LastSampleCount,
            diagnostics.PacketRatePps,
            diagnostics.CurrentDeltaMicroseconds,
            diagnostics.AverageDeltaMicroseconds,
            diagnostics.ExpectedDeltaMicroseconds,
            diagnostics.CurrentJitterMicroseconds,
            diagnostics.MaxAbsJitterMicroseconds,
            diagnostics.LastPacketTimestampUtc);

        return new SvRuntimeSnapshot(
            generation,
            createdUtc ?? DateTime.UtcNow,
            source.SelectedStreamId,
            identity,
            Array.AsReadOnly(channelCopies),
            diagnosticCopy,
            source.Waveform.SamplesPerCycle,
            source.Waveform.SampleRateHz,
            source.Waveform.MeasuredFrequencyHz,
            source.Waveform.WindowDurationMilliseconds,
            source.Waveform.StatusText,
            source.Waveform.ShapeSeverity,
            source.Waveform.ShapeStatusText,
            source.Waveform.HasShapeWarning);
    }
}

/// <summary>
/// Lock-free publication point for immutable runtime generations. A consumer sees
/// either the previous complete generation or the next complete generation, never
/// a partially assembled mix.
/// </summary>
public sealed class SvRuntimeSnapshotPublisher
{
    private long _generation;
    private SvRuntimeSnapshot _latest = SvRuntimeSnapshot.Empty;

    public SvRuntimeSnapshot Latest => Volatile.Read(ref _latest);

    public SvRuntimeSnapshot Publish(AnalyzerSnapshot source, DateTime? createdUtc = null)
    {
        var generation = Interlocked.Increment(ref _generation);
        var next = SvRuntimeSnapshotFactory.Create(source, generation, createdUtc);
        Volatile.Write(ref _latest, next);
        return next;
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _generation, 0);
        Volatile.Write(ref _latest, SvRuntimeSnapshot.Empty);
    }
}
