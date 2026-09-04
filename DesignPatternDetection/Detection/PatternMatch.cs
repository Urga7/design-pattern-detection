using DesignPatternDetection.Detection.Verification;

namespace DesignPatternDetection.Detection;

/// <summary>
/// A single detected occurrence of a design pattern, described by the SPARQL variable bindings that satisfied the
/// pattern's query.
/// </summary>
/// <remarks>
/// <c>Bindings</c> holds the display labels (simple names). <c>Fragments</c> keeps each role's full node URI fragment
/// - the key into <see cref="SourceGraph.Locations"/> - and omits roles bound to literals or blank nodes.
/// <c>Verdict</c> is null unless a <see cref="MatchVerifier"/> reviewed the scan. <c>RoleIris</c> names each role,
/// and <c>PatternIri</c> the pattern itself, in <see cref="FdpVocabulary"/>; both are null together for a pattern FDP
/// does not model.
/// </remarks>
public sealed record PatternMatch(
    string PatternName,
    IReadOnlyDictionary<string, string> Bindings,
    IReadOnlyDictionary<string, string>? Fragments = null,
    MatchVerdict? Verdict = null,
    IReadOnlyDictionary<string, string>? RoleIris = null,
    string? PatternIri = null)
{
    public override string ToString() =>
        string.Join(", ", Bindings.Select(binding => $"{binding.Key} = {binding.Value}"));
}
