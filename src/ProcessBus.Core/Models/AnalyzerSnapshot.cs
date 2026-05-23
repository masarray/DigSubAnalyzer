namespace ProcessBus.Core.Models;

public sealed class AnalyzerSnapshot
{
    public IReadOnlyList<SvStreamItem> Streams { get; init; } = Array.Empty<SvStreamItem>();
    public StreamDetailsModel? SelectedStreamDetails { get; init; }
    public AnalogValuesSnapshot AnalogValues { get; init; } = new();
    public WaveformSnapshot Waveform { get; init; } = new();
    public SvDiagnosticsSnapshot Diagnostics { get; init; } = new();
    public IReadOnlyList<DiagnosticEventItem> Events { get; init; } = Array.Empty<DiagnosticEventItem>();
    public ProtocolMonitorSnapshot ProtocolMonitor { get; init; } = new();
    public IReadOnlyList<PtpEventItem> PtpEvents { get; init; } = Array.Empty<PtpEventItem>();
}
