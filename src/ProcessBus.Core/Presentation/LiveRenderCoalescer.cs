using ProcessBus.Core.Models;

namespace ProcessBus.Core.Presentation;

/// <summary>
/// Compares published engineering values, not object references. A capture snapshot
/// may be reconstructed at every tick even when the observable signal is unchanged.
/// Never quantize: small real transients and quality-driven clearing must be rendered.
/// Intended for one WPF dispatcher/UI owner, not shared across threads.
/// </summary>
public sealed class LiveRenderCoalescer
{
    private string? _waveformStream;
    private WaveformSnapshot? _lastWaveform;
    private string? _analogStream;
    private AnalogValuesSnapshot? _lastAnalog;

    public bool PublishWaveform(string? streamId, WaveformSnapshot waveform)
    {
        if (string.Equals(_waveformStream, streamId, StringComparison.Ordinal) &&
            _lastWaveform is not null && SameWaveform(_lastWaveform, waveform))
            return false;

        _waveformStream = streamId;
        _lastWaveform = waveform;
        return true;
    }

    public bool PublishAnalog(string? streamId, AnalogValuesSnapshot analog)
    {
        if (string.Equals(_analogStream, streamId, StringComparison.Ordinal) &&
            _lastAnalog is not null && SameAnalog(_lastAnalog, analog))
            return false;

        _analogStream = streamId;
        _lastAnalog = analog;
        return true;
    }

    public void Reset()
    {
        _waveformStream = null;
        _lastWaveform = null;
        _analogStream = null;
        _lastAnalog = null;
    }

    public static bool SameAnalog(AnalogValuesSnapshot left, AnalogValuesSnapshot right) =>
        SameChannel(left.Ia, right.Ia) && SameChannel(left.Ib, right.Ib) &&
        SameChannel(left.Ic, right.Ic) && SameChannel(left.In, right.In) &&
        SameChannel(left.Ua, right.Ua) && SameChannel(left.Ub, right.Ub) &&
        SameChannel(left.Uc, right.Uc) && SameChannel(left.Un, right.Un);

    /// <summary>
    /// Only RMS magnitude, electrical angle and engineering unit define a displayed
    /// phasor. A new instantaneous SV sample must update the digital readout without
    /// forcing six arrow/label recalculations when the phasors themselves are unchanged.
    /// No quantization: genuine small angle or magnitude changes still redraw.
    /// </summary>
    public static bool SamePhasors(AnalogValuesSnapshot left, AnalogValuesSnapshot right) =>
        SamePhasorChannel(left.Ua, right.Ua) &&
        SamePhasorChannel(left.Ub, right.Ub) &&
        SamePhasorChannel(left.Uc, right.Uc) &&
        SamePhasorChannel(left.Ia, right.Ia) &&
        SamePhasorChannel(left.Ib, right.Ib) &&
        SamePhasorChannel(left.Ic, right.Ic);

    private static bool SamePhasorChannel(ChannelValueModel a, ChannelValueModel b) =>
        a.RmsValue == b.RmsValue &&
        a.AngleDegrees == b.AngleDegrees &&
        string.Equals(a.Unit, b.Unit, StringComparison.Ordinal);

    private static bool SameChannel(ChannelValueModel a, ChannelValueModel b) =>
        a.InstantValue == b.InstantValue && a.RmsValue == b.RmsValue &&
        a.AngleDegrees == b.AngleDegrees &&
        string.Equals(a.Unit, b.Unit, StringComparison.Ordinal);

    private static bool SameWaveform(WaveformSnapshot a, WaveformSnapshot b) =>
        a.SampleRateHz == b.SampleRateHz &&
        a.MeasuredFrequencyHz == b.MeasuredFrequencyHz &&
        a.SamplesPerCycle == b.SamplesPerCycle &&
        a.WindowDurationMilliseconds == b.WindowDurationMilliseconds &&
        a.HasShapeWarning == b.HasShapeWarning &&
        a.IsReconstructed == b.IsReconstructed &&
        string.Equals(a.StatusText, b.StatusText, StringComparison.Ordinal) &&
        string.Equals(a.ShapeStatusText, b.ShapeStatusText, StringComparison.Ordinal) &&
        string.Equals(a.ShapeSeverity, b.ShapeSeverity, StringComparison.Ordinal) &&
        SameSeries(a.VoltageSeries, b.VoltageSeries) &&
        SameSeries(a.CurrentSeries, b.CurrentSeries);

    private static bool SameSeries(IReadOnlyList<WaveformSeriesModel> a, IReadOnlyList<WaveformSeriesModel> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
        {
            var x = a[i];
            var y = b[i];
            if (!string.Equals(x.Name, y.Name, StringComparison.Ordinal) ||
                !string.Equals(x.Unit, y.Unit, StringComparison.Ordinal) ||
                !string.Equals(x.ShapeSeverity, y.ShapeSeverity, StringComparison.Ordinal) ||
                !string.Equals(x.ShapeStatusText, y.ShapeStatusText, StringComparison.Ordinal) ||
                x.ShapeResidualPercent != y.ShapeResidualPercent ||
                x.CrestFactor != y.CrestFactor ||
                x.HasShapeDistortion != y.HasShapeDistortion ||
                x.Samples.Count != y.Samples.Count)
                return false;

            for (var sample = 0; sample < x.Samples.Count; sample++)
                if (x.Samples[sample] != y.Samples[sample])
                    return false;
        }
        return true;
    }
}
