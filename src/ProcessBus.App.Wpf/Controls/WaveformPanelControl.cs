using System.Globalization;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ProcessBus.Core.Models;

namespace ProcessBus.App.Wpf.Controls;

public sealed class WaveformPanelControl : FrameworkElement
{
    private static readonly Brush PanelBrush = CreateBrush("#0C1B2B");
    private static readonly Brush PlotBackgroundBrush = CreateBrush("#071625");
    private static readonly Brush LaneHeaderBrush = CreateBrush("#0B1D31");
    private static readonly Brush TitleBrush = CreateBrush("#2B9CFF");
    private static readonly Brush LabelBrush = CreateBrush("#A7B8CB");
    private static readonly Brush MutedBrush = CreateBrush("#71869D");
    private static readonly Brush UaBrush = CreateBrush("#FF2945");
    private static readonly Brush UbBrush = CreateBrush("#F5B400");
    private static readonly Brush UcBrush = CreateBrush("#1878E8");
    private static readonly Brush NeutralBrush = CreateBrush("#607487");
    private static readonly Pen BorderPen = CreatePen("#1B344F", 1.0);
    private static readonly Pen GridPen = CreatePen("#24506F", 1.05, [2.0, 6.0]);
    private static readonly Pen MajorGridPen = CreatePen("#315F82", 1.15, [4.0, 6.0]);
    private static readonly Pen ZeroPen = CreatePen("#6F8FAE", 1.35);
    private static readonly Pen HighlightPen = CreatePen("#163B58", 1.0);
    private static readonly Brush AxisTextBrush = CreateBrush("#8FAAC3");
    private static readonly Pen CursorPen = CreatePen("#6ED2FF", 1.4);
    private static readonly Brush CursorDotBrush = CreateBrush("#2B9CFF");
    private static readonly Brush CursorFillBrush = CreateBrush("#D90B1D31");
    private static readonly Pen CursorBorderPen = CreatePen("#2B9CFF", 1.0);
    private static readonly Dictionary<string, SeriesPens> SeriesPenCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Typeface TitleTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Typeface LabelTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private double _cursorFraction = 0.5;
    private bool _isDraggingCursor;
    private double _voltageAutoPeak;
    private double _currentAutoPeak;
    private DateTime _voltagePeakUpdatedUtc = DateTime.MinValue;
    private DateTime _currentPeakUpdatedUtc = DateTime.MinValue;
    private readonly Stopwatch _smoothClock = new();
    private WaveformSnapshot? _fromSnapshot;
    private WaveformSnapshot? _targetSnapshot;
    private WaveformSnapshot? _visualSnapshot;
    private TimeSpan _lastVisualRender = TimeSpan.Zero;
    private bool _renderLoopAttached;

    private const double AutoScaleHeadroom = 1.28;
    private const double AutoScaleSmoothing = 0.18;
    private const double PeakHoldSeconds = 6.0;
    private const double SamplesPerPixel = 0.8;
    private const double VisualSmoothingMilliseconds = 300.0;
    private const double VisualRenderFps = 30.0;

    public WaveformPanelControl()
    {
        Focusable = true;
        Cursor = Cursors.Cross;
        Loaded += (_, _) => AttachRenderLoopIfNeeded();
        Unloaded += (_, _) => DetachRenderLoop();
    }

    public static readonly DependencyProperty SnapshotProperty =
        DependencyProperty.Register(
            nameof(Snapshot),
            typeof(WaveformSnapshot),
            typeof(WaveformPanelControl),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnSnapshotChanged));

    public static readonly DependencyProperty ShowModeProperty =
        DependencyProperty.Register(
            nameof(ShowMode),
            typeof(string),
            typeof(WaveformPanelControl),
            new FrameworkPropertyMetadata("Both", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LayoutModeProperty =
        DependencyProperty.Register(
            nameof(LayoutMode),
            typeof(string),
            typeof(WaveformPanelControl),
            new FrameworkPropertyMetadata("Overlay", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TimebaseModeProperty =
        DependencyProperty.Register(
            nameof(TimebaseMode),
            typeof(string),
            typeof(WaveformPanelControl),
            new FrameworkPropertyMetadata("4 cycles", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MarkersEnabledProperty =
        DependencyProperty.Register(
            nameof(MarkersEnabled),
            typeof(bool),
            typeof(WaveformPanelControl),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty VoltageScaleProperty =
        DependencyProperty.Register(
            nameof(VoltageScale),
            typeof(string),
            typeof(WaveformPanelControl),
            new FrameworkPropertyMetadata("Auto", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CurrentScaleProperty =
        DependencyProperty.Register(
            nameof(CurrentScale),
            typeof(string),
            typeof(WaveformPanelControl),
            new FrameworkPropertyMetadata("Auto", FrameworkPropertyMetadataOptions.AffectsRender));

    public WaveformSnapshot? Snapshot
    {
        get => (WaveformSnapshot?)GetValue(SnapshotProperty);
        set => SetValue(SnapshotProperty, value);
    }

    private static void OnSnapshotChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not WaveformPanelControl control)
            return;

        var next = args.NewValue as WaveformSnapshot;
        if (next is null)
        {
            control._fromSnapshot = null;
            control._targetSnapshot = null;
            control._visualSnapshot = null;
            control.DetachRenderLoop();
            control.InvalidateVisual();
            return;
        }

        var previousVisual = control._visualSnapshot ?? args.OldValue as WaveformSnapshot;
        if (previousVisual is null || !HasAnySeries(previousVisual))
        {
            control._visualSnapshot = next;
            control._targetSnapshot = next;
            control._fromSnapshot = next;
            control.InvalidateVisual();
            return;
        }

        control._fromSnapshot = previousVisual;
        control._targetSnapshot = next;
        control._smoothClock.Restart();
        control._lastVisualRender = TimeSpan.Zero;
        control.AttachRenderLoopIfNeeded();
    }

    public string ShowMode
    {
        get => (string)GetValue(ShowModeProperty);
        set => SetValue(ShowModeProperty, value);
    }

    public string LayoutMode
    {
        get => (string)GetValue(LayoutModeProperty);
        set => SetValue(LayoutModeProperty, value);
    }

    public string TimebaseMode
    {
        get => (string)GetValue(TimebaseModeProperty);
        set => SetValue(TimebaseModeProperty, value);
    }

    public bool MarkersEnabled
    {
        get => (bool)GetValue(MarkersEnabledProperty);
        set => SetValue(MarkersEnabledProperty, value);
    }

    public string VoltageScale
    {
        get => (string)GetValue(VoltageScaleProperty);
        set => SetValue(VoltageScaleProperty, value);
    }

    public string CurrentScale
    {
        get => (string)GetValue(CurrentScaleProperty);
        set => SetValue(CurrentScaleProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 900 : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? 430 : availableSize.Height;
        return new Size(Math.Max(480, width), Math.Max(320, height));
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var bounds = new Rect(new Point(0, 0), RenderSize);
        dc.DrawRectangle(PanelBrush, null, bounds);

        var snapshot = _visualSnapshot ?? Snapshot;
        if (snapshot is null || (snapshot.VoltageSeries.Count == 0 && snapshot.CurrentSeries.Count == 0))
        {
            DrawCenteredMessage(dc, bounds, "Waveform data pending");
            return;
        }

        var showVoltage = !string.Equals(ShowMode, "Current", StringComparison.OrdinalIgnoreCase);
        var showCurrent = !string.Equals(ShowMode, "Voltage", StringComparison.OrdinalIgnoreCase);

        var visibleLanes = new List<LaneDefinition>(2);
        if (showVoltage)
            visibleLanes.Add(new LaneDefinition("Voltage", "V", snapshot.VoltageSeries));
        if (showCurrent)
            visibleLanes.Add(new LaneDefinition("Current", "A", snapshot.CurrentSeries));

        if (visibleLanes.Count == 0)
        {
            DrawCenteredMessage(dc, bounds, "Waveform lanes hidden");
            return;
        }

        const double outerPadding = 10;
        const double laneGap = 12;
        var laneHeight = Math.Max(110, (bounds.Height - (outerPadding * 2) - (laneGap * (visibleLanes.Count - 1))) / visibleLanes.Count);
        var laneWidth = Math.Max(120, bounds.Width - (outerPadding * 2));
        var top = outerPadding;

        foreach (var lane in visibleLanes)
        {
            var laneRect = new Rect(outerPadding, top, laneWidth, laneHeight);
            DrawLane(dc, laneRect, lane, snapshot);
            top += laneHeight + laneGap;
        }
    }

    private void AttachRenderLoopIfNeeded()
    {
        if (_renderLoopAttached || !IsLoaded)
            return;

        CompositionTarget.Rendering += OnCompositionRendering;
        _renderLoopAttached = true;
    }

    private void DetachRenderLoop()
    {
        if (!_renderLoopAttached)
            return;

        CompositionTarget.Rendering -= OnCompositionRendering;
        _renderLoopAttached = false;
    }

    private void OnCompositionRendering(object? sender, EventArgs e)
    {
        if (_fromSnapshot is null || _targetSnapshot is null)
        {
            DetachRenderLoop();
            return;
        }

        var elapsed = _smoothClock.Elapsed;
        if (elapsed - _lastVisualRender < TimeSpan.FromMilliseconds(1000.0 / VisualRenderFps))
            return;

        _lastVisualRender = elapsed;
        var progress = Math.Clamp(elapsed.TotalMilliseconds / VisualSmoothingMilliseconds, 0.0, 1.0);
        var eased = EaseOutCubic(progress);
        _visualSnapshot = progress >= 1.0
            ? _targetSnapshot
            : InterpolateSnapshot(_fromSnapshot, _targetSnapshot, eased);

        InvalidateVisual();

        if (progress >= 1.0)
            DetachRenderLoop();
    }

    private static bool HasAnySeries(WaveformSnapshot snapshot)
    {
        return snapshot.VoltageSeries.Count > 0 || snapshot.CurrentSeries.Count > 0;
    }

    private static WaveformSnapshot InterpolateSnapshot(WaveformSnapshot from, WaveformSnapshot to, double amount)
    {
        return new WaveformSnapshot
        {
            VoltageSeries = InterpolateSeries(from.VoltageSeries, to.VoltageSeries, amount),
            CurrentSeries = InterpolateSeries(from.CurrentSeries, to.CurrentSeries, amount),
            SampleRateHz = Lerp(from.SampleRateHz, to.SampleRateHz, amount),
            MeasuredFrequencyHz = Lerp(from.MeasuredFrequencyHz, to.MeasuredFrequencyHz, amount),
            WindowDurationMilliseconds = Lerp(from.WindowDurationMilliseconds, to.WindowDurationMilliseconds, amount),
            StatusText = to.StatusText
        };
    }

    private static IReadOnlyList<WaveformSeriesModel> InterpolateSeries(
        IReadOnlyList<WaveformSeriesModel> fromSeries,
        IReadOnlyList<WaveformSeriesModel> toSeries,
        double amount)
    {
        if (toSeries.Count == 0)
            return Array.Empty<WaveformSeriesModel>();

        var fromByName = fromSeries.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        var result = new WaveformSeriesModel[toSeries.Count];
        for (var i = 0; i < toSeries.Count; i++)
        {
            var target = toSeries[i];
            if (!fromByName.TryGetValue(target.Name, out var source) || source.Samples.Count == 0 || target.Samples.Count == 0)
            {
                result[i] = target;
                continue;
            }

            var samples = new double[target.Samples.Count];
            for (var sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++)
            {
                var fraction = samples.Length <= 1 ? 0.0 : sampleIndex / (samples.Length - 1.0);
                var sourceValue = SampleAt(source.Samples, fraction);
                samples[sampleIndex] = Lerp(sourceValue, target.Samples[sampleIndex], amount);
            }

            result[i] = new WaveformSeriesModel
            {
                Name = target.Name,
                Unit = target.Unit,
                Samples = samples
            };
        }

        return result;
    }

    private static double SampleAt(IReadOnlyList<double> samples, double fraction)
    {
        if (samples.Count == 0)
            return 0;

        if (samples.Count == 1)
            return samples[0];

        var position = Math.Clamp(fraction, 0.0, 1.0) * (samples.Count - 1);
        var left = (int)Math.Floor(position);
        var right = Math.Min(samples.Count - 1, left + 1);
        var amount = position - left;
        return Lerp(samples[left], samples[right], amount);
    }

    private static double Lerp(double from, double to, double amount)
    {
        return from + ((to - from) * amount);
    }

    private static double EaseOutCubic(double amount)
    {
        var inverse = 1.0 - Math.Clamp(amount, 0.0, 1.0);
        return 1.0 - (inverse * inverse * inverse);
    }

    private void DrawLane(DrawingContext dc, Rect rect, LaneDefinition lane, WaveformSnapshot snapshot)
    {
        dc.DrawRoundedRectangle(PlotBackgroundBrush, BorderPen, rect, 10, 10);

        var headerRect = new Rect(rect.Left, rect.Top, rect.Width, 30);
        dc.DrawRoundedRectangle(LaneHeaderBrush, null, headerRect, 10, 10);
        dc.DrawRectangle(LaneHeaderBrush, null, new Rect(rect.Left, rect.Top + 15, rect.Width, 15));

        var innerRect = new Rect(rect.Left + 12, rect.Top + 38, rect.Width - 24, rect.Height - 50);
        if (innerRect.Width <= 20 || innerRect.Height <= 20)
            return;

        var laneSeries = BuildVisibleSeries(lane.Series, snapshot, innerRect.Width);
        var amplitudeMax = ResolveLaneAmplitudeMax(lane, laneSeries);

        dc.DrawText(CreateText(lane.Title, 13, TitleBrush, TitleTypeface), new Point(rect.Left + 12, rect.Top + 7));
        dc.DrawText(
            CreateText($"±{amplitudeMax:0.#} {lane.Unit}", 11, LabelBrush, LabelTypeface),
            new Point(rect.Right - 104, rect.Top + 8));

        DrawGrid(dc, innerRect);
        DrawYAxisScale(dc, innerRect, amplitudeMax, lane.Unit);

        if (laneSeries.Count == 0)
        {
            DrawCenteredMessage(dc, innerRect, $"{lane.Title} samples pending");
            return;
        }

        var stacked = string.Equals(LayoutMode, "Stacked", StringComparison.OrdinalIgnoreCase);
        for (var index = 0; index < laneSeries.Count; index++)
            DrawSeries(dc, innerRect, laneSeries[index], amplitudeMax, stacked, index, laneSeries.Count);

        // Cursor overlay is expensive during repaint/resize.
        // Re-enable later with cached text layout.
        if (MarkersEnabled)
            DrawCursor(dc, innerRect, laneSeries, amplitudeMax, stacked, snapshot);

        DrawTimebaseLabel(dc, innerRect, snapshot);
    }

    private IReadOnlyList<WaveformSeriesModel> BuildVisibleSeries(
        IReadOnlyList<WaveformSeriesModel> source,
        WaveformSnapshot snapshot,
        double plotWidth)
    {
        var visibleSamples = CalculateVisibleSamples(snapshot);

        if (visibleSamples <= 0)
            visibleSamples = source.Select(item => item.Samples.Count).DefaultIfEmpty(0).Max();

        var maxDisplaySamples = Math.Max(64, (int)Math.Ceiling(plotWidth * SamplesPerPixel));

        return source
            .Select(item =>
            {
                var samples = BuildDisplaySamples(item.Samples, visibleSamples, maxDisplaySamples);

                return new WaveformSeriesModel
                {
                    Name = item.Name,
                    Unit = item.Unit,
                    Samples = samples
                };
            })
            .Where(item => item.Samples.Count >= 2)
            .ToArray();
    }

    private static double[] BuildDisplaySamples(IReadOnlyList<double> source, int visibleSamples, int maxDisplaySamples)
    {
        var sourceCount = Math.Min(source.Count, visibleSamples);
        if (sourceCount <= 0)
            return Array.Empty<double>();

        if (sourceCount <= maxDisplaySamples)
        {
            var direct = new double[sourceCount];
            for (var i = 0; i < sourceCount; i++)
                direct[i] = source[i];

            return direct;
        }

        var bucketCount = Math.Max(2, maxDisplaySamples / 2);
        var result = new List<double>(bucketCount * 2);
        var step = sourceCount / (double)bucketCount;

        for (var bucket = 0; bucket < bucketCount; bucket++)
        {
            var start = (int)Math.Floor(bucket * step);
            var end = (int)Math.Floor((bucket + 1) * step);
            start = Math.Clamp(start, 0, sourceCount - 1);
            end = Math.Clamp(Math.Max(start + 1, end), start + 1, sourceCount);

            var min = double.PositiveInfinity;
            var max = double.NegativeInfinity;
            var minIndex = start;
            var maxIndex = start;

            for (var i = start; i < end; i++)
            {
                var sample = source[i];
                if (double.IsNaN(sample) || double.IsInfinity(sample))
                    sample = 0.0;

                if (sample < min)
                {
                    min = sample;
                    minIndex = i;
                }

                if (sample > max)
                {
                    max = sample;
                    maxIndex = i;
                }
            }

            if (minIndex <= maxIndex)
            {
                result.Add(min);
                result.Add(max);
            }
            else
            {
                result.Add(max);
                result.Add(min);
            }
        }

        return result.ToArray();
    }

    private double ResolveLaneAmplitudeMax(LaneDefinition lane, IReadOnlyList<WaveformSeriesModel> series)
    {
        var isVoltage = string.Equals(lane.Unit, "V", StringComparison.OrdinalIgnoreCase);
        var scaleText = isVoltage ? VoltageScale : CurrentScale;

        if (isVoltage && TryParseVoltageScalePeak(scaleText, out var fixedVoltagePeak))
            return Math.Max(fixedVoltagePeak, 1.0);

        if (!isVoltage && TryParseCurrentScalePeak(scaleText, out var fixedCurrentPeak))
            return Math.Max(fixedCurrentPeak, 1.0);

        var observedPeak = ResolveRobustObservedPeak(series);

        observedPeak = Math.Max(observedPeak, 1.0);

        var rmsPeakEstimate = ResolveRmsBasedPeak(series);
        var targetPeak = Math.Max(observedPeak * 1.08, rmsPeakEstimate * AutoScaleHeadroom);
        targetPeak = Math.Max(targetPeak, 1.0);

        if (string.Equals(scaleText, "Auto Peak Hold", StringComparison.OrdinalIgnoreCase))
            return ResolvePeakHoldScale(isVoltage, targetPeak);

        return ResolveSmoothedAutoScale(isVoltage, targetPeak);
    }

    private static double ResolveRmsBasedPeak(IReadOnlyList<WaveformSeriesModel> series)
    {
        var maxRms = 1.0;

        foreach (var item in series)
        {
            var count = 0;
            var sumSquare = 0.0;

            foreach (var sample in item.Samples)
            {
                if (double.IsNaN(sample) || double.IsInfinity(sample))
                    continue;

                sumSquare += sample * sample;
                count++;
            }

            if (count > 0)
                maxRms = Math.Max(maxRms, Math.Sqrt(sumSquare / count));
        }

        return maxRms * Math.Sqrt(2.0);
    }

    private static double ResolveRobustObservedPeak(IReadOnlyList<WaveformSeriesModel> series)
    {
        var maxPeak = 1.0;

        foreach (var item in series)
        {
            foreach (var sample in item.Samples)
            {
                if (double.IsNaN(sample) || double.IsInfinity(sample))
                    continue;

                maxPeak = Math.Max(maxPeak, Math.Abs(sample));
            }
        }

        return maxPeak;
    }

    private double ResolveSmoothedAutoScale(bool isVoltage, double targetPeak)
    {
        if (isVoltage)
        {
            if (_voltageAutoPeak <= 0)
                _voltageAutoPeak = targetPeak;

            _voltageAutoPeak = (_voltageAutoPeak * (1.0 - AutoScaleSmoothing)) + (targetPeak * AutoScaleSmoothing);
            return Math.Max(_voltageAutoPeak, 1.0);
        }

        if (_currentAutoPeak <= 0)
            _currentAutoPeak = targetPeak;

        _currentAutoPeak = (_currentAutoPeak * (1.0 - AutoScaleSmoothing)) + (targetPeak * AutoScaleSmoothing);
        return Math.Max(_currentAutoPeak, 1.0);
    }

    private double ResolvePeakHoldScale(bool isVoltage, double targetPeak)
    {
        var now = DateTime.UtcNow;

        if (isVoltage)
        {
            if (targetPeak >= _voltageAutoPeak || (now - _voltagePeakUpdatedUtc).TotalSeconds > PeakHoldSeconds)
            {
                _voltageAutoPeak = targetPeak;
                _voltagePeakUpdatedUtc = now;
            }
            else
            {
                _voltageAutoPeak = Math.Max(targetPeak, _voltageAutoPeak * 0.992);
            }

            return Math.Max(_voltageAutoPeak, 1.0);
        }

        if (targetPeak >= _currentAutoPeak || (now - _currentPeakUpdatedUtc).TotalSeconds > PeakHoldSeconds)
        {
            _currentAutoPeak = targetPeak;
            _currentPeakUpdatedUtc = now;
        }
        else
        {
            _currentAutoPeak = Math.Max(targetPeak, _currentAutoPeak * 0.992);
        }

        return Math.Max(_currentAutoPeak, 1.0);
    }

    private static bool TryParseVoltageScalePeak(string? scaleText, out double peak)
    {
        peak = 0.0;

        if (string.IsNullOrWhiteSpace(scaleText) ||
            string.Equals(scaleText, "Auto", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(scaleText, "Auto Peak Hold", StringComparison.OrdinalIgnoreCase))
            return false;

        var normalized = scaleText.Trim();

        if (normalized.EndsWith("kV", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Replace("kV", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();

            if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var kv))
                return false;

            peak = kv * 1000.0 * Math.Sqrt(2.0) * 1.10;
            return true;
        }

        if (normalized.EndsWith("V", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Replace("V", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();

            if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return false;

            peak = v * Math.Sqrt(2.0) * 1.10;
            return true;
        }

        return false;
    }

    private static bool TryParseCurrentScalePeak(string? scaleText, out double peak)
    {
        peak = 0.0;

        if (string.IsNullOrWhiteSpace(scaleText) ||
            string.Equals(scaleText, "Auto", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(scaleText, "Auto Peak Hold", StringComparison.OrdinalIgnoreCase))
            return false;

        var normalized = scaleText.Trim();

        if (normalized.EndsWith("kA", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Replace("kA", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();

            if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var ka))
                return false;

            peak = ka * 1000.0 * Math.Sqrt(2.0) * 1.10;
            return true;
        }

        if (normalized.EndsWith("A", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Replace("A", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();

            if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var ampere))
                return false;

            peak = ampere * Math.Sqrt(2.0) * 1.10;
            return true;
        }

        return false;
    }

    private static string FormatAxisLabel(double peak, string unit)
    {
        if (string.Equals(unit, "V", StringComparison.OrdinalIgnoreCase) && Math.Abs(peak) >= 1000.0)
            return $"+/- {peak / 1000.0:0.##} kVpk";

        if (string.Equals(unit, "A", StringComparison.OrdinalIgnoreCase) && Math.Abs(peak) >= 1000.0)
            return $"+/- {peak / 1000.0:0.##} kApk";

        return $"+/- {peak:0.#} {unit}pk";
    }

    private static double[] RepairDisplayGlitches(double[] samples)
    {
        if (samples.Length < 5)
            return samples;

        var repaired = samples
            .Select(static sample => double.IsNaN(sample) || double.IsInfinity(sample) ? 0.0 : sample)
            .ToArray();

        var amplitude = ResolveRobustAmplitude(repaired);
        var hardLimit = Math.Max(8.0, amplitude * 3.5);
        var jumpLimit = Math.Max(8.0, amplitude * 0.24);

        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 1; i < repaired.Length - 1; i++)
            {
                var previous = repaired[i - 1];
                var current = repaired[i];
                var next = repaired[i + 1];

                if (Math.Abs(current) > hardLimit ||
                    (Math.Abs(current - previous) > jumpLimit && Math.Abs(next - current) > jumpLimit))
                    repaired[i] = (previous + next) / 2.0;
            }
        }

        return repaired;
    }

    private static double ResolveRobustAmplitude(double[] samples)
    {
        var maxPeak = 1.0;

        foreach (var sample in samples)
            maxPeak = Math.Max(maxPeak, Math.Abs(sample));

        return maxPeak;
    }

    private int CalculateVisibleSamples(WaveformSnapshot snapshot)
    {
        var cycles = TimebaseMode switch
        {
            "1 cycle" => 1,
            "2 cycles" => 2,
            "8 cycles" => 8,
            _ => 4
        };

        const int samplesPerCycle = 80;
        return samplesPerCycle * cycles;
    }

    private void DrawGrid(DrawingContext dc, Rect rect)
    {
        // vertical minor grid
        for (var i = 1; i < 10; i++)
        {
            var x = rect.Left + (rect.Width * i / 10.0);
            var pen = i == 5 ? MajorGridPen : GridPen;
            dc.DrawLine(pen, new Point(x, rect.Top), new Point(x, rect.Bottom));
        }

        // horizontal grid
        for (var i = 1; i < 4; i++)
        {
            var y = rect.Top + (rect.Height * i / 4.0);
            var pen = i == 2 ? ZeroPen : GridPen;
            dc.DrawLine(pen, new Point(rect.Left, y), new Point(rect.Right, y));
        }

        var zeroY = rect.Top + (rect.Height / 2.0);

        // top/bottom subtle frame
        dc.DrawLine(HighlightPen, new Point(rect.Left, rect.Top + 1), new Point(rect.Right, rect.Top + 1));
        dc.DrawLine(HighlightPen, new Point(rect.Left, rect.Bottom - 1), new Point(rect.Right, rect.Bottom - 1));

        // zero axis stronger
        dc.DrawLine(ZeroPen, new Point(rect.Left, zeroY), new Point(rect.Right, zeroY));
    }

    private static void DrawYAxisScale(DrawingContext dc, Rect rect, double peak, string unit)
    {
        if (peak <= 0)
            return;

        var topText = FormatAxisLabelSigned(peak, unit, positive: true);
        var zeroText = "0";
        var bottomText = FormatAxisLabelSigned(peak, unit, positive: false);

        var top = CreateText(topText, 10.2, AxisTextBrush, LabelTypeface);
        var zero = CreateText(zeroText, 10.2, AxisTextBrush, LabelTypeface);
        var bottom = CreateText(bottomText, 10.2, AxisTextBrush, LabelTypeface);

        var x = rect.Left + 4;

        dc.DrawText(top, new Point(x, rect.Top + 4));
        dc.DrawText(zero, new Point(x, rect.Top + (rect.Height / 2.0) - (zero.Height / 2.0)));
        dc.DrawText(bottom, new Point(x, rect.Bottom - bottom.Height - 4));
    }

    private static string FormatAxisLabelSigned(double peak, string unit, bool positive)
    {
        var sign = positive ? "+" : "-";
        var absPeak = Math.Abs(peak);

        if (string.Equals(unit, "V", StringComparison.OrdinalIgnoreCase) && absPeak >= 1000.0)
            return $"{sign}{absPeak / 1000.0:0.##} kVpk";

        if (string.Equals(unit, "A", StringComparison.OrdinalIgnoreCase) && absPeak >= 1000.0)
            return $"{sign}{absPeak / 1000.0:0.##} kApk";

        return $"{sign}{absPeak:0.#} {unit}pk";
    }
    private void DrawSeries(DrawingContext dc, Rect rect, WaveformSeriesModel series, double amplitudeMax, bool stacked, int index, int totalSeries)
    {
        if (series.Samples.Count < 2)
            return;

        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();

        var verticalBand = stacked ? rect.Height / Math.Max(1, totalSeries) : rect.Height;
        var bandTop = stacked ? rect.Top + (verticalBand * index) : rect.Top;
        var bandCenter = bandTop + (verticalBand / 2.0);
        var bandScale = (stacked ? verticalBand : rect.Height) * 0.37;

        for (var i = 0; i < series.Samples.Count; i++)
        {
            var x = rect.Left + (rect.Width * i / (series.Samples.Count - 1.0));
            var normalized = amplitudeMax <= 0 ? 0 : series.Samples[i] / amplitudeMax;
            var y = bandCenter - (normalized * bandScale);
            var point = new Point(x, y);

            if (i == 0)
                ctx.BeginFigure(point, false, false);
            else
                ctx.LineTo(point, true, false);
        }

        geometry.Freeze();

        var brush = GetSeriesBrush(series.Name);
        var pens = GetSeriesPens(series.Name);

        dc.DrawGeometry(null, pens.Glow, geometry);
        dc.DrawGeometry(null, pens.Shadow, geometry);
        dc.DrawGeometry(null, pens.Main, geometry);

        // cleaner label badge at right side
        var labelY = GetLastY(rect, series.Samples, amplitudeMax, stacked, index, totalSeries) - 9;
        var labelText = CreateText(series.Name, 11, brush, TitleTypeface);
        var badge = new Rect(rect.Right - 43, labelY - 3, 38, labelText.Height + 6);

        dc.DrawRoundedRectangle(CloneBrushOpacity(brush, 0.12), null, badge, 7, 7);
        dc.DrawText(labelText, new Point(badge.Left + 8, badge.Top + 3));
    }

    private static SolidColorBrush CloneBrushOpacity(Brush brush, double opacity)
    {
        var color = brush is SolidColorBrush solid ? solid.Color : Colors.White;
        var clone = new SolidColorBrush(color) { Opacity = opacity };
        clone.Freeze();
        return clone;
    }

    private static SeriesPens GetSeriesPens(string name)
    {
        if (SeriesPenCache.TryGetValue(name, out var pens))
            return pens;

        var brush = GetSeriesBrush(name);
        var thickness = GetSeriesThickness(name);
        pens = new SeriesPens(
            CreateFrozenSeriesPen(CloneBrushOpacity(brush, 0.20), thickness + 5.2),
            CreateFrozenSeriesPen(CloneBrushOpacity(brush, 0.34), thickness + 2.2),
            CreateFrozenSeriesPen(brush, thickness));
        SeriesPenCache[name] = pens;
        return pens;
    }

    private static Pen CreateFrozenSeriesPen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();
        return pen;
    }

    private static void DrawMarkers(DrawingContext dc, Rect rect, WaveformSeriesModel series, double amplitudeMax, bool stacked, int index, int totalSeries, Brush brush)
    {
        if (series.Samples.Count < 2)
            return;

        var markerIndices = new[] { 0, series.Samples.Count / 2, series.Samples.Count - 1 }.Distinct();
        foreach (var sampleIndex in markerIndices)
        {
            var point = GetPoint(rect, series.Samples, amplitudeMax, stacked, index, totalSeries, sampleIndex);
            dc.DrawEllipse(brush, null, point, 2.2, 2.2);
        }
    }

    private static Point GetPoint(Rect rect, IReadOnlyList<double> samples, double amplitudeMax, bool stacked, int index, int totalSeries, int sampleIndex)
    {
        var verticalBand = stacked ? rect.Height / Math.Max(1, totalSeries) : rect.Height;
        var bandTop = stacked ? rect.Top + (verticalBand * index) : rect.Top;
        var bandCenter = bandTop + (verticalBand / 2.0);
        var bandScale = (stacked ? verticalBand : rect.Height) * 0.34;
        var x = rect.Left + (rect.Width * sampleIndex / (samples.Count - 1.0));
        var normalized = amplitudeMax <= 0 ? 0 : samples[sampleIndex] / amplitudeMax;
        var y = bandCenter - (normalized * bandScale);
        return new Point(x, y);
    }

    private static double GetLastY(Rect rect, IReadOnlyList<double> samples, double amplitudeMax, bool stacked, int index, int totalSeries)
    {
        return GetPoint(rect, samples, amplitudeMax, stacked, index, totalSeries, samples.Count - 1).Y;
    }

    private void DrawCursor(
        DrawingContext dc,
        Rect rect,
        IReadOnlyList<WaveformSeriesModel> series,
        double amplitudeMax,
        bool stacked,
        WaveformSnapshot snapshot)
    {
        var maxSamples = series.Select(item => item.Samples.Count).DefaultIfEmpty(0).Max();
        if (maxSamples < 2)
            return;

        var sampleIndex = (int)Math.Round(_cursorFraction * (maxSamples - 1));
        sampleIndex = Math.Clamp(sampleIndex, 0, maxSamples - 1);

        var x = rect.Left + (rect.Width * sampleIndex / (maxSamples - 1.0));
        dc.DrawLine(CursorPen, new Point(x, rect.Top), new Point(x, rect.Bottom));
        dc.DrawEllipse(CursorDotBrush, null, new Point(x, rect.Top + 7), 3.8, 3.8);

        var sampleRate = snapshot.SampleRateHz > 0 ? snapshot.SampleRateHz : 4000.0;
        var timeMs = sampleIndex / sampleRate * 1000.0;

        var freqText = snapshot.MeasuredFrequencyHz > 0
            ? $"{snapshot.MeasuredFrequencyHz:0.###} Hz"
            : "freq N/A";

        var chips = new List<(string Text, Brush Brush)>
    {
        ($"t={timeMs:0.###} ms", LabelBrush),
        ($"n={sampleIndex}", LabelBrush),
        (freqText, LabelBrush)
    };

        foreach (var item in series)
        {
            if (item.Samples.Count == 0)
                continue;

            var localIndex = Math.Clamp(sampleIndex, 0, item.Samples.Count - 1);
            chips.Add(($"{item.Name}={FormatEngineeringValue(item.Samples[localIndex], item.Unit)}", GetSeriesBrush(item.Name)));
        }

        var paddingX = 8.0;
        var paddingY = 5.0;
        var gap = 6.0;

        var chipLayouts = chips
            .Select(chip =>
            {
                var text = CreateText(chip.Text, 10.8, chip.Brush, LabelTypeface);
                var width = text.Width + (paddingX * 2);
                var height = text.Height + (paddingY * 2);
                return (chip.Text, chip.Brush, Text: text, Width: width, Height: height);
            })
            .ToArray();

        var totalWidth = chipLayouts.Sum(c => c.Width) + (gap * Math.Max(0, chipLayouts.Length - 1)) + 12;
        var maxHeight = chipLayouts.Select(c => c.Height).DefaultIfEmpty(24).Max() + 8;

        var boxWidth = Math.Min(rect.Width - 8, totalWidth);
        var boxX = Math.Clamp(x + 10, rect.Left + 4, rect.Right - boxWidth - 4);
        var boxY = rect.Top - (maxHeight + 6);

        // kalau terlalu atas, fallback ke dalam
        if (boxY < 4)
            boxY = rect.Top + 6;

        var box = new Rect(boxX, boxY, boxWidth, maxHeight);

        dc.DrawRoundedRectangle(CloneBrushOpacity(CursorFillBrush, 0.92), CursorBorderPen, box, 8, 8);

        var cx = box.Left + 6;
        var cy = box.Top + 4;

        foreach (var chip in chipLayouts)
        {
            if (cx + chip.Width > box.Right - 4)
                break;

            var chipRect = new Rect(cx, cy, chip.Width, chip.Height);
            dc.DrawRoundedRectangle(CloneBrushOpacity(chip.Brush, 0.16), null, chipRect, 7, 7);

            dc.DrawEllipse(chip.Brush, null, new Point(chipRect.Left + 8, chipRect.Top + chipRect.Height / 2), 3.2, 3.2);
            dc.DrawText(chip.Text, new Point(chipRect.Left + 14, chipRect.Top + paddingY));

            cx += chip.Width + gap;
        }
    }

    private static string FormatEngineeringValue(double value, string unit)
    {
        if (string.Equals(unit, "V", StringComparison.OrdinalIgnoreCase) && Math.Abs(value) >= 1000.0)
            return $"{value / 1000.0:0.##} kV";

        if (string.Equals(unit, "A", StringComparison.OrdinalIgnoreCase) && Math.Abs(value) >= 1000.0)
            return $"{value / 1000.0:0.##} kA";

        return $"{value:0.#} {unit}";
    }

    private static void DrawTimebaseLabel(DrawingContext dc, Rect rect, WaveformSnapshot snapshot)
    {
        var sampleRateText = snapshot.SampleRateHz > 0 ? $"{snapshot.SampleRateHz:0.#} Hz sample" : "Sample rate pending";
        var frequencyText = snapshot.MeasuredFrequencyHz > 0 ? $"{snapshot.MeasuredFrequencyHz:0.###} Hz signal" : "Signal frequency pending";
        dc.DrawText(CreateText(sampleRateText, 10.5, MutedBrush, LabelTypeface), new Point(rect.Left, rect.Bottom + 3));
        dc.DrawText(CreateText(frequencyText, 10.5, MutedBrush, LabelTypeface), new Point(rect.Right - 106, rect.Bottom + 3));
    }

    private static Brush GetSeriesBrush(string name)
    {
        return name switch
        {
            "Ua" or "Ia" => UaBrush,
            "Ub" or "Ib" => UbBrush,
            "Uc" or "Ic" => UcBrush,
            _ => NeutralBrush
        };
    }

    private static double GetSeriesThickness(string name)
    {
        return name.StartsWith("U", StringComparison.OrdinalIgnoreCase) ? 2.4 : 2.0;
    }

    private static void DrawCenteredMessage(DrawingContext dc, Rect bounds, string text)
    {
        var formatted = CreateText(text, 18, LabelBrush, TitleTypeface);
        dc.DrawText(formatted, new Point(bounds.Left + ((bounds.Width - formatted.Width) / 2.0), bounds.Top + ((bounds.Height - formatted.Height) / 2.0)));
    }

    private static FormattedText CreateText(string text, double size, Brush brush, Typeface typeface)
    {
        text = NormalizeDisplayText(text);
        return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, brush, 1.0);
    }

    private static string NormalizeDisplayText(string text)
    {
        if (!text.StartsWith("??", StringComparison.Ordinal) &&
            !text.StartsWith("+/-", StringComparison.Ordinal) &&
            !text.StartsWith("Â±", StringComparison.Ordinal) &&
            !text.StartsWith("±", StringComparison.Ordinal))
            return text;

        var normalized = text.StartsWith("??", StringComparison.Ordinal) || text.StartsWith("Â±", StringComparison.Ordinal)
            ? text[2..].Trim()
            : text.StartsWith("±", StringComparison.Ordinal)
                ? text[1..].Trim()
            : text[3..].Trim();
        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var peak))
            return text.Replace("??", "+/-", StringComparison.Ordinal);

        return FormatAxisLabel(peak, parts[1]);
    }

    private static Brush CreateBrush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }

    private static Pen CreatePen(string hex, double thickness, double[]? dash = null)
    {
        var pen = new Pen(CreateBrush(hex), thickness);
        if (dash is not null)
            pen.DashStyle = new DashStyle(dash, 0);
        pen.Freeze();
        return pen;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton != MouseButton.Left)
            return;

        Focus();
        CaptureMouse();
        _isDraggingCursor = true;
        UpdateCursorPosition(e.GetPosition(this).X);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_isDraggingCursor)
            return;

        UpdateCursorPosition(e.GetPosition(this).X);
        e.Handled = true;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.ChangedButton != MouseButton.Left)
            return;

        _isDraggingCursor = false;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    private void UpdateCursorPosition(double x)
    {
        const double horizontalPadding = 22.0;
        var width = Math.Max(1.0, ActualWidth - (horizontalPadding * 2.0));
        _cursorFraction = Math.Clamp((x - horizontalPadding) / width, 0.0, 1.0);
        InvalidateVisual();
    }

    private sealed record LaneDefinition(string Title, string Unit, IReadOnlyList<WaveformSeriesModel> Series);
    private sealed record SeriesPens(Pen Glow, Pen Shadow, Pen Main);
}
