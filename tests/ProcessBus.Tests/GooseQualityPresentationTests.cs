using ProcessBus.Core.Presentation;
using Xunit;

namespace ProcessBus.Tests;

public sealed class GooseQualityPresentationTests
{
    [Fact]
    public void ValidThirteenBitQualityDecodesAsProcessGood()
    {
        Assert.True(GooseQualityPresentation.TryDecode("030000", "BitString", out var quality));
        Assert.NotNull(quality);
        Assert.Equal("good", quality.Validity);
        Assert.Equal("process", quality.Source);
        Assert.False(quality.Test);
        Assert.False(quality.OperatorBlocked);
        Assert.Empty(quality.DetailFlags);
    }

    [Fact]
    public void ActiveQualityFlagsRemainExplicitAndNotReducedToAColor()
    {
        // invalid validity (01), oldData at bit 7, substituted at bit 10,
        // test at bit 11, operator blocked at bit 12.
        Assert.True(GooseQualityPresentation.TryDecode("034138", "BitString", out var quality));
        Assert.NotNull(quality);
        Assert.Equal("invalid", quality.Validity);
        Assert.Equal("substituted", quality.Source);
        Assert.True(quality.Test);
        Assert.True(quality.OperatorBlocked);
        Assert.Contains("oldData", quality.DetailFlags);
    }

    [Theory]
    [InlineData("034000", "reserved")]
    [InlineData("03C000", "questionable")]
    [InlineData("038000", "reserved")]
    public void QualityValidityVariantsRemainLiteral(string hex, string expected)
    {
        Assert.True(GooseQualityPresentation.TryDecode(hex, "BitString", out var parsed));
        Assert.Equal(expected, parsed!.Validity);
    }

    [Theory]
    [InlineData("", "BitString")]
    [InlineData("0300", "BitString")]
    [InlineData("040000", "BitString")]
    [InlineData("030001", "BitString")]
    [InlineData("030007", "BitString")]
    [InlineData("03ZZ00", "BitString")]
    [InlineData("030000", "Boolean")]
    [InlineData(null, "BitString")]
    public void InvalidOrNonQualityPayloadNeverFabricatesQuality(string? raw, string type)
    {
        Assert.False(GooseQualityPresentation.TryDecode(raw, type, out var quality));
        Assert.Null(quality);
    }
}
