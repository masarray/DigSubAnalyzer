namespace ProcessBus.Core.Services.Scl;

/// <summary>
/// Determines whether an observed IEC 61850 stream is sufficiently anchored to an
/// expected SCL publisher to be classified. Identity anchors remain authoritative
/// even when transport or configuration fields mismatch, so the caller can report
/// a precise MISMATCH instead of producing unrelated MISSING and UNEXPECTED rows.
/// </summary>
public static class BindingCandidateEligibility
{
    private const int AnchoredCleanScoreThreshold = 45;
    private const int GenericScoreThreshold = 65;

    public static bool IsEligible(
        string protocol,
        int score,
        int mismatchCount,
        IReadOnlyCollection<string> matchedFields)
    {
        ArgumentNullException.ThrowIfNull(matchedFields);

        var normalizedProtocol = (protocol ?? string.Empty).Trim();
        var hasPrimaryIdentityAnchor = HasPrimaryIdentityAnchor(
            normalizedProtocol,
            score,
            matchedFields);

        // A primary identity match means this is the same engineering publisher
        // candidate even when APPID, destination, VLAN, DataSet, or confRev differ.
        // Keep it eligible so the binding classifier can surface MISMATCH evidence.
        if (mismatchCount > 0)
            return hasPrimaryIdentityAnchor;

        return normalizedProtocol.ToUpperInvariant() switch
        {
            "SV" => hasPrimaryIdentityAnchor && score >= AnchoredCleanScoreThreshold,
            "GOOSE" => hasPrimaryIdentityAnchor && score >= AnchoredCleanScoreThreshold,
            _ => score >= GenericScoreThreshold
        };
    }

    /// <summary>
    /// Produces the final dashboard classification for an eligible binding candidate.
    /// The ordering intentionally mirrors the UI contract: hard conflicts dominate,
    /// then ambiguity, then field mismatch, followed by clean confidence outcomes.
    /// </summary>
    public static string Classify(
        int score,
        int mismatchCount,
        bool ambiguous,
        bool expectedConflict,
        bool liveAlreadyMatched,
        string? warningReason)
    {
        if (expectedConflict || liveAlreadyMatched)
            return "CONFLICT";

        if (ambiguous)
            return "AMBIGUOUS";

        if (mismatchCount > 0)
            return "MISMATCH";

        if (score >= GenericScoreThreshold)
            return string.IsNullOrWhiteSpace(warningReason) ? "MATCHED" : "MATCHED_WITH_WARNING";

        return "WEAK";
    }

    /// <summary>
    /// Evaluates eligibility and final classification together for regression tests
    /// and other callers that need one deterministic decision contract.
    /// </summary>
    public static BindingCandidateDecision Evaluate(
        string protocol,
        int score,
        int mismatchCount,
        bool ambiguous,
        bool expectedConflict,
        bool liveAlreadyMatched,
        string? warningReason,
        IReadOnlyCollection<string> matchedFields)
    {
        var eligible = IsEligible(protocol, score, mismatchCount, matchedFields);
        if (!eligible)
            return new BindingCandidateDecision(false, "INELIGIBLE");

        return new BindingCandidateDecision(
            true,
            Classify(
                score,
                mismatchCount,
                ambiguous,
                expectedConflict,
                liveAlreadyMatched,
                warningReason));
    }

    private static bool HasPrimaryIdentityAnchor(
        string protocol,
        int score,
        IReadOnlyCollection<string> matchedFields)
    {
        bool Has(string fieldName)
            => matchedFields.Any(field =>
                string.Equals(field, fieldName, StringComparison.OrdinalIgnoreCase));

        if (string.Equals(protocol, "SV", StringComparison.OrdinalIgnoreCase))
        {
            return Has("svID") ||
                (Has("APPID") && Has("Dst MAC")) ||
                (Has("APPID") && Has("DataSet") && Has("confRev"));
        }

        if (string.Equals(protocol, "GOOSE", StringComparison.OrdinalIgnoreCase))
        {
            return Has("GoCBRef") ||
                (Has("APPID") && Has("Dst MAC")) ||
                (Has("goID") && Has("DataSet"));
        }

        return score >= GenericScoreThreshold;
    }
}

public sealed record BindingCandidateDecision(bool IsEligible, string Status);
