using ProcessBus.Core.Presentation;
using Xunit;

namespace ProcessBus.Tests;

public sealed class GooseDatasetEntryPresentationTests
{
    [Theory]
    [InlineData(1, null, "BitString", "Bitstring 1")]
    [InlineData(2, "", "BitString", "Bitstring 2")]
    [InlineData(3, null, "Boolean", "Boolean 3")]
    [InlineData(4, null, "Integer", "Integer 4")]
    [InlineData(5, null, "unknown", "Data item 5")]
    [InlineData(6, "XCBR1.Pos.stVal", "BitString", "XCBR1.Pos.stVal")]
    public void WireTypeNameIsFamiliarButNeverInventsASignalReference(
        int index, string? mapped, string? type, string expected)
        => Assert.Equal(expected, GooseDatasetEntryPresentation.ResolveName(index, mapped, type));

    [Theory]
    [InlineData(1, null, "Data item 1")]
    [InlineData(2, "", "Data item 2")]
    [InlineData(3, "  ", "Data item 3")]
    [InlineData(4, "IEDLD0/XSWI5.Pos.stVal", "IEDLD0/XSWI5.Pos.stVal")]
    [InlineData(5, "  IEDLD0/XCBR1.Pos.q  ", "IEDLD0/XCBR1.Pos.q")]
    public void OnlyAnExplicitSclMappingProvidesASignalName(int index, string? mapped, string expected)
        => Assert.Equal(expected, GooseDatasetEntryPresentation.ResolveName(index, mapped));
}
