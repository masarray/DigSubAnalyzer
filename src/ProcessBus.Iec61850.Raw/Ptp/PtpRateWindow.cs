namespace ProcessBus.Iec61850.Raw.Ptp;

/// <summary>
/// Fixed-space one-second buckets for passive PTP traffic rates. Unlike a timestamp
/// queue, sustained packet floods cannot grow the retained state. The displayed
/// estimate is deliberately approximate, not a source clock measurement.
/// </summary>
public sealed class PtpRateWindow
{
    private const int WindowSeconds = 5;
    private readonly long[] _epochs = new long[WindowSeconds];
    private readonly int[] _counts = new int[WindowSeconds];
    private long _newestEpoch = long.MinValue;

    public PtpRateWindow() => Array.Fill(_epochs, long.MinValue);

    public void Observe(DateTime utc)
    {
        var epoch = utc.Ticks / TimeSpan.TicksPerSecond;
        if (_newestEpoch != long.MinValue && epoch < _newestEpoch - WindowSeconds + 1)
            return; // Never let old/reordered evidence evict a newer bucket.
        if (epoch > _newestEpoch)
            _newestEpoch = epoch;

        var index = (int)(epoch % WindowSeconds);
        if (_epochs[index] != epoch)
        {
            if (_epochs[index] > epoch)
                return;
            _epochs[index] = epoch;
            _counts[index] = 0;
        }

        if (_counts[index] < int.MaxValue)
            _counts[index]++;
    }

    public double? Rate(DateTime now)
    {
        var current = now.Ticks / TimeSpan.TicksPerSecond;
        long count = 0;
        for (var i = 0; i < WindowSeconds; i++)
        {
            if (_epochs[i] <= current && _epochs[i] > current - WindowSeconds)
                count += _counts[i];
        }
        return count == 0 ? null : count / (double)WindowSeconds;
    }
}
