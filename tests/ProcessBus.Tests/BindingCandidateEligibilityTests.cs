using ProcessBus.Core.Services.Scl;
using Xunit;

namespace ProcessBus.Tests;

public sealed class BindingCandidateEligibilityTests
{
    [Fact]
    public void Sv_PrimaryIdentityWithTransportMismatches_RemainsEligible()
    {
        var eligible = BindingCandidateEligibility.IsEligible(
            protocol: "SV",
            score: 25,
            mismatchCount: 4,
            matchedFields: new[] { "svID" });

        Assert.True(eligible);
    }

    [Fact]
    public void Goose_PrimaryIdentityWithConfigurationMismatches_RemainsEligible()
    {
        var eligible = BindingCandidateEligibility.IsEligible(
            protocol: "GOOSE",
            score: 20,
            mismatchCount: 3,
            matchedFields: new[] { "GoCBRef" });

        Assert.True(eligible);
    }

    [Fact]
    public void Sv_UnanchoredMismatchCandidate_RemainsIneligible()
    {
        var eligible = BindingCandidateEligibility.IsEligible(
            protocol: "SV",
            score: 95,
            mismatchCount: 1,
            matchedFields: new[] { "DataSet", "confRev" });

        Assert.False(eligible);
    }

    [Fact]
    public void Sv_AppIdAndDestinationMac_FormTransportIdentityAnchor()
    {
        var eligible = BindingCandidateEligibility.IsEligible(
            protocol: "SV",
            score: 45,
            mismatchCount: 0,
            matchedFields: new[] { "APPID", "Dst MAC" });

        Assert.True(eligible);
    }

    [Fact]
    public void CleanAnchoredCandidate_BelowConfidenceThreshold_RemainsIneligible()
    {
        var eligible = BindingCandidateEligibility.IsEligible(
            protocol: "GOOSE",
            score: 44,
            mismatchCount: 0,
            matchedFields: new[] { "GoCBRef" });

        Assert.False(eligible);
    }
}
