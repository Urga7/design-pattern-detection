namespace DesignPatternDetection.Detection.Verification;

/// <summary>What a semantic reviewer concluded about a candidate match.</summary>
public enum VerificationOutcome
{
    Confirmed,
    Uncertain,
    Rejected
}

/// <summary>One reviewer's ruling on a <see cref="PatternMatch"/>.</summary>
/// <param name="Outcome">The ruling.</param>
/// <param name="Rationale">One line naming the member or relationship that carries or breaks the pattern's defining trait.</param>
/// <param name="Model">The model that issued it.</param>
public sealed record MatchVerdict(VerificationOutcome Outcome, string Rationale, string Model);
