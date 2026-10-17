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
    // UI preset shown before any publisher is detected. It is not applied to
    // an unknown stream until its decoded profile is verified or the operator
    // explicitly selects nominal. No electrical values exist during idle.
    public const SvEngineeringScale IdlePresentationDefault = SvEngineeringScale.Nominal4I4V;

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

    // Offer engineering-friendly nominal units only when the receive decoder
    // has identified the exact 16-entry 4I+4V instMag/q layout. This is an
    // ASSUMED profile, not SCL-certified calibration or universal SV scaling.
    // Never use svID, APPID or raw frame length alone for scale authority.
    public static bool IsRecognizedNominalCandidate(string? mappingText) =>
        !string.IsNullOrWhiteSpace(mappingText) &&
        mappingText.Contains("raw 4I4V instMag/q", StringComparison.OrdinalIgnoreCase);

    public static bool IsPhysical(SvEngineeringScale mode)
        => mode == SvEngineeringScale.Nominal4I4V;
}
