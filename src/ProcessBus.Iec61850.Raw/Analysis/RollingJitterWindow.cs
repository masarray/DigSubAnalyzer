namespace ProcessBus.Iec61850.Raw.Analysis;

/// <summary>
/// Bounded, allocation-free-after-construction rolling jitter statistics.
/// Maintains an amortized O(1) maximum with a monotonically decreasing ring deque.
/// One instance belongs to exactly one SV stream; callers synchronize ownership.
/// </summary>
public sealed class RollingJitterWindow
{
    private readonly double[] _values;
    private readonly MaximumEntry[] _maxima;
    private int _head;
    private int _count;
    private int _maximumHead;
    private int _maximumCount;
    private long _sequence;
    private double _sum;

    private readonly record struct MaximumEntry(long Sequence, double Value);

    public RollingJitterWindow(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _values = new double[capacity];
        _maxima = new MaximumEntry[capacity];
    }

    public int Count => _count;
    public double? Average => _count == 0 ? null : _sum / _count;
    public double? Maximum => _maximumCount == 0 ? null : _maxima[_maximumHead].Value;

    public void Add(double absoluteMicroseconds)
    {
        if (!double.IsFinite(absoluteMicroseconds) || absoluteMicroseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteMicroseconds));

        if (_count == _values.Length)
        {
            _sum -= _values[_head];
            _head = (_head + 1) % _values.Length;
            _count--;
        }

        _values[(_head + _count) % _values.Length] = absoluteMicroseconds;
        _count++;
        _sum += absoluteMicroseconds;
        var sequence = _sequence++;
        var oldestSequence = _sequence - _count;

        // Expire first: the candidate deque must never exceed the fixed capacity.
        while (_maximumCount > 0 && _maxima[_maximumHead].Sequence < oldestSequence)
        {
            _maximumHead = (_maximumHead + 1) % _maxima.Length;
            _maximumCount--;
        }

        while (_maximumCount > 0)
        {
            var last = (_maximumHead + _maximumCount - 1) % _maxima.Length;
            if (_maxima[last].Value > absoluteMicroseconds)
                break;
            _maximumCount--;
        }

        _maxima[(_maximumHead + _maximumCount) % _maxima.Length] =
            new MaximumEntry(sequence, absoluteMicroseconds);
        _maximumCount++;
    }

    public void Clear()
    {
        _head = 0;
        _count = 0;
        _maximumHead = 0;
        _maximumCount = 0;
        _sequence = 0;
        _sum = 0;
    }
}
