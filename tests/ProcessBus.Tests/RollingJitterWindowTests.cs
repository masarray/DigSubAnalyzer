using ProcessBus.Iec61850.Raw.Analysis;
using Xunit;

namespace ProcessBus.Tests;

public sealed class RollingJitterWindowTests
{
    [Fact]
    public void ExpiredMaximumNeverLeaksAcrossWindow()
    {
        var stats = new RollingJitterWindow(3);
        stats.Add(100);
        stats.Add(3);
        stats.Add(2);
        Assert.Equal(100, stats.Maximum);
        stats.Add(1);
        Assert.Equal(3, stats.Maximum);
        Assert.Equal(2.0, stats.Average);
        stats.Add(0);
        Assert.Equal(2, stats.Maximum);
        Assert.Equal(1.0, stats.Average);
    }

    [Fact]
    public void StreamingMaximumAndMeanMatchBoundedReference()
    {
        const int capacity = 80;
        var stats = new RollingJitterWindow(capacity);
        var reference = new Queue<double>();
        for (var i = 0; i < 30_000; i++)
        {
            var value = (i % 181) * (i % 3 == 0 ? 1.25 : 0.4);
            stats.Add(value);
            reference.Enqueue(value);
            if (reference.Count > capacity)
                reference.Dequeue();

            if (i % 73 == 0)
            {
                Assert.Equal(reference.Count, stats.Count);
                Assert.Equal(reference.Max(), stats.Maximum);
                Assert.Equal(reference.Average(), stats.Average!.Value, 8);
            }
        }
        stats.Clear();
        Assert.Equal(0, stats.Count);
        Assert.Null(stats.Maximum);
        Assert.Null(stats.Average);
        stats.Add(7);
        Assert.Equal(7, stats.Maximum);
    }

    [Fact]
    public void InvalidInputAndCapacityFailClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RollingJitterWindow(0));
        var stats = new RollingJitterWindow(2);
        Assert.Throws<ArgumentOutOfRangeException>(() => stats.Add(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => stats.Add(-1));
        Assert.Equal(0, stats.Count);
    }
}
