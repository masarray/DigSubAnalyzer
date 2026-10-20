namespace ProcessBus.Core.Models;

// Physical watts/vars require real A/V units for every phase in one
// selected-stream coherent snapshot. Raw INT32 counts never qualify.
public readonly record struct SvPhysicalPowerTotals(double P, double Q, double S);

public static class SvPhysicalPowerMeter
{
    public static bool TryCompute(AnalogValuesSnapshot snapshot, out SvPhysicalPowerTotals totals)
    {
        totals = default;
        if (snapshot is null) return false;
        var phases = new[]
        {
            (U: snapshot.Ua, I: snapshot.Ia),
            (U: snapshot.Ub, I: snapshot.Ib),
            (U: snapshot.Uc, I: snapshot.Ic)
        };
        var p = 0.0;
        var q = 0.0;
        var s = 0.0;
        foreach (var (u, i) in phases)
        {
            // Held or missing inputs cannot claim a CURRENT three-phase power estimate.
            if (u.Freshness != ChannelFreshness.Live || i.Freshness != ChannelFreshness.Live ||
                u.Unit != "V" || i.Unit != "A" ||
                u.RmsValue is not { } uRms || i.RmsValue is not { } iRms ||
                u.AngleDegrees is not { } uAngle || i.AngleDegrees is not { } iAngle ||
                !double.IsFinite(uRms) || !double.IsFinite(iRms) ||
                !double.IsFinite(uAngle) || !double.IsFinite(iAngle) ||
                uRms < 0 || iRms < 0)
                return false;

            var apparent = uRms * iRms;
            var delta = (uAngle - iAngle) * Math.PI / 180.0;
            p += apparent * Math.Cos(delta);
            q += apparent * Math.Sin(delta);
            s += apparent;
        }
        if (!double.IsFinite(p) || !double.IsFinite(q) || !double.IsFinite(s)) return false;
        totals = new SvPhysicalPowerTotals(p, q, s);
        return true;
    }
}
