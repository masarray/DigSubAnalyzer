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

    [Fact]
    public void Sv_AnchoredMismatch_EvaluatesAsMismatchDecision()
    {
        var decision = BindingCandidateEligibility.Evaluate(
            protocol: "SV",
            score: 25,
            mismatchCount: 4,
            ambiguous: false,
            expectedConflict: false,
            liveAlreadyMatched: false,
            warningReason: null,
            matchedFields: new[] { "svID" });

        Assert.True(decision.IsEligible);
        Assert.Equal("MISMATCH", decision.Status);
    }

    [Fact]
    public void Goose_AnchoredMismatch_EvaluatesAsMismatchDecision()
    {
        var decision = BindingCandidateEligibility.Evaluate(
            protocol: "GOOSE",
            score: 20,
            mismatchCount: 3,
            ambiguous: false,
            expectedConflict: false,
            liveAlreadyMatched: false,
            warningReason: null,
            matchedFields: new[] { "GoCBRef" });

        Assert.True(decision.IsEligible);
        Assert.Equal("MISMATCH", decision.Status);
    }

    [Fact]
    public void UnanchoredMismatch_EvaluatesAsIneligibleInsteadOfFalseMatch()
    {
        var decision = BindingCandidateEligibility.Evaluate(
            protocol: "SV",
            score: 95,
            mismatchCount: 1,
            ambiguous: false,
            expectedConflict: false,
            liveAlreadyMatched: false,
            warningReason: null,
            matchedFields: new[] { "DataSet", "confRev" });

        Assert.False(decision.IsEligible);
        Assert.Equal("INELIGIBLE", decision.Status);
    }
}
