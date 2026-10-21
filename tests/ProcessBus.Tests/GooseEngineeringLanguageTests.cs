using ProcessBus.Core.Presentation;
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
        Assert.DoesNotContain("OPEN", display, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CLOSE", display, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Data item 1", GooseEngineeringLanguage.DataItemName(1, null));
    }

    [Theory]
    [InlineData("New", "First seen")]
    [InlineData("State Change", "State changed")]
    [InlineData("Retrans", "Repeated")]
    public void EventLabelsExplainReceiveHistoryWithoutChangingEngineEvents(string wire, string expected) =>
        Assert.Equal(expected, GooseEngineeringLanguage.Event(wire));

    [Fact]
    public void QualityInterpretationMustBeExplicitlyIdentified()
    {
        Assert.Equal("", GooseEngineeringLanguage.QualitySummary(""));
        Assert.Equal("Good quality", GooseEngineeringLanguage.QualitySummary("good"));
        Assert.Equal("Invalid quality · Substituted · Operator blocked",
            GooseEngineeringLanguage.QualitySummary("invalid · substituted · blocked"));
    }
}
