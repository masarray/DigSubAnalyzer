using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ProcessBus.Core.Models;

namespace ProcessBus.App.Wpf.Controls;

public sealed class PhasorDiagramControl : FrameworkElement
{
    private const double VoltageRadiusFactor = 0.96;
    private const double CurrentRadiusFactor = 0.78;
    private const double VoltageLineThickness = 3.2;
    private const double CurrentLineThickness = 2.8;
    private IReadOnlyList<PhasorDisplayItem> _visualItems = Array.Empty<PhasorDisplayItem>();

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable<PhasorDisplayItem>),
            typeof(PhasorDiagramControl),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnItemsSourceChanged));

    private static readonly double[] RingFractions = [0.25, 0.5, 0.75, 1.0];

    private static readonly Brush PanelBrush = CreateBrush("#0C1B2B");
    private static readonly Brush TextPrimaryBrush = CreateBrush("#EAF3FF");
    private static readonly Brush TextSecondaryBrush = CreateBrush("#A7B8CB");
    private static readonly Brush TextMutedBrush = CreateBrush("#7E93AA");
    private static readonly Brush OriginBrush = CreateBrush("#EAF3FF");

    private static readonly Brush PhaseRBrush = CreateBrush("#E3263A");
    private static readonly Brush PhaseSBrush = CreateBrush("#F5B400");
    private static readonly Brush PhaseTBrush = CreateBrush("#1E7BD6");

    private static readonly Pen OuterBorderPen = CreatePen("#1D8FFF", 1.55);
    private static readonly Pen RingPen = CreatePen("#28445E", 1.0, [4.0, 6.0]);
    private static readonly Pen AxisPen = CreatePen("#45627E", 1.35);
    private static readonly Pen DiagonalPen = CreatePen("#1D344E", 0.9, [3.0, 5.0]);

    private static readonly Typeface TitleTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    private static readonly Typeface LabelTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Typeface BodyTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    public IEnumerable<PhasorDisplayItem>? ItemsSource
    {
        get => (IEnumerable<PhasorDisplayItem>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public PhasorDiagramControl()
    {
        // Deliberately no vector tweening here. Protection/substation phasors must show
        // the latest computed state immediately; animation hides real state changes.
    }

    private static void OnItemsSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not PhasorDiagramControl control)
            return;

        var next = ((IEnumerable<PhasorDisplayItem>?)args.NewValue)?.ToArray() ?? [];
        // ARSVIN-style receiver behavior: phasor vectors are not animated/tweened.
        // The view renders the latest computed RMS/angle snapshot directly, so a publisher
        // phase/state change is visible on the next UI tick instead of taking seconds to settle.
        control._visualItems = next;
        control.InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 360 : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? 360 : availableSize.Height;
        return new Size(Math.Max(280, width), Math.Max(280, height));
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var bounds = new Rect(new Point(0, 0), RenderSize);
        dc.DrawRectangle(PanelBrush, null, bounds);

        var items = _visualItems.Count > 0
            ? _visualItems.ToArray()
            : ItemsSource?.ToArray() ?? [];
        var validItems = items.Where(static item => item.HasValue).ToArray();

        var voltageMax = validItems.Where(static item => item.Family == PhasorFamily.Voltage)
            .Select(static item => item.Magnitude!.Value)
            .DefaultIfEmpty()
            .Max();

        var currentMax = validItems.Where(static item => item.Family == PhasorFamily.Current)
            .Select(static item => item.Magnitude!.Value)
            .DefaultIfEmpty()
            .Max();

        var plotRect = new Rect(
            bounds.Left + 10,
            bounds.Top + 10,
            Math.Max(120, bounds.Width - 20),
            Math.Max(150, bounds.Height - 20));

        var center = new Point(
            plotRect.Left + plotRect.Width / 2.0,
            plotRect.Top + plotRect.Height / 2.0);

        var radius = Math.Max(0, Math.Min(plotRect.Width, plotRect.Height) / 2.0 - 24.0);

        DrawGrid(dc, center, radius);

        if (validItems.Length == 0)
        {
            DrawCenteredMessage(dc, center, "Phasor data pending");
            return;
        }

        DrawSoftOriginGlow(dc, center);

        foreach (var item in validItems)
            DrawPhasor(dc, center, radius, item, voltageMax, currentMax);

    }

    private static void DrawGrid(DrawingContext dc, Point center, double radius)
    {
        if (radius <= 10)
            return;

        // soft outer glow
        dc.DrawEllipse(null, CreatePen("#0D3357", 5.0), center, radius + 1.0, radius + 1.0);

        foreach (var fraction in RingFractions)
        {
            var pen = Math.Abs(fraction - 1.0) < 0.001 ? OuterBorderPen : RingPen;
            dc.DrawEllipse(null, pen, center, radius * fraction, radius * fraction);
        }

        dc.DrawLine(AxisPen, new Point(center.X - radius, center.Y), new Point(center.X + radius, center.Y));
        dc.DrawLine(AxisPen, new Point(center.X, center.Y - radius), new Point(center.X, center.Y + radius));

        var diagonalOffset = radius / Math.Sqrt(2.0);
        dc.DrawLine(DiagonalPen, new Point(center.X - diagonalOffset, center.Y - diagonalOffset), new Point(center.X + diagonalOffset, center.Y + diagonalOffset));
        dc.DrawLine(DiagonalPen, new Point(center.X + diagonalOffset, center.Y - diagonalOffset), new Point(center.X - diagonalOffset, center.Y + diagonalOffset));

        DrawSmallText(dc, "0°", new Point(center.X + radius + 6, center.Y - 10), TextMutedBrush);
        DrawSmallText(dc, "+90°", new Point(center.X - 16, center.Y - radius - 20), TextMutedBrush);
        DrawSmallText(dc, "180°", new Point(center.X - radius - 38, center.Y - 10), TextMutedBrush);
        DrawSmallText(dc, "-90°", new Point(center.X - 16, center.Y + radius + 4), TextMutedBrush);
    }

    private static void DrawSoftOriginGlow(DrawingContext dc, Point center)
    {
        dc.DrawEllipse(CreateBrush("#123F68"), null, center, 10.0, 10.0);
        dc.DrawEllipse(CreateBrush("#1D8FFF"), null, center, 6.2, 6.2);
        dc.DrawEllipse(OriginBrush, null, center, 4.0, 4.0);
    }

    private static void DrawPhasor(DrawingContext dc, Point center, double radius, PhasorDisplayItem item, double voltageMax, double currentMax)
    {
        var maxMagnitude = item.Family == PhasorFamily.Voltage ? voltageMax : currentMax;
        if (maxMagnitude <= 0)
            return;

        var normalized = Math.Clamp(item.Magnitude!.Value / maxMagnitude, 0.0, 1.0);

        var familyRadius = item.Family == PhasorFamily.Voltage
            ? radius * VoltageRadiusFactor
            : radius * CurrentRadiusFactor;

        var visualLength = familyRadius * normalized;
        var radians = item.AngleDegrees!.Value * Math.PI / 180.0;

        var end = new Point(
            center.X + visualLength * Math.Cos(radians),
            center.Y - visualLength * Math.Sin(radians));

        var brush = GetBrush(item);
        var glowPen = new Pen(CloneWithOpacity(brush, 0.20), item.Family == PhasorFamily.Voltage ? 8.0 : 6.5)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        glowPen.Freeze();

        var pen = CreateVectorPen(item, brush);

        dc.DrawLine(glowPen, center, end);
        dc.DrawLine(pen, center, end);

        DrawArrowHead(dc, center, end, brush, item.Family == PhasorFamily.Voltage ? 17.0 : 15.0);
        DrawVectorLabel(dc, item, center, end, brush);
    }

    private static void DrawArrowHead(DrawingContext dc, Point start, Point end, Brush brush, double arrowLength)
    {
        var direction = end - start;
        if (direction.Length <= 0.001)
            return;

        direction.Normalize();
        var normal = new Vector(-direction.Y, direction.X);
        var arrowWidth = arrowLength * 0.46;

        var p1 = end;
        var p2 = end - direction * arrowLength + normal * arrowWidth;
        var p3 = end - direction * arrowLength - normal * arrowWidth;

        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(p1, true, true);
        ctx.LineTo(p2, true, false);
        ctx.LineTo(p3, true, false);
        geometry.Freeze();

        dc.DrawGeometry(brush, null, geometry);
    }

    private static void DrawVectorLabel(DrawingContext dc, PhasorDisplayItem item, Point center, Point end, Brush brush)
    {
        var direction = end - center;
        if (direction.Length <= 0.001)
            direction = new Vector(1, 0);
        else
            direction.Normalize();

        var label = ToPhaseLabel(item.Name);
        var labelPoint = end + direction * 11.0;

        if (direction.X >= 0)
            labelPoint.X += 5;
        else
            labelPoint.X -= 48;

        if (direction.Y >= 0)
            labelPoint.Y += 2;
        else
            labelPoint.Y -= 17;

        DrawText(dc, label, 13, labelPoint, brush, LabelTypeface);
    }

    private static void DrawCenteredMessage(DrawingContext dc, Point center, string text)
    {
        var formatted = CreateText(text, 17, TextSecondaryBrush, LabelTypeface);
        dc.DrawText(formatted, new Point(center.X - formatted.Width / 2.0, center.Y - formatted.Height / 2.0));
    }

    private static Brush GetBrush(PhasorDisplayItem item)
    {
        return item.Name switch
        {
            "Ua" => PhaseRBrush,
            "Ub" => PhaseSBrush,
            "Uc" => PhaseTBrush,
            "Ia" => PhaseRBrush,
            "Ib" => PhaseSBrush,
            "Ic" => PhaseTBrush,
            _ => item.Family == PhasorFamily.Voltage ? PhaseRBrush : PhaseRBrush
        };
    }

    private static string ToPhaseLabel(string name)
    {
        return name switch
        {
            "Ua" => "Ua (R)",
            "Ub" => "Ub (S)",
            "Uc" => "Uc (T)",
            "Ia" => "Ia (R)",
            "Ib" => "Ib (S)",
            "Ic" => "Ic (T)",
            _ => name
        };
    }

    private static Pen CreateVectorPen(PhasorDisplayItem item, Brush brush)
    {
        var pen = new Pen(brush, item.Family == PhasorFamily.Voltage ? VoltageLineThickness : CurrentLineThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };

        pen.Freeze();
        return pen;
    }

    private static void DrawSmallText(DrawingContext dc, string text, Point point, Brush brush)
    {
        DrawText(dc, text, 11, point, brush, BodyTypeface);
    }

    private static void DrawText(DrawingContext dc, string text, double size, Point point, Brush brush, Typeface typeface)
    {
        dc.DrawText(CreateText(text, size, brush, typeface), point);
    }

    private static FormattedText CreateText(string text, double size, Brush brush, Typeface typeface)
    {
        return new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            size,
            brush,
            VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);
    }

    private static Brush CreateBrush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush CloneWithOpacity(Brush brush, double opacity)
    {
        var color = brush is SolidColorBrush solid ? solid.Color : Colors.Black;
        var clone = new SolidColorBrush(color) { Opacity = opacity };
        clone.Freeze();
        return clone;
    }

    private static Pen CreatePen(string hex, double thickness, double[]? dashArray = null)
    {
        var pen = new Pen(CreateBrush(hex), thickness);
        if (dashArray is not null)
            pen.DashStyle = new DashStyle(dashArray, 0);

        pen.Freeze();
        return pen;
    }

}

