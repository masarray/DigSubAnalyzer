namespace ProcessBus.Core.Models;

/// <summary>
/// Explicit scale authority for a selected SV stream. Packet shape, svID, APPID,
/// and a missing SCL are NOT evidence of engineering calibration.
/// </summary>
public enum SvEngineeringScale
{
    RawCounts = 0,
    Nominal4I4V = 1
}

public static class SvEngineeringScaleFactors
{
    // This explicitly opted-in 4I+4V lab/nominal profile is 1 mA per
    // current count and 10 mV per voltage count. It is NOT universal
    // IEC 61850 SV scaling and must never be auto-selected by svID.
    public static double Factor(SvEngineeringScale mode, string channel)
        => mode == SvEngineeringScale.Nominal4I4V
            ? (channel.StartsWith("U", StringComparison.OrdinalIgnoreCase) ? 0.01 : 0.001)
            : 1.0;

    public static string Unit(SvEngineeringScale mode, string channel)
        => mode != SvEngineeringScale.Nominal4I4V
            ? "counts"
            : channel.StartsWith("U", StringComparison.OrdinalIgnoreCase) ? "V" : "A";

    public static bool IsPhysical(SvEngineeringScale mode)
        => mode == SvEngineeringScale.Nominal4I4V;
}
