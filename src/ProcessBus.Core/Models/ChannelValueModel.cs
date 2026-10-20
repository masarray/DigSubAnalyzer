using System.Globalization;

namespace ProcessBus.Core.Models;

public sealed class ChannelValueModel
{
    public ChannelValueModel(string name)
    {
        Name = name;
    }

    public string Name { get; }
    public double? InstantValue { get; init; }
    public double? RmsValue { get; init; }
    public double? AngleDegrees { get; init; }
    public string Unit { get; init; } = string.Empty;
    public ChannelFreshness Freshness { get; init; } = ChannelFreshness.Live;
    public double FreshnessOpacity => ChannelFreshnessDisplay.Opacity(Freshness);
    public string FreshnessText => ChannelFreshnessDisplay.Suffix(Freshness);

    // The operator ledger keeps the numerical readout and its unit in separate columns.
    // Do not infer V/A when engineering scale authority has not supplied a unit.
    public string RmsNumberText => FormatNumber(RmsValue);
    public string RmsUnitText => !RmsValue.HasValue || !double.IsFinite(RmsValue.Value)
        ? "—"
        : DisplayUnit(RmsValue.Value);
    public string AngleNumberText => AngleDegrees is { } angle && double.IsFinite(angle)
        ? angle.ToString("0.00", CultureInfo.InvariantCulture) + "°"
        : "N/A";

    // Preserve existing display properties for the advanced inspector and clipboard.
    public string DisplayText => FormatEngineeringValue(InstantValue);
    public string RmsDisplayText => FormatEngineeringValue(RmsValue);
    public string AngleDisplayText => AngleDegrees is { } angle && double.IsFinite(angle)
        ? angle.ToString("0.00", CultureInfo.InvariantCulture) + " deg"
        : "N/A";

    private string DisplayUnit(double value)
    {
        if (string.IsNullOrWhiteSpace(Unit))
            return "—";

        return string.Equals(Unit, "V", StringComparison.OrdinalIgnoreCase) &&
               Math.Abs(value) >= 1000.0
            ? "kV"
            : Unit;
    }

    private string FormatNumber(double? value)
    {
        if (value is not { } number || !double.IsFinite(number))
            return "N/A";

        var scaled = string.Equals(Unit, "V", StringComparison.OrdinalIgnoreCase) &&
                     Math.Abs(number) >= 1000.0
            ? number / 1000.0
            : number;
        return scaled.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private string FormatEngineeringValue(double? value)
    {
        if (value is not { } number || !double.IsFinite(number))
            return "N/A";
        var unit = DisplayUnit(number);
        return unit == "—" ? FormatNumber(value) : FormatNumber(value) + " " + unit;
    }
}
