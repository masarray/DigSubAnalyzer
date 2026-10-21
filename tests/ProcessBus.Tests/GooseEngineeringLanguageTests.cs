using ProcessBus.Core.Presentation;
using ProcessBus.Core.Models.Scl;
using Xunit;

namespace ProcessBus.Tests;

public sealed class GooseEngineeringLanguageTests
{
    [Theory]
    [InlineData("BitString", "Binary bit pattern")]
    [InlineData("Boolean", "True / false value")]
    [InlineData("Integer", "Whole number")]
    [InlineData("OctetString", "Bytes")]
    [InlineData("UtcTime", "Date and time")]
    [InlineData("NONSPECIFIC_VENDOR_CODE", "Recorded type: NONSPECIFIC_VENDOR_CODE")]
    [InlineData(null, "Unknown data type")]
    public void KnownAndUnknownWireTypesHaveHonestPlainDescriptions(string? wire, string expected) =>
        Assert.Equal(expected, GooseEngineeringLanguage.DataType(wire));

    [Fact]
    public void UnmappedBitStringMustNotBecomeBreakerOpenOrClose()
    {
        var display = GooseEngineeringLanguage.DataType("BitString");
        Assert.Equal("Binary bit pattern", display);
        Assert.False(display.Contains("OPEN", StringComparison.OrdinalIgnoreCase));
        Assert.False(display.Contains("CLOSE", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Data item 1", GooseEngineeringLanguage.DataItemName(1, null));
    }

    [Theory]
    [InlineData("New", "First seen")]
    [InlineData("State Change", "State changed")]
    [InlineData("Retrans", "Repeated")]
    public void EventLabelsExplainReceiveHistoryWithoutChangingEngineEvents(string wire, string expected) =>
        Assert.Equal(expected, GooseEngineeringLanguage.Event(wire));

    [Fact]
    public void CircuitBreakerPositionNeedsVerifiedSignalMetadata()
    {
        var position = new SclDataSetEntryModel
        {
            Cdc = "DPC", LnClass = "XCBR", DoName = "Pos", DaName = "stVal"
        };
        Assert.True(GooseEngineeringLanguage.TryDescribeDoublePoint(position, "BitString", "10", out var closed));
        Assert.Equal("Closed (10)", closed);
        Assert.True(GooseEngineeringLanguage.TryDescribeDoublePoint(position, "BitString", "01", out var open));
        Assert.Equal("Open (01)", open);
        Assert.True(GooseEngineeringLanguage.TryDescribeDoublePoint(position, "BitString", "00", out var moving));
        Assert.Equal("Between positions (00)", moving);
    }

    [Fact]
    public void QualityAndUnmappedValuesCanNeverInventBreakerPositions()
    {
        var raw = new SclDataSetEntryModel { Cdc = "DPC", LnClass = "XCBR", DoName = "Pos", DaName = "stVal", IsQuality = true };
        Assert.False(GooseEngineeringLanguage.TryDescribeDoublePoint(null, "BitString", "10", out _));
        Assert.False(GooseEngineeringLanguage.TryDescribeDoublePoint(raw, "BitString", "10", out _));
        var wrongAttribute = new SclDataSetEntryModel { Cdc = "DPC", LnClass = "XCBR", DoName = "Pos", DaName = "q" };
        Assert.False(GooseEngineeringLanguage.TryDescribeDoublePoint(wrongAttribute, "BitString", "10", out _));
        var position = new SclDataSetEntryModel { Cdc = "DPC", LnClass = "XCBR", DoName = "Pos", DaName = "stVal" };
        Assert.False(GooseEngineeringLanguage.TryDescribeDoublePoint(position, "Boolean", "10", out _));
        Assert.False(GooseEngineeringLanguage.TryDescribeDoublePoint(position, "BitString", "unknown", out _));
    }

    [Fact]
    public void QualityInterpretationMustBeExplicitlyIdentified()
    {
        Assert.Equal("", GooseEngineeringLanguage.QualitySummary(""));
        Assert.Equal("Good quality", GooseEngineeringLanguage.QualitySummary("good"));
        Assert.Equal("Invalid quality · Substituted · Operator blocked",
            GooseEngineeringLanguage.QualitySummary("invalid · substituted · blocked"));
    }
}
