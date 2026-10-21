using ProcessBus.Core.Presentation;
using Xunit;

namespace ProcessBus.Tests;

public sealed class GooseRelativeTimePresentationTests
{
    [Fact]
    public void RelativeTimeIsZeroAtFirstMessageAndElapsedSinceThatSameMessage()
    {
        var first = new DateTime(2026, 10, 21, 10, 0, 0, DateTimeKind.Utc);
        Assert.Equal(0, GooseRelativeTimePresentation.ElapsedMilliseconds(first, first));
        Assert.Equal(1234, GooseRelativeTimePresentation.ElapsedMilliseconds(first, first.AddMilliseconds(1234)));
        Assert.Equal(5000, GooseRelativeTimePresentation.ElapsedMilliseconds(first, first.AddSeconds(5)));
    }

    [Fact]
    public void OutOfOrderHistoryTimeCannotMakeNegativeRelativeTime()
    {
        var first = new DateTime(2026, 10, 21, 10, 0, 0, DateTimeKind.Utc);
        Assert.Equal(0, GooseRelativeTimePresentation.ElapsedMilliseconds(first, first.AddMilliseconds(-5)));
    }
}
