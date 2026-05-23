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
    public string DisplayText => FormatEngineeringValue(InstantValue);
    public string RmsDisplayText => FormatEngineeringValue(RmsValue);
    public string AngleDisplayText => AngleDegrees.HasValue ? $"{AngleDegrees.Value:0.00} deg" : "N/A";

    private string DisplayUnit => !string.IsNullOrWhiteSpace(Unit)
        ? Unit
        : Name.StartsWith("U", StringComparison.OrdinalIgnoreCase) ? "V" : "A";

    private string FormatEngineeringValue(double? value)
    {
        if (!value.HasValue)
            return "N/A";

        var unit = DisplayUnit;
        if (string.Equals(unit, "V", StringComparison.OrdinalIgnoreCase) && Math.Abs(value.Value) >= 1000.0)
            return $"{value.Value / 1000.0:0.00} kV";

        return $"{value.Value:0.00} {unit}";
    }
}
