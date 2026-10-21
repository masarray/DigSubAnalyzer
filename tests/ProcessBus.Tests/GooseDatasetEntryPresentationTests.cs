using ProcessBus.Core.Presentation;
using Xunit;

namespace ProcessBus.Tests;

public sealed class GooseDatasetEntryPresentationTests
{
    [Theory]
    [InlineData(1, null, "Data item 1")]
    [InlineData(2, "", "Data item 2")]
    [InlineData(3, "  ", "Data item 3")]
    [InlineData(4, "IEDLD0/XSWI5.Pos.stVal", "IEDLD0/XSWI5.Pos.stVal")]
    [InlineData(5, "  IEDLD0/XCBR1.Pos.q  ", "IEDLD0/XCBR1.Pos.q")]
    public void OnlyAnExplicitSclMappingProvidesASignalName(int index, string? mapped, string expected)
        => Assert.Equal(expected, GooseDatasetEntryPresentation.ResolveName(index, mapped));
}
